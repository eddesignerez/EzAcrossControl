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
        private CancellationTokenSource? _cts;
        private WebSocket? _currentSocket;
        
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
        private IntPtr _windowToRestoreAfterNativeCapture;
        private NativeMethods.POINT? _pointerBeforeCapture;
        private ScreenEdge _edgeBeforeCapture;
        private string? _companionAddress;
        private string? _companionDeviceName;
        private AppSettings _appSettings;
        private InputSessionManager _sessionManager;
        private bool _showInputDebug = false;
        private Channel<string> _priorityNetworkQueue = Channel.CreateUnbounded<string>();
        private Channel<string> _standardNetworkQueue = Channel.CreateUnbounded<string>();
        private int _currentSessionId = 0;
        
        // V2 Scrcpy Engine
        private IScrcpyControlEngine _scrcpyEngine;
        private AndroidDeviceManager? _deviceManager;
        private WinForms.NotifyIcon? _trayIcon;
        private bool _exitRequested;
        private bool _trayHintShown;
        private double _heightBeforeAdvanced;
        private double _topBeforeAdvanced;


        public MainWindow()
        {
            InitializeComponent();
            InitializeTrayIcon();
            TxtLocalIP.Text = GetLocalIPAddress();
            TxtPort.Text = Config.DefaultPort.ToString();
            UpdateThemeButtons();
            
            // Auto-start the server
            BtnStart_Click(null, null);

            _appSettings = ConfigManager.Load();

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
                TxtEngineState.Text = state.ToString().ToUpper();
                TxtEngineStatus.Text = state.ToString().ToUpper();

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
                if (!_closing && IsAndroidCompanionConnected()) await StartEngineAsync();
            };
            _transportTimer.Start();

            // Input debug UI removed; input debug disabled by default.
            // _showInputDebug bool controls debug output if needed.

            Loaded += (_, _) => {
                TxtEngineState.Text = "WAITING APK";
                TxtEngineStatus.Text = "WAITING APK";
                BtnStartEngine.IsEnabled = false;
                UpdateEngineBadge();
                Log("Waiting for Connect in the Android app. Engine starts automatically after HELLO.");
            };

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

        private void UpdateEngineBadge()
        {
            TxtEngineState.Foreground = (Brush)FindResource(
                TxtEngineState.Text.Equals("READY", StringComparison.OrdinalIgnoreCase)
                    ? "SuccessPrimaryBrush" : "TextPrimaryBrush");
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
                var workArea = SystemParameters.WorkArea;
                Height = Math.Min(workArea.Height - 20, Math.Max(Height + 330, 950));
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
            var menu = new WinForms.ContextMenuStrip();
            menu.Items.Add("Restore", null, (_, _) => Dispatcher.BeginInvoke(RestoreFromTray));
            menu.Items.Add("Exit", null, (_, _) => Dispatcher.BeginInvoke(ExitFromTray));
            _trayIcon = new WinForms.NotifyIcon
            {
                Icon = File.Exists(iconPath) ? new Drawing.Icon(iconPath) : Drawing.SystemIcons.Application,
                Text = "EZ Across Control",
                ContextMenuStrip = menu,
                Visible = true
            };
            _trayIcon.DoubleClick += (_, _) => Dispatcher.BeginInvoke(RestoreFromTray);
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
                System.IO.File.AppendAllText("server_log.txt", $"[{DateTime.Now:HH:mm:ss}] {message}\n");
            }
            catch { }
            
            Dispatcher.Invoke(() =>
            {
                TxtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
                TxtLog.ScrollToEnd();
            });
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(TxtPort.Text, out int port))
            {
                MessageBox.Show("Invalid port");
                return;
            }

            _cts = new CancellationTokenSource();
            _listener = new HttpListener();
            
            string prefix = $"http://+:{port}/";
            Log($"[SERVER] Starting...");
            Log($"[SERVER] Prefix={prefix}");
            Log($"[SERVER] Port={port}");

            try
            {
                _listener.Prefixes.Add(prefix);
                _listener.Start();
                Log($"[SERVER] Listener.IsListening={_listener.IsListening}");
                StartListening(prefix);
            }
            catch (HttpListenerException ex) when (ex.ErrorCode == 5)
            {
                TxtServerStatus.Text = "Error";
                Log($"[SERVER] Error: Access Denied (HTTP 400 cause). You MUST run EZ Across Control as Administrator to listen on LAN IP.");
                MessageBox.Show("Please restart the application as Administrator to accept LAN connections.", "Administrator Required");
                _listener?.Close();
                _listener = null;
            }
            catch (Exception ex)
            {
                TxtServerStatus.Text = "Error";
                Log($"[SERVER] Error starting server: {ex.Message}");
                _listener?.Close();
                _listener = null;
            }
        }

        private void StartListening(string prefix)
        {
            TxtServerStatus.Text = "Running";
            BtnStart.IsEnabled = false;
            BtnStop.IsEnabled = true;
            TxtPort.IsEnabled = false;
            Log($"Server started on {prefix}");
            
            _ = AcceptConnectionsAsync();
            _ = NetworkWriterLoop();
        }

        private async Task AcceptConnectionsAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    
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
                var wsContext = await context.AcceptWebSocketAsync(null);
                Log("Client connected.");
                
                if (_currentSocket != null && _currentSocket.State == WebSocketState.Open)
                {
                    Log("Closing previous connection.");
                    await _currentSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "New client connected", CancellationToken.None);
                }

                _currentSocket = wsContext.WebSocket;
                _companionAddress = context.Request.RemoteEndPoint?.Address.MapToIPv4().ToString();
                _androidHandshakeComplete = false;
                // Dispatcher.Invoke(() => TxtAndroidStatus.Text = "Connected");
                _sessionManager.SetState(InputSessionState.Connected);

                await ReceiveLoopAsync(_currentSocket);
            }
            catch (Exception ex)
            {
                Log($"WebSocket accept error: {ex.Message}");
            }
        }

        private async Task ReceiveLoopAsync(WebSocket socket)
        {
            var buffer = new byte[4096];
            try
            {
                while (socket.State == WebSocketState.Open && !_cts.IsCancellationRequested)
                {
                    var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnected", CancellationToken.None);
                        Log("Client disconnected gracefully.");
                        break;
                    }
                    
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    HandleMessage(message, socket);
                }
            }
            catch (Exception ex)
            {
                Log($"WebSocket error: {ex.Message}");
            }
            finally
            {
                if (ReferenceEquals(socket, _currentSocket))
                {
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
                        UpdateTransportDisplay();
                        _sessionManager.SetState(InputSessionState.Disconnected);
                    });
                }
            }
        }

        private void HandleMessage(string json, WebSocket socket)
        {
            if (!ReferenceEquals(socket, _currentSocket)) return;
            try
            {
                using var doc = JsonDocument.Parse(json);
                var type = doc.RootElement.GetProperty("Type").GetString();
                var payload = doc.RootElement.GetProperty("Payload");

                if (type == "HELLO")
                {
                    int clientVersion = doc.RootElement.TryGetProperty("ProtocolVersion", out var pv) ? pv.GetInt32() : 0;
                    var deviceName = payload.GetProperty("DeviceName").GetString();
                    _companionDeviceName = deviceName;
                    Dispatcher.Invoke(() => TxtDeviceName.Text = deviceName);
                    Log($"Received HELLO from {deviceName} (v{clientVersion})");

                    _androidHandshakeComplete = true;
                    Dispatcher.InvokeAsync(UpdateTransportDisplay);
                    Dispatcher.InvokeAsync(async () => await StartEngineAsync());

                    if (clientVersion != 1)
                    {
                        Log($"Warning: Client connected with incompatible protocol version {clientVersion}");
                    }

                    var welcomeEnv = new MessageEnvelope
                    {
                        Type = "WELCOME",
                        ProtocolVersion = 1,
                        Payload = new { }
                    };
                    SendMessage(socket, JsonSerializer.Serialize(welcomeEnv));
                    _sessionManager.SetState(InputSessionState.Idle);
                }
                else if (type == "PING")
                {
                    var pongEnv = new MessageEnvelope
                    {
                        Type = "PONG",
                        ProtocolVersion = 1,
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
                else if (type == "INPUT_HANDOFF_END")
                {
                    Log("Received INPUT_HANDOFF_END from Android. Returning control.");
                    Dispatcher.InvokeAsync(async () => await _scrcpyEngine.ReleaseAsync());
                }
            }
            catch (Exception ex)
            {
                Log($"Message parse error: {ex.Message}");
            }
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

        private async Task NetworkWriterLoop()
        {
            while (!_cts?.IsCancellationRequested ?? true)
            {
                try
                {
                    string? message = null;
                    if (_priorityNetworkQueue.Reader.TryRead(out message))
                    {
                        // Priority message
                    }
                    else if (_standardNetworkQueue.Reader.TryRead(out message))
                    {
                        // Standard message
                    }
                    else
                    {
                        var priTask = _priorityNetworkQueue.Reader.WaitToReadAsync(_cts?.Token ?? CancellationToken.None).AsTask();
                        var stdTask = _standardNetworkQueue.Reader.WaitToReadAsync(_cts?.Token ?? CancellationToken.None).AsTask();
                        await Task.WhenAny(priTask, stdTask);
                        continue;
                    }

                    if (message != null && _currentSocket != null && _currentSocket.State == WebSocketState.Open)
                    {
                        var bytes = Encoding.UTF8.GetBytes(message);
                        await _currentSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts?.Token ?? CancellationToken.None);
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
            if (isPriority)
            {
                _priorityNetworkQueue.Writer.TryWrite(message);
            }
            else
            {
                _standardNetworkQueue.Writer.TryWrite(message);
            }
        }

        private void SendMessage(WebSocket socket, string message)
        {
            if (socket == _currentSocket)
            {
                SendMessage(message, true); // initial handshake is high priority
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
                
                TxtServerStatus.Text = "Stopped";
                BtnStart.IsEnabled = true;
                BtnStop.IsEnabled = false;
                TxtPort.IsEnabled = true;
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
            bool needsWireless = mode == ConnectionMode.Network
                || (mode == ConnectionMode.Auto
                    && AndroidDeviceManager.FindCompanionDevice(devices, ConnectionMode.Usb,
                        _companionDeviceName, _companionAddress) == null);
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
                TxtDeviceName.Text = "No Device Found";
                _engineTransport = "-";
                UpdateTransportDisplay();
                if (_lastUnavailableMode != mode.ToString()) Log(mode == ConnectionMode.Network
                    ? "Wi-Fi control unavailable: enable paired Wireless debugging on Android, then reconnect the APK. Connecting the APK alone does not establish Wi-Fi ADB."
                    : mode == ConnectionMode.Usb
                        ? "USB control unavailable: connect the authorized USB cable. Wi-Fi is not used in USB mode."
                        : "Automatic engine start: no matching ADB device found. Check the cable or paired Wireless debugging.");
                _lastUnavailableMode = mode.ToString();
                BtnStartEngine.IsEnabled = true;
                TxtEngineState.Text = "NO DEVICE";
                TxtEngineStatus.Text = "NO DEVICE";
                UpdateEngineBadge();
                return;
            }
            
            _lastUnavailableMode = null;
            TxtEngineState.Text = "STARTING...";
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
                && _scrcpyEngine.State == ScrcpyEngineState.Ready;
        }

        private bool UsesNativeScrcpyInput()
        {
            return _scrcpyEngine.IsRunning; // UHID input is identical over USB and Wi-Fi ADB.
        }

        private bool IsHandoffAvailable()
        {
            return IsAndroidCompanionConnected() && _scrcpyEngine.IsRunning;
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
                EntryNormalizedY = 0.5,
                ClientTxTimestamp = Stopwatch.GetTimestamp()
            });
            Log(UsesNativeScrcpyInput()
                ? $"Native {_engineTransport} capture started. Android return edge armed. Edge={handoff.Edge}, Session={_currentSessionId}"
                : $"Beginning Android handoff. Edge={handoff.Edge}, Session={_currentSessionId}");
        }

        // Legacy Uhid routing removed for V2
        private void EdgeTransitionService_StateChanged(object? sender, EdgeTransitionEventArgs e)
        {
            Dispatcher.InvokeAsync(() =>
            {
                switch (e.State)
                {
                    case EdgeState.Idle: TxtEdgeStatus.Text = "Waiting"; break;
                    case EdgeState.Candidate: TxtEdgeStatus.Text = "Edge Detected"; break;
                    case EdgeState.Armed: TxtEdgeStatus.Text = "Ready To Switch"; break;
                    case EdgeState.Disabled:
                    case EdgeState.Cancelled:
                        TxtEdgeStatus.Text = e.State.ToString();
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
