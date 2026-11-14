// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: ConfigurationSerializer.cs                                              │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using EnvManager.Exceptions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace EnvManager.Configuration;

/// <summary>
/// Reads and writes <see cref="EnvironmentConfiguration"/> instances to and from the
/// YAML <c>.EnvManager/config.yml</c> file used to persist an environment's state.
/// </summary>
public sealed class ConfigurationSerializer
{
    private const string SchemaReference = "# yaml-language-server: $schema=https://github.com/WolfBublitz/EnvManager/releases/latest/download/config.schema.1.0.0.yaml";
    private const string SchemaReferencePrefix = "# yaml-language-server:";
    private static readonly Regex FormatVersionPattern = new("(?m)^formatVersion\\s*:", RegexOptions.CultureInvariant);

    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ private Field                                                                   │
    // └────────────────────────────────────────────────────────────────────────────────┘

    private readonly ISerializer serializer = new StaticSerializerBuilder(new YamlConfigurationContext())
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    private readonly IDeserializer deserializer = new StaticDeserializerBuilder(new YamlConfigurationContext())
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ public Method                                                                   │
    // └────────────────────────────────────────────────────────────────────────────────┘

    /// <summary>
    /// Loads the environment configuration from the given file, returning an empty,
    /// named configuration when the file does not exist yet.
    /// </summary>
    /// <param name="file">The <c>.EnvManager/config.yml</c> file to read.</param>
    /// <param name="environmentName">The environment name to use when the file is missing.</param>
    public EnvironmentConfiguration Load(FileInfo file, string environmentName)
    {
        if (!file.Exists)
        {
            return new EnvironmentConfiguration { Name = environmentName };
        }

        string yaml = File.ReadAllText(file.FullName);

        if (string.IsNullOrWhiteSpace(yaml))
        {
            return new EnvironmentConfiguration { Name = environmentName };
        }

        EnvironmentConfiguration configuration = this.deserializer.Deserialize<EnvironmentConfiguration>(yaml) ?? new EnvironmentConfiguration { Name = environmentName };

        if (!string.Equals(configuration.FormatVersion, EnvironmentConfiguration.CurrentFormatVersion, StringComparison.Ordinal))
        {
            throw new ConfigurationException(
                $"Configuration format version '{configuration.FormatVersion}' is not supported. " +
                $"This version of EnvManager supports format version '{EnvironmentConfiguration.CurrentFormatVersion}'.");
        }

        return configuration;
    }

    /// <summary>Serializes and writes the given environment configuration to the given file.</summary>
    /// <param name="file">The <c>.EnvManager/config.yml</c> file to write.</param>
    /// <param name="configuration">The configuration to persist.</param>
    /// <returns>True when the schema reference was added or changed; otherwise, false.</returns>
    public bool Save(FileInfo file, EnvironmentConfiguration configuration)
    {
        file.Directory?.Create();

        string? existingSchemaReference = file.Exists
            ? File.ReadLines(file.FullName).FirstOrDefault()
            : null;
        string yaml = SchemaReference + "\n" + this.serializer.Serialize(configuration);
        bool contentUpdated = !file.Exists || !string.Equals(File.ReadAllText(file.FullName), yaml, StringComparison.Ordinal);

        if (contentUpdated)
        {
            File.WriteAllText(file.FullName, yaml);
        }

        return !string.Equals(existingSchemaReference, SchemaReference, StringComparison.Ordinal);
    }

    /// <summary>
    /// Updates schema metadata and adds the current format version to legacy configuration files.
    /// </summary>
    /// <param name="file">The configuration file to migrate.</param>
    /// <param name="configuration">The validated configuration loaded from the file.</param>
    /// <returns>True when the file was changed.</returns>
    public bool Migrate(FileInfo file, EnvironmentConfiguration configuration)
    {
        if (!file.Exists)
        {
            return false;
        }

        string yaml = File.ReadAllText(file.FullName);

        if (string.IsNullOrWhiteSpace(yaml))
        {
            _ = this.Save(file, configuration);
            return true;
        }

        string newline = yaml.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        string[] lines = yaml.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        List<string> migratedLines = [.. lines];

        if (migratedLines[0].StartsWith(SchemaReferencePrefix, StringComparison.Ordinal))
        {
            migratedLines[0] = SchemaReference;
        }
        else
        {
            migratedLines.Insert(0, SchemaReference);
        }

        string normalizedYaml = string.Join("\n", migratedLines);

        if (!FormatVersionPattern.IsMatch(normalizedYaml))
        {
            migratedLines.Insert(1, $"formatVersion: {configuration.FormatVersion}");
        }

        string migratedYaml = string.Join(newline, migratedLines);

        if (string.Equals(yaml, migratedYaml, StringComparison.Ordinal))
        {
            return false;
        }

        File.WriteAllText(file.FullName, migratedYaml);

        return true;
    }
}
