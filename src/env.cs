using Autofac;
using System;

ContainerBuilder containerBuilder = new();
containerBuilder.RegisterType<ConfigurationService>().OnActivating(async e => 
{
    await e.Instance.InitializeAsync().ConfigureAwait(false);
    await e.Instance.ReadAsync().ConfigureAwait(false);
}).SingleInstance();
containerBuilder.RegisterType<App>().SingleInstance();
containerBuilder.RegisterType<CommandLine>().SingleInstance();
containerBuilder.RegisterType<Logger>().SingleInstance();
containerBuilder.RegisterInstance(new SystemInfo()).SingleInstance();
containerBuilder.Register(ctx =>
{
    SystemInfo systemInfo = ctx.Resolve<SystemInfo>();
    Logger logger = ctx.Resolve<Logger>();

    return systemInfo.PackageManager switch
    {
        PackageManagerType.Brew => new HomebrewPackageManager(logger),
        _ => throw new NotSupportedException($"Unsupported package manager: {systemInfo.PackageManager}")
    };
}).As<IPackageManager>().SingleInstance();
containerBuilder.RegisterType<RootCommand>().SingleInstance();
containerBuilder.RegisterType<InitCommand>().SingleInstance();
containerBuilder.RegisterType<ToolsCommand>().SingleInstance();
containerBuilder.RegisterType<ToolsAddCommand>().SingleInstance();
containerBuilder.RegisterType<ToolsUpdateCommand>().SingleInstance();
containerBuilder.RegisterType<ToolsRemoveCommand>().SingleInstance();
containerBuilder.RegisterType<ToolsListCommand>().SingleInstance();
containerBuilder.RegisterType<FilesCommand>().SingleInstance();
containerBuilder.RegisterType<FilesAddCommand>().SingleInstance();
containerBuilder.RegisterType<FilesRemoveCommand>().SingleInstance();
containerBuilder.RegisterType<FilesListCommand>().SingleInstance();
containerBuilder.RegisterType<FilesPushCommand>().SingleInstance();
containerBuilder.RegisterType<FilesCommitCommand>().SingleInstance();
containerBuilder.RegisterType<FilesPullCommand>().SingleInstance();
containerBuilder.Register(ctx =>
{
    Logger logger = ctx.Resolve<Logger>();
    ConfigurationService configurationService = ctx.Resolve<ConfigurationService>();
    Configuration configuration = configurationService.Configuration;

    return new GitRepository(Configuration.WorkingDirectory, Configuration.RepositoryDirectory, logger);
}).SingleInstance();
containerBuilder.RegisterType<KeyboardService>().SingleInstance();
containerBuilder.RegisterType<CancellationServices>().SingleInstance();

await using IContainer container = containerBuilder.Build();

App app = container.Resolve<App>();

int exitCode = await app.ExecuteAsync(args).ConfigureAwait(false);

return exitCode;    