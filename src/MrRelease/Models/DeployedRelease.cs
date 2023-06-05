using Microsoft.VisualStudio.Services.ReleaseManagement.WebApi;

namespace MrRelease.Models;

public record DeployedRelease
{
    public required string Name { get; init; }
    public required int ReleaseId { get; init; }
    public required DateTime CreatedOn { get; init; }
    public DateTime? DeployedOn { get; init; }
    public required DeploymentStatus Status { get; init; }
    public required string WebUrl { get; init; }
    public required IReadOnlyList<string> Environments { get; init; }
}
