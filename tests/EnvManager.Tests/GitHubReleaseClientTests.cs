// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: GitHubReleaseClientTests.cs                                             │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-11-05                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using EnvManager.Updates;
using Semver;
using TUnit.Assertions;
using TUnit.Core;

namespace EnvManager.Tests;

public sealed class GitHubReleaseClientTests
{
    [Test]
    public async Task GetAvailableUpdatesAsyncReturnsOnlyNewerMatchingVerifiedAssetsInVersionOrder()
    {
        const string digest = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string asset = "EnvManager-linux-x64";
        string releases = $$"""
            [
              {{ReleaseJson("v1.2.0", asset, digest)}},
              {{ReleaseJson("v1.1.0", asset, digest)}},
              {{ReleaseJson("v0.9.0", asset, digest)}},
              {{ReleaseJson("v4.0.0", "EnvManager-windows-x64.exe", digest)}},
              {{ReleaseJson("v5.0.0", asset, "sha1:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")}},
              {{ReleaseJson("v6.0.0", asset, digest, "https://example.com/EnvManager")}}
            ]
            """;
        using HttpClient httpClient = CreateClient(releases);
        GitHubReleaseClient client = new(httpClient);

        var updates = await client.GetAvailableUpdatesAsync(SemVersion.Parse("1.0.0"), asset, CancellationToken.None);

        await Assert.That(updates.Count).IsEqualTo(2);
        await Assert.That(updates[0].Version.ToString()).IsEqualTo("1.2.0");
        await Assert.That(updates[1].Version.ToString()).IsEqualTo("1.1.0");
        await Assert.That(updates[0].Sha256).IsEqualTo(new string('a', 64));
    }

    [Test]
    public async Task GetAvailableUpdatesAsyncIncludesNewerPrereleasesAndIgnoresInvalidTags()
    {
        const string digest = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        string releases = $$"""
            [
              {{ReleaseJson("v2.0.0-beta.2", "EnvManager-linux-x64", digest)}},
              {{ReleaseJson("release-next", "EnvManager-linux-x64", digest)}},
              {{ReleaseJson("v1.0.0", "EnvManager-linux-x64", digest)}}
            ]
            """;
        using HttpClient httpClient = CreateClient(releases);
        GitHubReleaseClient client = new(httpClient);

        var updates = await client.GetAvailableUpdatesAsync(SemVersion.Parse("2.0.0-beta.1"), "EnvManager-linux-x64", CancellationToken.None);

        await Assert.That(updates.Count).IsEqualTo(1);
        await Assert.That(updates[0].Version.ToString()).IsEqualTo("2.0.0-beta.2");
    }

    [Test]
    public async Task GetAvailableUpdatesAsyncReturnsEmptyWhenNoNewerCompatibleReleaseExists()
    {
        const string digest = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        string releases = $$"""
            [
              {{ReleaseJson("v1.0.0", "EnvManager-linux-x64", digest)}},
              {{ReleaseJson("v2.0.0", "EnvManager-windows-x64.exe", digest)}}
            ]
            """;
        using HttpClient httpClient = CreateClient(releases);
        GitHubReleaseClient client = new(httpClient);

        var updates = await client.GetAvailableUpdatesAsync(SemVersion.Parse("1.0.0"), "EnvManager-linux-x64", CancellationToken.None);

        await Assert.That(updates.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetAvailableDowngradesAsyncReturnsOnlyOlderMatchingVerifiedAssetsInVersionOrder()
    {
        const string digest = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string asset = "EnvManager-linux-x64";
        string releases = $$"""
            [
              {{ReleaseJson("v1.2.0", asset, digest)}},
              {{ReleaseJson("v1.1.0", asset, digest)}},
              {{ReleaseJson("v0.9.0", asset, digest)}},
              {{ReleaseJson("v2.0.0", asset, digest)}},
              {{ReleaseJson("v3.0.0", asset, digest)}},
              {{ReleaseJson("v1.8.0", "EnvManager-windows-x64.exe", digest)}},
              {{ReleaseJson("v1.7.0", asset, "sha1:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")}},
              {{ReleaseJson("v1.6.0", asset, digest, "https://example.com/EnvManager")}}
            ]
            """;
        using HttpClient httpClient = CreateClient(releases);
        GitHubReleaseClient client = new(httpClient);

        var downgrades = await client.GetAvailableDowngradesAsync(
            SemVersion.Parse("2.0.0"),
            asset,
            CancellationToken.None);

        await Assert.That(downgrades.Count).IsEqualTo(3);
        await Assert.That(downgrades[0].Version.ToString()).IsEqualTo("1.2.0");
        await Assert.That(downgrades[1].Version.ToString()).IsEqualTo("1.1.0");
        await Assert.That(downgrades[2].Version.ToString()).IsEqualTo("0.9.0");
        await Assert.That(downgrades[0].Sha256).IsEqualTo(new string('a', 64));
    }

    [Test]
    public async Task GetAvailableDowngradesAsyncIncludesOlderPrereleasesAndIgnoresInvalidTags()
    {
        const string digest = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        string releases = $$"""
            [
              {{ReleaseJson("v2.0.0-beta.2", "EnvManager-linux-x64", digest)}},
              {{ReleaseJson("release-next", "EnvManager-linux-x64", digest)}},
              {{ReleaseJson("v1.9.9", "EnvManager-linux-x64", digest)}},
              {{ReleaseJson("v2.0.0-beta.3", "EnvManager-linux-x64", digest)}},
              {{ReleaseJson("v2.0.0-beta.4", "EnvManager-linux-x64", digest)}}
            ]
            """;
        using HttpClient httpClient = CreateClient(releases);
        GitHubReleaseClient client = new(httpClient);

        var downgrades = await client.GetAvailableDowngradesAsync(
            SemVersion.Parse("2.0.0-beta.3"),
            "EnvManager-linux-x64",
            CancellationToken.None);

        await Assert.That(downgrades.Count).IsEqualTo(2);
        await Assert.That(downgrades[0].Version.ToString()).IsEqualTo("2.0.0-beta.2");
        await Assert.That(downgrades[1].Version.ToString()).IsEqualTo("1.9.9");
    }

    [Test]
    public async Task GetAvailableDowngradesAsyncReturnsEmptyWhenNoOlderCompatibleReleaseExists()
    {
        const string digest = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        string releases = $$"""
            [
              {{ReleaseJson("v1.0.0", "EnvManager-linux-x64", digest)}},
              {{ReleaseJson("v2.0.0", "EnvManager-linux-x64", digest)}},
              {{ReleaseJson("v0.9.0", "EnvManager-windows-x64.exe", digest)}}
            ]
            """;
        using HttpClient httpClient = CreateClient(releases);
        GitHubReleaseClient client = new(httpClient);

        var downgrades = await client.GetAvailableDowngradesAsync(
            SemVersion.Parse("1.0.0"),
            "EnvManager-linux-x64",
            CancellationToken.None);

        await Assert.That(downgrades.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetAvailableDowngradesAsyncRejectsNonArrayApiResponses()
    {
        using HttpClient httpClient = CreateClient("{}");
        GitHubReleaseClient client = new(httpClient);

        await Assert.That(async () => await client.GetAvailableDowngradesAsync(
            SemVersion.Parse("1.0.0"),
            "EnvManager-linux-x64",
            CancellationToken.None)).Throws<System.Text.Json.JsonException>();
    }

    [Test]
    public async Task GetAvailableUpdatesAsyncRejectsNonArrayApiResponses()
    {
        using HttpClient httpClient = CreateClient("{}");
        GitHubReleaseClient client = new(httpClient);

        await Assert.That(async () => await client.GetAvailableUpdatesAsync(
            SemVersion.Parse("1.0.0"),
            "EnvManager-linux-x64",
            CancellationToken.None)).Throws<System.Text.Json.JsonException>();
    }

    private static HttpClient CreateClient(string content)
        => new(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json"),
        }));

    private static string ReleaseJson(string tag, string assetName, string digest, string? downloadUrl = null)
    {
        string escapedTag = System.Text.Json.JsonSerializer.Serialize(tag);
        string escapedAsset = System.Text.Json.JsonSerializer.Serialize(assetName);
        string escapedDigest = System.Text.Json.JsonSerializer.Serialize(digest);
        string escapedUrl = System.Text.Json.JsonSerializer.Serialize(
            downloadUrl ?? $"https://github.com/WolfBublitz/EnvManager/releases/download/{tag}/{assetName}");
        return $$"""
            {
              "draft": false,
              "tag_name": {{escapedTag}},
              "name": {{escapedTag}},
              "assets": [{
                "name": {{escapedAsset}},
                "browser_download_url": {{escapedUrl}},
                "digest": {{escapedDigest}}
              }]
            }
            """;
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responseFactory(request));
    }
}
