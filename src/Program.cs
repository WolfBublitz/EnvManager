// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: Program.cs                                                              │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System.CommandLine;
using System.Threading.Tasks;

using EnvManager.CommandLine;

namespace EnvManager;

/// <summary>The EnvManager CLI entry point.</summary>
public static class Program
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Method                                                                   │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Parses the command line arguments and invokes the matching command.</summary>
    /// <param name="args">The raw command line arguments.</param>
    /// <returns>The process exit code.</returns>
    public static async Task<int> Main(string[] args)
    {
        EnvManagerRootCommand rootCommand = new();
        ParseResult parseResult = rootCommand.Parse(args);

        return await parseResult.InvokeAsync().ConfigureAwait(false);
    }
}
