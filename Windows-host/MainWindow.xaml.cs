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
using WindowsHost.Input;
using WindowsHost.Input.Edge;
using WindowsHost.Protocol;
using System.Windows.Input;
using EZAcrossControl.Input;

namespace WindowsHost
{
    public partial class MainWindow : Window
    {
        private HttpListener _listener;
        private CancellationTokenSource _cts;
        private WebSocket _currentSocket;
        
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
        private ConcurrentQueue<InputEvent> _inputEventQueue;
        private List<string> _debugLogLines;
        
        private IEdgeTransitionService _edgeTransitionService;
        private AppSettings _appSettings;
        private InputSessionManager _sessionManager;
        private UhidDirectBackend _uhidBackend;
        private Channel<string> _priorityNetworkQueue = Channel.CreateUnbounded<string>();
        private Channel<string> _standardNetworkQueue = Channel.CreateUnbounded<string>();
        private int _currentSessionId = 0;

        public MainWindow()
        {
            InitializeComponent();
            TxtLocalIP.Text = GetLocalIPAddress();
            TxtPort.Text = Config.DefaultPort.ToString();
            
            var savedTheme = ThemeManager.LoadThemePreference();
            foreach (System.Windows.Controls.ComboBoxItem item in CmbTheme.Items)
            {
                if (item.Tag.ToString() == savedTheme.ToString())
                {
                    CmbTheme.SelectedItem = item;
                    break;
                }
            }
            
            // Auto-start the server
            BtnStart_Click(null, null);

            _appSettings = ConfigManager.Load();

            _inputCaptureService = new WindowsInputCaptureService();
            _inputCaptureService.InputEventCaptured += InputCaptureService_InputEventCaptured;
            
            _rawMouseInputService = new WindowsRawMouseInputService(this);
            _rawMouseInputService.InputEventCaptured += RawMouseInputService_InputEventCaptured;
            _rawMouseInputService.StartCapture(); // Run continuously to detect edge pushes
            
            _edgeTransitionService = new EdgeTransitionService(_inputCaptureService);
            _edgeTransitionService.StateChanged += EdgeTransitionService_StateChanged;
            _edgeTransitionService.UpdateOptions(_appSettings.EdgeTransition);
            _edgeTransitionService.Start();

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
            
            _inputEventQueue = new ConcurrentQueue<InputEvent>();
            _debugLogLines = new List<string>();
            
            _inputDebugTimer = new DispatcherTimer();
            _inputDebugTimer.Interval = TimeSpan.FromMilliseconds(50);
            _inputDebugTimer.Tick += InputDebugTimer_Tick;
            _inputDebugTimer.Start();

            ChkShowInputDebug.Checked += (s, e) => { TxtInputDebug.Visibility = Visibility.Visible; };
            ChkShowInputDebug.Unchecked += (s, e) => { TxtInputDebug.Visibility = Visibility.Collapsed; };

            ChkEnableStreamingTest.Checked += (s, e) => { _sessionManager.EnableInputStreamingTest = true; };
            ChkEnableStreamingTest.Unchecked += (s, e) => { _sessionManager.EnableInputStreamingTest = false; };
        }

        private void CmbTheme_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (CmbTheme.SelectedItem is System.Windows.Controls.ComboBoxItem selectedItem)
            {
                if (Enum.TryParse(selectedItem.Tag.ToString(), out AppTheme theme))
                {
                    ThemeManager.SaveThemePreference(theme);
                }
            }
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
                _listener.Close();
                _listener = null;
            }
            catch (Exception ex)
            {
                TxtServerStatus.Text = "Error";
                Log($"[SERVER] Error starting server: {ex.Message}");
                _listener.Close();
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
                Dispatcher.Invoke(() => TxtAndroidStatus.Text = "Connected");
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
                Dispatcher.Invoke(() => {
                    TxtAndroidStatus.Text = "Disconnected";
                    TxtDeviceName.Text = "-";
                });
                _sessionManager.SetState(InputSessionState.Disconnected);
            }
        }

        private void HandleMessage(string json, WebSocket socket)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var type = doc.RootElement.GetProperty("Type").GetString();
                var payload = doc.RootElement.GetProperty("Payload");

                if (type == "HELLO")
                {
                    int clientVersion = doc.RootElement.TryGetProperty("ProtocolVersion", out var pv) ? pv.GetInt32() : 0;
                    var deviceName = payload.GetProperty("DeviceName").GetString();
                    Dispatcher.Invoke(() => TxtDeviceName.Text = deviceName);
                    Log($"Received HELLO from {deviceName} (v{clientVersion})");

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
                else if (type == "INPUT_HANDOFF_END")
                {
                    Log("Received INPUT_HANDOFF_END from Android. Returning control.");
                    Dispatcher.Invoke(() => SafeReleaseInputOwnership("Normal"));
                }
            }
            catch (Exception ex)
            {
                Log($"Message parse error: {ex.Message}");
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
                TxtAndroidStatus.Text = "Disconnected";
                TxtDeviceName.Text = "-";
                Log("Server stopped.");
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            SafeReleaseInputOwnership("App closed");
            
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
                if (_ownershipState == PointerOwnershipState.Remote)
                {
                    Log("ESC pressed - Emergency return to Windows");
                    SafeReleaseInputOwnership("Emergency");
                    return;
                }
            }

            if (_uhidBackend != null && _uhidBackend.IsActive)
            {
                // We ignore MouseMove here because it is handled by RawMouseInputService
                if (!(e is MouseInputEvent me && me.Type == InputEventType.MouseMove))
                {
                    RouteToUhidBackend(e);
                }
            }
            else
            {
                _sessionManager?.EnqueueInput(e);
            }

            if (ChkShowInputDebug.IsChecked == true)
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
                if (_uhidBackend != null && _uhidBackend.IsActive)
                {
                    RouteToUhidBackend(e);
                }
                else
                {
                    _sessionManager?.EnqueueInput(e);
                }
                
                if (ChkShowInputDebug.IsChecked == true)
                {
                    _inputEventQueue.Enqueue(e);
                }
            }
        }

        private void SafeReleaseInputOwnership(string reason = "Emergency")
        {
            try
            {
                if (_ownershipState != PointerOwnershipState.Remote) return;

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
                
                if (_currentSocket != null && _currentSocket.State == WebSocketState.Open)
                {
                    _sessionManager?.EnqueueHandoff("INPUT_HANDOFF_END", new { Reason = reason, SessionId = _currentSessionId });
                }
                
                Log($"Returned control to Windows. Reason: {reason}");
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "SafeReleaseInputOwnership");
            }
        }

        private void InputDebugTimer_Tick(object? sender, EventArgs e)
        {
            if (ChkShowInputDebug.IsChecked != true) 
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

        private void BtnStartCapture_Click(object sender, RoutedEventArgs e)
        {
            _inputCaptureService.StartCapture();
            TxtCaptureStatus.Text = "Capturing";
            BtnStartCapture.IsEnabled = false;
            BtnStopCapture.IsEnabled = true;
        }

        private void BtnStopCapture_Click(object sender, RoutedEventArgs e)
        {
            _inputCaptureService.StopCapture();
            TxtCaptureStatus.Text = "Idle";
            BtnStartCapture.IsEnabled = true;
            BtnStopCapture.IsEnabled = false;
        }

        private async void BtnStartUHID_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                BtnStartUHID.IsEnabled = false;
                _uhidBackend = new UhidDirectBackend("123456");
                string ip = TxtLocalIP.Text;
                if (TxtAndroidStatus.Text == "Connected") 
                {
                    // For now use a hardcoded test IP or something? 
                    // Actually, ADB reverse or direct LAN to Android device IP is needed.
                    // Let's assume the user has forwarded port 8797 via adb, so we connect to localhost.
                    ip = "127.0.0.1";
                }
                else
                {
                    ip = "127.0.0.1";
                }
                
                await _uhidBackend.ConnectAsync(ip);
                _uhidBackend.SetRemoteControl(true);
                
                // Start capturing
                _inputCaptureService.SuppressLocalMouseEvents = true;
                _inputCaptureService.SuppressLocalKeyboardEvents = true;
                _inputCaptureService.StartCapture();
                
                _ownershipState = PointerOwnershipState.Remote;

                TxtCaptureStatus.Text = "UHID Active";
                BtnStopUHID.IsEnabled = true;
                Log("Started UHID Direct Test (Localhost 8797)");
            }
            catch (Exception ex)
            {
                Log($"Failed to start UHID test: {ex.Message}");
                BtnStartUHID.IsEnabled = true;
                if (_uhidBackend != null)
                {
                    _uhidBackend.Dispose();
                    _uhidBackend = null;
                }
            }
        }

        private void BtnStopUHID_Click(object sender, RoutedEventArgs e)
        {
            if (_uhidBackend != null)
            {
                _uhidBackend.Dispose();
                _uhidBackend = null;
            }
            
            _inputCaptureService.SuppressLocalMouseEvents = false;
            _inputCaptureService.SuppressLocalKeyboardEvents = false;
            _inputCaptureService.StopCapture();
            _ownershipState = PointerOwnershipState.Local;
            
            TxtCaptureStatus.Text = "Idle";
            BtnStartUHID.IsEnabled = true;
            BtnStopUHID.IsEnabled = false;
            Log("Stopped UHID Direct Test");
        }

        private List<byte> _pressedHidKeys = new List<byte>();
        private byte _currentMouseMask = 0;

        private void RouteToUhidBackend(InputEvent e)
        {
            if (e is MouseInputEvent me)
            {
                if (me.Type == InputEventType.MouseMove)
                {
                    _uhidBackend.SendMouseMove((short)me.DeltaX, (short)me.DeltaY);
                }
                else if (me.Type == InputEventType.MouseWheel || me.Type == InputEventType.HorizontalWheel)
                {
                    sbyte wv = me.Type == InputEventType.MouseWheel ? (sbyte)Math.Clamp(me.WheelDelta / 120, -127, 127) : (sbyte)0;
                    sbyte wh = me.Type == InputEventType.HorizontalWheel ? (sbyte)Math.Clamp(me.WheelDelta / 120, -127, 127) : (sbyte)0;
                    _uhidBackend.SendMouseWheel(wv, wh);
                }
                else if (me.Type.ToString().Contains("Button"))
                {
                    if (me.Type == InputEventType.LeftButtonDown) _currentMouseMask |= 1;
                    if (me.Type == InputEventType.LeftButtonUp) _currentMouseMask &= 0xFE;
                    if (me.Type == InputEventType.RightButtonDown) _currentMouseMask |= 2;
                    if (me.Type == InputEventType.RightButtonUp) _currentMouseMask &= 0xFD;
                    if (me.Type == InputEventType.MiddleButtonDown) _currentMouseMask |= 4;
                    if (me.Type == InputEventType.MiddleButtonUp) _currentMouseMask &= 0xFB;
                    
                    _uhidBackend.SendMouseButton(_currentMouseMask);
                }
            }
            else if (e is KeyboardInputEvent ke)
            {
                byte modifiers = 0;
                if (ke.CtrlPressed) modifiers |= 0x01; // Left Ctrl
                if (ke.ShiftPressed) modifiers |= 0x02; // Left Shift
                if (ke.AltPressed) modifiers |= 0x04; // Left Alt
                if (ke.WinPressed) modifiers |= 0x08; // Left GUI

                byte hidCode = HidKeyMapper.GetHidUsageId(ke.VirtualKeyCode);

                if (ke.Type == InputEventType.KeyDown)
                {
                    if (hidCode > 0 && !_pressedHidKeys.Contains(hidCode))
                    {
                        _pressedHidKeys.Add(hidCode);
                    }
                }
                else if (ke.Type == InputEventType.KeyUp)
                {
                    if (hidCode > 0)
                    {
                        _pressedHidKeys.Remove(hidCode);
                    }
                }

                // Send up to 6 keys
                byte[] keys = new byte[6];
                for (int i = 0; i < Math.Min(6, _pressedHidKeys.Count); i++)
                {
                    keys[i] = _pressedHidKeys[i];
                }

                _uhidBackend.SendKeyboardReport(modifiers, keys);
            }
        }

        private void EdgeTransitionService_StateChanged(object sender, EdgeTransitionEventArgs e)
        {
            if (e.State == EdgeTransitionState.Idle)
            {
                if (_sessionManager?.State == InputSessionState.HandoffArmed)
                {
                    _sessionManager.SetState(InputSessionState.Idle);
                    _sessionManager.EnqueueHandoff("INPUT_HANDOFF_CANCEL", new { });
                }
            }
            else if (e.State == EdgeTransitionState.Armed)
            {
                if (_sessionManager?.State != InputSessionState.HandoffArmed && _sessionManager?.State != InputSessionState.Controlling)
                {
                    _currentSessionId++;
                    _sessionManager?.SetState(InputSessionState.HandoffArmed);
                    long candidateEnterTimestamp = (_edgeTransitionService as EdgeTransitionService)?.GetCandidateEnterTimestamp() ?? Stopwatch.GetTimestamp();
                    _sessionManager?.EnqueueHandoff("INPUT_HANDOFF_BEGIN", new HandoffBeginPayload
                    {
                        Edge = e.Edge.ToString(),
                        SessionId = _currentSessionId,
                        EntryNormalizedY = 0.5,
                        ClientTxTimestamp = candidateEnterTimestamp
                    });
                    
                    double latencyMs = (Stopwatch.GetTimestamp() - candidateEnterTimestamp) / (double)Stopwatch.Frequency * 1000.0;
                    Dispatcher.InvokeAsync(() =>
                    {
                        Log($"[LATENCY] Handoff Candidate->Armed latency: {latencyMs:F2}ms");
                    });
                    
                    StartRemoteControl();
                }
            }
            
            Dispatcher.InvokeAsync(() =>
            {
                switch (e.State)
                {
                    case EdgeTransitionState.Idle: TxtEdgeStatus.Text = "Waiting"; break;
                    case EdgeTransitionState.Candidate: TxtEdgeStatus.Text = "Edge detected"; break;
                    case EdgeTransitionState.Armed: TxtEdgeStatus.Text = "Ready to switch"; break;
                    case EdgeTransitionState.Disabled:
                    case EdgeTransitionState.Cancelled:
                        TxtEdgeStatus.Text = e.State.ToString();
                        break;
                }

                if (ChkShowInputDebug.IsChecked == true)
                {
                    _debugLogLines.Add($"Edge {e.State}: {e.Edge}");
                    TxtInputDebug.Text = string.Join(Environment.NewLine, _debugLogLines);
                    TxtInputDebug.ScrollToEnd();
                }
            });
        }

        private void StartRemoteControl()
        {
            if (_ownershipState == PointerOwnershipState.Remote) return;
            
            _ownershipState = PointerOwnershipState.Remote;
            _sessionManager?.SetState(InputSessionState.Controlling);
            
            if (_inputCaptureService is WindowsInputCaptureService wic)
            {
                wic.SuppressLocalMouseEvents = true;
                wic.SuppressLocalKeyboardEvents = true;
            }
            
            _rawMouseInputService?.StartCapture();
            
            NativeMethods.GetCursorPos(out var cursorPos);
            var rect = new NativeMethods.RECT { left = cursorPos.x, top = cursorPos.y, right = cursorPos.x + 2, bottom = cursorPos.y + 2 };
            NativeMethods.ClipCursor(ref rect);
            _isCursorClipped = true;
            
            // Focus the hidden IME sink to ensure WPF captures keyboard input for IME composition
            this.Activate();
            HiddenImeSink.Focus();

            Log("Started remote control.");
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