// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: SwitchCommand.cs                                                        │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;
using System.Collections.Generic;
using System.CommandLine;

using EnvManager.Configuration;
using EnvManager.Exceptions;

using Spectre.Console;

namespace EnvManager.CommandLine;

/// <summary>
/// The <c>switch</c> command switches to a different environment configuration by
/// checking out its corresponding branch, creating it when requested and missing.
/// </summary>
public sealed class SwitchCommand : Command
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Constructor                                                              │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Initializes a new instance of the <see cref="SwitchCommand"/> class.</summary>
    public SwitchCommand()
        : base("switch", "Switches to a different environment configuration.")
    {
        Argument<string?> environmentNameArgument = new("environment_name")
        {
            Description = "The environment to switch to; omit to select interactively.",
            Arity = ArgumentArity.ZeroOrOne,
        };

        Option<bool> createOption = new("--create", "-c")
        {
            Description = "Create the environment if it does not exist yet.",
        };

        this.Add(environmentNameArgument);
        this.Add(createOption);

        this.SetAction((parseResult, cancellationToken) => CommandExecutor.RunAsync(async () =>
        {
            string? environmentName = parseResult.GetValue(environmentNameArgument);
            bool allowCreate = parseResult.GetValue(createOption);

            EnvironmentRepository repository = EnvironmentRepository.OpenExisting();
            if (environmentName is null)
            {
                if (allowCreate)
                {
                    throw new ConfigurationException("Specify an environment name when using --create.");
                }

                IReadOnlyList<string> environments = await repository.ListEnvironmentsAsync(cancellationToken).ConfigureAwait(false);
                if (environments.Count == 0)
                {
                    throw new ConfigurationException("No environments are available.");
                }
                if (!AnsiConsole.Profile.Capabilities.Interactive || Console.IsInputRedirected)
                {
                    throw new ConfigurationException("Specify an environment name when input is not interactive.");
                }

                environmentName = AnsiConsole.Prompt(new SelectionPrompt<string>()
                    .Title("Select an environment")
                    .EnableSearch()
                    .UseConverter(Markup.Escape)
                    .AddChoices(environments));
            }

            try
            {
                await repository.SwitchAsync(environmentName, allowCreate, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                BackupsCommand.ReportCreated(repository.Git);
            }
            ConsoleReporter.Success($"Switched to environment '{environmentName}'.");
        }));
    }
}
