using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

internal sealed class ConfigurationService
{
    public Configuration Configuration { get; private set; } = new();

    public Task InitializeAsync()
    {
        if (!Configuration.Directory.Exists)
        {
            Configuration.Directory.Create();
        }

        if (!Configuration.RepositoryDirectory.Exists)
        {
            Configuration.RepositoryDirectory.Create();
        }

        return Task.CompletedTask; 
    }

    public async Task ReadAsync()
    {
        if (Configuration.File.Exists)
        {
            string json = await File.ReadAllTextAsync(Configuration.File.FullName).ConfigureAwait(false);

            Configuration = JsonSerializer.Deserialize(json, ConfigurationContext.Default.Configuration) ?? Configuration;
        }
    }

    public Task WriteAsync()
    {
        string json = JsonSerializer.Serialize(Configuration, ConfigurationContext.Default.Configuration);

        return File.WriteAllTextAsync(Configuration.File.FullName, json);
    }
}