using System;

namespace WindowsHost.Input.Edge
{
    public class EdgeTransitionEventArgs : EventArgs
    {
        public EdgeTransitionState State { get; }
        public ScreenEdge Edge { get; }

        public EdgeTransitionEventArgs(EdgeTransitionState state, ScreenEdge edge)
        {
            State = state;
            Edge = edge;
        }
    }
}
