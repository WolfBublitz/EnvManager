using System;
using System.Collections.Generic;

internal sealed record HomebrewConfiguration
{
    public IReadOnlyList<string> Casks { get; init; } = [];
}