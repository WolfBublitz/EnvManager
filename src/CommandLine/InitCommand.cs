using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
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

    private readonly PackageManagerFactory packageManagerFactory;

    private readonly CancellationService cancellationServices;

    public InitCommand(
        Logger logger, 
        PackageManagerFactory packageManagerFactory, 
        ConfigurationService configurationService,
        CancellationService cancellationServices) 
        : base("init", "Initialize the environment")
    {
        this.logger = logger;
        this.packageManagerFactory = packageManagerFactory;
        this.configurationService = configurationService;
        this.cancellationServices = cancellationServices;

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

        try
        {
            await InitializeGitRepository(repositoryUrl, branch).ConfigureAwait(false);
            await InitializePackagesAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException operationCanceledException)
        {
            cancellationServices.Shutdown(operationCanceledException.Message);
        }
    }

    private async Task InitializeGitRepository(Uri repositoryUrl, string branch)
    {
        if (Configuration.RepositoryDirectory.Exists)
        {
            bool deleteRepository = AnsiConsole.Confirm($"The repository directory '{Configuration.RepositoryDirectory.FullName}' already exists. Do you want to delete it and clone the repository again?", false);
            
            if (!deleteRepository)
            {
                throw new OperationCanceledException("Operation cancelled by user.");
            }

            Directory.Delete(Configuration.RepositoryDirectory.FullName, true);
        }

        GitRepository gitRepository = await GitRepository.CloneAsync(repositoryUrl, branch, Configuration.WorkingDirectory, Configuration.RepositoryDirectory, logger).ConfigureAwait(false);
        await gitRepository.RestAsync().ConfigureAwait(false);
    }

    private async Task InitializePackagesAsync()
    {
        IPackageManager packageManager = await SelectPackageManagerAsync().ConfigureAwait(false);

        if (packageManager is HomebrewPackageManager homebrewPackageManager && configurationService.Configuration.PackageManager?.HomebrewConfiguration is not null)
        {
            foreach (string cask in configurationService.Configuration.PackageManager.HomebrewConfiguration.Casks)
            {
                try
                {
                    await homebrewPackageManager.TapAsync(cask).ConfigureAwait(false);

                    logger.Success($"Successfully tapped cask '{cask}'.");
                }
                catch (Exception ex)
                {
                    logger.Error($"Failed to tap cask '{cask}': {ex.Message}");
                }
            }
        }

        await packageManager.UpdateAsync().ConfigureAwait(false);

        foreach (string tool in configurationService.Configuration.Tools)
        {
            await packageManager.InstallPackageAsync(tool).ConfigureAwait(false);
        }
    }

    private async Task<IPackageManager> SelectPackageManagerAsync()
    {
        IEnumerable<PackageManagerInfo> availablePackageManagers = await packageManagerFactory.GetAvailablePackageManagersAsync().ToListAsync().ConfigureAwait(false);

        if (availablePackageManagers.Count() == 1)
        {
            PackageManagerInfo packageManagerInfo = availablePackageManagers.First();

            return (IPackageManager)Activator.CreateInstance(packageManagerInfo.Type, logger)!;
        }
        else
        {
            SelectionPrompt<PackageManagerInfo> packageManagerPrompt = new()
            {
                Title = "Select a package manager:",
                PageSize = 10,
                MoreChoicesText = "[grey](Move up and down to reveal more package managers)[/]",
                HighlightStyle = new Style(foreground: Color.Green),
                Converter = pm => $"{pm.Name} - {pm.Description}"
            };

            packageManagerPrompt.AddChoices(availablePackageManagers);

            PackageManagerInfo selectedPackageManager = await AnsiConsole.PromptAsync(packageManagerPrompt).ConfigureAwait(false);

            return (IPackageManager)Activator.CreateInstance(selectedPackageManager.Type, logger)!;
        }
    }
}