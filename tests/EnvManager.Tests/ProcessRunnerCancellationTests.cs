// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: ProcessRunnerCancellationTests.cs                                        │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-11-05                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;
using System.Threading;
using System.Threading.Tasks;

using EnvManager.PackageManagers;
using TUnit.Assertions;
using TUnit.Core;

namespace EnvManager.Tests;

public sealed class ProcessRunnerCancellationTests
{
    [Test]
    public async Task RunAsyncTerminatesTheProcessTreeWhenCancelled()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using CancellationTokenSource cancellation = new();
        Task<(int ExitCode, string StandardOutput, string StandardError)> operation =
            ProcessRunner.RunAsync("/bin/sh", ["-c", "sleep 30"], cancellation.Token);
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(100));

        await Assert.That(async () => await operation).Throws<OperationCanceledException>();
    }

    [Test]
    public async Task IsAvailableAsyncPropagatesCancellationInsteadOfReportingUnavailable()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.That(async () => await ProcessRunner.IsAvailableAsync("sh", ["--version"], cancellation.Token))
            .Throws<OperationCanceledException>();
    }
}
