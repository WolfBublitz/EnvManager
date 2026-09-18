// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: VariableRemoveCommand.cs                                                │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System.CommandLine;

using EnvManager.Configuration;
using EnvManager.Exceptions;

namespace EnvManager.CommandLine.Variable;

/// <summary>
/// The <c>variable remove</c> command removes an existing environment variable.
/// </summary>
public sealed class VariableRemoveCommand : Command
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Constructor                                                              │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Initializes a new instance of the <see cref="VariableRemoveCommand"/> class.</summary>
    public VariableRemoveCommand()
        : base("remove", "Removes an existing environment variable or configuration.")
    {
        Argument<string> nameArgument = new("variable_name") { Description = "The name of the environment variable to remove." };

        this.Add(nameArgument);

        this.SetAction((parseResult, cancellationToken) => CommandExecutor.RunAsync(async () =>
        {
            string name = parseResult.GetValue(nameArgument)!;

            EnvironmentRepository repository = EnvironmentRepository.OpenExisting();
            EnvironmentConfiguration configuration = await repository.LoadConfigurationAsync(cancellationToken).ConfigureAwait(false);

            if (!configuration.Variables.Remove(name))
            {
                throw new ConfigurationException($"Variable '{name}' does not exist in the current environment.");
            }

            await repository.SaveConfigurationAsync(configuration, $"Remove variable '{name}'.", cancellationToken).ConfigureAwait(false);

            ConsoleReporter.Success($"Removed variable '{name}'.");
        }));
    }
}
