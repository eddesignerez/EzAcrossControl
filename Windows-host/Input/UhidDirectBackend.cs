using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace EZAcrossControl.Input
{
    public class UhidDirectBackend : IAndroidInputBackend, IDisposable
    {
        private TcpClient _client;
        private NetworkStream _stream;
        private BinaryWriter _writer;
        
        private string _ipAddress;
        private string _token;
        private int _port;

        private bool _isActive;
        private bool _isConnected;
        
        // Pacing Loop state
        private CancellationTokenSource _cts;
        private Task _workerTask;
        private const int PACING_MS = 8; // 125Hz

        private object _mouseLock = new object();
        private short _sumDx = 0;
        private short _sumDy = 0;
        private byte _currentButtons = 0;
        private sbyte _sumWheelV = 0;
        private sbyte _sumWheelH = 0;
        private bool _mouseDirty = false;

        private int _sequence = 0;

        // Metrics
        private long _lastTicks = 0;

        public bool IsActive => _isActive;
        public bool IsConnected => _isConnected;

        public UhidDirectBackend(string token, int port = Protocol.BinaryUhidProtocol.DEFAULT_PORT)
        {
            _token = token;
            _port = port;
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public async Task ConnectAsync(string ipAddress)
        {
            _ipAddress = ipAddress;
            _client = new TcpClient();
            _client.NoDelay = true; // Crucial for low latency input
            
            await _client.ConnectAsync(ipAddress, _port);
            _stream = _client.GetStream();
            _writer = new BinaryWriter(_stream);
            var reader = new BinaryReader(_stream);

            // Send HELLO
            Protocol.BinaryUhidProtocol.WriteHello(_writer, _token);
            _writer.Flush();

            // Read HELLO_ACK
            byte type = reader.ReadByte();
            if (type != Protocol.BinaryUhidProtocol.TYPE_HELLO_ACK)
                throw new Exception($"Expected HELLO_ACK, got {type}");
                
            byte status = reader.ReadByte();
            if (status != 0)
                throw new Exception($"Session rejected, status: {status}");

            _isConnected = true;

            // Start pacing loop
            _cts = new CancellationTokenSource();
            _workerTask = Task.Run(() => PacingLoopAsync(_cts.Token));
        }

        public void SetRemoteControl(bool isActive)
        {
            _isActive = isActive;
            if (!isActive)
            {
                lock (_mouseLock)
                {
                    _sumDx = 0;
                    _sumDy = 0;
                    _currentButtons = 0;
                    _sumWheelV = 0;
                    _sumWheelH = 0;
                    _mouseDirty = true;
                }
            }
        }

        public void SendMouseMove(short dx, short dy)
        {
            if (!_isActive || !_isConnected) return;
            lock (_mouseLock)
            {
                _sumDx += dx;
                _sumDy += dy;
                _mouseDirty = true;
            }
        }

        public void SendMouseButton(byte buttonMask)
        {
            if (!_isActive || !_isConnected) return;
            lock (_mouseLock)
            {
                // This replaces the button state entirely to match the physical state
                _currentButtons = buttonMask;
                _mouseDirty = true;
            }
        }

        public void SendMouseWheel(sbyte wheelVertical, sbyte wheelHorizontal)
        {
            if (!_isActive || !_isConnected) return;
            lock (_mouseLock)
            {
                // Keep summing wheel until sent
                _sumWheelV += wheelVertical;
                _sumWheelH += wheelHorizontal;
                _mouseDirty = true;
            }
        }

        public void SendKeyboardReport(byte modifiers, byte[] keys)
        {
            if (!_isActive || !_isConnected) return;
            // Send keyboard report immediately, bypassing the mouse coalescing queue
            // since keyboard events are discrete and timing sensitive for combination
            lock (_writer)
            {
                uint seq = unchecked((uint)Interlocked.Increment(ref _sequence));
                Protocol.BinaryUhidProtocol.WriteKeyboardReport(_writer, seq, modifiers, keys);
                _writer.Flush();
            }
        }

        public void SendTextInput(string text)
        {
            // Not supported in UHID mode. UHID mode relies on the Android IME and Keyboard HID.
        }

        private async Task PacingLoopAsync(CancellationToken token)
        {
            var sw = Stopwatch.StartNew();

            while (!token.IsCancellationRequested)
            {
                try
                {
                    bool shouldSend = false;
                    short dx = 0, dy = 0;
                    byte btns = 0;
                    sbyte wv = 0, wh = 0;

                    lock (_mouseLock)
                    {
                        if (_mouseDirty)
                        {
                            dx = _sumDx;
                            dy = _sumDy;
                            btns = _currentButtons;
                            wv = _sumWheelV;
                            wh = _sumWheelH;

                            _sumDx = 0;
                            _sumDy = 0;
                            _sumWheelV = 0;
                            _sumWheelH = 0;
                            _mouseDirty = false;
                            shouldSend = true;
                        }
                    }

                    if (shouldSend)
                    {
                        lock (_writer)
                        {
                            uint seq = unchecked((uint)Interlocked.Increment(ref _sequence));
                            Protocol.BinaryUhidProtocol.WriteMouseRel(_writer, seq, dx, dy, btns, wv, wh);
                            _writer.Flush();
                        }

                        // Jitter Metrics
                        long ticks = sw.ElapsedTicks;
                        if (_lastTicks > 0)
                        {
                            double ms = (ticks - _lastTicks) / (double)Stopwatch.Frequency * 1000.0;
                            // Logger.Log("UHID", $"Pacing interval: {ms:F2} ms"); // Uncomment to trace jitter
                        }
                        _lastTicks = ticks;
                    }

                    await Task.Delay(PACING_MS, token);
                }
                catch (TaskCanceledException) { break; }
                catch (Exception ex)
                {
                    Console.WriteLine("PacingLoopAsync error: " + ex);
                    break;
                }
            }
        }

        public async Task ShutdownAsync()
        {
            _isConnected = false;
            _isActive = false;

            if (_cts != null)
            {
                _cts.Cancel();
                try { await _workerTask; } catch { }
                _cts.Dispose();
            }

            if (_writer != null)
            {
                try
                {
                    lock (_writer)
                    {
                        Protocol.BinaryUhidProtocol.WriteControlRelease(_writer);
                        _writer.Flush();
                    }
                }
                catch { }
                _writer.Dispose();
            }
            _stream?.Dispose();
            _client?.Dispose();
        }

        public void Dispose()
        {
            ShutdownAsync().Wait();
        }
    }
}
