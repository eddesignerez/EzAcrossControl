using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using WindowsHost.Input;
using WindowsHost.Input.Edge;

namespace WindowsHost.Tests
{
    public class MockMonitorGeometry : MonitorGeometry
    {
        public List<MonitorInfo> MockMonitors { get; set; } = new List<MonitorInfo>();

        public override IEnumerable<MonitorInfo> GetAllMonitors()
        {
            return MockMonitors;
        }

        public override MonitorInfo GetMonitorFromPoint(int x, int y)
        {
            return MockMonitors.FirstOrDefault(m => 
                x >= m.Bounds.left && x < m.Bounds.right &&
                y >= m.Bounds.top && y < m.Bounds.bottom);
        }
    }

    public class MockInputCaptureService : IInputCaptureService
    {
        public event EventHandler<InputEvent> InputEventCaptured;
        public bool IsCapturing { get; set; }

        public void StartCapture() { }
        public void StopCapture() { }

        public void RaiseEvent(InputEvent e)
        {
            InputEventCaptured?.Invoke(this, e);
        }
    }

    public class EdgeTransitionTests
    {
        private MockInputCaptureService _mockInput;
        private MockMonitorGeometry _mockGeometry;
        private EdgeTransitionService _service;
        private List<EdgeTransitionState> _stateChanges;

        public EdgeTransitionTests()
        {
            _mockInput = new MockInputCaptureService();
            _mockGeometry = new MockMonitorGeometry();
            
            // Single monitor 1920x1080 at 0,0
            _mockGeometry.MockMonitors.Add(new MonitorInfo
            {
                Handle = (IntPtr)1,
                Bounds = new NativeMethods.RECT { left = 0, top = 0, right = 1920, bottom = 1080 }
            });

            _service = new EdgeTransitionService(_mockInput, _mockGeometry);
            _stateChanges = new List<EdgeTransitionState>();
            _service.StateChanged += (s, e) => _stateChanges.Add(e.State);
        }

        [Fact]
        public void RightEdge_TransitionsToCandidate_AndArmed()
        {
            _service.UpdateOptions(new EdgeTransitionOptions { ActiveEdge = ScreenEdge.Right, EdgeThresholdPixels = 4, EdgeActivationDelayMs = 50 });
            _service.Start();

            // Initial pos
            _mockInput.RaiseEvent(new MouseInputEvent(InputEventType.MouseMove, false, 1000, 500, 0));
            
            // Move to edge (threshold is 4, max X is 1919, so >= 1915 triggers)
            _mockInput.RaiseEvent(new MouseInputEvent(InputEventType.MouseMove, false, 1918, 500, 0));

            Assert.Contains(EdgeTransitionState.Candidate, _stateChanges);

            // Wait for delay
            System.Threading.Thread.Sleep(60);

            // Trigger another move to evaluate delay
            _mockInput.RaiseEvent(new MouseInputEvent(InputEventType.MouseMove, false, 1919, 500, 0));

            Assert.Contains(EdgeTransitionState.Armed, _stateChanges);
            Assert.Equal(EdgeTransitionState.Armed, _service.CurrentState);
        }

        [Fact]
        public void RightEdge_Cancels_WhenMovingAway()
        {
            _service.UpdateOptions(new EdgeTransitionOptions { ActiveEdge = ScreenEdge.Right, EdgeThresholdPixels = 4, EdgeActivationDelayMs = 50 });
            _service.Start();

            _mockInput.RaiseEvent(new MouseInputEvent(InputEventType.MouseMove, false, 1000, 500, 0));
            
            // Move to edge
            _mockInput.RaiseEvent(new MouseInputEvent(InputEventType.MouseMove, false, 1918, 500, 0));
            Assert.Contains(EdgeTransitionState.Candidate, _stateChanges);

            // Move away before delay
            _mockInput.RaiseEvent(new MouseInputEvent(InputEventType.MouseMove, false, 1900, 500, 0));
            
            Assert.Contains(EdgeTransitionState.Cancelled, _stateChanges);
            Assert.Equal(EdgeTransitionState.Idle, _service.CurrentState);
        }

        [Fact]
        public void InternalEdge_DoesNotTrigger()
        {
            // Add a second monitor to the right
            _mockGeometry.MockMonitors.Add(new MonitorInfo
            {
                Handle = (IntPtr)2,
                Bounds = new NativeMethods.RECT { left = 1920, top = 0, right = 3840, bottom = 1080 }
            });

            _service.UpdateOptions(new EdgeTransitionOptions { ActiveEdge = ScreenEdge.Right, EdgeThresholdPixels = 4, EdgeActivationDelayMs = 50 });
            _service.Start();

            _mockInput.RaiseEvent(new MouseInputEvent(InputEventType.MouseMove, false, 1000, 500, 0));
            
            // Move to the right edge of the first monitor (which is now internal)
            _mockInput.RaiseEvent(new MouseInputEvent(InputEventType.MouseMove, false, 1918, 500, 0));

            Assert.DoesNotContain(EdgeTransitionState.Candidate, _stateChanges);
        }
        
        [Fact]
        public void NegativeCoordinates_TriggerCorrectly()
        {
            _mockGeometry.MockMonitors.Clear();
            // Monitor on the left (negative X)
            _mockGeometry.MockMonitors.Add(new MonitorInfo
            {
                Handle = (IntPtr)1,
                Bounds = new NativeMethods.RECT { left = -1920, top = 0, right = 0, bottom = 1080 }
            });

            _service.UpdateOptions(new EdgeTransitionOptions { ActiveEdge = ScreenEdge.Left, EdgeThresholdPixels = 4, EdgeActivationDelayMs = 50 });
            _service.Start();

            _mockInput.RaiseEvent(new MouseInputEvent(InputEventType.MouseMove, false, -1000, 500, 0));
            
            // Move to left edge (<= -1916)
            _mockInput.RaiseEvent(new MouseInputEvent(InputEventType.MouseMove, false, -1918, 500, 0));

            Assert.Contains(EdgeTransitionState.Candidate, _stateChanges);
        }
    }
}
