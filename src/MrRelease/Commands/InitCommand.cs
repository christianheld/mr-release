using System.Text.Json;

using Spectre.Console;
using Spectre.Console.Cli;

namespace MrRelease.Commands;

public class InitCommand : AsyncCommand
{
    private static JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true
    };

    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        var currentOptions = await TryReadCurrentOptionsAsync();
        var options = PromptForOptions(currentOptions);
        await SaveOptionsAsync(options);

        return 0;
    }

    private static TextPrompt<int> CreatePrompt(string message, int? currentValue)
    {
        var prompt = new TextPrompt<int>(message);
        if (currentValue != null)
        {
            prompt.DefaultValue(currentValue.Value);
        }

        return prompt;
    }

    private static TextPrompt<string> CreatePrompt(string message, string? currentValue)
    {
        var prompt = new TextPrompt<string>(message);
        if (currentValue != null)
        {
            prompt.DefaultValue(currentValue);
        }

        return prompt;
    }

    private static AzureDevOpsOptions PromptForOptions(AzureDevOpsOptions? currentOptions)
    {
        var options = new AzureDevOpsOptions();

        AnsiConsole.MarkupLine(
            "Enter the Url of your Azure DevOps collection. " +
            "Example: [blue]https://dev.azure.com/Contoso[/]");

        options.Collection = AnsiConsole.Prompt(
            CreatePrompt("Azure DevOps Url:", currentOptions?.Collection)
                .Validate(s =>
                {
                    if (!Uri.TryCreate(s, UriKind.RelativeOrAbsolute, out var _))
                    {
                        return ValidationResult.Error("[red]Invalid URL.[/]");
                    }

                    return ValidationResult.Success();
                }));

        options.Project = AnsiConsole.Prompt(
            CreatePrompt("Project Name:", currentOptions?.Project));

        AnsiConsole.MarkupLine(
            "Enter Personal Access Token. (Needs read and manage access to releases)");
        options.PersonalAccessToken = AnsiConsole.Prompt(
            CreatePrompt("Personal Access Token", currentOptions?.PersonalAccessToken));

        AnsiConsole.MarkupLine("Watch mode timeout in seconds.");
        options.RefreshSeconds = AnsiConsole.Prompt<int>(
            CreatePrompt("Timeout", currentOptions?.RefreshSeconds ?? 10));

        return options;
    }

    private static async Task SaveOptionsAsync(AzureDevOpsOptions options)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Program.DefaultSettingsPath)!);
        var json = JsonSerializer.Serialize(options, _serializerOptions);

        await File.WriteAllTextAsync(Program.DefaultSettingsPath, json);
    }

    private static async Task<AzureDevOpsOptions?> TryReadCurrentOptionsAsync()
    {
        AzureDevOpsOptions? currentOptions = null;
        if (File.Exists(Program.DefaultSettingsPath))
        {
            var currentJson = await File.ReadAllTextAsync(Program.DefaultSettingsPath);
            currentOptions = JsonSerializer.Deserialize<AzureDevOpsOptions>(currentJson);
        }

        return currentOptions;
    }
}
