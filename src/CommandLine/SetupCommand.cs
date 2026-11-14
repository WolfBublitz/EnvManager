using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;

using EnvManager.Configuration;
using EnvManager.Exceptions;
using EnvManager.PackageManagers;

using Spectre.Console;

namespace EnvManager.CommandLine;

/// <summary>Guides a new user through repository and shell setup.</summary>
public sealed class SetupCommand : Command
{
    public SetupCommand()
        : base("setup", "Interactively create or clone an environment.")
    {
        this.SetAction((_, cancellationToken) => CommandExecutor.RunAsync(async () =>
        {
            if (!AnsiConsole.Profile.Capabilities.Interactive || Console.IsInputRedirected)
            {
                throw new ConfigurationException("Setup requires an interactive terminal. Use 'init' or 'clone' for scripts.");
            }
            if (EnvironmentRepository.IsInitialized)
            {
                throw new ConfigurationException("An environment is already configured. Run 'EnvManager status' to inspect it.");
            }

            string choice = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title("How would you like to start?")
                .AddChoices("Create a local environment", "Clone an existing repository"));

            EnvironmentRepository repository;
            if (choice == "Create a local environment")
            {
                string name = AnsiConsole.Prompt(new TextPrompt<string>("Environment name:")
                    .DefaultValue(Environment.MachineName));
                repository = await EnvironmentRepository.InitAsync(name, cancellationToken).ConfigureAwait(false);
                ConsoleReporter.Success($"Created environment '{name}' locally.");

                if (await ConsoleReporter.ConfirmAsync("Connect a Git remote now?", defaultValue: false, cancellationToken).ConfigureAwait(false))
                {
                    string remote = AnsiConsole.Ask<string>("Git remote URL or path:");
                    await repository.Git.SetRemoteAsync(remote, cancellationToken).ConfigureAwait(false);
                    ConsoleReporter.Success("Configured origin. Run 'EnvManager push' when ready to publish.");
                }
            }
            else
            {
                string remote = AnsiConsole.Ask<string>("Repository URL:");
                if (!Uri.TryCreate(remote, UriKind.Absolute, out Uri? uri))
                {
                    throw new ConfigurationException("Provide an absolute repository URL.");
                }

                (int exitCode, string output, string error) = await ProcessRunner.RunAsync(
                    "git", ["ls-remote", "--heads", uri.ToString()], cancellationToken).ConfigureAwait(false);
                if (exitCode != 0)
                {
                    throw new GitCommandException("ls-remote --heads", exitCode, output, error);
                }

                List<string> branches = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(line => line.Split('\t').Last())
                    .Where(reference => reference.StartsWith("refs/heads/", StringComparison.Ordinal))
                    .Select(reference => reference["refs/heads/".Length..])
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToList();
                const string CreateNew = "Create a new empty environment";
                branches.Add(CreateNew);
                string selected = AnsiConsole.Prompt(new SelectionPrompt<string>()
                    .Title("Choose an environment")
                    .EnableSearch()
                    .UseConverter(Markup.Escape)
                    .AddChoices(branches));
                string name = selected == CreateNew
                    ? AnsiConsole.Prompt(new TextPrompt<string>("New environment name:").DefaultValue(Environment.MachineName))
                    : selected;

                ConsoleReporter.Warning("Cloning can move existing files into an EnvManager backup and replace them with repository files.");
                if (!await ConsoleReporter.ConfirmAsync("Clone and check out this environment?", defaultValue: false, cancellationToken).ConfigureAwait(false))
                {
                    ConsoleReporter.Info("Setup cancelled; no repository was created.");
                    return;
                }

                repository = await EnvironmentRepository.CloneAsync(uri, name, cancellationToken).ConfigureAwait(false);
                BackupsCommand.ReportCreated(repository.Git);
                ConsoleReporter.Success($"Cloned environment '{name}'.");
            }

            ConsoleReporter.Warning("Shell integration loads scripts from the environment repository at shell startup. Review them before trusting the repository.");
            if (await ConsoleReporter.ConfirmAsync("Trust this repository and enable a shell now?", defaultValue: false, cancellationToken).ConfigureAwait(false))
            {
                string[] shells = OperatingSystem.IsWindows()
                    ? ["powershell", "cmd"]
                    : ["bash", "zsh", "fish", "powershell"];
                string shell = AnsiConsole.Prompt(new SelectionPrompt<string>().Title("Choose a shell").AddChoices(shells));
                int result = await new EnvManagerRootCommand().Parse(["shells", "add", shell, "--yes"]).InvokeAsync().ConfigureAwait(false);
                if (result != 0)
                {
                    throw new ConfigurationException("Shell integration failed; the environment is still available locally.");
                }
            }

            ConsoleReporter.Info("Run 'EnvManager status' to inspect the environment. Publishing requires 'EnvManager push'.");
        }));
    }
}
