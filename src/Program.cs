// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: Program.cs                                                              │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System.Collections.Frozen;
using System.CommandLine;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using EnvManager.CommandLine;
using Semver;
using Spectre.Console;

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
        PrintHeader();

        EnvManagerRootCommand rootCommand = new();
        ParseResult parseResult = rootCommand.Parse(args);

        return await parseResult.InvokeAsync().ConfigureAwait(false);
    }

    private static void PrintHeader()
    {
        AnsiConsole.Write(new FigletText("EnvManager")
        {
            Color = Color.Blue,
        });

        AnsiConsole.Write(new Rule(ApplicationVersion.ToString())
        {
            Justification = Justify.Left,
        });
    }

    private static SemVersion ApplicationVersion
    {
        get {
            AssemblyInformationalVersionAttribute? attribute = typeof(Program).Assembly.GetCustomAttributes<AssemblyInformationalVersionAttribute>().FirstOrDefault() as AssemblyInformationalVersionAttribute;
        
            SemVersion version = SemVersion.Parse(attribute?.InformationalVersion ?? "0.0.0");

            return version.WithoutMetadata();
        }
    }
}
