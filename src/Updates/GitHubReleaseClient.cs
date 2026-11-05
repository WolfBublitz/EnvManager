// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: GitHubReleaseClient.cs                                                  │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-11-05                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Semver;

namespace EnvManager.Updates;

internal sealed class GitHubReleaseClient
{
    private static readonly Uri ReleasesUri = new("https://api.github.com/repos/WolfBublitz/EnvManager/releases?per_page=100");

    private readonly HttpClient httpClient;

    public GitHubReleaseClient(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<IReadOnlyList<UpdateRelease>> GetAvailableUpdatesAsync(
        SemVersion currentVersion,
        string assetName,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, ReleasesUri);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("EnvManager", currentVersion.ToString()));

        using HttpResponseMessage response = await this.httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using System.IO.Stream content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using JsonDocument document = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("The GitHub releases response must be a JSON array.");
        }

        List<UpdateRelease> releases = [];
        foreach (JsonElement release in document.RootElement.EnumerateArray())
        {
            if (release.ValueKind != JsonValueKind.Object
                || !release.TryGetProperty("draft", out JsonElement draft)
                || draft.ValueKind != JsonValueKind.False
                || !release.TryGetProperty("tag_name", out JsonElement tag)
                || tag.ValueKind != JsonValueKind.String
                || !TryParseTag(tag.GetString(), out SemVersion? version)
                || version is null
                || version.ComparePrecedenceTo(currentVersion) <= 0
                || !release.TryGetProperty("assets", out JsonElement assets)
                || assets.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (JsonElement asset in assets.EnumerateArray())
            {
                if (!asset.TryGetProperty("name", out JsonElement name)
                    || !string.Equals(name.GetString(), assetName, StringComparison.Ordinal)
                    || !asset.TryGetProperty("browser_download_url", out JsonElement url)
                    || url.ValueKind != JsonValueKind.String
                    || !Uri.TryCreate(url.GetString(), UriKind.Absolute, out Uri? downloadUri)
                    || !IsRepositoryReleaseAsset(downloadUri)
                    || !asset.TryGetProperty("digest", out JsonElement digest)
                    || !TryParseSha256(digest.GetString(), out string? sha256)
                    || sha256 is null)
                {
                    continue;
                }

                string releaseName = release.TryGetProperty("name", out JsonElement displayName)
                    && displayName.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(displayName.GetString())
                        ? displayName.GetString()!
                        : tag.GetString()!;
                releases.Add(new UpdateRelease(version, releaseName, downloadUri, sha256));
                break;
            }
        }

        return releases
            .OrderByDescending(release => release.Version, SemVersion.PrecedenceComparer)
            .ToArray();
    }

    internal static bool TryParseTag(string? tag, out SemVersion? version)
    {
        string normalizedTag = tag?.StartsWith('v', StringComparison.OrdinalIgnoreCase) == true
            ? tag[1..]
            : tag ?? string.Empty;

        return SemVersion.TryParse(normalizedTag, out version);
    }

    private static bool IsRepositoryReleaseAsset(Uri uri)
        => uri.Scheme == Uri.UriSchemeHttps
            && string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase)
            && uri.AbsolutePath.StartsWith("/WolfBublitz/EnvManager/releases/download/", StringComparison.Ordinal);

    private static bool TryParseSha256(string? digest, out string? sha256)
    {
        sha256 = null;
        if (digest is null || !digest.StartsWith("sha256:", StringComparison.Ordinal))
        {
            return false;
        }

        string value = digest["sha256:".Length..];
        if (value.Length != 64 || !value.All(Uri.IsHexDigit))
        {
            return false;
        }

        sha256 = value.ToLower(CultureInfo.InvariantCulture);
        return true;
    }
}
