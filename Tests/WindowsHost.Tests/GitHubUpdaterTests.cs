using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WindowsHost.Updates;

namespace WindowsHost.Tests;

public class GitHubUpdaterTests
{
    private static readonly byte[] Package = Encoding.UTF8.GetBytes("synthetic installer fixture; never executed");
    private static string Digest => Convert.ToHexString(SHA256.HashData(Package)).ToLowerInvariant();
    private static string Release(string version = "1.1.20", string? url = null, string? digest = null, bool draft = false, bool prerelease = false) =>
        JsonSerializer.Serialize(new {
            tag_name = "v" + version, draft, prerelease,
            assets = new[] { new {
                name = $"EZAcrossControl-{version}-windows-x64-setup.exe", size = Package.Length,
                state = "uploaded", digest = "sha256:" + (digest ?? Digest),
                browser_download_url = url ?? $"https://github.com/eddesignerez/EzAcrossControl/releases/download/v{version}/EZAcrossControl-{version}-windows-x64-setup.exe"
            } }
        });

    [Fact]
    public void OnlyNewStableVersionWithExactRepositoryAssetAndDigestIsOffered()
    {
        Assert.NotNull(GitHubUpdater.ParseRelease(Release(), "1.1.19"));
        Assert.Null(GitHubUpdater.ParseRelease(Release(), "1.1.20"));
        Assert.Null(GitHubUpdater.ParseRelease(Release(), "1.1.21"));
        Assert.Null(GitHubUpdater.ParseRelease(Release(draft: true), "1.1.19"));
        Assert.Null(GitHubUpdater.ParseRelease(Release(prerelease: true), "1.1.19"));
        Assert.Null(GitHubUpdater.ParseRelease(Release(url: "https://github.com/other/repository/setup.exe"), "1.1.19"));
        Assert.Null(GitHubUpdater.ParseRelease(Release(digest: "invalid"), "1.1.19"));
        Assert.Null(GitHubUpdater.ParseRelease(Release(version: "01.1.20"), "1.1.19"));
    }

    [Theory]
    [InlineData("v1.1.20-beta")]
    [InlineData("latest")]
    [InlineData("1.1")]
    [InlineData("1.1.2147483648")]
    [InlineData("1.1.20 & cmd")]
    public void InvalidVersionsNeverProduceInstallArguments(string text) => Assert.Null(GitHubUpdater.ParseVersion(text));

    [Theory]
    [InlineData("http://github.com/eddesignerez/EzAcrossControl/releases/download/v1/a.exe")]
    [InlineData("https://github.com.evil.example/eddesignerez/EzAcrossControl/releases/download/v1/a.exe")]
    [InlineData("https://localhost/a.exe")]
    [InlineData("https://user:password@release-assets.githubusercontent.com/a.exe")]
    [InlineData("https://release-assets.githubusercontent.com:8443/a.exe")]
    public void RedirectsCannotEscapeApprovedHttpsHosts(string url) => Assert.False(GitHubUpdater.AllowedDownloadRedirect(new Uri(url)));

    private sealed class FakeHttp(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(response(request));
    }

    private static string TempDirectory() => Path.Combine(Path.GetTempPath(), "ezac-update-test-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task AuthorizedCandidateDownloadsToUniqueFileAndChecksHash()
    {
        using var updater = new GitHubUpdater(new FakeHttp(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Package) }));
        var candidate = GitHubUpdater.ParseRelease(Release(), "1.1.19")!;
        var directory = TempDirectory();
        string file = await updater.DownloadAsync(candidate, directory);
        try { Assert.Equal(Package, await File.ReadAllBytesAsync(file)); }
        finally { File.Delete(file); Directory.Delete(directory); }
    }

    [Fact]
    public async Task CorruptDownloadRemovesOnlyPartialFile()
    {
        using var updater = new GitHubUpdater(new FakeHttp(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Package) }));
        var candidate = GitHubUpdater.ParseRelease(Release(digest: new string('0', 64)), "1.1.19")!;
        var directory = TempDirectory();
        await Assert.ThrowsAsync<InvalidDataException>(() => updater.DownloadAsync(candidate, directory));
        Assert.Empty(Directory.GetFiles(directory));
        Directory.Delete(directory);
    }

    [Fact]
    public async Task UntrustedRedirectNeverDownloadsPayload()
    {
        int requests = 0;
        using var updater = new GitHubUpdater(new FakeHttp(_ => {
            requests++;
            var response = new HttpResponseMessage(HttpStatusCode.Redirect);
            response.Headers.Location = new Uri("http://127.0.0.1/setup.exe");
            return response;
        }));
        var directory = TempDirectory();
        await Assert.ThrowsAsync<InvalidDataException>(() => updater.DownloadAsync(GitHubUpdater.ParseRelease(Release(), "1.1.19")!, directory));
        Assert.Equal(1, requests);
        Assert.Empty(Directory.GetFiles(directory));
        Directory.Delete(directory);
    }

    [Fact]
    public async Task OfflineCheckFailsWithoutOfferingAnUpdate()
    {
        using var updater = new GitHubUpdater(new FakeHttp(_ => throw new HttpRequestException("Offline fixture")));
        await Assert.ThrowsAsync<HttpRequestException>(() => updater.CheckAsync("1.1.19"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public async Task IncompleteOrOversizedDownloadIsRejected(int extra)
    {
        var bytes = new byte[Package.Length + extra];
        using var updater = new GitHubUpdater(new FakeHttp(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) }));
        var directory = TempDirectory();
        await Assert.ThrowsAsync<InvalidDataException>(() => updater.DownloadAsync(GitHubUpdater.ParseRelease(Release(), "1.1.19")!, directory));
        Assert.Empty(Directory.GetFiles(directory));
        Directory.Delete(directory);
    }

    [Fact]
    public async Task CancellationDoesNotLeaveInstaller()
    {
        using var cancellation = new CancellationTokenSource();
        using var updater = new GitHubUpdater(new FakeHttp(_ => {
            cancellation.Cancel();
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Package) };
        }));
        var directory = TempDirectory();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => updater.DownloadAsync(GitHubUpdater.ParseRelease(Release(), "1.1.19")!, directory, cancellation: cancellation.Token));
        Assert.Empty(Directory.GetFiles(directory));
        Directory.Delete(directory);
    }

    [Fact]
    public async Task OfficialAssetCdnRedirectIsSupported()
    {
        int requests = 0;
        using var updater = new GitHubUpdater(new FakeHttp(request => {
            requests++;
            if (request.RequestUri!.Host == "github.com") {
                var response = new HttpResponseMessage(HttpStatusCode.Redirect);
                response.Headers.Location = new Uri("https://release-assets.githubusercontent.com/fixture/setup.exe");
                return response;
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Package) };
        }));
        var directory = TempDirectory();
        var file = await updater.DownloadAsync(GitHubUpdater.ParseRelease(Release(), "1.1.19")!, directory);
        try { Assert.Equal(2, requests); Assert.Equal(Package, await File.ReadAllBytesAsync(file)); }
        finally { File.Delete(file); Directory.Delete(directory); }
    }
}
