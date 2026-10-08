using System;
using System.Diagnostics;
using WindowsHost.Input;
using EZAcrossControl.Input;

namespace WindowsHost.V2.Edge
{
    public class EdgeTransitionService : IEdgeTransitionService
    {
        private readonly IInputCaptureService _inputCaptureService;
        private readonly MonitorGeometry _monitorGeometry;
        private EdgeOptions _options;
        private EdgeState _currentState = EdgeState.Idle;
        
        private int _lastX;
        private int _lastY;
        private bool _hasLastPos;
        private int _outwardDelta;
        
        private int _preEdgeOutwardPush;
        private const int PreEdgeZonePixels = 32;

        private Stopwatch _candidateTimer;
        private CancellationTokenSource? _autoArmCts;
        
        private long _candidateEnterTimestamp;
        private bool _isRearmGuardActive = false;
        private long _rearmGuardEndTimestamp = 0;

        public event EventHandler<EdgeTransitionEventArgs>? StateChanged;

        public EdgeState CurrentState => _currentState;
        public EdgeOptions Options => _options;

        public EdgeTransitionService(IInputCaptureService inputCaptureService, MonitorGeometry? monitorGeometry = null)
        {
            _inputCaptureService = inputCaptureService;
            _monitorGeometry = monitorGeometry ?? new MonitorGeometry();
            _options = new EdgeOptions();
            _candidateTimer = new Stopwatch();
        }

        public void Start()
        {
            _inputCaptureService.InputEventCaptured += OnInputEventCaptured;
            ChangeState(EdgeState.Idle);
            _hasLastPos = false;
        }

        public void Stop()
        {
            _inputCaptureService.InputEventCaptured -= OnInputEventCaptured;
            _candidateTimer.Stop();
            _autoArmCts?.Cancel();
            ChangeState(EdgeState.Disabled);
        }

        public void UpdateOptions(EdgeOptions options)
        {
            _options = options;
            if (_options.ActiveEdge == ScreenEdge.Disabled)
            {
                ChangeState(EdgeState.Disabled);
            }
            else if (_currentState == EdgeState.Disabled)
            {
                ChangeState(EdgeState.Idle);
            }
            else
            {
                ChangeState(EdgeState.Idle); // Reset state when options change
            }
        }

        private void OnInputEventCaptured(object sender, InputEvent e)
        {
            if (_currentState == EdgeState.Disabled) return;
            if (_options.ActiveEdge == ScreenEdge.Disabled) return;

            if (e is MouseInputEvent mouseEvent && mouseEvent.Type == InputEventType.MouseMove)
            {
                ProcessMouseMove(mouseEvent.X, mouseEvent.Y);
            }
        }

        public void StartRearmGuard()
        {
            _isRearmGuardActive = true;
            _rearmGuardEndTimestamp = Stopwatch.GetTimestamp() + (long)(Stopwatch.Frequency * 0.250); // 250ms guard
            ChangeState(EdgeState.Idle);
            _hasLastPos = false;
        }

        private void ProcessMouseMove(int x, int y)
        {
            if (_isRearmGuardActive)
            {
                if (Stopwatch.GetTimestamp() > _rearmGuardEndTimestamp)
                {
                    // Time elapsed, but we also require moving out of the edge zone to fully rearm
                    if (!IsCursorNearEdge(x, y))
                    {
                        _isRearmGuardActive = false;
                    }
                }

                if (_isRearmGuardActive)
                {
                    _lastX = x;
                    _lastY = y;
                    return; // Ignore events while guard is active
                }
            }

            if (!_hasLastPos)
            {
                _lastX = x;
                _lastY = y;
                _hasLastPos = true;
                return;
            }

            int dx = x - _lastX;
            int dy = y - _lastY;
            _lastX = x;
            _lastY = y;

            if (_currentState == EdgeState.Armed)
            {
                // We are already armed, we just check if we moved out of the threshold
                if (!IsCursorInThreshold(x, y))
                {
                    ChangeState(EdgeState.Idle);
                }
                return;
            }

            bool inThreshold = IsCursorInThreshold(x, y);
            bool crossedEdge = false;

            if (_currentState == EdgeState.Idle)
            {
                // Swept Edge Detection: Check if segment crossed the edge zone start
                if (!inThreshold)
                {
                    crossedEdge = DidSegmentCrossEdgeThreshold(_lastX, _lastY, x, y);
                }

                if (inThreshold || crossedEdge)
                {
                    ChangeState(EdgeState.Candidate);
                    _candidateEnterTimestamp = Stopwatch.GetTimestamp();
                    _candidateTimer.Restart();
                    
                    // Transfer pre-push buffer
                    _outwardDelta = _preEdgeOutwardPush;
                    
                    if (_outwardDelta >= _options.EdgePushThreshold)
                    {
                        _autoArmCts?.Cancel();
                        ChangeState(EdgeState.Armed);
                        _candidateTimer.Stop();
                        return;
                    }
                    
                    _autoArmCts?.Cancel();
                    _autoArmCts = new CancellationTokenSource();
                    _ = StartActivationDelayAsync(_autoArmCts.Token);
                }
                else
                {
                    // Reset pre-edge push if moved away from near edge zone
                    if (!IsCursorNearEdge(x, y))
                    {
                        _preEdgeOutwardPush = 0;
                    }
                }
            }
            else if (_currentState == EdgeState.Candidate)
            {
                if (!inThreshold)
                {
                    _autoArmCts?.Cancel();
                    ChangeState(EdgeState.Cancelled);
                    ChangeState(EdgeState.Idle);
                    _candidateTimer.Stop();
                    _outwardDelta = 0;
                }
            }
        }

        private async Task StartActivationDelayAsync(CancellationToken token)
        {
            try
            {
                int delay = _options.EdgeActivationDelayMs > 0 ? _options.EdgeActivationDelayMs : 1;
                await Task.Delay(delay, token);
                
                if (!token.IsCancellationRequested && _currentState == EdgeState.Candidate)
                {
                    // Delay expired, we are still in Candidate, intention remains
                    ChangeState(EdgeState.Armed);
                }
            }
            catch (TaskCanceledException)
            {
                // Ignored
            }
        }

        public void ProcessRawMouseMove(int dx, int dy)
        {
            if (_isRearmGuardActive) return;

            if (_currentState == EdgeState.Candidate)
            {
                if (_options.ActiveEdge == ScreenEdge.Right && dx > 0) _outwardDelta += dx;
                else if (_options.ActiveEdge == ScreenEdge.Left && dx < 0) _outwardDelta -= dx; // Note: dx is negative, so subtracting makes it positive
                else if (_options.ActiveEdge == ScreenEdge.Bottom && dy > 0) _outwardDelta += dy;
                else if (_options.ActiveEdge == ScreenEdge.Top && dy < 0) _outwardDelta -= dy;

                if (_outwardDelta >= _options.EdgePushThreshold)
                {
                    _autoArmCts?.Cancel();
                    ChangeState(EdgeState.Armed);
                    _candidateTimer.Stop();
                }
            }
            else if (_currentState == EdgeState.Idle)
            {
                if (IsCursorNearEdge(_lastX, _lastY))
                {
                    if (_options.ActiveEdge == ScreenEdge.Right && dx > 0) _preEdgeOutwardPush += dx;
                    else if (_options.ActiveEdge == ScreenEdge.Left && dx < 0) _preEdgeOutwardPush -= dx;
                    else if (_options.ActiveEdge == ScreenEdge.Bottom && dy > 0) _preEdgeOutwardPush += dy;
                    else if (_options.ActiveEdge == ScreenEdge.Top && dy < 0) _preEdgeOutwardPush -= dy;
                }
            }
        }

        public long GetCandidateEnterTimestamp() => _candidateEnterTimestamp;

        private bool IsCursorInThreshold(int x, int y)
        {
            var monitor = _monitorGeometry.GetMonitorFromPoint(x, y);
            if (monitor == null) return false;

            if (_monitorGeometry.IsInternalEdge(_options.ActiveEdge, monitor))
            {
                return false;
            }

            int t = _options.EdgeThresholdPixels;
            return CheckEdgeDistance(x, y, monitor, t);
        }

        private bool IsCursorNearEdge(int x, int y)
        {
            var monitor = _monitorGeometry.GetMonitorFromPoint(x, y);
            if (monitor == null) return false;
            if (_monitorGeometry.IsInternalEdge(_options.ActiveEdge, monitor)) return false;

            return CheckEdgeDistance(x, y, monitor, PreEdgeZonePixels);
        }

        private bool CheckEdgeDistance(int x, int y, MonitorInfo monitor, int threshold)
        {
            switch (_options.ActiveEdge)
            {
                case ScreenEdge.Left:
                    return x <= monitor.Bounds.left + threshold;
                case ScreenEdge.Right:
                    return x >= monitor.Bounds.right - threshold - 1;
                case ScreenEdge.Top:
                    return y <= monitor.Bounds.top + threshold;
                case ScreenEdge.Bottom:
                    return y >= monitor.Bounds.bottom - threshold - 1;
                default:
                    return false;
            }
        }

        private bool DidSegmentCrossEdgeThreshold(int oldX, int oldY, int newX, int newY)
        {
            var monitor = _monitorGeometry.GetMonitorFromPoint(newX, newY);
            if (monitor == null) return false;
            if (_monitorGeometry.IsInternalEdge(_options.ActiveEdge, monitor)) return false;

            int t = _options.EdgeThresholdPixels;
            switch (_options.ActiveEdge)
            {
                case ScreenEdge.Left:
                    int leftEdge = monitor.Bounds.left + t;
                    return oldX > leftEdge && newX <= leftEdge;
                case ScreenEdge.Right:
                    int rightEdge = monitor.Bounds.right - t - 1;
                    return oldX < rightEdge && newX >= rightEdge;
                case ScreenEdge.Top:
                    int topEdge = monitor.Bounds.top + t;
                    return oldY > topEdge && newY <= topEdge;
                case ScreenEdge.Bottom:
                    int bottomEdge = monitor.Bounds.bottom - t - 1;
                    return oldY < bottomEdge && newY >= bottomEdge;
                default:
                    return false;
            }
        }

        private bool IsMovingTowardsEdge(int dx, int dy)
        {
            // Simple intent detection: predominantly moving towards the active edge
            switch (_options.ActiveEdge)
            {
                case ScreenEdge.Left:
                    return dx < 0; // && Math.Abs(dx) >= Math.Abs(dy) - maybe too strict? Let's keep it simple.
                case ScreenEdge.Right:
                    return dx > 0;
                case ScreenEdge.Top:
                    return dy < 0;
                case ScreenEdge.Bottom:
                    return dy > 0;
                default:
                    return false;
            }
        }

        private void ChangeState(EdgeState newState)
        {
            if (_currentState != newState)
            {
                _currentState = newState;
                StateChanged?.Invoke(this, new EdgeTransitionEventArgs(_currentState, _options.ActiveEdge));
            }
        }
    }
}
