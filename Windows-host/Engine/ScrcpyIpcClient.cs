using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WindowsHost.Engine
{
    public class ScrcpyIpcClient : IDisposable
    {
        public const string PipeName = "EZAcrossScrcpy";
        private NamedPipeServerStream? _pipeServer;
        private StreamReader? _reader;
        private StreamWriter? _writer;
        private CancellationTokenSource? _disposeCts;

        public event EventHandler<string>? MessageReceived;
        public event EventHandler? Disconnected;

        public void CreateServer()
        {
            _disposeCts = new CancellationTokenSource();
            _pipeServer = new NamedPipeServerStream(
                PipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Message,
                PipeOptions.Asynchronous);

            System.Diagnostics.Debug.WriteLine("[IPC] Server created");
        }

        public async Task WaitForConnectionAsync(CancellationToken lifecycleToken)
        {
            try
            {
                if (_pipeServer == null) throw new InvalidOperationException("Server not created");

                System.Diagnostics.Debug.WriteLine("[IPC] WaitForConnectionAsync armed");

                await _pipeServer.WaitForConnectionAsync(lifecycleToken).ConfigureAwait(false);

                System.Diagnostics.Debug.WriteLine("[IPC] Connected");

                var utf8NoBom = new UTF8Encoding(false);
                _reader = new StreamReader(_pipeServer, utf8NoBom);
                _writer = new StreamWriter(_pipeServer, utf8NoBom) { AutoFlush = true };

                System.Diagnostics.Debug.WriteLine("[IPC] Stream writer created");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IPC] Exception in WaitForConnectionAsync: {ex.GetType().Name} - {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }

        public void StartReceiveLoop(CancellationToken lifecycleToken)
        {
            _ = Task.Run(() => ReceiveLoop(lifecycleToken));
        }

        private async Task ReceiveLoop(CancellationToken lifecycleToken)
        {
            System.Diagnostics.Debug.WriteLine("[IPC] Receive loop started");
            try
            {
                while (!lifecycleToken.IsCancellationRequested && _disposeCts != null && !_disposeCts.Token.IsCancellationRequested && _pipeServer != null && _pipeServer.IsConnected)
                {
                    string? message = await _reader!.ReadLineAsync();
                    if (message == null)
                    {
                        System.Diagnostics.Debug.WriteLine("[IPC] RX null (stream ended)");
                        break;
                    }

                    System.Diagnostics.Debug.WriteLine($"[IPC] RX={message}");
                    MessageReceived?.Invoke(this, message);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IPC] Receive error: {ex.Message}");
            }
            finally
            {
                Disconnected?.Invoke(this, EventArgs.Empty);
            }
        }

        public async Task SendMessageAsync(string message)
        {
            if (_pipeServer != null && _pipeServer.IsConnected && _writer != null)
            {
                System.Diagnostics.Debug.WriteLine($"[IPC] TX={message.Trim()}");
                await _writer.WriteLineAsync(message);
            }
        }

        public void Dispose()
        {
            _disposeCts?.Cancel();
            _writer?.Dispose();
            _reader?.Dispose();
            _pipeServer?.Dispose();
            _disposeCts?.Dispose();
        }
    }
}
