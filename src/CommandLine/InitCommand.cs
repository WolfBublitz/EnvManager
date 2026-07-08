using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;

internal sealed class InitCommand : BaseCommand
{
    private readonly Argument<string> repositoryUrlArgument = new("repository-url")
    {
        Description = "The URL of the repository to clone",
        Arity = ArgumentArity.ZeroOrOne
    };

    private readonly Argument<string> branchArgument = new("branch")
    {
        Description = "The branch to clone",
        Arity = ArgumentArity.ZeroOrOne
    };

    private readonly Logger logger;
    private readonly ConfigurationService configurationService;

    private readonly IPackageManager packageManager;

    public InitCommand(Logger logger, ConfigurationService configurationService, IPackageManager packageManager) 
        : base("init", "Initialize the environment")
    {
        this.logger = logger;
        this.configurationService = configurationService;
        this.packageManager = packageManager;

        Add(repositoryUrlArgument);
        Add(branchArgument);
    }

    protected sealed override async Task ExecuteAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        Uri? repositoryUrl = parseResult.GetValue(repositoryUrlArgument) switch
        {
            string url => new Uri(url),
            null => null,
        };
        repositoryUrl ??= AnsiConsole.Ask<Uri>("Enter the repository URL:");

        string? branch = parseResult.GetValue(branchArgument);
        branch ??= AnsiConsole.Ask("Enter the branch:", "main");

        await AnsiConsole.Status().StartAsync($"Cloning repository ...", async ctx =>
        {
            GitRepository repository = await GitRepository.CloneAsync(repositoryUrl, branch, Configuration.WorkingDirectory, Configuration.RepositoryDirectory, logger).ConfigureAwait(false);
            
            await repository.SetLocalConfigAsync("status.showUntrackedFiles", "no").ConfigureAwait(false);
            await repository.SetLocalConfigAsync("push.autoSetupRemote", "true").ConfigureAwait(false);
            await repository.RestAsync(hard: true).ConfigureAwait(false);
        }).ConfigureAwait(false);

        await configurationService.ReadAsync().ConfigureAwait(false);

        Configuration configuration = configurationService.Configuration;

        await packageManager.UpdateAsync().ConfigureAwait(false);

        if (packageManager is HomebrewPackageManager homebrewPackageManager && configuration.Homebrew is not null)
        {
            await SetupHomebrewAsync(homebrewPackageManager, configuration.Homebrew).ConfigureAwait(false);
        }

        await InstallToolsAsync(configuration.Tools).ConfigureAwait(false);
    }

    private Task SetupHomebrewAsync(HomebrewPackageManager homebrewPackageManager, HomebrewConfiguration homebrewConfiguration)
    {
        return AnsiConsole.Status().StartAsync($"Setting up Homebrew ...", async ctx =>
        {
            foreach (string cask in homebrewConfiguration.Casks)
            {
                await homebrewPackageManager.TapAsync(cask).ConfigureAwait(false);
            }
        });
    }

    private async Task InstallToolsAsync(IReadOnlyList<string> tools)
    {
        await AnsiConsole.Status().StartAsync($"Installing tools ...", async ctx =>
        {
            foreach (string tool in tools)
            {
                await packageManager.InstallPackageAsync(tool).ConfigureAwait(false);
            }
        }).ConfigureAwait(false);
    }
}