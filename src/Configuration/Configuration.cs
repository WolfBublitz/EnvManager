using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

internal sealed class Configuration
{
    [JsonIgnore]
    public static DirectoryInfo Directory { get; } = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".envmanager"));

    [JsonIgnore]
    public static DirectoryInfo RepositoryDirectory => new(Path.Combine(Directory.FullName, "repository"));

    [JsonIgnore]
    public static DirectoryInfo WorkingDirectory => new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));

    [JsonIgnore]
    public static FileInfo File => new(Path.Combine(Directory.FullName, "config.json"));

    public List<string> Tools { get; set; } = [];

    public HomebrewConfiguration? Homebrew { get; set; }
}
