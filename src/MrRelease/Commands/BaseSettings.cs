using System.ComponentModel;

using Spectre.Console.Cli;

namespace MrDeploy.Commands;

public abstract class BaseSettings : CommandSettings
{
    [Description("The project name")]
    [CommandOption("-p|--project <ProjectName>")]
    public string? Project { get; init; }
}
