// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: FilesEditCommandTests.cs                                                 │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-11-06                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using EnvManager.CommandLine.Files;
using EnvManager.Configuration;

using TUnit.Assertions;
using TUnit.Core;

namespace EnvManager.Tests;

public sealed class FilesEditCommandTests
{
    [Test]
    public async Task EditorSettingRoundTripsUnderSettingsEditor()
    {
        DirectoryInfo root = new(Path.Combine(Path.GetTempPath(), $"EnvManager-FilesEditTests-{Guid.NewGuid():N}"));
        root.Create();

        try
        {
            FileInfo configFile = new(Path.Combine(root.FullName, "config.yml"));
            ConfigurationSerializer serializer = new();
            EnvironmentConfiguration configuration = new() { Name = "main" };
            configuration.Settings["editor"] = "code --wait";

            serializer.Save(configFile, configuration);

            await Assert.That(File.ReadAllText(configFile.FullName)).Contains("settings:");
            await Assert.That(File.ReadAllText(configFile.FullName)).Contains("editor: code --wait");
            EnvironmentConfiguration loaded = serializer.Load(configFile, "main");
            await Assert.That(loaded.Settings["editor"]).IsEqualTo("code --wait");
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    [Test]
    public async Task CreateEditorStartInfoPassesArgumentsAndSelectedFile()
    {
        ProcessStartInfo startInfo = FilesEditCommand.CreateEditorStartInfo(
            "code --wait",
            "/home/test/.config/settings",
            "/home/test");

        await Assert.That(startInfo.FileName).IsEqualTo("code");
        await Assert.That(startInfo.ArgumentList.Count).IsEqualTo(2);
        await Assert.That(startInfo.ArgumentList[0]).IsEqualTo("--wait");
        await Assert.That(startInfo.ArgumentList[1]).IsEqualTo("/home/test/.config/settings");
        await Assert.That(startInfo.WorkingDirectory).IsEqualTo("/home/test");
        await Assert.That(startInfo.UseShellExecute).IsFalse();
    }

    [Test]
    public async Task CreateEditorStartInfoSupportsQuotedExecutablePaths()
    {
        string executable = Path.Combine(Path.GetTempPath(), "Text Editor", "editor");
        string editor = $"\"{executable}\" --wait";

        ProcessStartInfo startInfo = FilesEditCommand.CreateEditorStartInfo(editor, "/home/test/file", "/home/test");

        await Assert.That(startInfo.FileName).IsEqualTo(executable);
        await Assert.That(startInfo.ArgumentList.Count).IsEqualTo(2);
        await Assert.That(startInfo.ArgumentList[0]).IsEqualTo("--wait");
        await Assert.That(startInfo.ArgumentList[1]).IsEqualTo("/home/test/file");
    }

    [Test]
    public async Task EditPromptsForEditorAndOpensTheSelectedTrackedFile()
    {
        DirectoryInfo root = new(Path.Combine(Path.GetTempPath(), $"EnvManager-FilesEditTests-{Guid.NewGuid():N}"));
        root.Create();

        try
        {
            string home = Directory.CreateDirectory(Path.Combine(root.FullName, "home")).FullName;
            await RunAsync(home, [], "init", "--environment", "main");

            File.WriteAllText(Path.Combine(home, "first"), "first file");
            File.WriteAllText(Path.Combine(home, "selected"), "selected file");
            await RunAsync(home, [], "files", "add", "first");
            await RunAsync(home, [], "files", "add", "selected");

            string outputFile = Path.Combine(root.FullName, "opened-file.txt");
            string editorCommand = CreateRecordingEditor(root.FullName, outputFile);

            string firstRunOutput = await RunAsync(home, $"2{Environment.NewLine}{editorCommand}{Environment.NewLine}", "files", "edit");

            await Assert.That(firstRunOutput).Contains("settings.editor");
            await Assert.That(File.ReadAllLines(outputFile)).IsEquivalentTo([Path.Combine(home, "selected")]);
            await Assert.That(File.ReadAllText(Path.Combine(home, ".EnvManager", "config.yml"))).Contains("editor:");

            await RunAsync(home, $"2{Environment.NewLine}", "files", "edit");

            await Assert.That(File.ReadAllLines(outputFile)).IsEquivalentTo(
                [Path.Combine(home, "selected"), Path.Combine(home, "selected")]);
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    private static string CreateRecordingEditor(string root, string outputFile)
    {
        if (OperatingSystem.IsWindows())
        {
            string scriptPath = Path.Combine(root, "editor.cmd");
            File.WriteAllText(scriptPath, $"@echo off\r\n>> \"{outputFile}\" echo %~1\r\n");
            return $"cmd.exe /c \"{scriptPath}\"";
        }

        string editorPath = Path.Combine(root, "editor");
        File.WriteAllText(editorPath, $"#!/bin/sh\nprintf '%s\\n' \"$1\" >> {QuoteShellArgument(outputFile)}\n");
        File.SetUnixFileMode(
            editorPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return $"\"{editorPath}\"";
    }

    private static string QuoteShellArgument(string value)
        => $"'{value.Replace("'", "'\"'\"'", StringComparison.Ordinal)}'";

    private static async Task<string> RunAsync(string home, string[] inputLines, params string[] arguments)
        => await RunAsync(home, string.Join(Environment.NewLine, inputLines), arguments);

    private static async Task<string> RunAsync(string home, string input, params string[] arguments)
    {
        ProcessStartInfo startInfo = new("dotnet")
        {
            WorkingDirectory = home,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.Environment["ENVMANAGER_HOME"] = home;
        startInfo.Environment["GIT_AUTHOR_NAME"] = "Test User";
        startInfo.Environment["GIT_AUTHOR_EMAIL"] = "test@example.com";
        startInfo.Environment["GIT_COMMITTER_NAME"] = "Test User";
        startInfo.Environment["GIT_COMMITTER_EMAIL"] = "test@example.com";
        startInfo.ArgumentList.Add(typeof(Program).Assembly.Location);
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start EnvManager.");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        await process.StandardInput.WriteAsync(input);
        process.StandardInput.Close();
        await process.WaitForExitAsync(CancellationToken.None);
        string result = await output;
        string errorOutput = await error;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"EnvManager {string.Join(' ', arguments)} failed: {result}{errorOutput}");
        }

        return result.TrimEnd();
    }
}
