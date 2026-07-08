using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using R3;

internal sealed class GitRepository(DirectoryInfo workingDirectory, DirectoryInfo gitDirectory, Logger logger)
{   
    private readonly DirectoryInfo gitDirectory = gitDirectory;


    public DirectoryInfo WorkingDirectory { get; } = workingDirectory;


    public static async Task<GitRepository> CloneAsync(Uri repositoryUrl, string branch, DirectoryInfo workingDirectory, DirectoryInfo repositoryDirectory, Logger logger)
    {
        using Process cloneProcess = new("git", "clone", "--branch", branch, "--bare", repositoryUrl.ToString(), repositoryDirectory.FullName)
        {
            WorkingDirectory = workingDirectory
        };

        cloneProcess.InfoOutput.Subscribe(o => logger.Info(["Git"], o));
        cloneProcess.ErrorOutput.Subscribe(o => logger.Error(["Git"], o));

        try
        {
            await cloneProcess.RunAsync().ConfigureAwait(false);

            return new GitRepository(workingDirectory, repositoryDirectory, logger);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"Failed to clone the repository'.", exception)
                .WithData("RepositoryUrl", repositoryUrl)
                .WithData("Branch", branch)
                .WithData("TargetDirectory", repositoryDirectory.FullName);
        }
    }

    public async Task CheckoutAsync(string branch)
    {
        using Process process = CreateGitProcess("checkout", "-t", $"origin/{branch}");

        process.InfoOutput.Subscribe(line =>
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                logger.Info(["Git"], line);
            }
        });
        process.ErrorOutput.Subscribe(line =>
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                logger.Error(["Git"], line);
            }
        });

        await process.RunAsync().ConfigureAwait(false);

        logger.Success($"Checked out branch '{branch}'.");
    }

    public async Task RestAsync(bool hard = false)
    {
        List<string> arguments = ["reset"];

        arguments.AddIfTrue(hard, "--hard");

        using Process process = CreateGitProcess([.. arguments]);

        process.InfoOutput.Subscribe(line =>
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                logger.Info(["Git"], line);
            }
        });
        process.ErrorOutput.Subscribe(line =>
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                logger.Error(["Git"], line);
            }
        });

        await process.RunAsync().ConfigureAwait(false);

        logger.Success($"Reset repository state.");
    }

    public async Task SetLocalConfigAsync(string key, string value)
    {
        using Process process = CreateGitProcess("config", "--local", key, value);

        process.InfoOutput.Subscribe(line =>
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                logger.Info(["Git"], line);
            }
        });
        process.ErrorOutput.Subscribe(line =>
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                logger.Error(["Git"], line);
            }
        });

        await process.RunAsync().ConfigureAwait(false);

        logger.Success($"Set local Git config '{key}' to '{value}'.");
    }

    public async Task<IEnumerable<FileInfo>> ListFilesAsync()
    {
        using Process process = CreateGitProcess("ls-files");

        logger.Info(process.CommandLine);

        List<FileInfo> files = [];

        process.InfoOutput.Subscribe(line =>
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                string fullPath = Path.Combine(WorkingDirectory.FullName, line);

                files.Add(new FileInfo(fullPath));
            }
        });

        await process.RunAsync().ConfigureAwait(false);

        return files;
    }

    public async Task<IEnumerable<FileInfo>> ListChangedFilesAsync()
    {
        using Process process = CreateGitProcess("ls-files", "--modified");

        logger.Info(process.CommandLine);

        List<FileInfo> files = [];

        process.InfoOutput.Subscribe(line =>
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                string fullPath = Path.Combine(WorkingDirectory.FullName, line);

                files.Add(new FileInfo(fullPath));
            }
        });

        await process.RunAsync().ConfigureAwait(false);

        return files;
    }

    public async Task AddFileAsync(FileInfo filePath)
    {
        using Process process = CreateGitProcess("add", filePath.FullName);

        process.InfoOutput.Subscribe(line =>
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                logger.Info(["Git"], line);
            }
        });
        process.ErrorOutput.Subscribe(line =>
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                logger.Error(["Git"], line);
            }
        });

        await process.RunAsync().ConfigureAwait(false);

        logger.Success($"Added file '{filePath.FullName}' to the repository.");
    }

    public async Task RemoveFileAsync(FileInfo filePath)
    {
        using Process process = CreateGitProcess("rm", filePath.FullName);

        process.InfoOutput.Subscribe(line =>
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                logger.Info(["Git"], line);
            }
        });
        process.ErrorOutput.Subscribe(line =>
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                logger.Error(["Git"], line);
            }
        });

        await process.RunAsync().ConfigureAwait(false);

        logger.Success($"Removed file '{filePath.FullName}' from the repository.");
    }

    public async Task CommitAsync(string message)
    {
        using Process process = CreateGitProcess("commit", "-m", $"\"{message}\"");

        process.InfoOutput.Subscribe(line =>
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                logger.Info(["Git"], line);
            }
        });
        process.ErrorOutput.Subscribe(line =>
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                logger.Error(["Git"], line);
            }
        });

        await process.RunAsync().ConfigureAwait(false);

        logger.Success("Committed changes to the repository.");
    }

    public async Task PushAsync()
    {
        using Process process = CreateGitProcess("push");

        process.InfoOutput.Subscribe(line =>
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                logger.Info(["Git"], line);
            }
        });
        process.ErrorOutput.Subscribe(line =>
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                logger.Error(["Git"], line);
            }
        });

        await process.RunAsync().ConfigureAwait(false);

        logger.Success("Pushed changes to the repository.");
    }

    public async Task PullAsync()
    {
        using Process process = CreateGitProcess("pull");

        process.InfoOutput.Subscribe(line =>
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                logger.Info(["Git"], line);
            }
        });
        process.ErrorOutput.Subscribe(line =>
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                logger.Error(["Git"], line);
            }
        });

        await process.RunAsync().ConfigureAwait(false);

        logger.Success("Pulled changes from the repository.");
    }

    private Process CreateGitProcess(params string[] arguments)
        => new("git", [$"--git-dir={gitDirectory}", $"--work-tree={WorkingDirectory}", ..arguments])
        {
            WorkingDirectory = WorkingDirectory
        };
}
