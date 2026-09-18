// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: CloneCommand.cs                                                         │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;
using System.CommandLine;

using EnvManager.Configuration;
using EnvManager.Exceptions;

namespace EnvManager.CommandLine;

/// <summary>
/// The <c>clone</c> command clones an existing EnvManager configuration repository and
/// checks out the requested environment (creating it if it does not yet exist remotely).
/// </summary>
public sealed class CloneCommand : Command
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Constructor                                                              │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Initializes a new instance of the <see cref="CloneCommand"/> class.</summary>
    public CloneCommand()
        : base("clone", "Clones the environment configuration repository.")
    {
        Argument<string> repositoryUrlArgument = new("repository_url")
        {
            Description = "The URL of the git repository to clone.",
        };

        Option<string> environmentOption = new("--environment", "-e")
        {
            Description = "The name of the environment (branch) to check out after cloning. Defaults to the local machine name.",
            DefaultValueFactory = _ => Environment.MachineName,
        };

        this.Add(repositoryUrlArgument);
        this.Add(environmentOption);

        this.SetAction((parseResult, cancellationToken) => CommandExecutor.RunAsync(async () =>
        {
            string repositoryUrlText = parseResult.GetValue(repositoryUrlArgument)!;
            string environmentName = parseResult.GetValue(environmentOption)!;

            if (!Uri.TryCreate(repositoryUrlText, UriKind.Absolute, out Uri? repositoryUrl))
            {
                throw new ConfigurationException($"'{repositoryUrlText}' is not a valid repository URL.");
            }

            ConsoleReporter.Info($"Cloning '{repositoryUrl}'...");

            await EnvironmentRepository.CloneAsync(repositoryUrl, environmentName, cancellationToken).ConfigureAwait(false);

            ConsoleReporter.Success($"Cloned repository and checked out environment '{environmentName}'.");
        }));
    }
}
