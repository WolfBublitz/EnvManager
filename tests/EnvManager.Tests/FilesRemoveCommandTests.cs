// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: FilesRemoveCommandTests.cs                                                │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-11-06                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using EnvManager.CommandLine.Files;
using EnvManager.Exceptions;
using TUnit.Assertions;
using TUnit.Core;

namespace EnvManager.Tests;

public sealed class FilesRemoveCommandTests
{
    [Test]
    public async Task SelectFilePromptsWithTrackedFilesAndReturnsTheChosenFile()
    {
        IReadOnlyList<string> files = ["settings", ".config/app"];
        bool promptInvoked = false;

        string selectedFile = FilesRemoveCommand.SelectFile(
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
    public async Task SelectFileRequiresInteractiveInputWhenNoFileNameIsProvided()
    {
        bool promptInvoked = false;

        await Assert.That(() => FilesRemoveCommand.SelectFile(
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
    public async Task ConfirmSelectedFilePromptsForTheSelectedPathAndReturnsTheResponse()
    {
        string? confirmationMessage = null;

        bool confirmed = await FilesRemoveCommand.ConfirmSelectedFileAsync(
            ".config/app",
            message =>
            {
                confirmationMessage = message;
                return Task.FromResult(false);
            });

        await Assert.That(confirmationMessage).IsEqualTo("Stage file '.config/app' for removal?");
        await Assert.That(confirmed).IsFalse();
    }
}
