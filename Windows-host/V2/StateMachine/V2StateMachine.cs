using System;

namespace WindowsHost.V2.StateMachine
{
    public enum SessionState
    {
        Disconnected,
        ReadyLocal,
        EdgeDetected,
        EnteringAndroid,
        RemoteAndroid,
        ReturningWindows
    }

    public class V2StateMachine
    {
        public SessionState CurrentState { get; private set; } = SessionState.Disconnected;

        public event EventHandler<SessionState>? StateChanged;

        public void TransitionTo(SessionState newState)
        {
            if (CurrentState != newState)
            {
                CurrentState = newState;
                StateChanged?.Invoke(this, CurrentState);
            }
        }
    }
}
