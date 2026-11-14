using System;
using System.CommandLine;
using System.IO;
using System.Linq;

using EnvManager.Configuration;
using EnvManager.Exceptions;
using EnvManager.Git;

namespace EnvManager.CommandLine;

/// <summary>Lists switch backups and restores individual files without overwriting existing files.</summary>
public sealed class BackupsCommand : Command
{
    public BackupsCommand()
        : base("backups", "List and recover files preserved during clone or switch.")
    {
        Command list = new("list", "Lists available file backups.");
        list.SetAction((_, _) => CommandExecutor.RunAsync(() =>
        {
            string root = BackupRoot();
            if (!Directory.Exists(root))
            {
                ConsoleReporter.Info("No switch or clone file backups are available.");
                return System.Threading.Tasks.Task.CompletedTask;
            }

            foreach (string directory in Directory.EnumerateDirectories(root).OrderByDescending(path => path, StringComparer.Ordinal))
            {
                ConsoleReporter.Info($"Backup: {Path.GetFileName(directory)}");
                foreach (string file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
                {
                    ConsoleReporter.Info($"  {Path.GetRelativePath(directory, file)}");
                }
            }
            return System.Threading.Tasks.Task.CompletedTask;
        }));

        Command restore = new("restore", "Restores one backed-up file without overwriting existing files.");
        Argument<string> id = new("backup_id") { Description = "Backup identifier from 'backups list'." };
        Argument<string> file = new("file") { Description = "Path inside the backup, relative to the managed home." };
        Option<string?> destination = new("--to") { Description = "Destination path; defaults to the original location. Existing paths are never overwritten." };
        restore.Add(id);
        restore.Add(file);
        restore.Add(destination);
        restore.SetAction((parseResult, _) => CommandExecutor.RunAsync(() =>
        {
            string backupId = parseResult.GetValue(id)!;
            string requestedFile = parseResult.GetValue(file)!;
            string root = BackupRoot();
            if (!Directory.Exists(root) || Path.GetFileName(backupId) != backupId)
            {
                throw new ConfigurationException("Backup not found. Run 'EnvManager backups list'.");
            }

            string? backup = Directory.EnumerateDirectories(root)
                .FirstOrDefault(path => string.Equals(Path.GetFileName(path), backupId, StringComparison.Ordinal));
            if (backup is null || new DirectoryInfo(backup).LinkTarget is not null)
            {
                throw new ConfigurationException("Backup not found. Run 'EnvManager backups list'.");
            }

            string source = Path.GetFullPath(requestedFile, backup);
            string relative = Path.GetRelativePath(backup, source);
            if (relative == ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) || Path.IsPathRooted(relative))
            {
                throw new ConfigurationException("The file must be inside the selected backup.");
            }
            string? parent = Path.GetDirectoryName(source);
            while (parent is not null && !string.Equals(parent, backup, StringComparison.Ordinal))
            {
                if (new DirectoryInfo(parent).LinkTarget is not null)
                {
                    throw new ConfigurationException("Backup paths cannot traverse symbolic links.");
                }
                parent = Path.GetDirectoryName(parent);
            }

            FileInfo sourceFile = new(source);
            string? linkTarget = sourceFile.LinkTarget;
            if (linkTarget is null && !sourceFile.Exists)
            {
                throw new ConfigurationException($"File '{requestedFile}' was not found in the backup.");
            }

            string requestedDestination = parseResult.GetValue(destination) ?? Path.Combine(EnvManagerPaths.HomeDirectory.FullName, relative);
            string destinationRelative = EnvironmentRepository.ResolveRelativePath(requestedDestination, Environment.CurrentDirectory);
            string target = Path.Combine(EnvManagerPaths.HomeDirectory.FullName, destinationRelative);
            if (File.Exists(target) || Directory.Exists(target) || new FileInfo(target).LinkTarget is not null)
            {
                throw new ConfigurationException($"Destination '{target}' already exists. Use --to to choose another path.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            if (linkTarget is null)
            {
                File.Copy(source, target, overwrite: false);
            }
            else
            {
                File.CreateSymbolicLink(target, linkTarget);
            }
            ConsoleReporter.Success($"Restored '{relative}' to '{target}'. The backup was retained.");
            return System.Threading.Tasks.Task.CompletedTask;
        }));
        this.Add(list);
        this.Add(restore);
    }

    internal static void ReportCreated(GitClient git)
    {
        foreach (string backup in git.BackupsCreated)
        {
            ConsoleReporter.Warning($"Files were preserved in backup '{Path.GetFileName(backup)}':");
            foreach (string file in Directory.EnumerateFiles(backup, "*", SearchOption.AllDirectories))
            {
                ConsoleReporter.Info($"  {Path.GetRelativePath(backup, file)}");
            }
            ConsoleReporter.Info("Run 'EnvManager backups list' and 'EnvManager backups restore <id> <file> [--to <path>]' to recover a file.");
        }
    }

    private static string BackupRoot()
    {
        _ = EnvironmentRepository.OpenExisting();
        return Path.Combine(EnvManagerPaths.GitDirectory.FullName, "overwritten-files");
    }
}
