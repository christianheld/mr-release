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
    private readonly ReleaseService _releaseService;
    private readonly AzureDevOpsOptions _azureDevOpsOptions;

    public ShowDeployedVersionCommand(ReleaseService releaseService, IOptions<AzureDevOpsOptions> azureDevOpsOptions)
    {
        if (azureDevOpsOptions is null) throw new ArgumentNullException(nameof(azureDevOpsOptions));

        _releaseService = releaseService;
        _azureDevOpsOptions = azureDevOpsOptions.Value;
    }

    public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] Settings settings)
    {
        AnsiConsole.MarkupLine($"Directory:   [blue]{settings.Folder}[/]");
        AnsiConsole.MarkupLine($"Environment: [green]{settings.Environment}[/]");

        var project = settings.Project ?? _azureDevOpsOptions.Project;

        var deployedReleases = await AnsiConsole.Status()
            .StartAsync(
                "Fetching Release Information",
                async _ => await _releaseService.GetDeployedReleases(project, settings.Folder, settings.Environment));

        if (deployedReleases.Count == 0)
        {
            AnsiConsole.Markup("[red]No Releases.[/]");
            return -1;
        }

        var orderedReleases = settings.Order switch
        {
            Order.Name => deployedReleases.OrderBy(x => x.Name),
            _ => deployedReleases.OrderByDescending(x => x.DeployedOn),
        };

        if (settings.Detailed)
        {
            RenderList(orderedReleases);
        }
        else
        {
            RenderTable(orderedReleases);
        }

        return 0;
    }

    private static void RenderList(IOrderedEnumerable<MrRelease.Models.DeployedRelease> orderedReleases)
    {
        foreach (var release in orderedReleases)
        {
            AnsiConsole.Markup($"Release: [white]{release.Name}[/] - ");
            AnsiConsole.Write(RenderStatus(release));
            AnsiConsole.WriteLine();

            AnsiConsole.MarkupLine($"Id: {release.ReleaseId}");
            AnsiConsole.MarkupLine($"CreatedOn: [yellow]{release.CreatedOn}[/]");
            AnsiConsole.MarkupLine($"DeployedOn: [green]{release.DeployedOn}[/]");

            AnsiConsole.Markup($"Environments: ");
            AnsiConsole.Write(RenderEnvironments(release.Environments));
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
            _ => new Markup(release.Status.ToString())
        };
    }

    private static void RenderTable(IOrderedEnumerable<MrRelease.Models.DeployedRelease> orderedReleases)
    {
        var table = new Table()
            .MarkdownBorder()
            .BorderColor(Color.FromConsoleColor(ConsoleColor.DarkGray));

        table.AddColumn("Release");
        table.AddColumn("Status");
        table.AddColumn("Created");
        table.AddColumn("Deployed");
        table.AddColumns("Environments");

        foreach (var release in orderedReleases)
        {
            table.AddRow(
                new Markup($"{release.Name}", new Style(foreground: Color.Blue, decoration: Decoration.None, link: release.WebUrl)),
                RenderStatus(release),
                new Markup($"[yellow]{FormatDateTime(release.CreatedOn)}[/]"),
                new Markup($"[green]{FormatDateTime(release.DeployedOn)}[/]"),
                RenderEnvironments(release.Environments));
        }

        AnsiConsole.Write(table);
    }

    private static Markup RenderEnvironments(IReadOnlyList<string> environments)
    {
        return new Markup(string.Join(", ", environments));
    }

    private static string FormatDateTime(DateTime dateTime)
    {
        var localTime = dateTime.ToLocalTime();
        return localTime.ToString(CultureInfo.CurrentCulture);
    }

    public class Settings : BaseSettings
    {
        [Description("The Release Folder")]
        [CommandArgument(0, "<FOLDER>")]
        public string Folder { get; init; } = null!;

        [Description("The Environment")]
        [CommandArgument(1, "<ENVIRONMENT>")]
        public string Environment { get; init; } = null!;

        [CommandOption("-o|--order-by <ORDER>")]
        [Description("Sets the order. Valid values: DeployedOn, Name.")]
        [DefaultValue("DeployedOn")]
        public Order Order { get; init; } = Order.DeployedOn;

        [Description("Show Detailed information")]
        [CommandOption("-d|--detailed")]
        public bool Detailed { get; init; }
    }

    public enum Order
    {
        DeployedOn,
        Name
    }
}