using System.Reflection;
using WindowsHost.Engine;

namespace WindowsHost.Tests;

public class ScrcpyCaptureStateTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static ScrcpyIpcClient Attach(ScrcpyProcessManager engine)
    {
        var client = new ScrcpyIpcClient(); // No pipe or native process is started.
        typeof(ScrcpyProcessManager).GetField("_ipcClient", Private)!.SetValue(engine, client);
        typeof(ScrcpyProcessManager).GetProperty("State")!.SetValue(engine, ScrcpyEngineState.Ready);
        return client;
    }
    private static void Receive(ScrcpyProcessManager engine, ScrcpyIpcClient client, string message) =>
        typeof(ScrcpyProcessManager).GetMethod("OnIpcMessageReceived", Private)!.Invoke(engine, new object[] { client, message });

    [Fact]
    public async Task ConfirmedCaptureAndNativeFocusLossPublishCorrectOwnership()
    {
        var engine = new ScrcpyProcessManager();
        var client = Attach(engine);
        var states = new List<ScrcpyEngineState>();
        engine.StateChanged += (_, state) => states.Add(state);
        var capture = engine.CaptureAsync();
        Receive(engine, client, "CAPTURED");
        await capture.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.True(engine.IsCaptured);
        Assert.Equal(ControlOwner.Android, engine.ControlOwner);
        Receive(engine, client, "CAPTURE_LOST");
        Assert.Equal(ScrcpyEngineState.Ready, engine.State);
        Assert.Equal(ControlOwner.Windows, engine.ControlOwner);
        Assert.Equal(new[] { ScrcpyEngineState.Captured, ScrcpyEngineState.Ready }, states);
        await engine.StopAsync();
    }

    [Theory]
    [InlineData("CAPTURE_LOST")]
    [InlineData("ERROR CAPTURE_FAILED")]
    public async Task RejectedPendingCaptureRollsBackPreparedHandoff(string response)
    {
        var engine = new ScrcpyProcessManager();
        var client = Attach(engine);
        var states = new List<ScrcpyEngineState>();
        engine.StateChanged += (_, state) => states.Add(state);
        var capture = engine.CaptureAsync();
        Receive(engine, client, response);
        await capture.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.False(engine.IsCaptured);
        Assert.Equal(ControlOwner.Windows, engine.ControlOwner);
        Assert.Contains(ScrcpyEngineState.Ready, states); // MainWindow clears the prepared handoff here.
        Receive(engine, client, "CAPTURED"); // Late acknowledgement must not undo the rollback.
        Assert.False(engine.IsCaptured);
        await engine.StopAsync();
    }

    [Fact]
    public async Task FocusLossBetweenAcknowledgementAndContinuationCannotRestoreCapturedState()
    {
        var engine = new ScrcpyProcessManager();
        var client = Attach(engine);
        var stateLock = typeof(ScrcpyProcessManager).GetField("_captureStateLock", Private)!.GetValue(engine)!;
        var capture = engine.CaptureAsync();
        lock (stateLock)
        {
            Receive(engine, client, "CAPTURED");
            Receive(engine, client, "CAPTURE_LOST");
        }
        await capture.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.False(engine.IsCaptured);
        Assert.Equal(ControlOwner.Windows, engine.ControlOwner);
        await engine.StopAsync();
    }

    [Theory]
    [InlineData("CAPTURE_LOST")]
    [InlineData("RELEASED")]
    [InlineData("ERROR CAPTURE_FAILED")]
    public async Task NotificationsFromReplacedEngineCannotReleaseCurrentCapture(string response)
    {
        var engine = new ScrcpyProcessManager();
        var client = Attach(engine);
        var capture = engine.CaptureAsync();
        Receive(engine, client, "CAPTURED");
        await capture.WaitAsync(TimeSpan.FromSeconds(1));
        using var stale = new ScrcpyIpcClient();
        Receive(engine, stale, response);
        Assert.True(engine.IsCaptured);
        Assert.Equal(ControlOwner.Android, engine.ControlOwner);
        Receive(engine, client, "RELEASED");
        Assert.False(engine.IsCaptured);
        await engine.StopAsync();
    }
}
