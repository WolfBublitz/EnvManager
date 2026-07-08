using System.Threading.Tasks;
using R3;

internal sealed class AptPackageManager(Logger logger) : IPackageManager
{
    private readonly Logger logger = logger;

    public async Task UpdateAsync()
    {
        using Process process = new("apt-get", "update");

        process.InfoOutput.Subscribe(o => logger.Info(["Apt"], o));
        process.ErrorOutput.Subscribe(o => logger.Error(["Apt"], o));

        await process.RunAsync().ConfigureAwait(false);
    }

    public async Task InstallPackageAsync(string packageName)
    {
        using Process process = new("apt-get", "install", "-y", packageName);

        process.InfoOutput.Subscribe(o => logger.Info(["Apt"], o));
        process.ErrorOutput.Subscribe(o => logger.Error(["Apt"], o));

        await process.RunAsync().ConfigureAwait(false);
    }

    public async Task UpdatePackageAsync(string packageName)
    {
        using Process process = new("apt-get", "upgrade", "-y", packageName);

        process.InfoOutput.Subscribe(o => logger.Info(["Apt"], o));
        process.ErrorOutput.Subscribe(o => logger.Error(["Apt"], o));

        await process.RunAsync().ConfigureAwait(false);
    }

    public async Task UninstallPackageAsync(string packageName)
    {
        using Process process = new("apt-get", "remove", "-y", packageName);

        process.InfoOutput.Subscribe(o => logger.Info(["Apt"], o));
        process.ErrorOutput.Subscribe(o => logger.Error(["Apt"], o));

        await process.RunAsync().ConfigureAwait(false);
    }
}