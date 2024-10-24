using System.Diagnostics;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Physical;
using Microsoft.Extensions.Options;

using MrRelease.Commands;
using MrRelease.Infrastructure;
using MrRelease.Services;

using Spectre.Console;
using Spectre.Console.Cli;

namespace MrRelease;

public sealed class Program
{
    private static readonly string _userProfileFolder =
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private static string? _settingsJsonPath;

    public static string DefaultSettingsPath
    {
        get
        {
            if (_settingsJsonPath is null)
            {
                var configurationDirectory = Path.Combine(_userProfileFolder);
                _settingsJsonPath = Path.Combine(configurationDirectory, ".mr-release");
            }

            return _settingsJsonPath;
        }
    }

    private static IConfiguration BuildConfiguration()
    {
        var configurationBuilder = new ConfigurationBuilder();

        foreach (var configFile in GetConfigurationFiles())
        {
            var configFilePath = Path.GetDirectoryName(configFile)!;
            var configFileName = Path.GetFileName(configFile);

            configurationBuilder.AddJsonFile(
                new PhysicalFileProvider(configFilePath, ExclusionFilters.None),
                configFileName,
                optional: true,
                reloadOnChange: true);
        }

        return configurationBuilder.Build();
    }

    [Conditional("DEBUG")]
    private static void ConfigureDebugSettings(IConfigurator configurator)
    {
        configurator.PropagateExceptions();
        configurator.ValidateExamples();
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AzureDevOpsOptions>()
            .Bind(configuration)
            .ValidateDataAnnotations();

        services.AddSingleton<ReleaseService>();
    }

    private static CommandApp CreateApp(IServiceCollection services)
    {
        var app = new CommandApp(new TypeRegistrar(services));

        app.Configure(cfg =>
        {
            ConfigureDebugSettings(cfg);

            cfg.AddCommand<InitCommand>("init")
                .WithDescription($"Create or Update configuration");

            cfg.AddCommand<ShowDeployedVersionCommand>("show")
                .WithDescription($"Show currently deployed releases. Run [grey]show --help[/] for details.");

            cfg.SetExceptionHandler((ex, _) =>
            {
                if (ex.InnerException is OptionsValidationException validationException)
                {
                    AnsiConsole.MarkupLine("[red]Invalid Configuration[/]");
                    foreach (var failure in validationException.Failures)
                    {
                        AnsiConsole.MarkupLine($"[grey]- {failure}[/]");
                    }
                    AnsiConsole.WriteLine();
                    AnsiConsole.WriteLine($"Run \"init\" command to build new configuration.");

                    return -2;
                }

                AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
                return -1;
            });
        });

        return app;
    }

    private static List<string> GetConfigurationFiles()
    {
        var configFiles = new List<string> { DefaultSettingsPath };
        var currentDirectory = Directory.GetCurrentDirectory();

        do
        {
            var configFilename = Path.Combine(currentDirectory!, ".mr-release");
            if (File.Exists(configFilename) && currentDirectory != _userProfileFolder)
            {
                configFiles.Add(configFilename);
            }

            currentDirectory = Directory.GetParent(currentDirectory!)?.FullName;
        } while (currentDirectory == null);

        return configFiles;
    }

    private static int Main(string[] args)
    {
        MigrateConfiguration();
        var configuration = BuildConfiguration();
        var services = new ServiceCollection();

        ConfigureServices(services, configuration);

        var app = CreateApp(services);
        return app.Run(args);
    }

    private static void MigrateConfiguration()
    {
        var legacySettingsPath = Path.Combine(_userProfileFolder, ".MrRelease", "settings.json");

        if (File.Exists(legacySettingsPath) && !File.Exists(DefaultSettingsPath))
        {
            File.Move(legacySettingsPath, DefaultSettingsPath);
            Directory.Delete(Path.Combine(_userProfileFolder, ".MrRelease"));
            AnsiConsole.MarkupLine(
                ":party_popper: Migrated configuration to \".mr-release\" file :party_popper:" +
                Environment.NewLine);
        }
    }
}
