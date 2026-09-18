// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: VariableUpdateCommand.cs                                               │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System.CommandLine;

using EnvManager.Configuration;
using EnvManager.Exceptions;

namespace EnvManager.CommandLine.Variable;

/// <summary>
/// The <c>variable update</c> command updates the value of an existing environment
/// variable. Unlike <c>variable set</c>, it fails when the variable does not exist yet.
/// </summary>
public sealed class VariableUpdateCommand : Command
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Constructor                                                              │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Initializes a new instance of the <see cref="VariableUpdateCommand"/> class.</summary>
    public VariableUpdateCommand()
        : base("update", "Updates an existing environment variable or configuration.")
    {
        Argument<string> nameArgument = new("variable_name") { Description = "The name of the environment variable to update." };
        Argument<string> valueArgument = new("variable_value") { Description = "The new value of the environment variable." };

        this.Add(nameArgument);
        this.Add(valueArgument);

        this.SetAction((parseResult, cancellationToken) => CommandExecutor.RunAsync(async () =>
        {
            string name = parseResult.GetValue(nameArgument)!;
            string value = parseResult.GetValue(valueArgument)!;

            EnvironmentRepository repository = EnvironmentRepository.OpenExisting();
            EnvironmentConfiguration configuration = await repository.LoadConfigurationAsync(cancellationToken).ConfigureAwait(false);

            if (!configuration.Variables.ContainsKey(name))
            {
                throw new ConfigurationException($"Variable '{name}' does not exist in the current environment. Use 'variable set' to create it.");
            }

            configuration.Variables[name] = value;

            await repository.SaveConfigurationAsync(configuration, $"Update variable '{name}'.", cancellationToken).ConfigureAwait(false);

            ConsoleReporter.Success($"Updated variable '{name}'.");
        }));
    }
}
