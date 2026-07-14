using System;
using System.Text.Json.Serialization;

internal sealed class PackageManagerConfiguration
{
    [JsonPropertyName("homebrew")]
    public HomebrewConfiguration? HomebrewConfiguration { get; init; }
}
