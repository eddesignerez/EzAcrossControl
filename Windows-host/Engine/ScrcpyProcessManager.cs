using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WindowsHost.Input;

namespace WindowsHost.Engine
{
    public class ScrcpyProcessManager : IScrcpyControlEngine
    {
        private Process? _scrcpyProcess;
        private IntPtr _inputWindowHandle;
        private ScrcpyIpcClient? _ipcClient;
        private SemaphoreSlim _commandLock = new SemaphoreSlim(1, 1);
        private CancellationTokenSource? _lifecycleCts;

        private TaskCompletionSource<bool>? _readyTcs;
        private TaskCompletionSource<bool>? _capturedTcs;
        private TaskCompletionSource<bool>? _releasedTcs;

        public event EventHandler<ScrcpyEngineState>? StateChanged;
        public event EventHandler<string>? Error;

        private ScrcpyEngineState _state = ScrcpyEngineState.Offline;
        public ScrcpyEngineState State
        {
            get => _state;
            private set
            {
                if (_state != value)
                {
                    _state = value;
                    StateChanged?.Invoke(this, _state);
                }
            }
        }

        public ControlOwner ControlOwner { get; private set; } = ControlOwner.Windows;

        public bool IsRunning => State == ScrcpyEngineState.Ready || State == ScrcpyEngineState.Captured;
        public bool IsCaptured => State == ScrcpyEngineState.Captured;
        public ConnectionMode? ActiveTransport { get; private set; }
        public string? ActiveSerial { get; private set; }
        private const string InputWindowTitle = "EZ Across native input";

        private IntPtr ResolveInputWindow()
        {
            if (_scrcpyProcess == null || _scrcpyProcess.HasExited) return IntPtr.Zero;
            // MainWindowHandle omits utility/hidden windows. Match our actual SDL
            // input window by title and PID, excluding SDL's helper and IME windows.
            if (!NativeMethods.IsWindow(_inputWindowHandle))
                _inputWindowHandle = NativeMethods.FindProcessWindow(_scrcpyProcess.Id, InputWindowTitle);
            return _inputWindowHandle;
        }

        public bool ActivateInputWindow()
        {
            if (_scrcpyProcess == null) return false;

            return NativeMethods.ActivateWindow(ResolveInputWindow());
        }

        public bool ParkInputWindowAt(int x, int y)
        {
            IntPtr window = ResolveInputWindow();
            return window != IntPtr.Zero && NativeMethods.SetWindowPos(
                window, IntPtr.Zero, x, y, 0, 0,
                NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
        }

        public void HideInputWindow()
        {
            if (_scrcpyProcess == null) return;
            ResolveInputWindow();
            if (_inputWindowHandle != IntPtr.Zero)
                NativeMethods.ShowWindow(_inputWindowHandle, 0); // SW_HIDE while Windows owns input
        }

        public async Task StartAsync(ConnectionMode mode, AndroidDevice? device, CancellationToken cancellationToken = default)
        {
            await _commandLock.WaitAsync(cancellationToken);
            try
            {
                if (State == ScrcpyEngineState.Starting || State == ScrcpyEngineState.Ready || State == ScrcpyEngineState.Captured) return;

                State = ScrcpyEngineState.Starting;
                _inputWindowHandle = IntPtr.Zero;
                Debug.WriteLine("[PM] State=Starting");

                _lifecycleCts?.Cancel();
                _lifecycleCts?.Dispose();
                _lifecycleCts = new CancellationTokenSource();

                var locator = ScrcpyInstallationLocator.Locate();
                string? scrcpyPath = locator.ExecutablePath;
                string? serverPath = locator.ServerPath;

                bool executableExists = scrcpyPath != null && File.Exists(scrcpyPath);
                bool serverExists = serverPath != null && File.Exists(serverPath);

                if (!executableExists || !serverExists)
                {
                    FireError($"Scrcpy executable or server not found.");
                    return;
                }

                _ipcClient?.Dispose();
                _ipcClient = new ScrcpyIpcClient();
                _ipcClient.MessageReceived += OnIpcMessageReceived;
                _ipcClient.Disconnected += OnIpcDisconnected;

                _ipcClient.CreateServer();
                Task connectTask = _ipcClient.WaitForConnectionAsync(_lifecycleCts.Token);

                string? serial = device?.GetSerial(mode);
                if (string.IsNullOrWhiteSpace(serial))
                {
                    FireError(mode == ConnectionMode.Network ? "Wi-Fi ADB unavailable. Enable wireless debugging and connect the paired device." : "Requested ADB transport unavailable.");
                    return;
                }
                ActiveTransport = serial == device!.UsbSerial ? ConnectionMode.Usb : ConnectionMode.Network;
                ActiveSerial = serial;

                string args = "--no-video --no-audio --mouse=uhid --keyboard=uhid --window-borderless --window-width=1 --window-height=1";

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = scrcpyPath,
                    WorkingDirectory = Path.GetDirectoryName(scrcpyPath),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                foreach (var argument in args.Split(' ')) psi.ArgumentList.Add(argument);
                psi.ArgumentList.Add("--window-title");
                psi.ArgumentList.Add(InputWindowTitle);
                psi.ArgumentList.Add("--serial");
                psi.ArgumentList.Add(serial);

                psi.EnvironmentVariables["SCRCPY_SERVER_PATH"] = serverPath;
                psi.EnvironmentVariables["ADB"] = AndroidDeviceManager.AdbPath;

                Debug.WriteLine("[SCRCPY] Process.Start");
                _scrcpyProcess = new Process { StartInfo = psi };
                _scrcpyProcess.Exited += (s, e) => _ = StopInternalAsync(s);
                _scrcpyProcess.EnableRaisingEvents = true;

                try
                {
                    _scrcpyProcess.Start();
                }
                catch (Exception ex)
                {
                    FireError($"Failed to start scrcpy process: {ex.Message}");
                    return;
                }
                Debug.WriteLine($"[SCRCPY] PID={_scrcpyProcess.Id}");

                _ = ReadStreamAsync(_scrcpyProcess.StandardOutput, "STDOUT");
                _ = ReadStreamAsync(_scrcpyProcess.StandardError, "STDERR");

                try
                {
                    await connectTask.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    FireError("IPC_CONNECT_TIMEOUT");
                    return;
                }

                _readyTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _ipcClient.StartReceiveLoop(_lifecycleCts.Token);
                try
                {
                    await _readyTcs.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    FireError("IPC_READY_TIMEOUT");
                    return;
                }

                Debug.WriteLine("[PM] State=Ready");
                State = ScrcpyEngineState.Ready;
                ControlOwner = ControlOwner.Windows;
            }
            catch (OperationCanceledException)
            {
                FireError("START_CANCELLED");
            }
            catch (Exception ex)
            {
                FireError($"Start error: {ex.Message}");
            }
            finally
            {
                _commandLock.Release();
            }
        }

        private async Task ReadStreamAsync(StreamReader reader, string prefix)
        {
            try
            {
                while (true)
                {
                    string? line = await reader.ReadLineAsync();
                    if (line == null) break;

                    Debug.WriteLine($"[SCRCPY {prefix}] {line}");
                }
            }
            catch { }
        }

        private void OnIpcMessageReceived(object? sender, string message)
        {
            switch (message.Trim())
            {
                case "READY":
                    _readyTcs?.TrySetResult(true);
                    break;
                case "CAPTURED":
                    _capturedTcs?.TrySetResult(true);
                    break;
                case "RELEASED":
                    _releasedTcs?.TrySetResult(true);
                    break;
                default:
                    if (message.StartsWith("ERROR"))
                    {
                        Debug.WriteLine($"[IPC] ERROR received: {message}");
                    }
                    break;
            }
        }

        private void OnIpcDisconnected(object? sender, EventArgs e)
        {
            Debug.WriteLine("[IPC] Disconnected event");
            _ = StopInternalAsync(sender);
        }

        public async Task CaptureAsync()
        {
            await _commandLock.WaitAsync();
            try
            {
                if (State != ScrcpyEngineState.Ready || _ipcClient == null) return;

                _capturedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                await _ipcClient.SendMessageAsync("CAPTURE_ON");

                try
                {
                    await _capturedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch (TimeoutException)
                {
                    Debug.WriteLine("[IPC] Capture timeout");
                    return; // Don't crash, just ignore or fire error
                }

                Debug.WriteLine("[PM] State=Captured");
                ControlOwner = ControlOwner.Android;
                State = ScrcpyEngineState.Captured;
            }
            finally
            {
                _commandLock.Release();
            }
        }

        public async Task ReleaseAsync()
        {
            await _commandLock.WaitAsync();
            try
            {
                if (State != ScrcpyEngineState.Captured || _ipcClient == null) return;

                _releasedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                await _ipcClient.SendMessageAsync("CAPTURE_OFF");

                try
                {
                    await _releasedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch (TimeoutException)
                {
                    Debug.WriteLine("[IPC] Release timeout");
                }

                Debug.WriteLine("[PM] State=Ready");
                ControlOwner = ControlOwner.Windows;
                State = ScrcpyEngineState.Ready;
            }
            finally
            {
                _commandLock.Release();
            }
        }

        public async Task StopAsync()
        {
            await _commandLock.WaitAsync();
            try
            {
                await StopCoreAsync();
            }
            finally
            {
                _commandLock.Release();
            }
        }

        private async Task StopInternalAsync(object? source = null)
        {
            await _commandLock.WaitAsync();
            try
            {
                // Shutdown callbacks from a replaced USB/Wi-Fi session must
                // never stop the newly started engine.
                if (source is Process process && !ReferenceEquals(process, _scrcpyProcess)) return;
                if (source is ScrcpyIpcClient ipc && !ReferenceEquals(ipc, _ipcClient)) return;
                await StopCoreAsync();
            }
            finally
            {
                _commandLock.Release();
            }
        }

        private async Task StopCoreAsync()
        {
            if (State == ScrcpyEngineState.Offline || State == ScrcpyEngineState.Error) return;

            if (State == ScrcpyEngineState.Captured && _ipcClient != null)
            {
                _releasedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                await _ipcClient.SendMessageAsync("CAPTURE_OFF");
                try
                {
                    await _releasedTcs.Task.WaitAsync(TimeSpan.FromSeconds(1));
                }
                catch { }
            }

            _lifecycleCts?.Cancel();

            if (_ipcClient != null)
            {
                await _ipcClient.SendMessageAsync("SHUTDOWN");
                await Task.Delay(200); // Give it a moment to send
                _ipcClient.Dispose();
                _ipcClient = null;
            }

            if (_scrcpyProcess != null && !_scrcpyProcess.HasExited)
            {
                try
                {
                    _scrcpyProcess.Kill();
                }
                catch { }
                _scrcpyProcess.Dispose();
                _scrcpyProcess = null;
            }

            ControlOwner = ControlOwner.Windows;
            ActiveTransport = null;
            ActiveSerial = null;
            State = ScrcpyEngineState.Offline;
        }

        private void FireError(string msg)
        {
            Debug.WriteLine($"[ENGINE] ERROR: {msg}");
            
            _lifecycleCts?.Cancel();

            // Clean up synchronously as much as possible since we might be inside a lock
            _ipcClient?.Dispose();
            _ipcClient = null;

            if (_scrcpyProcess != null && !_scrcpyProcess.HasExited)
            {
                try { _scrcpyProcess.Kill(); } catch { }
                _scrcpyProcess.Dispose();
                _scrcpyProcess = null;
            }

            ControlOwner = ControlOwner.Windows;
            ActiveTransport = null;
            ActiveSerial = null;
            State = ScrcpyEngineState.Error;
            Error?.Invoke(this, msg);
        }

        public void Dispose()
        {
            _ = StopInternalAsync();
        }
    }
}
