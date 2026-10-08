using System;
using System.Threading;
using System.Threading.Tasks;

namespace WindowsHost.Engine
{
    public enum ScrcpyEngineState
    {
        Offline,
        Starting,
        Ready,
        Captured,
        Error
    }

    public enum ControlOwner
    {
        Windows,
        Android
    }

    public interface IScrcpyControlEngine : IDisposable
    {
        event EventHandler<ScrcpyEngineState> StateChanged;
        event EventHandler<string> Error;

        ScrcpyEngineState State { get; }
        ControlOwner ControlOwner { get; }

        bool IsRunning { get; }
        bool IsCaptured { get; }

        Task StartAsync(ConnectionMode mode, AndroidDevice device, CancellationToken cancellationToken = default);
        Task StopAsync();
        
        Task CaptureAsync();
        Task ReleaseAsync();
    }
}
