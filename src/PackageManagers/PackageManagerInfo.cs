using System;

internal sealed class PackageManagerInfo
{
    public required string Name { get; init; }

    public required string Description { get; init; }

    public string? Version { get; init; }

    public required Type Type { get; init; }
}
