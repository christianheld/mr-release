using Microsoft.VisualStudio.Services.ReleaseManagement.WebApi;

namespace MrRelease.Models;

public record DeployedRelease
{
    // TODO: Make properties "required" in .NET 7
    public string Name { get; init; } = null!;
    public int ReleaseId { get; init; }
    public DateTime CreatedOn { get; init; }
    public DateTime DeployedOn { get; init; }
    public DeploymentStatus Status { get; init; }
    public string WebUrl { get; init; } = null!;
    public IReadOnlyList<string> Environments { get; init; } = Array.Empty<string>();
}
