// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: InitCommand.cs                                                          │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;
using System.CommandLine;

using EnvManager.Configuration;

namespace EnvManager.CommandLine;

/// <summary>
/// The <c>init</c> command creates a brand-new, local-only environment configuration
/// repository (without a remote), useful for working offline before a remote is
/// configured, or for trying EnvManager out. A remote can be attached later with
/// <c>git -C &lt;repository&gt; remote add origin &lt;url&gt;</c> followed by <c>push</c>.
/// </summary>
public sealed class InitCommand : Command
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Constructor                                                              │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Initializes a new instance of the <see cref="InitCommand"/> class.</summary>
    public InitCommand()
        : base("init", "Initializes a new, local-only environment configuration repository.")
    {
        Option<string> environmentOption = new("--environment", "-e")
        {
            Description = "The name of the initial environment (branch) to create. Defaults to the local machine name.",
            DefaultValueFactory = _ => Environment.MachineName,
        };

        this.Add(environmentOption);

        this.SetAction((parseResult, cancellationToken) => CommandExecutor.RunAsync(async () =>
        {
            string environmentName = parseResult.GetValue(environmentOption)!;

            await EnvironmentRepository.InitAsync(environmentName, cancellationToken).ConfigureAwait(false);

            ConsoleReporter.Success($"Initialized a new EnvManager repository with environment '{environmentName}'.");
        }));
    }
}
