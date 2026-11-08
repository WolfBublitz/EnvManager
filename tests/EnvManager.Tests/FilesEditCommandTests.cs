// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: FilesEditCommandTests.cs                                                 │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-11-08                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

using EnvManager.CommandLine.Files;
using EnvManager.Exceptions;

using TUnit.Assertions;
using TUnit.Core;

namespace EnvManager.Tests;

public sealed class FilesEditCommandTests
{
    [Test]
    public async Task SelectFilePromptsWithTrackedFilesAndReturnsTheChosenFile()
    {
        IReadOnlyList<string> files = ["settings", ".config/app"];
        bool promptInvoked = false;

        string selectedFile = FilesEditCommand.SelectFile(
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

        await Assert.That(() => FilesEditCommand.SelectFile(
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

        await Assert.That(() => FilesEditCommand.SelectFile(
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
    public async Task CreateEditorStartInfoPassesFilePathAsOneArgument()
    {
        string filePath = "/home/user/.config/my settings";

        ProcessStartInfo startInfo = FilesEditCommand.CreateEditorStartInfo("my editor", filePath);

        await Assert.That(startInfo.FileName).IsEqualTo("my editor");
        await Assert.That(startInfo.ArgumentList.Count).IsEqualTo(1);
        await Assert.That(startInfo.ArgumentList[0]).IsEqualTo(filePath);
        await Assert.That(startInfo.UseShellExecute).IsFalse();
    }

    [Test]
    public async Task CreateEditorStartInfoRejectsAnEmptyEditor()
    {
        await Assert.That(() => FilesEditCommand.CreateEditorStartInfo("  ", "/home/user/file"))
            .Throws<ConfigurationException>();
    }

}
