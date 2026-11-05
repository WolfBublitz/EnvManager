// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: GitClientCancellationTests.cs                                            │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-11-05                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using EnvManager.Git;
using TUnit.Assertions;
using TUnit.Core;

namespace EnvManager.Tests;

public sealed class GitClientCancellationTests
{
    [Test]
    public async Task RunAsyncHonorsCancellationBeforeStartingGit()
    {
        DirectoryInfo root = new(Path.Combine(Path.GetTempPath(), $"EnvManager-GitCancellation-{Guid.NewGuid():N}"));
        root.Create();
        try
        {
            DirectoryInfo home = root.CreateSubdirectory("home");
            DirectoryInfo gitDirectory = new(Path.Combine(home.FullName, ".EnvManager", "repo"));
            GitClient client = new(home, gitDirectory);
            await client.InitAsync("main");

            using CancellationTokenSource cancellation = new();
            cancellation.Cancel();
            Task<GitCommandResult> operation = client.RunAsync(["--version"], cancellation.Token);

            await Assert.That(async () => await operation).Throws<OperationCanceledException>();
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }
}
