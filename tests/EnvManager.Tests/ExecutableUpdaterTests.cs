// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: ExecutableUpdaterTests.cs                                                │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-11-05                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using EnvManager.Exceptions;
using EnvManager.Updates;
using Semver;
using TUnit.Assertions;
using TUnit.Core;

namespace EnvManager.Tests;

public sealed class ExecutableUpdaterTests
{
    [Test]
    public async Task DownloadAndReplaceAsyncVerifiesAndReplacesExecutablePreservingUnixMode()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        byte[] replacement = Encoding.UTF8.GetBytes("updated executable");
        string directory = CreateTemporaryDirectory();
        string executablePath = Path.Combine(directory, "EnvManager");
        await File.WriteAllTextAsync(executablePath, "old executable");
        UnixFileMode expectedMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
        File.SetUnixFileMode(executablePath, expectedMode);
        using HttpClient httpClient = CreateClient(replacement);
        ExecutableUpdater updater = new(httpClient);

        try
        {
            await updater.DownloadAndReplaceAsync(CreateRelease(replacement), executablePath, Environment.ProcessId, CancellationToken.None);

            await Assert.That(await File.ReadAllTextAsync(executablePath)).IsEqualTo("updated executable");
            await Assert.That(File.GetUnixFileMode(executablePath)).IsEqualTo(expectedMode);
            await Assert.That(Directory.GetFiles(directory).Length).IsEqualTo(1);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task DownloadAndReplaceAsyncRejectsDigestMismatchAndPreservesCurrentExecutable()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        string directory = CreateTemporaryDirectory();
        string executablePath = Path.Combine(directory, "EnvManager");
        await File.WriteAllTextAsync(executablePath, "old executable");
        byte[] replacement = Encoding.UTF8.GetBytes("tampered executable");
        UpdateRelease release = CreateRelease(replacement) with { Sha256 = new string('0', 64) };
        using HttpClient httpClient = CreateClient(replacement);
        ExecutableUpdater updater = new(httpClient);

        try
        {
            await Assert.That(async () => await updater.DownloadAndReplaceAsync(
                release,
                executablePath,
                Environment.ProcessId,
                CancellationToken.None)).Throws<ConfigurationException>();

            await Assert.That(await File.ReadAllTextAsync(executablePath)).IsEqualTo("old executable");
            await Assert.That(Directory.GetFiles(directory).Length).IsEqualTo(1);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task DownloadAndReplaceAsyncHttpFailurePreservesCurrentExecutable()
    {
        string directory = CreateTemporaryDirectory();
        string executablePath = Path.Combine(directory, "EnvManager");
        await File.WriteAllTextAsync(executablePath, "old executable");
        using HttpClient httpClient = new(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)));
        ExecutableUpdater updater = new(httpClient);

        try
        {
            await Assert.That(async () => await updater.DownloadAndReplaceAsync(
                CreateRelease(Encoding.UTF8.GetBytes("replacement")),
                executablePath,
                Environment.ProcessId,
                CancellationToken.None)).Throws<HttpRequestException>();

            await Assert.That(await File.ReadAllTextAsync(executablePath)).IsEqualTo("old executable");
            await Assert.That(Directory.GetFiles(directory).Length).IsEqualTo(1);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task WindowsReplacementScriptWaitsForCurrentProcessBeforeReplacing()
    {
        string script = ExecutableUpdater.CreateWindowsReplacementScript();

        await Assert.That(script).Contains("Wait-Process -Id $ProcessId");
        await Assert.That(script).Contains("Move-Item -LiteralPath $SourcePath -Destination $DestinationPath -Force");
        await Assert.That(script).Contains("Remove-Item -LiteralPath $ScriptPath -Force");
    }

    private static UpdateRelease CreateRelease(byte[] content)
        => new(
            SemVersion.Parse("2.0.0"),
            "2.0.0",
            new Uri("https://github.com/WolfBublitz/EnvManager/releases/download/v2.0.0/EnvManager"),
            Convert.ToHexStringLower(SHA256.HashData(content)));

    private static HttpClient CreateClient(byte[] content)
        => new(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(content),
        }));

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"EnvManager-update-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responseFactory(request));
    }
}
