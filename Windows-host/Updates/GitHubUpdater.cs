using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WindowsHost.Updates;

public sealed record UpdateRelease(string Version, Uri DownloadUrl, long Size, string Sha256);
public enum UpdateAvailability { Unknown, Current, Available }
public sealed record UpdateCheckResult(UpdateAvailability Availability, UpdateRelease? Release = null);

/// <summary>Only the public, stable release in our repository can supply an installer.</summary>
public sealed class GitHubUpdater : IDisposable
{
    public const string Repository = "eddesignerez/EzAcrossControl";
    public const long MaximumInstallerBytes = 256 * 1024 * 1024;
    private readonly HttpClient _http;
    public GitHubUpdater(HttpMessageHandler? handler = null)
    {
        _http = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false });
        _http.Timeout = Timeout.InfiniteTimeSpan;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("EZAcrossControl-Updater/1.0");
    }

    public static Version? ParseVersion(string? text) =>
        text != null && Regex.IsMatch(text, "^v?[0-9]+\\.[0-9]+\\.[0-9]+$")
        && Version.TryParse(text.TrimStart('v'), out var version) ? version : null;

    public static UpdateRelease? ParseRelease(string json, string installed)
        => ParseCheck(json, installed).Release;

    public static UpdateCheckResult ParseCheck(string json, string installed)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean()) return new(UpdateAvailability.Unknown);
        string? tag = root.GetProperty("tag_name").GetString();
        var remote = ParseVersion(tag);
        var current = ParseVersion(installed);
        if (remote == null || current == null || tag != "v" + remote) return new(UpdateAvailability.Unknown);
        string name = $"EZAcrossControl-{remote}-windows-x64-setup.exe";
        var assets = root.GetProperty("assets").EnumerateArray().Where(a => a.GetProperty("name").GetString() == name).ToArray();
        if (assets.Length != 1) return new(UpdateAvailability.Unknown);
        var asset = assets[0];
        var size = asset.GetProperty("size").GetInt64();
        string? digest = asset.TryGetProperty("digest", out var value) ? value.GetString() : null;
        string url = $"https://github.com/{Repository}/releases/download/{tag}/{name}";
        if (size <= 0 || size > MaximumInstallerBytes || asset.GetProperty("state").GetString() != "uploaded"
            || asset.GetProperty("browser_download_url").GetString() != url
            || digest == null || !Regex.IsMatch(digest, "^sha256:[0-9a-f]{64}$")) return new(UpdateAvailability.Unknown);
        return remote <= current ? new(UpdateAvailability.Current)
            : new(UpdateAvailability.Available, new UpdateRelease(remote.ToString(), new Uri(url), size, digest[7..]));
    }

    public async Task<UpdateRelease?> CheckAsync(string installed, CancellationToken cancellation = default)
        => (await CheckStatusAsync(installed, cancellation)).Release;

    public async Task<UpdateCheckResult> CheckStatusAsync(string installed, CancellationToken cancellation = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var response = await _http.GetAsync($"https://api.github.com/repos/{Repository}/releases/latest",
            HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        if (response.StatusCode == HttpStatusCode.NotFound) return new(UpdateAvailability.Unknown);
        response.EnsureSuccessStatusCode();
        var body = await ReadBoundedAsync(response.Content, 1024 * 1024, timeout.Token);
        return ParseCheck(System.Text.Encoding.UTF8.GetString(body), installed);
    }

    public static bool AllowedDownloadRedirect(Uri uri) => uri.Scheme == "https" && uri.IsDefaultPort
        && uri.UserInfo.Length == 0 && (uri.Host == "release-assets.githubusercontent.com"
            || (uri.Host == "github.com" && uri.AbsolutePath.StartsWith($"/{Repository}/releases/download/", StringComparison.Ordinal)));

    public async Task<string> DownloadAsync(UpdateRelease release, string directory, IProgress<int>? progress = null,
        CancellationToken cancellation = default)
    {
        // Revalidate the caller's candidate; never accept a path/name from release JSON.
        string name = $"EZAcrossControl-{release.Version}-windows-x64-setup.exe";
        if (ParseVersion(release.Version) == null || release.DownloadUrl.AbsoluteUri !=
            $"https://github.com/{Repository}/releases/download/v{release.Version}/{name}"
            || release.Size <= 0 || release.Size > MaximumInstallerBytes
            || !Regex.IsMatch(release.Sha256, "^[0-9a-f]{64}$")) throw new InvalidDataException("Invalid update candidate");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromMinutes(10));
        Directory.CreateDirectory(directory);
        string target = Path.Combine(directory, Guid.NewGuid().ToString("N") + "-setup.exe");
        try
        {
            Uri url = release.DownloadUrl;
            for (int redirects = 0; redirects <= 5; redirects++)
            {
                using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                if ((int)response.StatusCode is 301 or 302 or 303 or 307 or 308)
                {
                    var location = response.Headers.Location ?? throw new InvalidDataException("Missing redirect");
                    url = location.IsAbsoluteUri ? location : new Uri(url, location);
                    if (!AllowedDownloadRedirect(url)) throw new InvalidDataException("Untrusted download redirect");
                    continue;
                }
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength is long length && length != release.Size)
                    throw new InvalidDataException("Installer size mismatch");
                await using var input = await response.Content.ReadAsStreamAsync(timeout.Token);
                await using (var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
                {
                    using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                    byte[] buffer = new byte[65536];
                    long total = 0;
                    int count;
                    while ((count = await input.ReadAsync(buffer, timeout.Token)) > 0)
                    {
                        total += count;
                        if (total > release.Size) throw new InvalidDataException("Installer exceeded expected size");
                        hash.AppendData(buffer, 0, count);
                        await output.WriteAsync(buffer.AsMemory(0, count), timeout.Token);
                        progress?.Report((int)(total * 100 / release.Size));
                    }
                    if (total != release.Size || !CryptographicOperations.FixedTimeEquals(hash.GetHashAndReset(), Convert.FromHexString(release.Sha256)))
                        throw new InvalidDataException("Installer integrity check failed");
                }
                return target;
            }
            throw new InvalidDataException("Too many redirects");
        }
        catch
        {
            if (File.Exists(target)) File.Delete(target); // only our unique, newly created partial file
            throw;
        }
    }

    private static async Task<byte[]> ReadBoundedAsync(HttpContent content, int limit, CancellationToken cancellation)
    {
        await using var input = await content.ReadAsStreamAsync(cancellation);
        using var output = new MemoryStream();
        byte[] buffer = new byte[8192];
        int count;
        while ((count = await input.ReadAsync(buffer, cancellation)) > 0)
        {
            if (output.Length + count > limit) throw new InvalidDataException("Release metadata too large");
            output.Write(buffer, 0, count);
        }
        return output.ToArray();
    }
    public void Dispose() => _http.Dispose();
}
