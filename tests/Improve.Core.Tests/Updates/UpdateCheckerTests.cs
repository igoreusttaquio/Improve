using System.Net;
using Improve.Core.Tests.Fakes;
using Improve.Core.Updates;

namespace Improve.Core.Tests.Updates;

public class UpdateCheckerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private const string Release = """
        {
          "tag_name": "v0.2.0",
          "assets": [
            { "name": "improve-linux-amd64.deb", "size": 10, "browser_download_url": "https://example.test/improve-linux-amd64.deb" },
            { "name": "improve-setup-x64.exe", "size": 12, "browser_download_url": "https://example.test/improve-setup-x64.exe" }
          ]
        }
        """;

    [Fact]
    public async Task Finds_the_installer_for_this_platform_when_a_newer_release_exists()
    {
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.Json(Release));
        var checker = new UpdateChecker(handler.CreateClient(), "dono/repo");

        var update = await checker.CheckAsync(new Version(0, 1, 0), "improve-setup-x64.exe", Ct);

        Assert.NotNull(update);
        Assert.Equal(new Version(0, 2, 0), update.Version);
        Assert.Equal("https://example.test/improve-setup-x64.exe", update.DownloadUrl.ToString());
        var sent = Assert.Single(handler.Requests);
        Assert.Equal("https://api.github.com/repos/dono/repo/releases/latest", sent.Uri.ToString());
        Assert.Contains("improve", sent.Headers["User-Agent"], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, 2, 0)]
    [InlineData(0, 3, 0)]
    public async Task Returns_null_when_the_installed_version_is_current_or_newer(int major, int minor, int patch)
    {
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.Json(Release));
        var checker = new UpdateChecker(handler.CreateClient());

        Assert.Null(await checker.CheckAsync(new Version(major, minor, patch), "improve-setup-x64.exe", Ct));
    }

    [Fact]
    public async Task Returns_null_when_the_release_has_no_installer_for_this_platform()
    {
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.Json(Release));
        var checker = new UpdateChecker(handler.CreateClient());

        Assert.Null(await checker.CheckAsync(new Version(0, 1, 0), "improve-macos-arm64.dmg", Ct));
    }

    [Fact]
    public async Task Falls_back_to_the_release_page_redirect_when_the_api_is_rate_limited()
    {
        var handler = new FakeHttpHandler(request => request.RequestUri!.Host == "api.github.com"
            ? FakeHttpHandler.Json("{}", HttpStatusCode.Forbidden)
            // O HttpClient real segue o redirecionamento; aqui a resposta já vem da página da tag.
            : new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://github.com/dono/repo/releases/tag/v0.2.0") });
        var checker = new UpdateChecker(handler.CreateClient(), "dono/repo");

        var update = await checker.CheckAsync(new Version(0, 1, 0), "improve-setup-x64.exe", Ct);

        Assert.NotNull(update);
        Assert.Equal(new Version(0, 2, 0), update.Version);
        Assert.Equal("https://github.com/dono/repo/releases/download/v0.2.0/improve-setup-x64.exe", update.DownloadUrl.ToString());
        Assert.Equal("https://github.com/dono/repo/releases/latest", handler.Requests[1].Uri.ToString());
    }

    [Fact]
    public async Task Throws_when_github_fails()
    {
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.Json("{}", HttpStatusCode.InternalServerError));
        var checker = new UpdateChecker(handler.CreateClient());

        await Assert.ThrowsAsync<HttpRequestException>(() => checker.CheckAsync(new Version(0, 1, 0), "improve-setup-x64.exe", Ct));
    }

    [Fact]
    public async Task Downloads_the_installer_and_reports_progress()
    {
        var bytes = new byte[300_000];
        Random.Shared.NextBytes(bytes);
        var handler = new FakeHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
        var checker = new UpdateChecker(handler.CreateClient());
        var update = new UpdateInfo(new Version(0, 2, 0), "v0.2.0", new Uri("https://example.test/improve-setup-x64.exe"), "improve-setup-x64.exe", bytes.Length);
        var directory = Path.Combine(Path.GetTempPath(), "improve-tests", Guid.NewGuid().ToString("N"));
        var reports = new List<double>();

        try
        {
            var path = await checker.DownloadAsync(update, directory, new SyncProgress(reports.Add), Ct);

            Assert.Equal(bytes, await File.ReadAllBytesAsync(path, Ct));
            Assert.False(File.Exists(path + ".part"));
            Assert.Equal(1, reports[^1]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("v1.2.3", 1, 2, 3)]
    [InlineData("1.2", 1, 2, 0)]
    [InlineData("v0.3.0-beta.1", 0, 3, 0)]
    public void Parses_tags(string tag, int major, int minor, int patch)
    {
        Assert.True(UpdateChecker.TryParseVersion(tag, out var version));
        Assert.Equal(new Version(major, minor, patch), version);
    }

    // Progress<T> publica no contexto de sincronização; aqui o relatório precisa ser imediato.
    private sealed class SyncProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}
