namespace WindowsHost.Input.Edge
{
    public class EdgeTransitionOptions
    {
        public ScreenEdge ActiveEdge { get; set; } = ScreenEdge.Right;
        public int EdgeThresholdPixels { get; set; } = 6;
        public int EdgeActivationDelayMs { get; set; } = 35;
        public int EdgePushThreshold { get; set; } = 8;
    }
}
