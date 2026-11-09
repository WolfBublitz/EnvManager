// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: ShellEnvironmentExporter.cs                                             │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-11-05                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using EnvManager.Configuration;
using EnvManager.Exceptions;

namespace EnvManager.Shells;

/// <summary>Writes safe, shell-specific exports for configured environment variables.</summary>
internal static class ShellEnvironmentExporter
{
    private static readonly Dictionary<string, string> GeneratedFileNames = new(StringComparer.Ordinal)
    {
        ["bash"] = "00-envmanager-variables.sh",
        ["zsh"] = "00-envmanager-variables.sh",
        ["fish"] = "00-envmanager-variables.fish",
        ["powershell"] = "00-envmanager-variables.ps1",
        ["cmd"] = "00-envmanager-variables.cmd",
    };

    /// <summary>Gets the home-relative directories containing supported shell files.</summary>
    public static IEnumerable<string> ShellDirectoryRelativePaths
        => GeneratedFileNames.Keys.Select(shellName => $".EnvManager/{shellName}");

    /// <summary>Validates that a variable name is portable across supported shells.</summary>
    /// <param name="name">The environment variable name.</param>
    /// <exception cref="ConfigurationException">The name is not a portable identifier.</exception>
    public static void ValidateVariableName(string name)
    {
        if (string.IsNullOrEmpty(name) || !IsAsciiLetter(name[0]) && name[0] != '_'
            || name.Skip(1).Any(character => !IsAsciiLetter(character) && character != '_' && !char.IsAsciiDigit(character)))
        {
            throw new ConfigurationException($"'{name}' is not a valid environment variable name. Use ASCII letters, digits, and underscores, beginning with a letter or underscore.");
        }
    }

    /// <summary>Synchronizes generated exports for shells available on the current operating system.</summary>
    /// <param name="configuration">The environment configuration containing shells and variables.</param>
    /// <param name="homeDirectory">The user's home-directory work tree.</param>
    /// <param name="isWindows">Whether the current operating system is Windows.</param>
    public static void Synchronize(EnvironmentConfiguration configuration, DirectoryInfo homeDirectory, bool isWindows)
    {
        HashSet<string> configuredShells = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> renderedExports = new(StringComparer.Ordinal);
        foreach (string shellName in configuration.Shells)
        {
            string normalizedShell = shellName.ToLowerInvariant();
            if (!GeneratedFileNames.ContainsKey(normalizedShell))
            {
                throw new ConfigurationException($"Cannot export variables for unsupported shell '{shellName}'.");
            }

            if (IsAvailableOnPlatform(normalizedShell, isWindows))
            {
                configuredShells.Add(normalizedShell);
            }
        }

        foreach (string shellName in configuredShells)
        {
            renderedExports.Add(shellName, Render(configuration.Variables, shellName));
        }

        foreach ((string shellName, string content) in renderedExports)
        {
            string generatedPath = Path.Combine(homeDirectory.FullName, ".EnvManager", shellName, GeneratedFileNames[shellName]);
            WriteAtomically(generatedPath, content);
        }

        foreach ((string shellName, string fileName) in GeneratedFileNames)
        {
            if (configuredShells.Contains(shellName) || !IsAvailableOnPlatform(shellName, isWindows))
            {
                continue;
            }

            string generatedPath = Path.Combine(homeDirectory.FullName, ".EnvManager", shellName, fileName);
            if (File.Exists(generatedPath))
            {
                File.Delete(generatedPath);
            }
        }
    }

    private static string Render(IReadOnlyDictionary<string, string> variables, string shellName)
    {
        StringBuilder content = new();
        foreach ((string name, string value) in variables.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            ValidateVariableName(name);
            if (value.Contains('\0'))
            {
                throw new ConfigurationException($"Variable '{name}' contains a NUL character and cannot be exported.");
            }

            string line = shellName switch
            {
                "bash" or "zsh" => $"export {name}='{value.Replace("'", "'\"'\"'", StringComparison.Ordinal)}'",
                "fish" => $"set -gx {name} '{value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal)}'",
                "powershell" => $"$env:{name} = '{value.Replace("'", "''", StringComparison.Ordinal)}'",
                "cmd" => RenderCommandPromptVariable(name, value),
                _ => throw new ConfigurationException($"Cannot export variables for unsupported shell '{shellName}'."),
            };

            content.AppendLine(line);
        }

        return content.ToString();
    }

    private static string RenderCommandPromptVariable(string name, string value)
    {
        if (value.IndexOfAny(['"', '%', '!', '\r', '\n']) >= 0)
        {
            throw new ConfigurationException(
                $"Variable '{name}' contains characters that cannot be safely exported to CMD. " +
                "Remove double quotes, percent signs, exclamation marks, or line breaks, or do not configure CMD for this environment.");
        }

        return $"@set \"{name}={value}\"";
    }

    private static void WriteAtomically(string path, string content)
    {
        string? directory = Path.GetDirectoryName(path);
        if (directory is null)
        {
            throw new IOException($"Could not determine the directory for generated shell variables at '{path}'.");
        }

        Directory.CreateDirectory(directory);
        string temporaryPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporaryPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    private static bool IsAsciiLetter(char character)
        => character is >= 'A' and <= 'Z' or >= 'a' and <= 'z';

    private static bool IsAvailableOnPlatform(string shellName, bool isWindows)
        => shellName switch
        {
            "cmd" => isWindows,
            "bash" or "zsh" or "fish" => !isWindows,
            "powershell" => true,
            _ => false,
        };
}
