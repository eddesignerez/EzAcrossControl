using System;

namespace WindowsHost.Input.Edge
{
    public interface IEdgeTransitionService
    {
        event EventHandler<EdgeTransitionEventArgs> StateChanged;
        
        EdgeTransitionState CurrentState { get; }
        EdgeTransitionOptions Options { get; }

        void Start();
        void Stop();
        void UpdateOptions(EdgeTransitionOptions options);
    }
}
