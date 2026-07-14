using System.Threading.Tasks;
using R3;

internal sealed class ScoopPackageManager(Logger logger) : IPackageManager
{
    private readonly Logger logger = logger;

    public Task InstallPackageAsync(string packageName)
    {
        using Process process = new("scoop", $"install {packageName}");

        process.InfoOutput.Subscribe(o => logger.Info(["Scoop"], o));
        process.ErrorOutput.Subscribe(o => logger.Error(["Scoop"], o));

        return process.RunAsync();
    }

    public Task UninstallPackageAsync(string packageName)
    {
        using Process process = new("scoop", $"uninstall {packageName}");

        process.InfoOutput.Subscribe(o => logger.Info(["Scoop"], o));
        process.ErrorOutput.Subscribe(o => logger.Error(["Scoop"], o));

        return process.RunAsync();
    }

    public Task UpdateAsync()
    {
        using Process process = new("scoop", "update");

        process.InfoOutput.Subscribe(o => logger.Info(["Scoop"], o));
        process.ErrorOutput.Subscribe(o => logger.Error(["Scoop"], o));

        return process.RunAsync();
    }

    public Task UpdatePackageAsync(string packageName)
    {
        using Process process = new("scoop", $"update {packageName}");

        process.InfoOutput.Subscribe(o => logger.Info(["Scoop"], o));
        process.ErrorOutput.Subscribe(o => logger.Error(["Scoop"], o));

        return process.RunAsync();  
    }

    public static async Task<bool> IsAvailableAsync()
    {
        using Process process = new("scoop", "--version");

        try
        {
            await process.RunAsync().ConfigureAwait(false);

            return true;
        }
        catch
        {
            return false;
        }
    }
}