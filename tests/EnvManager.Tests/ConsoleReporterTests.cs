// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: ConsoleReporterTests.cs                                                  │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-11-05                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System.Collections.Generic;
using System.Threading.Tasks;

using EnvManager.CommandLine;
using TUnit.Assertions;
using TUnit.Core;

namespace EnvManager.Tests;

public sealed class ConsoleReporterTests
{
    [Test]
    public async Task ConfirmationAcceptsAffirmativeResponses()
    {
        await Assert.That(ConsoleReporter.ParseConfirmationResponse("y", defaultValue: false)).IsTrue();
        await Assert.That(ConsoleReporter.ParseConfirmationResponse(" YES ", defaultValue: false)).IsTrue();
    }

    [Test]
    public async Task ConfirmationUsesDefaultForBlankResponse()
    {
        await Assert.That(ConsoleReporter.ParseConfirmationResponse(string.Empty, defaultValue: true)).IsTrue();
        await Assert.That(ConsoleReporter.ParseConfirmationResponse("  ", defaultValue: false)).IsFalse();
    }

    [Test]
    public async Task ConfirmationRejectsDeclinedInvalidAndEndOfInputResponses()
    {
        await Assert.That(ConsoleReporter.ParseConfirmationResponse("n", defaultValue: true)).IsFalse();
        await Assert.That(ConsoleReporter.ParseConfirmationResponse("unexpected", defaultValue: true)).IsFalse();
        await Assert.That(ConsoleReporter.ParseConfirmationResponse(null, defaultValue: true)).IsFalse();
    }

    [Test]
    public async Task ProgressRunnerProcessesItemsInOrder()
    {
        List<int> processed = [];

        await ConsoleReporter.RunProgressAsync(
            "Updating tools",
            new[] { 1, 2, 3 },
            item => $"Tool {item}",
            item =>
            {
                processed.Add(item);
                return Task.CompletedTask;
            });

        await Assert.That(processed.Count).IsEqualTo(3);
        await Assert.That(processed[0]).IsEqualTo(1);
        await Assert.That(processed[1]).IsEqualTo(2);
        await Assert.That(processed[2]).IsEqualTo(3);
    }

    [Test]
    public async Task ProgressRunnerDoesNotInvokeActionsForAnEmptyList()
    {
        bool actionInvoked = false;

        await ConsoleReporter.RunProgressAsync(
            "Updating tools",
            new int[] { },
            item => $"Tool {item}",
            _ =>
            {
                actionInvoked = true;
                return Task.CompletedTask;
            });

        await Assert.That(actionInvoked).IsFalse();
    }
}
