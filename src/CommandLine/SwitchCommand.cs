// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: SwitchCommand.cs                                                        │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System.CommandLine;

using EnvManager.Configuration;

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
        Argument<string> environmentNameArgument = new("environment_name")
        {
            Description = "The name of the environment configuration to switch to.",
        };

        Option<bool> createOption = new("--create", "-c")
        {
            Description = "Create the environment if it does not exist yet.",
        };

        this.Add(environmentNameArgument);
        this.Add(createOption);

        this.SetAction((parseResult, cancellationToken) => CommandExecutor.RunAsync(async () =>
        {
            string environmentName = parseResult.GetValue(environmentNameArgument)!;
            bool allowCreate = parseResult.GetValue(createOption);

            EnvironmentRepository repository = EnvironmentRepository.OpenExisting();

            await repository.SwitchAsync(environmentName, allowCreate, cancellationToken).ConfigureAwait(false);

            ConsoleReporter.Success($"Switched to environment '{environmentName}'.");
        }));
    }
}
