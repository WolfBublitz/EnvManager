// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: FilesCopyToEnvironmentCommandTests.cs                                    │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-11-10                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using EnvManager.CommandLine.Files;
using EnvManager.Exceptions;
using EnvManager.Git;

using TUnit.Assertions;
using TUnit.Core;

namespace EnvManager.Tests;

public sealed class FilesCopyToEnvironmentCommandTests
{
    [Test]
    public async Task SelectFilePromptsWithTrackedFilesAndReturnsTheChosenFile()
    {
        IReadOnlyList<string> files = ["settings", ".config/app"];
        bool promptInvoked = false;

        string selectedFile = FilesCopyToEnvironmentCommand.SelectFile(
            files,
            interactive: true,
            choices =>
            {
                promptInvoked = true;
                return choices[1];
            });

        await Assert.That(promptInvoked).IsTrue();
        await Assert.That(selectedFile).IsEqualTo(".config/app");
    }

    [Test]
    public async Task SelectFileRequiresInteractiveInput()
    {
        bool promptInvoked = false;

        await Assert.That(() => FilesCopyToEnvironmentCommand.SelectFile(
            ["settings"],
            interactive: false,
            _ =>
            {
                promptInvoked = true;
                return "settings";
            })).Throws<ConfigurationException>();

        await Assert.That(promptInvoked).IsFalse();
    }

    [Test]
    public async Task SelectFileRejectsAnEmptyFileList()
    {
        bool promptInvoked = false;

        await Assert.That(() => FilesCopyToEnvironmentCommand.SelectFile(
            [],
            interactive: true,
            _ =>
            {
                promptInvoked = true;
                return string.Empty;
            })).Throws<ConfigurationException>();

        await Assert.That(promptInvoked).IsFalse();
    }

    [Test]
    public async Task SelectEnvironmentPromptsWithTargetBranchesAndReturnsTheChosenBranch()
    {
        IReadOnlyList<string> environments = ["laptop", "server"];
        bool promptInvoked = false;

        string selectedEnvironment = FilesCopyToEnvironmentCommand.SelectEnvironment(
            environments,
            interactive: true,
            choices =>
            {
                promptInvoked = true;
                return choices[1];
            });

        await Assert.That(promptInvoked).IsTrue();
        await Assert.That(selectedEnvironment).IsEqualTo("server");
    }

    [Test]
    public async Task SelectEnvironmentRequiresInteractiveInput()
    {
        bool promptInvoked = false;

        await Assert.That(() => FilesCopyToEnvironmentCommand.SelectEnvironment(
            ["server"],
            interactive: false,
            _ =>
            {
                promptInvoked = true;
                return "server";
            })).Throws<ConfigurationException>();

        await Assert.That(promptInvoked).IsFalse();
    }

    [Test]
    public async Task CopyFileToBranchCommitsWorkingTreeContentWithoutChangingActiveEnvironment()
    {
        DirectoryInfo root = Directory.CreateTempSubdirectory("EnvManagerFileCopyTests-");
        try
        {
            DirectoryInfo home = Directory.CreateDirectory(Path.Combine(root.FullName, "home"));
            DirectoryInfo gitDirectory = new(Path.Combine(home.FullName, ".EnvManager", "repo"));
            GitClient git = new(home, gitDirectory);
            await git.InitAsync("source");
            await git.RunAsync(["config", "user.name", "EnvManager Tests"]);
            await git.RunAsync(["config", "user.email", "envmanager-tests@example.invalid"]);

            const string relativePath = ".config/my settings";
            string filePath = Path.Combine(home.FullName, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, "source version");
            await git.AddAsync(relativePath);
            await git.CommitAsync("Add source file");

            await git.RunAsync(["branch", "target"]);
            await git.RunAsync(["checkout", "target"]);
            File.WriteAllText(filePath, "existing target version");
            await git.AddAsync(relativePath);
            await git.CommitAsync("Add target version");
            await git.RunAsync(["checkout", "source"]);

            string sourceCommit = (await git.RunAsync(["rev-parse", "HEAD"])).StandardOutput;
            string targetCommit = (await git.RunAsync(["rev-parse", "refs/heads/target"])).StandardOutput;
            File.WriteAllText(filePath, "current working-tree version");

            bool copied = await git.CopyFileToBranchAsync(relativePath, "target");

            await Assert.That(copied).IsTrue();
            await Assert.That(await git.GetCurrentBranchAsync()).IsEqualTo("source");
            await Assert.That((await git.RunAsync(["rev-parse", "HEAD"])).StandardOutput).IsEqualTo(sourceCommit);
            await Assert.That(await File.ReadAllTextAsync(filePath)).IsEqualTo("current working-tree version");
            await Assert.That((await git.RunAsync(["diff", "--cached", "--name-only"])).StandardOutput).IsEqualTo(string.Empty);
            await Assert.That((await git.RunAsync(["show", $"refs/heads/target:{relativePath}"])).StandardOutput)
                .IsEqualTo("current working-tree version");
            await Assert.That((await git.RunAsync(["rev-parse", "refs/heads/target^"])).StandardOutput).IsEqualTo(targetCommit);

            string copiedCommit = (await git.RunAsync(["rev-parse", "refs/heads/target"])).StandardOutput;
            bool copiedAgain = await git.CopyFileToBranchAsync(relativePath, "target");

            await Assert.That(copiedAgain).IsFalse();
            await Assert.That((await git.RunAsync(["rev-parse", "refs/heads/target"])).StandardOutput).IsEqualTo(copiedCommit);
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }
}
