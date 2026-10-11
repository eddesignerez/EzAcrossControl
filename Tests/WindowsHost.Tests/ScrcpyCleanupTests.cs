using System.IO;
using System.Reflection;
using WindowsHost.Engine;

namespace WindowsHost.Tests;

public class ScrcpyCleanupTests
{
    private sealed class BrokenStream : MemoryStream
    {
        public bool Broken { get; set; }
        public override void Flush()
        {
            if (Broken) throw new IOException("Pipe is broken");
            base.Flush();
        }
    }

    private static ScrcpyIpcClient BrokenClient()
    {
        var stream = new BrokenStream();
        var writer = new StreamWriter(stream) { AutoFlush = true };
        stream.Broken = true;
        var client = new ScrcpyIpcClient();
        typeof(ScrcpyIpcClient).GetField("_writer", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(client, writer);
        return client;
    }

    [Fact]
    public void DisposeToleratesBrokenPipeAndIsRepeatable()
    {
        var client = BrokenClient();
        client.Dispose();
        client.Dispose();
        Assert.Null(typeof(ScrcpyIpcClient).GetField("_writer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(client));
    }

    [Fact]
    public async Task StopResetsCapturedStateEvenWhenPipeCleanupFails()
    {
        using var engine = new ScrcpyProcessManager();
        typeof(ScrcpyProcessManager).GetField("_ipcClient", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(engine, BrokenClient());
        typeof(ScrcpyProcessManager).GetProperty("State")!.SetValue(engine, ScrcpyEngineState.Captured);
        typeof(ScrcpyProcessManager).GetProperty("ControlOwner")!.SetValue(engine, ControlOwner.Android);
        await engine.StopAsync();
        Assert.Equal(ScrcpyEngineState.Offline, engine.State);
        Assert.Equal(ControlOwner.Windows, engine.ControlOwner);
        Assert.False(engine.IsCaptured);
    }
}
