using System.Diagnostics;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using MrDeploy;
using MrDeploy.Commands;
using MrDeploy.Infrastructure;
using MrDeploy.Services;

using MrRelease.Commands;

using Spectre.Console;
using Spectre.Console.Cli;

internal class Program
{
    private static string? _settingsJsonPath;

    public static string SettingsJsonPath
    {
        get
        {
            if (_settingsJsonPath is null)
            {
                var configurationDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".MrRelease");
                _settingsJsonPath = Path.Combine(configurationDirectory, "settings.json");
            }

            return _settingsJsonPath;
        }
    }

    private static IConfiguration BuildConfiguration()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsJsonPath)!);

        return new ConfigurationBuilder()
            .AddJsonFile(SettingsJsonPath, optional: true)
            .Build();
    }

    [Conditional("DEBUG")]
    private static void ConfigureDebugSettings(IConfigurator configurator)
    {
        //configurator.PropagateExceptions();
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

            cfg.SetExceptionHandler(ex =>
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

    private static int Main(string[] args)
    {
        var configuration = BuildConfiguration();
        var services = new ServiceCollection();

        ConfigureServices(services, configuration);

        var app = CreateApp(services);

        return app.Run(args);
    }
}
