using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using WindowsHost.Input;
using EZAcrossControl.Input;
namespace WindowsHost.Protocol
{
    public class InputSessionManager : IDisposable, IAndroidInputBackend
    {
        private InputSessionState _state = InputSessionState.Disconnected;
        public InputSessionState State => _state;

        private long _sequence = 0;
        private ConcurrentQueue<InputEvent> _queue = new ConcurrentQueue<InputEvent>();
        
        private CancellationTokenSource _cts;
        private Task _workerTask;
        private Action<string, bool> _sendMessageAction;

        private InputEvent? _lastMouseMove = null;
        private const int MAX_QUEUE_SIZE = 500;
        private const int THROTTLE_MS = 8; // ~120 Hz
        
        private long _txMouseEvents = 0;
        private DateTime _lastTxLogTime = DateTime.UtcNow;

        public bool EnableInputStreamingTest { get; set; } = false;

        public InputSessionManager(Action<string, bool> sendMessageAction)
        {
            _sendMessageAction = sendMessageAction;
            _cts = new CancellationTokenSource();
            _workerTask = Task.Run(() => WorkerLoopAsync(_cts.Token));
        }

        public void SetState(InputSessionState newState)
        {
            _state = newState;
            if (newState == InputSessionState.Disconnected)
            {
                _sequence = 0;
                while (_queue.TryDequeue(out _)) { }
                _lastMouseMove = null;
            }
        }

        public Task InitializeAsync() => Task.CompletedTask;
        
        public Task ConnectAsync(string ipAddress) => Task.CompletedTask; // Already handled by WebSocket connection in MainWindow

        public void SetRemoteControl(bool isActive)
        {
            SetState(isActive ? InputSessionState.Controlling : InputSessionState.Connected);
        }

        public void SendMouseMove(short dx, short dy)
        {
            // Legacy backend expects absolute coordinates, but translates Delta internally
            // This is just a stub wrapper if we need it
        }

        public void SendMouseButton(byte buttonMask) { }

        public void SendMouseWheel(sbyte wheelVertical, sbyte wheelHorizontal) { }

        public void SendKeyboardReport(byte modifiers, byte[] keys) { }

        public void SendTextInput(string text)
        {
            SendEnvelope("INPUT_TEXT_COMMIT", new TextCommitPayload { Text = text });
        }

        public Task ShutdownAsync()
        {
            SetState(InputSessionState.Disconnected);
            return Task.CompletedTask;
        }

        public void EnqueueInput(InputEvent e)
        {
            if (_state == InputSessionState.Disconnected) return;
            if (_state != InputSessionState.Controlling && !EnableInputStreamingTest) return;

            if (e is MouseInputEvent me && me.Type == InputEventType.MouseMove)
            {
                if (_lastMouseMove != null)
                {
                    var existing = (MouseInputEvent)_lastMouseMove;
                    _lastMouseMove = new MouseInputEvent(
                        InputEventType.MouseMove,
                        me.IsInjected,
                        me.X, me.Y,
                        existing.DeltaX + me.DeltaX,
                        existing.DeltaY + me.DeltaY,
                        existing.Button,
                        existing.WheelDelta
                    );
                }
                else
                {
                    _lastMouseMove = me;
                }
                return;
            }

            if (e is KeyboardInputEvent ke)
            {
                ProcessKeyboardEvent(ke);
                return;
            }

            if (_queue.Count < MAX_QUEUE_SIZE)
            {
                _queue.Enqueue(e);
            }
        }

        private void ProcessKeyboardEvent(KeyboardInputEvent ke)
        {
            if (!string.IsNullOrEmpty(ke.TextContent) && ke.Type == InputEventType.KeyDown)
            {
                SendEnvelope("INPUT_TEXT_COMMIT", new TextCommitPayload
                {
                    Text = ke.TextContent
                });
            }
            else
            {
                string msgType = ke.Type == InputEventType.KeyDown ? "INPUT_KEY_DOWN" : "INPUT_KEY_UP";
                SendEnvelope(msgType, new KeyPayload
                {
                    VirtualKeyCode = ke.VirtualKeyCode,
                    ScanCode = ke.ScanCode,
                    IsExtended = ke.IsExtended,
                    IsInjected = ke.IsInjected,
                    Modifiers = new ModifiersPayload
                    {
                        Ctrl = ke.CtrlPressed,
                        Shift = ke.ShiftPressed,
                        Alt = ke.AltPressed,
                        Win = ke.WinPressed
                    }
                });
            }
        }

        public void EnqueueHandoff(string messageType, object payload)
        {
            SendEnvelope(messageType, payload, true);
        }

        private async Task WorkerLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    bool processedAny = false;

                    // Process the coalesced mouse move
                    if (_lastMouseMove != null)
                    {
                        var m = (MouseInputEvent)_lastMouseMove;
                        _lastMouseMove = null;
                        double nx = (m.X - System.Windows.SystemParameters.VirtualScreenLeft) / System.Windows.SystemParameters.VirtualScreenWidth;
                        double ny = (m.Y - System.Windows.SystemParameters.VirtualScreenTop) / System.Windows.SystemParameters.VirtualScreenHeight;

                        SendEnvelope("INPUT_MOUSE_MOVE", new MouseMovePayload
                        {
                            X = m.X,
                            Y = m.Y,
                            DeltaX = m.DeltaX,
                            DeltaY = m.DeltaY,
                            NormalizedX = nx,
                            NormalizedY = ny,
                            IsInjected = m.IsInjected
                        });
                        processedAny = true;
                    }

                    // Process queue
                    while (_queue.TryDequeue(out var ev))
                    {
                        if (ev is MouseInputEvent me)
                        {
                            if (me.Type == InputEventType.LeftButtonDown || me.Type == InputEventType.LeftButtonUp ||
                                me.Type == InputEventType.RightButtonDown || me.Type == InputEventType.RightButtonUp ||
                                me.Type == InputEventType.MiddleButtonDown || me.Type == InputEventType.MiddleButtonUp)
                            {
                                string btn = me.Type.ToString().Replace("ButtonDown", "").Replace("ButtonUp", "");
                                string act = me.Type.ToString().EndsWith("Down") ? "Down" : "Up";

                                SendEnvelope("INPUT_MOUSE_BUTTON", new MouseButtonPayload
                                {
                                    Button = btn,
                                    Action = act,
                                    X = me.X,
                                    Y = me.Y,
                                    IsInjected = me.IsInjected
                                });
                            }
                            else if (me.Type == InputEventType.MouseWheel || me.Type == InputEventType.HorizontalWheel)
                            {
                                SendEnvelope("INPUT_MOUSE_WHEEL", new MouseWheelPayload
                                {
                                    Axis = me.Type == InputEventType.MouseWheel ? "Vertical" : "Horizontal",
                                    Delta = me.WheelDelta,
                                    X = me.X,
                                    Y = me.Y
                                });
                            }
                        }
                        processedAny = true;
                    }

                    if (processedAny)
                    {
                        var now = DateTime.UtcNow;
                        if ((now - _lastTxLogTime).TotalSeconds >= 1.0)
                        {
                            long count = Interlocked.Exchange(ref _txMouseEvents, 0);
                            Logger.Log("METRICS", $"txMouseEventsPerSecond: {count}");
                            _lastTxLogTime = now;
                        }
                    }

                    await Task.Delay(THROTTLE_MS, token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex, "WorkerLoopAsync");
                    await Task.Delay(100, token); // Backoff
                }
            }
        }

        public void SendEnvelope(string type, object payload, bool isPriority = false)
        {
            try
            {
                var seq = Interlocked.Increment(ref _sequence);
                var env = new MessageEnvelope
                {
                    Type = type,
                    ProtocolVersion = 1,
                    Sequence = seq,
                    Payload = payload
                };
                var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                string json = JsonSerializer.Serialize(env, options);
                _sendMessageAction?.Invoke(json, isPriority);
                
                if (type == "INPUT_MOUSE_MOVE")
                {
                    Interlocked.Increment(ref _txMouseEvents);
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "SendEnvelope");
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            try { _workerTask.Wait(500); } catch { }
            _cts.Dispose();
        }
    }
}
