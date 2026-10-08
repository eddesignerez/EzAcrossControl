using System;
using WindowsHost.Engine;

namespace WindowsHost.V2.Edge
{
    public interface IEdgeHandoffService
    {
        void Start();
        void Stop();
        void AttachEngine(IScrcpyControlEngine engine);
    }
}
