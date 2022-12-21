using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.Services.ReleaseManagement.WebApi;

using MrRelease.Models;
using MrRelease.Services;

using Spectre.Console;
using Spectre.Console.Cli;

namespace MrRelease.Commands;

public class ShowDeployedVersionCommand : AsyncCommand<ShowDeployedVersionCommand.Settings>
{
    private readonly AzureDevOpsOptions _azureDevOpsOptions;
    private readonly ReleaseService _releaseService;

    public ShowDeployedVersionCommand(ReleaseService releaseService, IOptions<AzureDevOpsOptions> azureDevOpsOptions)
    {
        if (azureDevOpsOptions is null) throw new ArgumentNullException(nameof(azureDevOpsOptions));

        _releaseService = releaseService;
        _azureDevOpsOptions = azureDevOpsOptions.Value;
    }

    public enum Order
    {
        DeployedOn,
        Name
    }

    public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] Settings settings)
    {
        if (settings.WatchMode)
        {
            AnsiConsole.Clear();
        }

        AnsiConsole.MarkupLine($"Directory:   [blue]{settings.Folder}[/]");
        AnsiConsole.MarkupLine($"Environment: [green]{settings.Environment}[/]");

        var deployedReleases = await AnsiConsole.Status()
            .StartAsync(
                "Fetching Release Information",
                async _ => await GetReleasesAsync(settings));

        if (deployedReleases.Count == 0 && !settings.WatchMode)
        {
            AnsiConsole.Markup("[red]No Releases.[/]");
            return -1;
        }

        if (settings.Detailed)
        {
            RenderList(deployedReleases, settings.Environment);
        }
        else
        {
            var table = CreateReleaseTable();
            await AnsiConsole.Live(table)
                .StartAsync(async context =>
                {
                    while (true)
                    {
                        table.Rows.Clear();
                        RenderTableRows(table, deployedReleases, settings.Environment);
                        context.Refresh();

                        if (!settings.WatchMode)
                        {
                            return 0;
                        }

                        for (int i = _azureDevOpsOptions.RefreshSeconds * 2; i > 0; i--)
                        {
                            table
                                .Caption(string.Format(CultureInfo.InvariantCulture, "Refresh in {0}s", (i + 1) / 2))
                                .LeftAligned();

                            context.Refresh();
                            await Task.Delay(TimeSpan.FromMilliseconds(500));
                        }

                        table.Caption($"Loading...").LeftAligned();
                        context.Refresh();
                        deployedReleases = await GetReleasesAsync(settings);
                    }
                });
        }

        return 0;
    }

    private static Table CreateReleaseTable()
    {
        var table = new Table()
            .RoundedBorder()
            .BorderColor(Color.FromConsoleColor(ConsoleColor.DarkGray));

        table.AddColumn("Release");
        table.AddColumn("Status");
        table.AddColumn("Created");
        table.AddColumn("Deployed");
        table.AddColumns("Environments");

        return table;
    }

    private static string FormatDateTime(DateTime? dateTime) =>
            dateTime.HasValue ? FormatDateTime(dateTime.Value) : "";

    private static string FormatDateTime(DateTime dateTime)
    {
        var localTime = dateTime.ToLocalTime();
        return localTime.ToString(CultureInfo.CurrentCulture);
    }

    private static Markup RenderEnvironments(IReadOnlyList<string> environments, string currentEnvironment)
    {
        var markupLines = new string[environments.Count];
        for (int i = 0; i < environments.Count; i++)
        {
            var environment = environments[i];
            markupLines[i] = environment.StartsWith(currentEnvironment, StringComparison.OrdinalIgnoreCase)
                ? $"[green]{environment}[/]"
                : environment;
        }

        return new Markup(string.Join(", ", markupLines));
    }

    private static void RenderList(IEnumerable<DeployedRelease> releases, string currentEnvironment)
    {
        foreach (var release in releases)
        {
            AnsiConsole.Markup($"Release: [white]{release.Name}[/] - ");
            AnsiConsole.Write(RenderStatus(release));
            AnsiConsole.WriteLine();

            AnsiConsole.MarkupLine($"Id: {release.ReleaseId}");
            AnsiConsole.MarkupLine($"CreatedOn: [yellow]{release.CreatedOn}[/]");
            AnsiConsole.MarkupLine($"DeployedOn: [green]{release.DeployedOn}[/]");

            AnsiConsole.Markup($"Environments: ");
            AnsiConsole.Write(RenderEnvironments(release.Environments, currentEnvironment));
            AnsiConsole.WriteLine();

            AnsiConsole.MarkupLine($"[grey]{release.WebUrl}[/]");
            AnsiConsole.WriteLine();
        }
    }

    private static Markup RenderStatus(DeployedRelease release)
    {
        return release.Status switch
        {
            DeploymentStatus.Succeeded => new Markup("OK", new Style(Color.Green)),
            DeploymentStatus.PartiallySucceeded => new Markup("PARTIAL", new Style(Color.Yellow)),
            DeploymentStatus.Failed => new Markup("FAILED", new Style(Color.Red)),
            DeploymentStatus.InProgress => new Markup("IN_PROGRESS", new Style(Color.Blue)),
            _ => new Markup(release.Status.ToString().ToUpperInvariant())
        };
    }

    private static void RenderTableRows(
        Table table,
        IReadOnlyList<DeployedRelease> orderedReleases,
        string currentEnvironment)
    {
        foreach (var release in orderedReleases)
        {
            table.AddRow(
                new Markup(
                    $"{release.Name}",
                    new Style(foreground: Color.Blue, decoration: Decoration.None, link: release.WebUrl)),
                RenderStatus(release),
                new Markup($"[yellow]{FormatDateTime(release.CreatedOn)}[/]"),
                new Markup($"[green]{FormatDateTime(release.DeployedOn)}[/]"),
                RenderEnvironments(release.Environments, currentEnvironment));
        }
    }

    private async Task<IReadOnlyList<DeployedRelease>> GetReleasesAsync(Settings settings)
    {
        var project = settings.Project ?? _azureDevOpsOptions.Project;
        var deployedReleases = await _releaseService.GetDeployedReleases(project, settings.Folder, settings.Environment);

        if (settings.OnlyFailed)
        {
            deployedReleases = deployedReleases
                .Where(dr => dr.Status != DeploymentStatus.Succeeded)
                .ToList();
        }

        var orderedReleases = settings.Order switch
        {
            Order.Name => deployedReleases.OrderBy(x => x.Name),
            _ => deployedReleases.OrderByDescending(x => x.DeployedOn),
        };

        return orderedReleases.ToList();
    }

    public class Settings : BaseSettings
    {
        [Description("Show Detailed information")]
        [CommandOption("-d|--detailed")]
        public bool Detailed { get; init; }

        [Description("The Environment")]
        [CommandArgument(1, "<ENVIRONMENT>")]
        public string Environment { get; init; } = null!;

        [Description("The Release Folder")]
        [CommandArgument(0, "<FOLDER>")]
        public string Folder { get; init; } = null!;

        [Description("Show only failed and partial status releases")]
        [CommandOption("-f|--failed")]
        public bool OnlyFailed { get; init; }

        [CommandOption("-o|--order-by <ORDER>")]
        [Description("Sets the order. Valid values: DeployedOn, Name.")]
        [DefaultValue("DeployedOn")]
        public Order Order { get; init; } = Order.DeployedOn;

        [Description("Watch mode")]
        [CommandOption("-w|--watch")]
        public bool WatchMode { get; init; }
    }
}
