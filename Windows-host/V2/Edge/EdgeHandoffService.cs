using System;
using System.Threading.Tasks;
using WindowsHost.Engine;

namespace WindowsHost.V2.Edge
{
    public class EdgeHandoffService : IEdgeHandoffService
    {
        private readonly IEdgeTransitionService _edgeTransitionService;
        private readonly Func<bool> _isHandoffAvailable;
        private readonly Action<EdgeTransitionEventArgs> _onHandoffArmed;
        private readonly Func<Task<bool>> _prepareCapture;
        private bool _capturePending;
        private IScrcpyControlEngine? _engine;
        
        public EdgeHandoffService(IEdgeTransitionService edgeTransitionService,
                                  Func<bool>? isHandoffAvailable = null,
                                  Action<EdgeTransitionEventArgs>? onHandoffArmed = null,
                                  Func<Task<bool>>? prepareCapture = null)
        {
            _edgeTransitionService = edgeTransitionService;
            _isHandoffAvailable = isHandoffAvailable ?? (() => true);
            _onHandoffArmed = onHandoffArmed ?? (_ => { });
            _prepareCapture = prepareCapture ?? (() => Task.FromResult(true));
            _edgeTransitionService.StateChanged += OnEdgeStateChanged;
        }

        public void AttachEngine(IScrcpyControlEngine engine)
        {
            _engine = engine;
        }

        public void Start()
        {
            _edgeTransitionService.Start();
        }

        public void Stop()
        {
            _edgeTransitionService.Stop();
        }

        private async void OnEdgeStateChanged(object? sender, EdgeTransitionEventArgs e)
        {
            if (e.State == EdgeState.Armed)
            {
                if (!_capturePending && _isHandoffAvailable() && _engine != null && _engine.IsRunning && !_engine.IsCaptured)
                {
                    _capturePending = true;
                    try
                    {
                        if (!await _prepareCapture() || !_isHandoffAvailable()) return;
                        _onHandoffArmed(e);
                        await _engine.CaptureAsync();
                    }
                    finally { _capturePending = false; }
                }
            }
        }
    }
}
