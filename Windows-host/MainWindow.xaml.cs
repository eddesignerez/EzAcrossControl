using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Windows.Threading;
using System.Threading.Channels;
using System.Diagnostics;
using System.ComponentModel;
using System.IO;
using WindowsHost.Input;
using WindowsHost.V2.Edge;
using WindowsHost.Protocol;
using System.Windows.Input;
using EZAcrossControl.Input;
using WindowsHost.Engine;
using System.Windows.Media;
using WinForms = System.Windows.Forms;
using Drawing = System.Drawing;
using Brush = System.Windows.Media.Brush;
using MessageBox = System.Windows.MessageBox;

namespace WindowsHost
{
    public partial class MainWindow : Window
    {
        private HttpListener? _listener;
        private volatile bool _controlPaused;
        private readonly SemaphoreSlim _readinessGate = new(1, 1);
        private CancellationTokenSource? _cts;
        private WebSocket? _currentSocket;
        private readonly object _socketGate = new();
        private readonly ConcurrentDictionary<WebSocket, string> _pendingChallenges = new();
        private readonly ConcurrentDictionary<WebSocket, SessionQueues> _sessionQueues = new();
        private const int MaxWebSocketMessageBytes = 64 * 1024;
        private const int ProtocolVersion = 2;

        private sealed class SessionQueues : IDisposable
        {
            public CancellationTokenSource Cancellation { get; } = new();
            public Channel<string> Priority { get; } = Channel.CreateBounded<string>(new BoundedChannelOptions(64)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            });
            public Channel<string> Standard { get; } = Channel.CreateBounded<string>(new BoundedChannelOptions(256)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            });

            public void Dispose() => Cancellation.Dispose();
        }
        
        public enum PointerOwnershipState
        {
            Local,
            Remote
        }

        private PointerOwnershipState _ownershipState = PointerOwnershipState.Local;
        private WindowsRawMouseInputService _rawMouseInputService;
        private bool _isCursorClipped = false;
        
        private IInputCaptureService _inputCaptureService;
        private DispatcherTimer _inputDebugTimer;
        private DispatcherTimer _transportTimer;
        private bool _closing;
        private string? _lastUnavailableMode;
        private ConcurrentQueue<InputEvent> _inputEventQueue;
        private List<string> _debugLogLines;
        
        private IEdgeTransitionService _edgeTransitionService;
        private IEdgeHandoffService _edgeHandoffService;
        private bool _androidHandshakeComplete;
        private AndroidDevice? _activeDevice;
        private string _engineTransport = "-";
        private string? _lastPublishedSessionStatus;
        private IntPtr _windowToRestoreAfterNativeCapture;
        private NativeMethods.POINT? _pointerBeforeCapture;
        private ScreenEdge _edgeBeforeCapture;
        private string? _companionAddress;
        private string? _companionDeviceName;
        private AppSettings _appSettings;
        private InputSessionManager _sessionManager;
        private bool _showInputDebug = false;
        private int _currentSessionId = 0;
        
        // V2 Scrcpy Engine
        private IScrcpyControlEngine _scrcpyEngine;
        private AndroidDeviceManager? _deviceManager;
        private WinForms.NotifyIcon? _trayIcon;
        private WinForms.ToolStripMenuItem? _restoreTrayMenuItem;
        private WinForms.ToolStripMenuItem? _exitTrayMenuItem;
        private WinForms.ToolStripMenuItem? _restartTrayMenuItem;
        private Drawing.Icon? _connectedTrayIcon;
        private Drawing.Icon? _disconnectedTrayIcon;
        private bool? _trayConnected;
        private bool _exitRequested;
        private bool _trayHintShown;
        private double _heightBeforeAdvanced;
        private double _topBeforeAdvanced;
        private bool _updatingLanguage;
        private bool _startingServer;


        public MainWindow()
        {
            InitializeComponent();
            InitializeTrayIcon();
            TxtApplicationVersion.Text = $"v{GetApplicationVersion()}";
            CmbLanguage.ItemsSource = new[] { new LanguageOption("system", Localization.T("System Language")) }.Concat(Localization.Languages);
            CmbLanguage.SelectedValue = Localization.Preference;
            ApplyLanguageLayout();
            TxtLocalIP.Text = GetLocalIPAddress();
            _appSettings = ConfigManager.Load();
            TxtPort.Text = (LanPortPermission.IsValidPort(_appSettings.ServerPort) ? _appSettings.ServerPort : Config.DefaultPort).ToString();
            UpdateThemeButtons();
            
            RefreshServerVisuals();

            _inputCaptureService = new WindowsInputCaptureService();
            _inputCaptureService.InputEventCaptured += InputCaptureService_InputEventCaptured;
            _inputCaptureService.StartCapture();
            
            _rawMouseInputService = new WindowsRawMouseInputService(this);
            _rawMouseInputService.InputEventCaptured += RawMouseInputService_InputEventCaptured;
            _rawMouseInputService.StartCapture(); // Run continuously to detect edge pushes
            
            _edgeTransitionService = new EdgeTransitionService(_inputCaptureService);
            _edgeTransitionService.StateChanged += EdgeTransitionService_StateChanged;
            _edgeTransitionService.UpdateOptions(_appSettings.EdgeTransition);
            
            _edgeHandoffService = new EdgeHandoffService(
                _edgeTransitionService,
                IsHandoffAvailable,
                BeginAndroidHandoff,
                PrepareCaptureAsync);

            // V2 Initialization
            _scrcpyEngine = new ScrcpyProcessManager();
            _edgeHandoffService.AttachEngine(_scrcpyEngine);
            _edgeHandoffService.Start();

            _scrcpyEngine.StateChanged += (s, state) => Dispatcher.InvokeAsync(() => {
                Localization.SetStatus(TxtEngineState, state.ToString().ToUpper());
                Localization.SetStatus(TxtEngineStatus, state.ToString().ToUpper());

                switch (state)
                {
                    case ScrcpyEngineState.Offline:
                        BtnStartEngine.IsEnabled = true;
                        BtnCaptureAndroid.IsEnabled = false;
                        BtnReturnWindows.IsEnabled = false;
                        TxtControlState.Text = "Windows";
                        _engineTransport = "-";
                        UpdateTransportDisplay();
                        SafeReleaseInputOwnership("Offline");
                        break;

                    case ScrcpyEngineState.Starting:
                        BtnStartEngine.IsEnabled = false;
                        BtnCaptureAndroid.IsEnabled = false;
                        BtnReturnWindows.IsEnabled = false;
                        TxtControlState.Text = "Windows";
                        UpdateTransportDisplay();
                        break;

                    case ScrcpyEngineState.Ready:
                        BtnStartEngine.IsEnabled = false;
                        BtnCaptureAndroid.IsEnabled = true;
                        BtnReturnWindows.IsEnabled = false;
                        TxtControlState.Text = "Windows";
                        bool returningFromCapture = _pointerBeforeCapture.HasValue;
                        SafeReleaseInputOwnership("Ready");
                        if (UsesNativeScrcpyInput() && !returningFromCapture)
                            (_scrcpyEngine as ScrcpyProcessManager)?.HideInputWindow();
                        UpdateTransportDisplay();
                        break;

                    case ScrcpyEngineState.Captured:
                        BtnStartEngine.IsEnabled = false;
                        BtnCaptureAndroid.IsEnabled = false;
                        BtnReturnWindows.IsEnabled = true;
                        TxtControlState.Text = "Android";
                        TakeInputOwnership();
                        break;

                    case ScrcpyEngineState.Error:
                        BtnStartEngine.IsEnabled = true;
                        BtnCaptureAndroid.IsEnabled = false;
                        BtnReturnWindows.IsEnabled = false;
                        TxtControlState.Text = "Windows";
                        SafeReleaseInputOwnership("Error");
                        UpdateTransportDisplay();
                        break;
                }
                UpdateEngineBadge();
                UpdateTransportDisplay();
            });

            _scrcpyEngine.Error += (s, msg) => Dispatcher.InvokeAsync(() => {
                Log($"[SCRCPY ERROR] {msg}");
            });

            _sessionManager = new InputSessionManager(SendMessage);
            _sessionManager.SetState(InputSessionState.Disconnected);

            // Setup Edge UI
            TxtEdgeThreshold.Text = _appSettings.EdgeTransition.EdgeThresholdPixels.ToString();
            TxtEdgeDelay.Text = _appSettings.EdgeTransition.EdgeActivationDelayMs.ToString();
            foreach (System.Windows.Controls.ComboBoxItem item in CmbActiveEdge.Items)
            {
                if (item.Tag.ToString() == _appSettings.EdgeTransition.ActiveEdge.ToString())
                {
                    CmbActiveEdge.SelectedItem = item;
                    break;
                }
            }
            
            foreach (System.Windows.Controls.ComboBoxItem item in CmbConnectionMode.Items)
            {
                if (item.Tag.ToString() == _appSettings.ConnectionMode.ToString())
                {
                    CmbConnectionMode.SelectedItem = item;
                    break;
                }
            }
            
            _inputEventQueue = new ConcurrentQueue<InputEvent>();
            _debugLogLines = new List<string>();
            
            _inputDebugTimer = new DispatcherTimer();
            _inputDebugTimer.Interval = TimeSpan.FromMilliseconds(50);
            _inputDebugTimer.Tick += InputDebugTimer_Tick;
            _inputDebugTimer.Start();

            _transportTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _transportTimer.Tick += async (_, _) =>
            {
                if (!_closing) RefreshServerVisuals();
                if (!_closing && IsAndroidCompanionConnected()) await StartEngineAsync();
            };
            _transportTimer.Start();

            // Input debug UI removed; input debug disabled by default.
            // _showInputDebug bool controls debug output if needed.

            Loaded += (_, _) => {
                Localization.SetStatus(TxtEngineState, "WAITING APK");
                Localization.SetStatus(TxtEngineStatus, "WAITING APK");
                BtnStartEngine.IsEnabled = false;
                UpdateEngineBadge();
                Log("Waiting for Connect in the Android app. Engine starts automatically after HELLO.");
                BtnStart_Click(this, new RoutedEventArgs());
            };

        }

        private static string GetApplicationVersion()
        {
            var productVersion = FileVersionInfo.GetVersionInfo(Environment.ProcessPath ?? string.Empty).ProductVersion;
            var version = productVersion?.Split('+', '-')[0];
            return string.IsNullOrWhiteSpace(version) ? "—" : version;
        }

        private void BtnThemeLight_Click(object sender, RoutedEventArgs e) => SetTheme(AppTheme.Light);

        private void BtnThemeDark_Click(object sender, RoutedEventArgs e) => SetTheme(AppTheme.Dark);

        private void SetTheme(AppTheme theme)
        {
            ThemeManager.SaveThemePreference(theme);
            UpdateThemeButtons();
        }

        private void UpdateThemeButtons()
        {
            bool dark = ThemeManager.IsDarkTheme(ThemeManager.LoadThemePreference());
            BtnThemeLight.Background = (Brush)FindResource(dark ? "SurfaceSoftBrush" : "BrandCoralSoftBrush");
            BtnThemeLight.Foreground = (Brush)FindResource(dark ? "TextSecondaryBrush" : "BrandCoralBrush");
            BtnThemeDark.Background = (Brush)FindResource(dark ? "BrandCoralSoftBrush" : "SurfaceSoftBrush");
            BtnThemeDark.Foreground = (Brush)FindResource(dark ? "BrandCoralBrush" : "TextSecondaryBrush");
            UpdateEngineBadge();
        }

        private void CmbLanguage_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_updatingLanguage || CmbLanguage.SelectedValue is not string code) return;
            Localization.Save(code);
            _updatingLanguage = true;
            try {
                CmbLanguage.ItemsSource = new[] { new LanguageOption("system", Localization.T("System Language")) }.Concat(Localization.Languages);
                CmbLanguage.SelectedValue = code;
            } finally { _updatingLanguage = false; }
            ApplyLanguageLayout();
            if (_trayIcon?.ContextMenuStrip is { } menu) {
                _restoreTrayMenuItem!.Text = Localization.T("Restore");
                _exitTrayMenuItem!.Text = Localization.T("Exit");
                _restartTrayMenuItem!.Text = Localization.T("Restart");
            }
            Dispatcher.BeginInvoke(() => {
                UpdateLayout();
                var area = SystemParameters.WorkArea;
                Height = Math.Min(area.Height - 8, MainContent.ActualHeight + MainContent.Margin.Top + MainContent.Margin.Bottom);
                Top = Math.Max(area.Top, Math.Min(Top, area.Bottom - Height));
            });
        }

        private void UpdateEngineBadge()
        {
            TxtEngineState.Foreground = (Brush)FindResource(
                _scrcpyEngine?.State == ScrcpyEngineState.Ready
                    ? "SuccessPrimaryBrush" : "TextPrimaryBrush");
        }

        private double MeasureLabel(string source, double size, FontWeight weight)
        {
            var text = new FormattedText(Localization.T(source), System.Globalization.CultureInfo.CurrentUICulture,
                System.Windows.FlowDirection.LeftToRight, new Typeface(FontFamily, FontStyles.Normal, weight, FontStretches.Normal),
                size, (Brush)FindResource("TextPrimaryBrush"), VisualTreeHelper.GetDpi(this).PixelsPerDip);
            return Math.Ceiling(text.WidthIncludingTrailingWhitespace);
        }

        private void ApplyLanguageLayout()
        {
            var labels = new[] { "Android Position", "Connection Mode", "Transport / Control", "Sensitivity",
                "Activation Delay", "Edge Status", "Device", "Local IP", "Port" };
            var labelWidth = Math.Max(155, labels.Max(x => MeasureLabel(x, 12, FontWeights.Normal)) + 18);
            labelWidth = Math.Max(labelWidth, new[] { "Running", "Stopped", "Error" }
                .Max(x => MeasureLabel(x, 22, FontWeights.Bold)) + 14);
            System.Windows.Application.Current.Resources["FieldLabelWidth"] = new GridLength(labelWidth);
            BtnStart.Width = new[] { "Start Server", "Started" }.Max(x => MeasureLabel(x, FontSize, FontWeights.SemiBold)) + 30;
            BtnStop.Width = Math.Max(52, MeasureLabel("Stop", FontSize, FontWeights.SemiBold) + 30);
            CmbActiveEdge.Width = Math.Max(130, new[] { "Right", "Left", "Top", "Bottom", "Disabled" }
                .Max(x => MeasureLabel(x, FontSize, FontWeights.Normal)) + 44);
            CmbConnectionMode.Width = Math.Max(130, MeasureLabel("Auto", FontSize, FontWeights.Normal) + 44);
            var fieldWidth = Math.Max(CmbActiveEdge.Width, Math.Max(CmbConnectionMode.Width, BtnStart.Width + BtnStop.Width + 8));
            // Use the same two columns in every language; expand only as much as the translated content needs.
            var cardWidth = labelWidth + fieldWidth + 34;
            var actionWidth = new[] { "Capture Android", "Return Windows" }.Sum(x => MeasureLabel(x, FontSize, FontWeights.SemiBold) + 30) + 8 + 34;
            var titles = new[] { "Connection Status", "Android Device", "Screen Edge", "Network" }
                .Max(x => MeasureLabel(x, 17, FontWeights.SemiBold)) + 34;
            cardWidth = Math.Max(cardWidth, Math.Max(actionWidth, titles));
            var desired = Math.Max(740, cardWidth * 2 + 12 + 32);
            var available = SystemParameters.WorkArea.Width - 8;
            MinWidth = Math.Min(desired, available);
            Width = MinWidth;
            if (IsLoaded) Left = Math.Max(SystemParameters.WorkArea.Left, Math.Min(Left, SystemParameters.WorkArea.Right - Width));
        }

        private void BtnAdvanced_Click(object sender, RoutedEventArgs e)
        {
            if (AdvancedPanel.Visibility == Visibility.Visible)
            {
                AdvancedPanel.Visibility = Visibility.Collapsed;
                if (_heightBeforeAdvanced > 0)
                {
                    Height = _heightBeforeAdvanced;
                    Top = _topBeforeAdvanced;
                }
            }
            else
            {
                _heightBeforeAdvanced = Height;
                _topBeforeAdvanced = Top;
                AdvancedPanel.Visibility = Visibility.Visible;
                UpdateLayout();
                var workArea = SystemParameters.WorkArea;
                var contentHeight = MainContent.ActualHeight + MainContent.Margin.Top + MainContent.Margin.Bottom;
                Height = Math.Min(workArea.Height - 8, Math.Max(_heightBeforeAdvanced, contentHeight));
                Top = Math.Max(workArea.Top, Math.Min(Top, workArea.Bottom - Height));
            }
        }

        private void Header_DragMove(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        private void InitializeTrayIcon()
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "EzAcrossControl152D4D.ico");
            var offlineIconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "EzAcrossControlOFF.ico");
            _connectedTrayIcon = File.Exists(iconPath) ? new Drawing.Icon(iconPath) : (Drawing.Icon)Drawing.SystemIcons.Application.Clone();
            _disconnectedTrayIcon = File.Exists(offlineIconPath) ? new Drawing.Icon(offlineIconPath) : (Drawing.Icon)_connectedTrayIcon.Clone();
            var menu = new WinForms.ContextMenuStrip();
            _restoreTrayMenuItem = new WinForms.ToolStripMenuItem(Localization.T("Restore"), null,
                (_, _) => Dispatcher.BeginInvoke(RestoreFromTray));
            _exitTrayMenuItem = new WinForms.ToolStripMenuItem(Localization.T("Exit"), null,
                (_, _) => Dispatcher.BeginInvoke(ExitFromTray));
            _restartTrayMenuItem = new WinForms.ToolStripMenuItem(Localization.T("Restart"), null,
                (_, _) => Dispatcher.BeginInvoke(RestartApplication));
            menu.Items.AddRange(new WinForms.ToolStripItem[] { _restoreTrayMenuItem, _exitTrayMenuItem, _restartTrayMenuItem });
            _trayIcon = new WinForms.NotifyIcon
            {
                Icon = _disconnectedTrayIcon,
                Text = "EZ Across Control",
                ContextMenuStrip = menu,
                Visible = true
            };
            _trayIcon.DoubleClick += (_, _) => Dispatcher.BeginInvoke(RestoreFromTray);
        }

        private void UpdateTrayConnectionIcon(bool connected)
        {
            if (_trayConnected == connected) return;
            _trayConnected = connected;
            if (_trayIcon != null) _trayIcon.Icon = connected ? _connectedTrayIcon : _disconnectedTrayIcon;
        }

        private void RestoreFromTray()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private void ExitFromTray()
        {
            _exitRequested = true;
            Close();
        }

        private void RestartApplication()
        {
            var executablePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            {
                Log("[APP] Unable to restart because the executable path is unavailable.");
                return;
            }

            try
            {
                Log("[APP] Restarting the application.");
                Process.Start(new ProcessStartInfo
                {
                    FileName = executablePath,
                    WorkingDirectory = AppContext.BaseDirectory,
                    UseShellExecute = true
                });
                _exitRequested = true;
                Close();
            }
            catch (Exception ex)
            {
                Log($"[APP] Failed to restart the application: {ex.Message}");
                MessageBox.Show(this, ex.Message, "EZ Across Control", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_exitRequested)
            {
                e.Cancel = true;
                Hide();
                if (!_trayHintShown && _trayIcon != null)
                {
                    _trayHintShown = true;
                    _trayIcon.ShowBalloonTip(2500, "EZ Across Control", "Running in the background. Double-click the tray icon to restore.", WinForms.ToolTipIcon.Info);
                }
                return;
            }
            base.OnClosing(e);
        }

        private string GetLocalIPAddress()
        {
            var (bestIp, diagnostics) = NetworkInterfaceSelector.GetPreferredLanIPv4();
            
            // Log the diagnostics to the server log
            Log(diagnostics);

            if (bestIp != null)
            {
                return bestIp.ToString();
            }
            
            return "No active LAN address detected";
        }

        private void Log(string message)
        {
            try
            {
                System.IO.File.AppendAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EZ Across Control", "server_log.txt"), $"[{DateTime.Now:HH:mm:ss}] {message}\n");
            }
            catch { }
            
            Dispatcher.Invoke(() =>
            {
                TxtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
                TxtLog.ScrollToEnd();
            });
        }

        private void RefreshServerVisuals()
        {
            bool running = _listener?.IsListening == true && !_startingServer;
            bool connected = running && IsAndroidCompanionConnected();
            UpdateTrayConnectionIcon(connected);
            var state = running ? (connected ? "Connected" : "Server Waiting") : "Off";
            BtnStart.SetResourceReference(System.Windows.Controls.ContentControl.ContentProperty, running ? "Ui.Started" : "Ui.Start Server");
            BtnStart.SetResourceReference(System.Windows.Controls.Control.ForegroundProperty, running ? "SuccessPrimaryBrush" : "TextPrimaryBrush");
            Localization.SetStatus(TxtServerStatus, running ? "Running" : "Stopped");
            TxtServerStatus.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextPrimaryBrush");
            Localization.SetStatus(TxtLocalServerState, state);
            TxtLocalServerState.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty,
                !running ? "ErrorPrimaryBrush" : connected ? "SuccessPrimaryBrush" : "WarningPrimaryBrush");
            BtnStart.IsEnabled = !running && !_startingServer;
            BtnStop.IsEnabled = running;
            TxtPort.IsEnabled = !_startingServer;
            BtnApplyPort.IsEnabled = !_startingServer;
        }

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (_startingServer || _listener?.IsListening == true) return;
            if (!int.TryParse(TxtPort.Text, out int port) || !LanPortPermission.IsValidPort(port))
            {
                MessageBox.Show(Localization.T("Invalid port"));
                return;
            }

            await StartServerAsync(port);
        }

        private async void BtnApplyPort_Click(object sender, RoutedEventArgs e)
        {
            if (_startingServer) return;
            if (!int.TryParse(TxtPort.Text, out int port) || !LanPortPermission.IsValidPort(port))
            {
                MessageBox.Show(Localization.T("Invalid port"));
                return;
            }

            bool running = _listener?.IsListening == true;
            if (!running)
            {
                _appSettings.ServerPort = port;
                ConfigManager.Save(_appSettings);
                Log($"[SERVER] Port {port} saved. The LAN server remains stopped.");
                return;
            }

            if (_appSettings.ServerPort == port) return;
            if (MessageBox.Show(this, string.Format(Localization.T("Restart Application for Port"), port),
                    Localization.T("Change Port"), MessageBoxButton.OKCancel, MessageBoxImage.Information)
                != MessageBoxResult.OK)
            {
                TxtPort.Text = _appSettings.ServerPort.ToString();
                return;
            }

            _appSettings.ServerPort = port;
            ConfigManager.Save(_appSettings);
            Log($"[SERVER] Port {port} saved. Restarting the application to apply it.");
            RestartApplication();
        }

        private async Task StartServerAsync(int port)
        {
            if (_startingServer || _listener?.IsListening == true) return;
            _startingServer = true;
            RefreshServerVisuals();
            string prefix = $"http://+:{port}/";
            try
            {
                bool needsPermission = !LanPortPermission.HasFirewallRule(port, Environment.ProcessPath!);
                _listener = new HttpListener();
                _listener.Prefixes.Add(prefix);
                try { _listener.Start(); }
                catch (HttpListenerException ex) when (ex.ErrorCode == 5) { needsPermission = true; }
                if (needsPermission)
                {
                    _listener.Close();
                    _listener = null;
                    if (MessageBox.Show(this, string.Format(Localization.T("Authorize LAN Port"), port),
                        Localization.T("Administrator Required"), MessageBoxButton.OKCancel, MessageBoxImage.Information)
                        != MessageBoxResult.OK) return;
                    Log("[SERVER] Requesting Windows permission for the selected LAN port.");
                    if (!await LanPortPermission.RequestAsync(port))
                    {
                        Log("[SERVER] Port configuration was not authorized or failed.");
                        MessageBox.Show(Localization.T("Port Permission Required"));
                        return;
                    }
                    if (_closing) return;
                    if (!LanPortPermission.HasFirewallRule(port, Environment.ProcessPath!))
                        throw new InvalidOperationException("The selected port has no matching private LAN firewall rule.");
                    _listener = new HttpListener();
                    _listener.Prefixes.Add(prefix);
                    _listener.Start();
                }
                _cts = new CancellationTokenSource();
                _appSettings.ServerPort = port;
                ConfigManager.Save(_appSettings);
                _startingServer = false;
                StartListening(prefix);
            }
            catch (Exception ex)
            {
                Log($"[SERVER] Error starting server: {ex.Message}");
                _listener?.Close();
                _listener = null;
                MessageBox.Show(Localization.T("Port Permission Required"));
            }
            finally { _startingServer = false; RefreshServerVisuals(); }
        }

        private void StartListening(string prefix)
        {
            RefreshServerVisuals();
            Log($"Server started on {prefix}");
            
            _ = AcceptConnectionsAsync(_listener!, _cts!.Token);
        }

        private async Task AcceptConnectionsAsync(HttpListener listener, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var context = await listener.GetContextAsync();

                    if (!context.Request.IsWebSocketRequest && context.Request.Url?.AbsolutePath == "/readiness")
                    {
                        ProcessRequest(context);
                        continue;
                    }
                    
                    Log($"--- HTTP Request ---");
                    Log($"Method: {context.Request.HttpMethod}");
                    Log($"RawUrl: {context.Request.RawUrl}");
                    Log($"Host: {context.Request.UserHostName}");
                    Log($"IsWebSocketRequest: {context.Request.IsWebSocketRequest}");
                    Log($"Upgrade: {context.Request.Headers["Upgrade"]}");
                    Log($"Connection: {context.Request.Headers["Connection"]}");
                    Log($"Sec-WebSocket-Version: {context.Request.Headers["Sec-WebSocket-Version"]}");
                    Log($"User-Agent: {context.Request.Headers["User-Agent"]}");

                    if (context.Request.IsWebSocketRequest)
                    {
                        ProcessRequest(context);
                    }
                    else
                    {
                        context.Response.StatusCode = 400;
                        context.Response.Close();
                    }
                }
                catch (HttpListenerException)
                {
                    // Listener stopped
                }
                catch (ObjectDisposedException)
                {
                    // Listener disposed
                }
                catch (Exception ex)
                {
                    Log($"Accept error: {ex.Message}");
                }
            }
        }

        private async void ProcessRequest(HttpListenerContext context)
        {
            try
            {
                if (!context.Request.IsWebSocketRequest)
                {
                    if (context.Request.HttpMethod != "GET" || context.Request.Url?.AbsolutePath != "/readiness")
                    { context.Response.StatusCode = 404; context.Response.Close(); return; }
                    var name = context.Request.QueryString["device"];
                    if (string.IsNullOrWhiteSpace(name) || name.Length > 128)
                    { context.Response.StatusCode = 400; context.Response.Close(); return; }
                    if (!await _readinessGate.WaitAsync(0))
                    { context.Response.StatusCode = 503; context.Response.Close(); return; }
                    try
                    {
                        var address = context.Request.RemoteEndPoint?.Address.MapToIPv4().ToString();
                        var devices = await new AndroidDeviceManager().GetDevicesAsync();
                        var result = new {
                            UsbReady = AndroidDeviceManager.FindCompanionDevice(devices, ConnectionMode.Usb, name, address) != null,
                            WifiReady = AndroidDeviceManager.FindCompanionDevice(devices, ConnectionMode.Network, name, address) != null
                        };
                        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(result));
                        context.Response.ContentType = "application/json";
                        context.Response.Headers["Cache-Control"] = "no-store";
                        await context.Response.OutputStream.WriteAsync(bytes);
                    }
                    finally { _readinessGate.Release(); context.Response.Close(); }
                    return;
                }
                var wsContext = await context.AcceptWebSocketAsync(null);
                var socket = wsContext.WebSocket;
                var socketAddress = context.Request.RemoteEndPoint?.Address.MapToIPv4().ToString();
                Log("Client connected; awaiting authenticated HELLO.");
                var challenge = PairingAuthenticator.CreateChallenge();
                _pendingChallenges[socket] = challenge;
                await SendDirectAsync(socket, new MessageEnvelope
                {
                    Type = "PAIR_CHALLENGE",
                    ProtocolVersion = ProtocolVersion,
                    Payload = new { Nonce = challenge }
                });
                await ReceiveLoopAsync(socket, socketAddress);
            }
            catch (Exception ex)
            {
                Log($"WebSocket accept error: {ex.Message}");
            }
        }

        private async Task ReceiveLoopAsync(WebSocket socket, string? remoteAddress)
        {
            var token = _cts!.Token;
            var buffer = new byte[4096];
            try
            {
                while (socket.State == WebSocketState.Open && !token.IsCancellationRequested)
                {
                    var message = await ReceiveTextMessageAsync(socket, buffer, token);
                    if (message == null)
                    {
                        break;
                    }
                    HandleMessage(message, socket, remoteAddress);
                }
            }
            catch (Exception ex)
            {
                Log($"WebSocket error: {ex.Message}");
            }
            finally
            {
                _pendingChallenges.TryRemove(socket, out _);
                if (ReferenceEquals(socket, _currentSocket))
                {
                    StopSessionWriter(socket);
                    _currentSocket = null;
                    _androidHandshakeComplete = false;

                    if (_scrcpyEngine.IsCaptured)
                    {
                        Log("Android companion disconnected. Returning control to Windows.");
                        await _scrcpyEngine.ReleaseAsync();
                    }

                    Dispatcher.Invoke(() => {
                        if (_currentSocket != null) return;
                        TxtDeviceName.Text = "-";
                        RefreshServerVisuals();
                        UpdateTransportDisplay();
                        _sessionManager.SetState(InputSessionState.Disconnected);
                    });
                }
            }
        }

        private async Task<string?> ReceiveTextMessageAsync(WebSocket socket, byte[] buffer, CancellationToken token)
        {
            using var stream = new MemoryStream();
            while (true)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    if (socket.State == WebSocketState.CloseReceived)
                        await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Client disconnected", CancellationToken.None);
                    Log("Client disconnected gracefully.");
                    return null;
                }
                if (result.MessageType != WebSocketMessageType.Text || stream.Length + result.Count > MaxWebSocketMessageBytes)
                {
                    await CloseRejectedSocketAsync(socket, "Invalid or oversized WebSocket message");
                    return null;
                }
                stream.Write(buffer, 0, result.Count);
                if (result.EndOfMessage) return Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
            }
        }

        private void HandleMessage(string json, WebSocket socket, string? remoteAddress)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var type = root.GetProperty("Type").GetString();
                var payload = root.GetProperty("Payload");

                if (type == "HELLO")
                {
                    int clientVersion = root.TryGetProperty("ProtocolVersion", out var pv) ? pv.GetInt32() : 0;
                    if (clientVersion != ProtocolVersion || !TryActivateCompanion(socket, remoteAddress, payload))
                    {
                        _ = CloseRejectedSocketAsync(socket, clientVersion == ProtocolVersion
                            ? "Pairing was not approved or authentication failed"
                            : "Protocol v2 required; update both Host and Android companion");
                        return;
                    }

                    _controlPaused = false;
                    if (payload.TryGetProperty("ConnectionMode", out var requestedMode))
                    {
                        var mode = requestedMode.GetString() switch {
                            "USB" => ConnectionMode.Usb, "Wi-Fi" => ConnectionMode.Network,
                            "Auto" => ConnectionMode.Auto, _ => _appSettings.ConnectionMode
                        };
                        Dispatcher.Invoke(() => {
                            _appSettings.ConnectionMode = mode;
                            CmbConnectionMode.SelectedIndex = (int)mode;
                        });
                    }
                    Dispatcher.InvokeAsync(RefreshServerVisuals);
                    _lastPublishedSessionStatus = null;
                    Dispatcher.InvokeAsync(UpdateTransportDisplay);
                    Dispatcher.InvokeAsync(async () => await StartEngineAsync());

                    var welcomeEnv = new MessageEnvelope
                    {
                        Type = "WELCOME",
                        ProtocolVersion = ProtocolVersion,
                        Payload = new { }
                    };
                    SendMessage(socket, JsonSerializer.Serialize(welcomeEnv));
                    _sessionManager.SetState(InputSessionState.Idle);
                }
                else if (!ReferenceEquals(socket, _currentSocket))
                {
                    _ = CloseRejectedSocketAsync(socket, "Authenticated HELLO required");
                }
                else if (!root.TryGetProperty("ProtocolVersion", out var version) || version.GetInt32() != ProtocolVersion)
                {
                    _ = CloseRejectedSocketAsync(socket, "Protocol v2 required");
                }
                else if (type == "PING")
                {
                    var pongEnv = new MessageEnvelope
                    {
                        Type = "PONG",
                        ProtocolVersion = ProtocolVersion,
                        Payload = payload
                    };
                    SendMessage(socket, JsonSerializer.Serialize(pongEnv));
                }
                else if (type == "RETURN_TO_WINDOWS")
                {
                    if (!ReferenceEquals(socket, _currentSocket) || !IsAndroidCompanionConnected()) return;
                    var returnPayload = payload.Clone(); // Dispatcher runs after JsonDocument is disposed.
                    if (returnPayload.TryGetProperty("SessionId", out var returnSession)
                        && returnSession.GetInt32() != _currentSessionId) return;
                    Log("Received RETURN_TO_WINDOWS from Android. Releasing Scrcpy capture.");
                    Dispatcher.Invoke(async () => {
                        // Native button input is sent by scrcpy, not the APK.
                        // A delayed edge request must never terminate a drag.
                        if (NativeMethods.IsMouseButtonPressed())
                        {
                            Log("Android edge return ignored: mouse button is held; native capture remains active.");
                            return;
                        }
                        if (returnPayload.TryGetProperty("SessionId", out var session)
                            && session.GetInt32() != _currentSessionId) return;
                        if (_scrcpyEngine != null && _scrcpyEngine.IsCaptured)
                        {
                            await _scrcpyEngine.ReleaseAsync();
                            SafeReleaseInputOwnership("Android edge");

                            // Re-arm guard
                            (_edgeTransitionService as EdgeTransitionService)?.StartRearmGuard();
                        }
                    });
                }
                else if (type == "CONTROL_STOP")
                {
                    if (!ReferenceEquals(socket, _currentSocket) || !IsAndroidCompanionConnected()) return;
                    _controlPaused = true;
                    Dispatcher.InvokeAsync(async () => {
                        await _scrcpyEngine.ReleaseAsync();
                        SafeReleaseInputOwnership("Android Stop Control");
                        UpdateTransportDisplay();
                    });
                }
                else if (type == "INPUT_HANDOFF_END")
                {
                    Log("Received INPUT_HANDOFF_END from Android. Returning control.");
                    Dispatcher.InvokeAsync(async () => await _scrcpyEngine.ReleaseAsync());
                }
            }
            catch (Exception ex)
            {
                Log($"Message parse error: {ex.Message}");
                _ = CloseRejectedSocketAsync(socket, "Malformed protocol message");
            }
        }

        private bool TryActivateCompanion(WebSocket socket, string? remoteAddress, JsonElement payload)
        {
            if (!_pendingChallenges.TryRemove(socket, out var challenge))
            {
                Log("[PAIRING] HELLO rejected: no pending challenge for this socket.");
                return false;
            }
            if (!payload.TryGetProperty("InstallationId", out var idElement)
                || !payload.TryGetProperty("PublicKey", out var keyElement)
                || !payload.TryGetProperty("Signature", out var signatureElement))
            {
                Log("[PAIRING] HELLO rejected: required identity fields are missing.");
                return false;
            }

            var installationId = idElement.GetString();
            var publicKey = keyElement.GetString();
            var signature = signatureElement.GetString();
            var deviceName = payload.TryGetProperty("DeviceName", out var nameElement) ? nameElement.GetString() : null;
            var suppliedCode = payload.TryGetProperty("PairingCode", out var codeElement) ? codeElement.GetString() : null;
            if (!PairingAuthenticator.IsValidInstallationId(installationId)
                || string.IsNullOrWhiteSpace(publicKey) || publicKey.Length > 2048
                || string.IsNullOrWhiteSpace(signature) || signature.Length > 1024
                || string.IsNullOrWhiteSpace(deviceName) || deviceName.Length > 128)
            {
                Log("[PAIRING] HELLO rejected: identity field validation failed.");
                return false;
            }
            if (!PairingAuthenticator.Verify(publicKey, challenge, installationId!, signature))
            {
                Log("[PAIRING] HELLO rejected: signature verification failed.");
                return false;
            }

            string expectedCode;
            try { expectedCode = PairingAuthenticator.CreatePairingCode(publicKey); }
            catch
            {
                Log("[PAIRING] HELLO rejected: pairing-code calculation failed.");
                return false;
            }
            if (!string.Equals(expectedCode, suppliedCode, StringComparison.Ordinal))
            {
                Log("[PAIRING] HELLO rejected: pairing code did not match the public key.");
                return false;
            }

            var existing = _appSettings.CompanionPairing;
            var isKnownKey = existing != null
                && existing.InstallationId == installationId
                && existing.PublicKey == publicKey;
            if (!isKnownKey)
            {
                var approval = Dispatcher.Invoke(() => MessageBox.Show(
                    $"Approve this Android companion?\n\nDevice: {deviceName}\nPairing code: {expectedCode}\n\nApprove only if the same code is visible in the Android app.",
                    "EZ Across Control pairing", MessageBoxButton.YesNo, MessageBoxImage.Question));
                if (approval != MessageBoxResult.Yes) return false;
                _appSettings.CompanionPairing = new CompanionPairing
                {
                    InstallationId = installationId!, DeviceName = deviceName!, PublicKey = publicKey
                };
                ConfigManager.Save(_appSettings);
                Log($"Approved Android pairing for {deviceName} ({installationId}).");
            }

            WebSocket? previous;
            lock (_socketGate)
            {
                previous = _currentSocket;
                _currentSocket = socket;
                _companionAddress = remoteAddress;
                _companionDeviceName = deviceName;
                _androidHandshakeComplete = true;
                _sessionQueues[socket] = new SessionQueues();
            }
            _ = NetworkWriterLoop(socket, _sessionQueues[socket], _cts!.Token);
            if (previous != null && !ReferenceEquals(previous, socket))
            {
                StopSessionWriter(previous);
                _ = CloseRejectedSocketAsync(previous, "Companion reconnected");
            }
            Dispatcher.Invoke(() => TxtDeviceName.Text = deviceName);
            Log($"Authenticated Android companion {deviceName} (v{ProtocolVersion}).");
            return true;
        }

        private async Task SendDirectAsync(WebSocket socket, MessageEnvelope message)
        {
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts?.Token ?? CancellationToken.None);
        }

        private async Task CloseRejectedSocketAsync(WebSocket socket, string reason)
        {
            try
            {
                if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
                    await socket.CloseOutputAsync(WebSocketCloseStatus.PolicyViolation, reason, CancellationToken.None);
            }
            catch { socket.Abort(); }
        }

        private void PositionMouseOnReturn(string returnEdgeStr)
        {
            try
            {
                if (!NativeMethods.GetCursorPos(out var currentPt) && !_pointerBeforeCapture.HasValue)
                    return;
                currentPt = _pointerBeforeCapture ?? currentPt;

                var mg = new MonitorGeometry();
                var monitor = mg.GetMonitorFromPoint(currentPt.x, currentPt.y);
                if (monitor == null)
                {
                    var all = mg.GetAllMonitors();
                    monitor = all.FirstOrDefault(m => m.IsPrimary) ?? all.FirstOrDefault();
                }
                if (monitor == null) return;

                int inset = Math.Max(16, _appSettings.EdgeTransition.EdgeThresholdPixels + 4);
                int x = 0;
                int y = currentPt.y;

                switch (returnEdgeStr)
                {
                    case "Right":
                        x = monitor.Bounds.right - inset - 1;
                        break;
                    case "Left":
                        x = monitor.Bounds.left + inset;
                        break;
                    case "Top":
                        x = currentPt.x;
                        y = monitor.Bounds.top + inset;
                        break;
                    case "Bottom":
                        x = currentPt.x;
                        y = monitor.Bounds.bottom - inset - 1;
                        break;
                    default:
                        // A manual capture may not have a configured edge. Return to
                        // the Windows position saved before scrcpy took the mouse.
                        x = currentPt.x;
                        y = currentPt.y;
                        break;
                }

                NativeMethods.SetCursorPos(x, y);
                Log($"Return cursor: edge={returnEdgeStr}, Windows=({currentPt.x},{currentPt.y}), target=({x},{y})");
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "PositionMouseOnReturn");
            }
        }

        private async Task NetworkWriterLoop(WebSocket socket, SessionQueues queues, CancellationToken serverToken)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(serverToken, queues.Cancellation.Token);
            var token = linked.Token;
            while (!token.IsCancellationRequested && ReferenceEquals(socket, _currentSocket))
            {
                try
                {
                    string? message = null;
                    if (queues.Priority.Reader.TryRead(out message))
                    {
                        // Priority message
                    }
                    else if (queues.Standard.Reader.TryRead(out message))
                    {
                        // Standard message
                    }
                    else
                    {
                        var priTask = queues.Priority.Reader.WaitToReadAsync(token).AsTask();
                        var stdTask = queues.Standard.Reader.WaitToReadAsync(token).AsTask();
                        await Task.WhenAny(priTask, stdTask);
                        continue;
                    }

                    if (message != null && ReferenceEquals(socket, _currentSocket) && socket.State == WebSocketState.Open)
                    {
                        var bytes = Encoding.UTF8.GetBytes(message);
                        await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, token);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Log($"Network send error: {ex.Message}");
                }
            }
        }

        private void SendMessage(string message, bool isPriority)
        {
            var socket = _currentSocket;
            if (socket == null || !_sessionQueues.TryGetValue(socket, out var queues)) return;
            (isPriority ? queues.Priority : queues.Standard).Writer.TryWrite(message);
        }

        private void SendMessage(WebSocket socket, string message)
        {
            if (socket == _currentSocket)
            {
                SendMessage(message, true); // initial handshake is high priority
            }
        }

        private void StopSessionWriter(WebSocket socket)
        {
            if (_sessionQueues.TryRemove(socket, out var queues))
            {
                queues.Cancellation.Cancel();
            }
        }



        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            StopServer();
        }

        private void StopServer()
        {
            if (_listener != null && _listener.IsListening)
            {
                _cts?.Cancel();
                _listener.Stop();
                _listener.Close();
                _listener = null;
                
                _androidHandshakeComplete = false;
                SafeReleaseInputOwnership("Server stopped");
                if (_currentSocket != null) StopSessionWriter(_currentSocket);
                _currentSocket?.Abort();
                _currentSocket = null;
                RefreshServerVisuals();
                // TxtAndroidStatus.Text = "Disconnected";
                TxtDeviceName.Text = "-";
                Log("Server stopped.");
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _closing = true;
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.ContextMenuStrip?.Dispose();
                _trayIcon.Dispose();
                _trayIcon = null;
            }
            _connectedTrayIcon?.Dispose();
            _disconnectedTrayIcon?.Dispose();
            _transportTimer.Stop();
            SafeReleaseInputOwnership("App closed");

            try
            {
                Task.Run(() => _scrcpyEngine.StopAsync()).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Stop scrcpy engine on app close");
            }
            
            _edgeTransitionService?.Stop();
            _inputCaptureService?.StopCapture();
            if (_inputCaptureService is IDisposable disposable)
            {
                disposable.Dispose();
            }
            _rawMouseInputService?.StopCapture();
            _rawMouseInputService?.Dispose();
            
            _sessionManager?.Dispose();
            StopServer();
            base.OnClosed(e);
        }

        private void InputCaptureService_InputEventCaptured(object? sender, InputEvent e)
        {
            if (e is KeyboardInputEvent ke && ke.Type == InputEventType.KeyDown && ke.KeyName == "Escape")
            {
                if (_scrcpyEngine != null && _scrcpyEngine.IsRunning && _scrcpyEngine.IsCaptured)
                {
                    Log("ESC pressed - Emergency return to Windows");
                    _ = _scrcpyEngine.ReleaseAsync();
                    return;
                }
                else if (_ownershipState == PointerOwnershipState.Remote)
                {
                    Log("ESC pressed - Emergency return to Windows (Legacy)");
                    SafeReleaseInputOwnership("Emergency");
                    return;
                }
            }

            _sessionManager?.EnqueueInput(e);

            if (_showInputDebug)
            {
                _inputEventQueue.Enqueue(e);
            }
        }

        private void RawMouseInputService_InputEventCaptured(object? sender, InputEvent e)
        {
            if (e is MouseInputEvent mouseEvent && mouseEvent.Type == InputEventType.MouseMove)
            {
                (_edgeTransitionService as EdgeTransitionService)?.ProcessRawMouseMove(mouseEvent.DeltaX, mouseEvent.DeltaY);
            }

            if (_ownershipState == PointerOwnershipState.Remote)
            {
                _sessionManager?.EnqueueInput(e);
                
                if (_showInputDebug)
                {
                    _inputEventQueue.Enqueue(e);
                }
            }
        }

        private void TakeInputOwnership()
        {
            if (_ownershipState == PointerOwnershipState.Remote) return;

            _ownershipState = PointerOwnershipState.Remote;

            if (UsesNativeScrcpyInput())
            {
                _sessionManager?.SetState(InputSessionState.Idle);
                Log($"Captured control to Android through native {_engineTransport} scrcpy input.");
            }
            else if (_inputCaptureService is WindowsInputCaptureService wic)
            {
                wic.SuppressLocalMouseEvents = true;
                wic.SuppressLocalKeyboardEvents = true;
                _sessionManager?.SetState(InputSessionState.Controlling);
                Log("Captured control to Android through the companion app.");
            }
        }

        private void SafeReleaseInputOwnership(string reason = "Emergency")
        {
            try
            {
                if (_ownershipState != PointerOwnershipState.Remote && !_pointerBeforeCapture.HasValue) return;

                _ownershipState = PointerOwnershipState.Local;
                
                if (_inputCaptureService is WindowsInputCaptureService wic)
                {
                    wic.SuppressLocalMouseEvents = false;
                    wic.SuppressLocalKeyboardEvents = false;
                }
                
                // _rawMouseInputService?.StopCapture(); - Removed as it runs continuously now
                
                if (_isCursorClipped)
                {
                    NativeMethods.ClipCursor(IntPtr.Zero);
                    _isCursorClipped = false;
                }

                _sessionManager?.SetState(InputSessionState.Idle);

                (_scrcpyEngine as ScrcpyProcessManager)?.HideInputWindow();

                if (_windowToRestoreAfterNativeCapture != IntPtr.Zero)
                {
                    NativeMethods.ActivateWindow(_windowToRestoreAfterNativeCapture);
                    _windowToRestoreAfterNativeCapture = IntPtr.Zero;
                }
                PositionMouseOnReturn(_edgeBeforeCapture.ToString());
                _pointerBeforeCapture = null;
                
                if (_currentSocket != null && _currentSocket.State == WebSocketState.Open)
                {
                    _sessionManager?.EnqueueHandoff("INPUT_HANDOFF_END", new { Reason = reason, SessionId = _currentSessionId });
                }
                
                Log($"Returned control to Windows. Reason: {reason}");
                (_edgeTransitionService as EdgeTransitionService)?.StartRearmGuard();
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "SafeReleaseInputOwnership");
            }
        }

        private void InputDebugTimer_Tick(object? sender, EventArgs e)
        {
            if (!_showInputDebug) 
            {
                // Discard items if debug is off but somehow queued
                while (_inputEventQueue.TryDequeue(out _)) { }
                return;
            }
            
            bool added = false;
            int count = 0;
            // Cap at processing max 50 items per tick (1000 items per sec at 20fps) to avoid hanging UI
            while (count < 50 && _inputEventQueue.TryDequeue(out var inputEvent))
            {
                _debugLogLines.Add(inputEvent.ToString());
                added = true;
                count++;
            }

            if (added)
            {
                // Keep only last 100
                if (_debugLogLines.Count > 100)
                {
                    _debugLogLines.RemoveRange(0, _debugLogLines.Count - 100);
                }
                
                TxtInputDebug.Text = string.Join(Environment.NewLine, _debugLogLines);
                TxtInputDebug.ScrollToEnd();
            }
        }

        private async void BtnStartEngine_Click(object sender, RoutedEventArgs e)
        {
            await StartEngineAsync();
        }

        private bool _engineStartPending;

        private async Task StartEngineAsync()
        {
            if (!IsAndroidCompanionConnected())
            {
                Log("Waiting for Connect in the Android app.");
                return;
            }
            if (_closing || _engineStartPending) return;
            _engineStartPending = true;
            try
            {
            if (_deviceManager == null) _deviceManager = new AndroidDeviceManager();
            var devices = await _deviceManager.GetDevicesAsync();
            var mode = _appSettings.ConnectionMode;
            bool needsWireless = mode == ConnectionMode.Network || mode == ConnectionMode.Auto;
            if (needsWireless && AndroidDeviceManager.FindCompanionDevice(devices,
                ConnectionMode.Network, _companionDeviceName, _companionAddress) == null)
            {
                Log("Looking for the paired Wi-Fi ADB connection...");
                var usb = AndroidDeviceManager.FindCompanionDevice(devices, ConnectionMode.Usb,
                    _companionDeviceName, _companionAddress);
                await _deviceManager.DiscoverNetworkAsync(_companionAddress, usb);
                devices = await _deviceManager.GetDevicesAsync();
            }
            var device = AndroidDeviceManager.FindCompanionDevice(devices, mode,
                _companionDeviceName, _companionAddress);
            if (_closing || !IsAndroidCompanionConnected()) return;
            _activeDevice = device;

            string? selectedSerial = device?.GetSerial(mode);
            var manager = _scrcpyEngine as ScrcpyProcessManager;
            if (_scrcpyEngine.IsRunning && manager?.ActiveSerial == selectedSerial)
            {
                UpdateTransportDisplay();
                return;
            }
            if (_scrcpyEngine.IsRunning)
            {
                Log($"Applying connection mode: {mode}. Previous transport: {_engineTransport}.");
                await _scrcpyEngine.StopAsync();
                SafeReleaseInputOwnership("Transport changed or disconnected");
            }
            
            if (device != null)
            {
                TxtDeviceName.Text = device.Model ?? "Unknown Device";
                UpdateTransportDisplay();
            }
            else
            {
                TxtDeviceName.Text = Localization.T("No Device Found");
                _engineTransport = "-";
                UpdateTransportDisplay();
                if (_lastUnavailableMode != mode.ToString()) Log(mode == ConnectionMode.Network
                    ? "Wi-Fi control unavailable: enable paired Wireless debugging on Android, then reconnect the APK. Connecting the APK alone does not establish Wi-Fi ADB."
                    : mode == ConnectionMode.Usb
                        ? "USB control unavailable: connect the authorized USB cable. Wi-Fi is not used in USB mode."
                        : "Automatic engine start: no matching ADB device found. Check the cable or paired Wireless debugging.");
                _lastUnavailableMode = mode.ToString();
                BtnStartEngine.IsEnabled = true;
                Localization.SetStatus(TxtEngineState, "NO DEVICE");
                Localization.SetStatus(TxtEngineStatus, "NO DEVICE");
                UpdateEngineBadge();
                return;
            }
            
            _lastUnavailableMode = null;
            Localization.SetStatus(TxtEngineState, "STARTING...");
            UpdateEngineBadge();
            BtnStartEngine.IsEnabled = false;
            await _scrcpyEngine.StartAsync(mode, device);
            UpdateTransportDisplay();
            Log($"Connection mode: {mode}. Active transport: {_engineTransport}. Engine: {_scrcpyEngine.State}.");
            }
            catch (Exception ex) { Log($"Engine start failed: {ex.Message}"); }
            finally { _engineStartPending = false; }
        }

        private async void BtnCaptureAndroid_Click(object sender, RoutedEventArgs e)
        {
            if (!IsHandoffAvailable())
            {
                Log("Capture ignored: press Connect in the Android companion app first.");
                return;
            }

            if (!await PrepareCaptureAsync()) return;
            BeginAndroidHandoff(new EdgeTransitionEventArgs(EdgeState.Armed, _appSettings.EdgeTransition.ActiveEdge));
            await _scrcpyEngine.CaptureAsync();
        }

        private async void BtnReturnWindows_Click(object sender, RoutedEventArgs e)
        {
            await _scrcpyEngine.ReleaseAsync();
        }

        private bool IsAndroidCompanionConnected()
        {
            return _androidHandshakeComplete && _currentSocket?.State == WebSocketState.Open;
        }

        private void UpdateTransportDisplay()
        {
            _engineTransport = _scrcpyEngine.IsRunning
                ? ((_scrcpyEngine as ScrcpyProcessManager)?.ActiveTransport == ConnectionMode.Network ? "Wi-Fi" : "USB")
                : "-";
            TxtTransport.Text = _engineTransport;
            BtnCaptureAndroid.IsEnabled = IsAndroidCompanionConnected()
                && !_controlPaused && _scrcpyEngine.State == ScrcpyEngineState.Ready;
            var signature = _engineTransport + ":" + _scrcpyEngine.State + ":" + _controlPaused;
            if (IsAndroidCompanionConnected() && signature != _lastPublishedSessionStatus)
            {
                _lastPublishedSessionStatus = signature;
                SendMessage(_currentSocket!, JsonSerializer.Serialize(new MessageEnvelope
                {
                    Type = "SESSION_STATUS",
                    ProtocolVersion = ProtocolVersion,
                    Payload = new
                    {
                        ControlTransport = _engineTransport == "-" ? null : _engineTransport,
                        EngineState = _scrcpyEngine.State.ToString(),
                        ControlEnabled = !_controlPaused
                    }
                }));
            }
        }

        private bool UsesNativeScrcpyInput()
        {
            return _scrcpyEngine.IsRunning; // UHID input is identical over USB and Wi-Fi ADB.
        }

        private bool IsHandoffAvailable()
        {
            return IsAndroidCompanionConnected() && !_controlPaused && _scrcpyEngine.IsRunning;
        }

        private bool ParkNativeInputAtEdge(NativeMethods.POINT entryPointer)
        {
            var monitor = new MonitorGeometry().GetMonitorFromPoint(entryPointer.x, entryPointer.y);
            if (monitor == null) return false;

            int x = Math.Clamp(entryPointer.x, monitor.Bounds.left, monitor.Bounds.right - 1);
            int y = Math.Clamp(entryPointer.y, monitor.Bounds.top, monitor.Bounds.bottom - 1);
            switch (_edgeBeforeCapture)
            {
                case ScreenEdge.Right: x = monitor.Bounds.right - 1; break;
                case ScreenEdge.Left: x = monitor.Bounds.left; break;
                case ScreenEdge.Top: y = monitor.Bounds.top; break;
                case ScreenEdge.Bottom: y = monitor.Bounds.bottom - 1; break;
            }

            return (_scrcpyEngine as ScrcpyProcessManager)?.ParkInputWindowAt(x, y) == true;
        }

        private async Task<bool> PrepareCaptureAsync()
        {
            if (!Dispatcher.CheckAccess())
                return await Dispatcher.InvokeAsync(PrepareCaptureAsync).Task.Unwrap();
            if (!IsHandoffAvailable() || _scrcpyEngine.IsCaptured) return false;
            if (!UsesNativeScrcpyInput()) return true;
            _windowToRestoreAfterNativeCapture = NativeMethods.GetForegroundWindow();
            NativeMethods.GetCursorPos(out var entryPointer);
            _pointerBeforeCapture = entryPointer;
            _edgeBeforeCapture = _appSettings.EdgeTransition.ActiveEdge;
            bool focused = ParkNativeInputAtEdge(entryPointer)
                && (_scrcpyEngine as ScrcpyProcessManager)?.ActivateInputWindow() == true;
            if (!focused)
            {
                _windowToRestoreAfterNativeCapture = IntPtr.Zero;
                _pointerBeforeCapture = null;
                (_scrcpyEngine as ScrcpyProcessManager)?.HideInputWindow();
                NativeMethods.SetCursorPos(entryPointer.x, entryPointer.y);
                Log("Capture cancelled: Windows did not place and activate the scrcpy input window.");
                (_edgeTransitionService as EdgeTransitionService)?.StartRearmGuard();
            }
            return focused;
        }

        private void BeginAndroidHandoff(EdgeTransitionEventArgs handoff)
        {
            if (!IsAndroidCompanionConnected()) return;

            _currentSessionId++;
            _sessionManager.EnqueueHandoff("INPUT_HANDOFF_BEGIN", new HandoffBeginPayload
            {
                Edge = handoff.Edge.ToString(),
                SessionId = _currentSessionId,
                EntryNormalizedY = GetEntryNormalizedCoordinate(handoff.Edge),
                ClientTxTimestamp = Stopwatch.GetTimestamp()
            });
            Log(UsesNativeScrcpyInput()
                ? $"Native {_engineTransport} capture started. Android return edge armed. Edge={handoff.Edge}, Session={_currentSessionId}"
                : $"Beginning Android handoff. Edge={handoff.Edge}, Session={_currentSessionId}");
        }

        private double GetEntryNormalizedCoordinate(ScreenEdge edge)
        {
            var pointer = _pointerBeforeCapture;
            NativeMethods.POINT point;
            if (pointer.HasValue) point = pointer.Value;
            else if (!NativeMethods.GetCursorPos(out point)) return 0.5;
            var monitor = new MonitorGeometry().GetMonitorFromPoint(point.x, point.y);
            if (monitor == null) return 0.5;
            var alongVerticalEdge = edge == ScreenEdge.Left || edge == ScreenEdge.Right;
            var start = alongVerticalEdge ? monitor.Bounds.top : monitor.Bounds.left;
            var length = alongVerticalEdge ? monitor.Bounds.bottom - monitor.Bounds.top : monitor.Bounds.right - monitor.Bounds.left;
            var coordinate = alongVerticalEdge ? point.y : point.x;
            return length > 1 ? Math.Clamp((coordinate - start) / (double)(length - 1), 0, 1) : 0.5;
        }

        // Legacy Uhid routing removed for V2
        private void EdgeTransitionService_StateChanged(object? sender, EdgeTransitionEventArgs e)
        {
            Dispatcher.InvokeAsync(() =>
            {
                switch (e.State)
                {
                    case EdgeState.Idle: Localization.SetStatus(TxtEdgeStatus, "Waiting"); break;
                    case EdgeState.Candidate: Localization.SetStatus(TxtEdgeStatus, "Edge Detected"); break;
                    case EdgeState.Armed: Localization.SetStatus(TxtEdgeStatus, "Ready To Switch"); break;
                    case EdgeState.Disabled:
                    case EdgeState.Cancelled:
                        Localization.SetStatus(TxtEdgeStatus, e.State.ToString());
                        break;
                }
            });
        }

        private async void CmbConnectionMode_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_appSettings == null) return;
            
            if (CmbConnectionMode.SelectedItem is System.Windows.Controls.ComboBoxItem item)
            {
                if (Enum.TryParse(item.Tag.ToString(), out ConnectionMode mode))
                {
                    if (_appSettings.ConnectionMode == mode) return;
                    _appSettings.ConnectionMode = mode;
                    ConfigManager.Save(_appSettings);
                    if (_scrcpyEngine != null && IsAndroidCompanionConnected())
                    {
                        CmbConnectionMode.IsEnabled = false;
                        try
                        {
                            await StartEngineAsync();
                        }
                        catch (Exception ex) { Log($"Transport switch failed: {ex.Message}"); }
                        finally { CmbConnectionMode.IsEnabled = true; }
                    }
                }
            }
        }

        private void CmbActiveEdge_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_appSettings == null || _edgeTransitionService == null) return;

            if (CmbActiveEdge.SelectedItem is System.Windows.Controls.ComboBoxItem item)
            {
                if (Enum.TryParse(item.Tag.ToString(), out ScreenEdge edge))
                {
                    _appSettings.EdgeTransition.ActiveEdge = edge;
                    SaveSettingsAndUpdateService();
                }
            }
        }

        private void TxtEdgeOptions_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_appSettings == null || _edgeTransitionService == null) return;

            if (int.TryParse(TxtEdgeThreshold.Text, out int threshold) && threshold > 0)
            {
                _appSettings.EdgeTransition.EdgeThresholdPixels = threshold;
            }

            if (int.TryParse(TxtEdgeDelay.Text, out int delay) && delay >= 0)
            {
                _appSettings.EdgeTransition.EdgeActivationDelayMs = delay;
            }

            SaveSettingsAndUpdateService();
        }

        private void SaveSettingsAndUpdateService()
        {
            ConfigManager.Save(_appSettings);
            _edgeTransitionService.UpdateOptions(_appSettings.EdgeTransition);
        }

        private void HiddenImeSink_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (_ownershipState == PointerOwnershipState.Remote && _sessionManager != null && !string.IsNullOrEmpty(e.Text))
            {
                _sessionManager.SendEnvelope("INPUT_TEXT_COMMIT", new TextCommitPayload
                {
                    Text = e.Text
                });
                e.Handled = true; // Prevent text from accumulating in the TextBox
                HiddenImeSink.Text = ""; // Clear just in case
            }
        }

        private void HiddenImeSink_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (HiddenImeSink.Text.Length > 0 && _ownershipState == PointerOwnershipState.Remote)
            {
                // Some IME systems bypass PreviewTextInput and directly update Text.
                // If text arrives here, we commit it and clear.
                if (_sessionManager != null)
                {
                    _sessionManager.SendEnvelope("INPUT_TEXT_COMMIT", new TextCommitPayload
                    {
                        Text = HiddenImeSink.Text
                    });
                }
                HiddenImeSink.Text = "";
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_inputCaptureService is IDisposable disposableInput)
            {
                disposableInput.Dispose();
            }
            if (_rawMouseInputService != null)
            {
                _rawMouseInputService.Dispose();
            }
            if (_listener != null && _listener.IsListening)
            {
                _listener.Stop();
            }
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel();
            }
        }
    }
}
