using System;
using WindowsHost.Input;
using EZAcrossControl.Input;

namespace WindowsHost.V2.Edge
{
    public interface IEdgeTransitionService
    {
        event EventHandler<EdgeTransitionEventArgs> StateChanged;
        
        EdgeState CurrentState { get; }
        EdgeOptions Options { get; }

        void Start();
        void Stop();
        void UpdateOptions(EdgeOptions options);
    }
}
