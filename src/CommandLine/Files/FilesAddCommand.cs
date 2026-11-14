// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: FilesAddCommand.cs                                                     │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;
using System.CommandLine;
using System.IO;

using EnvManager.Configuration;

namespace EnvManager.CommandLine.Files;

/// <summary>
/// The <c>files add</c> command adds an existing file from the home directory to the
/// environment repository, so that it is tracked and versioned as part of the
/// currently checked-out environment.
/// </summary>
public sealed class FilesAddCommand : Command
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Constructor                                                              │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>Initializes a new instance of the <see cref="FilesAddCommand"/> class.</summary>
    public FilesAddCommand()
        : base("add", "Adds a new file or configuration to the repository.")
    {
        Argument<string[]> fileNameArgument = new("file_name")
        {
            Description = "Files to add, relative to the current working directory (or absolute paths inside the home directory).",
            Arity = ArgumentArity.OneOrMore,
        };

        this.Add(fileNameArgument);

        this.SetAction((parseResult, cancellationToken) => CommandExecutor.RunAsync(async () =>
        {
            string[] fileNames = parseResult.GetValue(fileNameArgument)!;

            EnvironmentRepository repository = EnvironmentRepository.OpenExisting();

            foreach (string fileName in fileNames)
            {
                string path = EnvironmentRepository.ResolveRelativePath(fileName, Environment.CurrentDirectory);
                string fullPath = Path.Combine(EnvManagerPaths.HomeDirectory.FullName, path);
                if (!File.Exists(fullPath))
                {
                    throw new EnvManager.Exceptions.ConfigurationException($"File '{fileName}' was not found at '{fullPath}'.");
                }
            }

            foreach (string fileName in fileNames)
            {
                string path = EnvironmentRepository.ResolveRelativePath(fileName, Environment.CurrentDirectory);
                if (MayContainSecrets(path))
                {
                    ConsoleReporter.Warning($"'{path}' may contain secrets. Tracked files can be committed and published; review it before pushing.");
                }
                await repository.AddFileAsync(fileName, cancellationToken).ConfigureAwait(false);
                ConsoleReporter.Success($"Staged '{path}' for addition.");
            }

            ConsoleReporter.Info("Run 'EnvManager push' to commit and publish staged files.");
        }));
    }

    internal static bool MayContainSecrets(string path)
    {
        string normalized = $"/{path.Replace('\\', '/')}";
        string name = Path.GetFileName(path);
        return normalized.Contains("/.ssh/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/.aws/", StringComparison.OrdinalIgnoreCase)
            || name.Equals(".env", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith(".env.", StringComparison.OrdinalIgnoreCase)
            || name.Equals("credentials", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".pem", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".key", StringComparison.OrdinalIgnoreCase);
    }
}
