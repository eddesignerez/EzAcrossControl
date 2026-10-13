using WindowsHost;

namespace WindowsHost.Tests;

public class WindowsStartupRegistrationTests
{
    private sealed class Store : IStartupRegistrationStore
    {
        public string? Command;
        public bool Fail;
        public string? Read() => Command;
        public void Write(string? command)
        {
            if (Fail) throw new UnauthorizedAccessException("Fixture denied");
            Command = command;
        }
    }

    [Fact]
    public void CheckboxOptInPersistsQuotedExecutableAndOptOutRemovesIt()
    {
        var store = new Store();
        const string executable = @"C:\Program Files\EZ Across Control\EzAcrossControl.exe";
        var registration = new WindowsStartupRegistration(executable, store);
        Assert.False(registration.IsEnabled);
        registration.SetEnabled(true);
        Assert.Equal('"' + executable + '"', store.Command);
        Assert.True(new WindowsStartupRegistration(executable, store).IsEnabled);
        registration.SetEnabled(false);
        Assert.Null(store.Command);
        Assert.False(registration.IsEnabled);
    }

    [Fact]
    public void DifferentExecutableIsNotReportedAsEnabled()
    {
        var store = new Store { Command = @"""C:\Old\EzAcrossControl.exe""" };
        Assert.False(new WindowsStartupRegistration(@"C:\Current\EzAcrossControl.exe", store).IsEnabled);
    }

    [Fact]
    public void WriteFailureDoesNotChangePreviousPreference()
    {
        var store = new Store();
        var registration = new WindowsStartupRegistration(@"C:\Apps\EzAcrossControl.exe", store);
        registration.SetEnabled(true);
        store.Fail = true;
        Assert.Throws<UnauthorizedAccessException>(() => registration.SetEnabled(false));
        Assert.True(registration.IsEnabled);
    }

    [Theory]
    [InlineData("EzAcrossControl.exe")]
    [InlineData("C:\\Apps\\other.cmd")]
    [InlineData("C:\\Apps\\quoted\".exe")]
    public void InvalidStartupCommandsAreRejected(string executable) =>
        Assert.Throws<ArgumentException>(() => WindowsStartupRegistration.BuildCommand(executable));

    [Fact]
    public void CommandBeyondWindowsRunLimitIsRejected() =>
        Assert.Throws<ArgumentException>(() => WindowsStartupRegistration.BuildCommand(@"C:\Apps\" + new string('x', 251) + ".exe"));
}
