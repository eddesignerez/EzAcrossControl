using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using WindowsHost.Engine;
using WindowsHost.Input;
using WindowsHost.V2.Edge;

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
                y >= m.Bounds.top && y < m.Bounds.bottom)!;
        }
    }

    public class MockInputCaptureService : IInputCaptureService
    {
        public event EventHandler<InputEvent>? InputEventCaptured;
        public bool IsCapturing { get; set; }
        public bool SuppressLocalMouseEvents { get; set; }
        public bool SuppressLocalKeyboardEvents { get; set; }

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
        private ConcurrentQueue<EdgeState> _stateChanges;

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
            _stateChanges = new ConcurrentQueue<EdgeState>();
            _service.StateChanged += (s, e) => _stateChanges.Enqueue(e.State);
        }

        [Fact]
        public void RightEdge_TransitionsToCandidate_AndArmed()
        {
            _service.UpdateOptions(new EdgeOptions { ActiveEdge = ScreenEdge.Right, EdgeThresholdPixels = 4, EdgeActivationDelayMs = 50 });
            _service.Start();

            // Initial pos
            _mockInput.RaiseEvent(new MouseInputEvent(InputEventType.MouseMove, false, 1000, 500, 0));
            
            // Move to edge (threshold is 4, max X is 1919, so >= 1915 triggers)
            _mockInput.RaiseEvent(new MouseInputEvent(InputEventType.MouseMove, false, 1918, 500, 0));

            Assert.Contains(EdgeState.Candidate, _stateChanges);

            // Wait for delay
            System.Threading.Thread.Sleep(60);

            // Trigger another move to evaluate delay
            _mockInput.RaiseEvent(new MouseInputEvent(InputEventType.MouseMove, false, 1919, 500, 0));

            Assert.Contains(EdgeState.Armed, _stateChanges);
            Assert.Equal(EdgeState.Armed, _service.CurrentState);
        }

        [Fact]
        public void RightEdge_Cancels_WhenMovingAway()
        {
            _service.UpdateOptions(new EdgeOptions { ActiveEdge = ScreenEdge.Right, EdgeThresholdPixels = 4, EdgeActivationDelayMs = 50 });
            _service.Start();

            _mockInput.RaiseEvent(new MouseInputEvent(InputEventType.MouseMove, false, 1000, 500, 0));
            
            // Move to edge
            _mockInput.RaiseEvent(new MouseInputEvent(InputEventType.MouseMove, false, 1918, 500, 0));
            Assert.Contains(EdgeState.Candidate, _stateChanges);

            // Move away before delay
            _mockInput.RaiseEvent(new MouseInputEvent(InputEventType.MouseMove, false, 1900, 500, 0));
            
            Assert.Contains(EdgeState.Cancelled, _stateChanges);
            Assert.Equal(EdgeState.Idle, _service.CurrentState);
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

            _service.UpdateOptions(new EdgeOptions { ActiveEdge = ScreenEdge.Right, EdgeThresholdPixels = 4, EdgeActivationDelayMs = 50 });
            _service.Start();

            _mockInput.RaiseEvent(new MouseInputEvent(InputEventType.MouseMove, false, 1000, 500, 0));
            
            // Move to the right edge of the first monitor (which is now internal)
            _mockInput.RaiseEvent(new MouseInputEvent(InputEventType.MouseMove, false, 1918, 500, 0));

            Assert.DoesNotContain(EdgeState.Candidate, _stateChanges);
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

            _service.UpdateOptions(new EdgeOptions { ActiveEdge = ScreenEdge.Left, EdgeThresholdPixels = 4, EdgeActivationDelayMs = 50 });
            _service.Start();

            _mockInput.RaiseEvent(new MouseInputEvent(InputEventType.MouseMove, false, -1000, 500, 0));
            
            // Move to left edge (<= -1916)
            _mockInput.RaiseEvent(new MouseInputEvent(InputEventType.MouseMove, false, -1918, 500, 0));

            Assert.Contains(EdgeState.Candidate, _stateChanges);
        }
    }

    public class EdgeHandoffServiceTests
    {
        [Fact]
        public async Task ArmedEdge_WaitsForPreparationAndRechecksConnection()
        {
            var edge = new FakeEdgeTransitionService();
            using var engine = new FakeScrcpyControlEngine();
            var ready = new TaskCompletionSource<bool>();
            bool connected = true;
            var handoff = new EdgeHandoffService(edge, () => connected, prepareCapture: () => ready.Task);
            handoff.AttachEngine(engine);
            edge.Raise(EdgeState.Armed);
            Assert.Equal(0, engine.CaptureCalls);
            connected = false;
            ready.SetResult(true);
            await Task.Yield();
            Assert.Equal(0, engine.CaptureCalls);
        }

        [Fact]
        public void ArmedEdge_DoesNotCaptureWhenWindowActivationFails()
        {
            var edge = new FakeEdgeTransitionService();
            using var engine = new FakeScrcpyControlEngine();
            var handoff = new EdgeHandoffService(edge, () => true, prepareCapture: () => Task.FromResult(false));
            handoff.AttachEngine(engine);
            edge.Raise(EdgeState.Armed);
            Assert.Equal(0, engine.CaptureCalls);
        }

        [Fact]
        public void ArmedEdge_CapturesOnlyWhenAndroidCompanionIsConnected()
        {
            var edge = new FakeEdgeTransitionService();
            using var engine = new FakeScrcpyControlEngine();
            var handoff = new EdgeHandoffService(edge, () => false);
            handoff.AttachEngine(engine);

            edge.Raise(EdgeState.Armed);

            Assert.Equal(0, engine.CaptureCalls);
        }

        [Fact]
        public void ArmedEdge_CapturesWhenAndroidCompanionIsConnected()
        {
            var edge = new FakeEdgeTransitionService();
            var operations = new List<string>();
            using var engine = new FakeScrcpyControlEngine(operations);
            var handoff = new EdgeHandoffService(edge, () => true, _ => operations.Add("handoff"));
            handoff.AttachEngine(engine);

            edge.Raise(EdgeState.Armed);

            Assert.True(SpinWait.SpinUntil(() => engine.CaptureCalls == 1, TimeSpan.FromSeconds(1)));
            Assert.Equal(new[] { "handoff", "capture" }, operations);
        }

        private sealed class FakeEdgeTransitionService : IEdgeTransitionService
        {
            public event EventHandler<EdgeTransitionEventArgs>? StateChanged;
            public EdgeState CurrentState { get; private set; } = EdgeState.Idle;
            public EdgeOptions Options { get; } = new();

            public void Start() { }
            public void Stop() { }
            public void UpdateOptions(EdgeOptions options) { }

            public void Raise(EdgeState state)
            {
                CurrentState = state;
                StateChanged?.Invoke(this, new EdgeTransitionEventArgs(state, ScreenEdge.Right));
            }
        }

        private sealed class FakeScrcpyControlEngine : IScrcpyControlEngine
        {
            private readonly List<string>? _operations;

            public FakeScrcpyControlEngine(List<string>? operations = null)
            {
                _operations = operations;
            }

            public event EventHandler<ScrcpyEngineState>? StateChanged
            {
                add { }
                remove { }
            }
            public event EventHandler<string>? Error
            {
                add { }
                remove { }
            }
            public ScrcpyEngineState State => ScrcpyEngineState.Ready;
            public ControlOwner ControlOwner => ControlOwner.Windows;
            public bool IsRunning => true;
            public bool IsCaptured => false;
            public int CaptureCalls { get; private set; }

            public Task StartAsync(ConnectionMode mode, AndroidDevice device, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task StopAsync() => Task.CompletedTask;
            public Task CaptureAsync()
            {
                CaptureCalls++;
                _operations?.Add("capture");
                return Task.CompletedTask;
            }
            public Task ReleaseAsync() => Task.CompletedTask;
            public void Dispose() { }
        }
    }
}
