using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EZAcross.IpcProbe
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== EZ Across V2 IPC Probe ===");

            string repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
            string scrcpyDir = Path.Combine(repoRoot, "third_party", "scrcpy-ezacross", "bin");
            string scrcpyExe = Path.Combine(scrcpyDir, "scrcpy.exe");
            string scrcpyServer = Path.Combine(scrcpyDir, "scrcpy-server");

            Console.WriteLine($"[PROBE] Validating paths...");
            Console.WriteLine($"[PROBE] Exe: {scrcpyExe}");
            Console.WriteLine($"[PROBE] Server: {scrcpyServer}");

            if (!File.Exists(scrcpyExe))
            {
                Console.WriteLine("FAIL: SCRCPY_START (Executable not found)");
                return;
            }

            if (!File.Exists(scrcpyServer))
            {
                Console.WriteLine("FAIL: SCRCPY_START (Server not found)");
                return;
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            using var pipeServer = new NamedPipeServerStream(
                "EZAcrossScrcpy",
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Message,
                PipeOptions.Asynchronous);

            Console.WriteLine($"[PROBE] Created NamedPipeServerStream 'EZAcrossScrcpy'. Waiting for connection...");

            Task pipeConnectTask = pipeServer.WaitForConnectionAsync(cts.Token);

            string scrcpyArgs = "-s 4f9b94ad8955 --no-video --no-audio --mouse=uhid --keyboard=uhid";
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = scrcpyExe,
                Arguments = scrcpyArgs,
                WorkingDirectory = scrcpyDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            psi.EnvironmentVariables["SCRCPY_SERVER_PATH"] = scrcpyServer;

            Console.WriteLine($"[PROBE] Starting scrcpy...");
            Console.WriteLine($"[PROBE] Args: {scrcpyArgs}");

            using Process scrcpyProcess = new Process { StartInfo = psi };
            scrcpyProcess.Start();

            _ = Task.Run(async () =>
            {
                try
                {
                    while (!scrcpyProcess.StandardOutput.EndOfStream)
                    {
                        var line = await scrcpyProcess.StandardOutput.ReadLineAsync();
                        if (line != null) Console.WriteLine($"[STDOUT] {line}");
                    }
                }
                catch { }
            });

            _ = Task.Run(async () =>
            {
                try
                {
                    while (!scrcpyProcess.StandardError.EndOfStream)
                    {
                        var line = await scrcpyProcess.StandardError.ReadLineAsync();
                        if (line != null) Console.WriteLine($"[STDERR] {line}");
                    }
                }
                catch { }
            });

            try
            {
                Console.WriteLine("[PROBE] Waiting for IPC connection (5s timeout)...");
                var connectTimeoutTask = Task.Delay(TimeSpan.FromSeconds(5), cts.Token);
                var completedTask = await Task.WhenAny(pipeConnectTask, connectTimeoutTask);

                if (completedTask == connectTimeoutTask && !pipeConnectTask.IsCompleted)
                {
                    Console.WriteLine("FAIL: PIPE_CONNECT (Timeout)");
                    scrcpyProcess.Kill();
                    return;
                }

                await pipeConnectTask; // Ensure exceptions are thrown if any
                Console.WriteLine("[PROBE] PIPE CONNECTED");
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("FAIL: PIPE_CONNECT (Cancelled)");
                scrcpyProcess.Kill();
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAIL: PIPE_CONNECT ({ex.Message})");
                scrcpyProcess.Kill();
                return;
            }

            try
            {
                Console.WriteLine("[PROBE] Waiting for READY (10s timeout)...");
                using var reader = new StreamReader(pipeServer, Encoding.UTF8);

                var readTask = reader.ReadLineAsync();
                var readTimeoutTask = Task.Delay(TimeSpan.FromSeconds(10), cts.Token);
                var completedRead = await Task.WhenAny(readTask, readTimeoutTask);

                if (completedRead == readTimeoutTask)
                {
                    Console.WriteLine("FAIL: READY_READ (Timeout)");
                    scrcpyProcess.Kill();
                    return;
                }

                string message = await readTask;
                if (message == null)
                {
                    Console.WriteLine("FAIL: READY_READ (Null received)");
                    scrcpyProcess.Kill();
                    return;
                }

                Console.WriteLine($"[PROBE] RX: {message}");

                if (message.Trim() == "READY")
                {
                    Console.WriteLine("PASS");
                }
                else
                {
                    Console.WriteLine($"FAIL: READY_READ (Unexpected message: '{message}')");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAIL: READY_READ ({ex.Message})");
            }
            finally
            {
                scrcpyProcess.Kill();
            }
        }
    }
}
