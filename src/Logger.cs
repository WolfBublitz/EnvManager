using System;
using System.Collections.Generic;
using System.Linq;
using R3;
using Spectre.Console;

internal sealed class Logger
{
    public void Info(string message)
    {
        AnsiConsole.MarkupLine($"[green][[INFO]][/]: {message}");
    }

    public void Info(string[] scopes, string message)
    {
        AnsiConsole.MarkupLine($"[green][[INFO]][/] {GetScopePrefix(scopes)}: {message}");
    }

    public void Warning(string message)
    {
        AnsiConsole.MarkupLine($"[yellow][[WARNING]][/]: {message}");
    }

    public void Warning(string[] scopes, string message)
    {
        AnsiConsole.MarkupLine($"[yellow][[WARNING]][/] {GetScopePrefix(scopes)}: {message}");
    }

    public void Error(string message)
    {
        AnsiConsole.MarkupLine($"[red][[ERROR]][/]: {message}");
    }

    public void Error(string[] scopes, string message)
    {
        AnsiConsole.MarkupLine($"[red][[ERROR]][/] {GetScopePrefix(scopes)}: {message}");
    }

    public void Success(string message)
    {
        AnsiConsole.MarkupLine($"[white on green][[SUCC]][/]: {message}");
    }

    public void Success(string[] scopes, string message)
    {
        AnsiConsole.MarkupLine($"[white on green][[SUCC]][/] {GetScopePrefix(scopes)}: {message}");
    }

    private string GetScopePrefix(string[] scopes)
    {
        if (scopes.Length == 0)
        {
            return string.Empty;
        }

        return scopes.Select(s => $"[blue][[{s}]][/]").Aggregate((a, b) => $"{a} {b}") + " ";
    }
}