using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WindowsHost.Engine;

namespace EZAcross.EngineProbe
{
    class Program
    {
        static async Task Main(string[] args)
        {
            System.Diagnostics.Trace.Listeners.Add(new System.Diagnostics.ConsoleTraceListener());
            Console.WriteLine("=== EZ Across V2 PRODUCTION ENGINE PROBE ===");

            var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EZ Across Control", "Logs");
            Directory.CreateDirectory(logPath);
            string logFile = Path.Combine(logPath, "v2-engine.log");

            void Log(string message)
            {
                var line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
                Console.WriteLine(line);
                File.AppendAllText(logFile, line + Environment.NewLine);
            }

            Log("[ENGINE PROBE] Initializing real production classes...");

            var mode = Array.IndexOf(args, "--network") >= 0 ? ConnectionMode.Network : ConnectionMode.Usb;
            var devices = await new AndroidDeviceManager().GetDevicesAsync();
            var device = devices.Find(d => !string.IsNullOrEmpty(d.GetSerial(mode)));
            if (device == null)
            {
                Log($"ENGINE PROBE FAIL: no {mode} ADB device connected.");
                Environment.ExitCode = 1;
                return;
            }
            Log($"[ENGINE PROBE] Transport={mode}");

            // StopAsync below owns the complete shutdown. Do not use a `using`
            // declaration here: Dispose previously started a second asynchronous
            // shutdown after Main had already completed.
            var engine = new ScrcpyProcessManager();
            bool testFocus = Array.IndexOf(args, "--focus") >= 0;
            var previousWindow = testFocus ? WindowsHost.Input.NativeMethods.GetForegroundWindow() : IntPtr.Zero;
            string engineInstanceId = Guid.NewGuid().ToString().Substring(0, 8);
            Log($"[ENGINE PROBE] Engine instance ID={engineInstanceId}");

            engine.StateChanged += (s, state) => Log($"[ENGINE PROBE] StateChanged -> {state}");
            engine.Error += (s, msg) => Log($"[ENGINE PROBE] Error event fired: {msg}");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            try
            {
                Log($"[ENGINE PROBE] Executing StartAsync...");
                await engine.StartAsync(mode, device, cts.Token);
                Log($"[ENGINE PROBE] StartAsync returned. Current State={engine.State}");

                if (engine.State != ScrcpyEngineState.Ready)
                {
                    Log("ENGINE PROBE FAIL");
                    Log("Stage=StartAsync");
                    Log($"State={engine.State}");
                    return;
                }

                Log($"[ENGINE PROBE] Executing CaptureAsync...");
                if (testFocus)
                {
                    engine.HideInputWindow();
                    if (!engine.ActivateInputWindow())
                        throw new InvalidOperationException("Native input window activation failed after hiding.");
                    Log("[ENGINE PROBE] Hidden native input window activation PASS");
                }
                await engine.CaptureAsync();

                if (engine.State != ScrcpyEngineState.Captured)
                {
                    Log("ENGINE PROBE FAIL");
                    Log("Stage=CaptureAsync");
                    Log($"State={engine.State}");
                    return;
                }

                // Briefly hold capture to prove it works
                await Task.Delay(Array.IndexOf(args, "--long") >= 0 ? 20000 : 2000);

                Log($"[ENGINE PROBE] Executing ReleaseAsync...");
                await engine.ReleaseAsync();

                if (engine.State != ScrcpyEngineState.Ready)
                {
                    Log("ENGINE PROBE FAIL");
                    Log("Stage=ReleaseAsync");
                    Log($"State={engine.State}");
                    return;
                }

                Log("ENGINE PROBE PASS");
            }
            catch (Exception ex)
            {
                Log("ENGINE PROBE FAIL");
                Log("Stage=Execution");
                Log($"State={engine.State}");
                Log($"Exception={ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                Log($"[ENGINE PROBE] Shutting down engine...");
                await engine.StopAsync();
                if (previousWindow != IntPtr.Zero) WindowsHost.Input.NativeMethods.ActivateWindow(previousWindow);
            }
        }
    }
}
