using System.Windows.Interop;
using WindowsHost.Input;

namespace WindowsHost.Tests;

public class NativeInputWindowTests
{
    [Fact]
    public async Task FindsHiddenUtilityInputWindowByProcessAndTitle()
    {
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                var title = "EZ Across test " + Guid.NewGuid();
                var parameters = new HwndSourceParameters(title)
                {
                    WindowStyle = unchecked((int)0x80000000), // hidden WS_POPUP
                    ExtendedWindowStyle = 0x80, // WS_EX_TOOLWINDOW
                    Width = 1,
                    Height = 1
                };
                using var window = new HwndSource(parameters);
                Assert.Equal(window.Handle, NativeMethods.FindProcessWindow(Environment.ProcessId, title));
                Assert.Equal(IntPtr.Zero, NativeMethods.FindProcessWindow(Environment.ProcessId, title + " other"));
                Assert.Equal(IntPtr.Zero, NativeMethods.FindProcessWindow(-1, title));
                completed.SetResult();
            }
            catch (Exception error) { completed.SetException(error); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }
}
