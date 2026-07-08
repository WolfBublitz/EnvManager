using R3;
using System.Threading.Tasks;

internal sealed class HomebrewPackageManager(Logger logger) : IPackageManager
{
    private readonly Logger logger = logger;

    public async Task UpdateAsync()
    {
        using Process process = new("brew", "update");

        process.InfoOutput.Subscribe(o => logger.Info(["Homebrew"], o));
        process.ErrorOutput.Subscribe(o => logger.Error(["Homebrew"], o));

        await process.RunAsync().ConfigureAwait(false);
    }

    public async Task InstallPackageAsync(string packageName)
    {
        using Process process = new("brew", "install", "--no-ask", packageName);

        process.InfoOutput.Subscribe(o => logger.Info(["Homebrew"], o));
        process.ErrorOutput.Subscribe(o => logger.Error(["Homebrew"], o));

        await process.RunAsync().ConfigureAwait(false);
    }

    public async Task UpdatePackageAsync(string packageName)
    {
        using Process process = new("brew", "upgrade", "--no-ask", packageName);

        process.InfoOutput.Subscribe(o => logger.Info(["Homebrew"], o));
        process.ErrorOutput.Subscribe(o => logger.Error(["Homebrew"], o));

        await process.RunAsync().ConfigureAwait(false);
    }

    public async Task UninstallPackageAsync(string packageName)
    {
        using Process process = new("brew", "uninstall", "--no-ask", packageName);

        process.InfoOutput.Subscribe(o => logger.Info(["Homebrew"], o));
        process.ErrorOutput.Subscribe(o => logger.Error(["Homebrew"], o));

        await process.RunAsync().ConfigureAwait(false);
    }

    public async Task TapAsync(string name)
    {
        using Process process = new("brew", "tap", name);

        process.InfoOutput.Subscribe(o => logger.Info(["Homebrew"], o));
        process.ErrorOutput.Subscribe(o => logger.Error(["Homebrew"], o));

        await process.RunAsync().ConfigureAwait(false);
    }
}
