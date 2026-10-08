using System;

namespace WindowsHost.V2.Edge
{
    public class EdgeTransitionEventArgs : EventArgs
    {
        public EdgeState State { get; }
        public ScreenEdge Edge { get; }

        public EdgeTransitionEventArgs(EdgeState state, ScreenEdge edge)
        {
            State = state;
            Edge = edge;
        }
    }
}
