// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: VariableSetCommand.cs                                                   │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System.CommandLine;

using EnvManager.Configuration;

namespace EnvManager.CommandLine.Variable;

/// <summary>
/// The <c>variable set</c> command adds a new environment variable, or overwrites the
/// value of an existing one.
/// </summary>
public sealed class VariableSetCommand : Command
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Constructor                                                              │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Initializes a new instance of the <see cref="VariableSetCommand"/> class.</summary>
    public VariableSetCommand()
        : base("set", "Sets a new environment variable or configuration.")
    {
        Argument<string> nameArgument = new("variable_name") { Description = "The name of the environment variable to set." };
        Argument<string> valueArgument = new("variable_value") { Description = "The value of the environment variable to set." };

        this.Add(nameArgument);
        this.Add(valueArgument);

        this.SetAction((parseResult, cancellationToken) => CommandExecutor.RunAsync(async () =>
        {
            string name = parseResult.GetValue(nameArgument)!;
            string value = parseResult.GetValue(valueArgument)!;

            EnvironmentRepository repository = EnvironmentRepository.OpenExisting();
            EnvironmentConfiguration configuration = await repository.LoadConfigurationAsync(cancellationToken).ConfigureAwait(false);

            bool isUpdate = configuration.Variables.ContainsKey(name);

            configuration.Variables[name] = value;

            await repository.SaveConfigurationAsync(configuration, $"Set variable '{name}'.", cancellationToken).ConfigureAwait(false);

            ConsoleReporter.Success(isUpdate ? $"Updated variable '{name}'." : $"Set variable '{name}'.");
        }));
    }
}
