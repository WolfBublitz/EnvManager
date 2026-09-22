// ┌────────────────────────────────────────────────────────────────────────────────┐
// │ File: ConfigurationSerializer.cs                                              │
// │ Author: EnvManager Contributors                                                │
// │ Created: 2026-09-18                                                            │
// └────────────────────────────────────────────────────────────────────────────────┘

using System.IO;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace EnvManager.Configuration;

/// <summary>
/// Reads and writes <see cref="EnvironmentConfiguration"/> instances to and from the
/// YAML <c>environment.yaml</c> file used to persist an environment's state.
/// </summary>
public sealed class ConfigurationSerializer
{
    // ┌────────────────────────────────────────────────────────────────────────────────┐
    // │ private Field                                                                   │
    // └────────────────────────────────────────────────────────────────────────────────┘

    private readonly ISerializer serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    private readonly IDeserializer deserializer = new DeserializerBuilder()
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
    /// <param name="file">The <c>environment.yaml</c> file to read.</param>
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

        return this.deserializer.Deserialize<EnvironmentConfiguration>(yaml) ?? new EnvironmentConfiguration { Name = environmentName };
    }

    /// <summary>Serializes and writes the given environment configuration to the given file.</summary>
    /// <param name="file">The <c>environment.yaml</c> file to write.</param>
    /// <param name="configuration">The configuration to persist.</param>
    public void Save(FileInfo file, EnvironmentConfiguration configuration)
    {
        file.Directory?.Create();

        string yaml = this.serializer.Serialize(configuration);

        File.WriteAllText(file.FullName, yaml);
    }
}
