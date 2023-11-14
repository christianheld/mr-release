using System.Globalization;

using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.Services.ReleaseManagement.WebApi;
using Microsoft.VisualStudio.Services.ReleaseManagement.WebApi.Clients;
using Microsoft.VisualStudio.Services.ReleaseManagement.WebApi.Contracts;
using Microsoft.VisualStudio.Services.WebApi;

using MrRelease.Models;

namespace MrRelease.Services;

public class ReleaseService
{
    private readonly AzureDevOpsOptions _azureDevOpsSettings;

    public ReleaseService(IOptions<AzureDevOpsOptions> azureDevOpsSettings)
    {
        _azureDevOpsSettings = azureDevOpsSettings?.Value ?? throw new ArgumentNullException(nameof(azureDevOpsSettings));
    }

    private async Task<IReadOnlyList<Release>> GetActiveReleasesAsync(string project, string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
            throw new ArgumentException($"'{nameof(folder)}' cannot be null or whitespace.", nameof(folder));

        using var connection = _azureDevOpsSettings.CreateConnection();
        var client = await connection.GetClientAsync<ReleaseHttpClient2>();

        var path = await GetFolderPathAsync(client, project, folder);

        var releases = new List<Release>();

        int? token = null;
        do
        {
            var page = await client.GetReleasesAsync2(
                project: project,
                path: path,
                statusFilter: ReleaseStatus.Active,
                top: 100,
                continuationToken: token,
                expand: ReleaseExpands.Environments);

            releases.AddRange(page);

            token = page.ContinuationToken == null
                ? null
                : int.Parse(page.ContinuationToken, CultureInfo.InvariantCulture);
        } while (token != null);

        return releases;
    }

    public async Task<IReadOnlyList<DeployedRelease>> GetDeployedReleases(string project, string folder, string environment)
    {
        var activeReleases = await GetActiveReleasesAsync(project, folder);

        return activeReleases
            .Where(release => release.Environments
                .Any(env =>
                    env.Name.StartsWith(environment, StringComparison.OrdinalIgnoreCase)
                    && IsCompleted(env.Status)))
            .GroupBy(release => release.ReleaseDefinitionReference.Name)
            .Select(group => group.MaxBy(release => release.CreatedOn)!)
            .Select(release => MapToDeployedRelease(release, environment))
            .ToList();
    }

    private static bool IsCompleted(EnvironmentStatus status)
    {
        return status is EnvironmentStatus.Succeeded
            or EnvironmentStatus.PartiallySucceeded
            or EnvironmentStatus.Rejected
            or EnvironmentStatus.InProgress;
    }

    private static DeployedRelease MapToDeployedRelease(Release release, string environmentName)
    {
        ArgumentNullException.ThrowIfNull(release);

        var environment = release.Environments
              .First(env => env.Name.StartsWith(environmentName, StringComparison.OrdinalIgnoreCase));

        var deployedEnvironments = release.Environments
            .Where(env => IsCompleted(env.Status))
            .Select(env => env.Name)
            .ToList();

        var currentAttempt = environment.DeploySteps
            .Where(step => step.Status is not (DeploymentStatus.Undefined or DeploymentStatus.NotDeployed))
            .OrderBy(step => step.Attempt)
            .LastOrDefault();

        var web = (ReferenceLink)release.Links.Links["web"];

        return new DeployedRelease
        {
            Name = release.Name,
            ReleaseId = release.Id,
            CreatedOn = release.CreatedOn,
            DeployedOn = currentAttempt?.LastModifiedOn,
            Status = currentAttempt?.Status ?? DeploymentStatus.Undefined,
            WebUrl = web.Href,
            Environments = deployedEnvironments,
        };
    }

    private static async Task<string> GetFolderPathAsync(ReleaseHttpClient2 client, string project, string folder)
    {
        var searchFolder = $"\\{folder.Replace('/', '\\')}";

        var folders = await client.GetFoldersAsync(project, searchFolder);
        if (folders.Count == 0)
        {
            throw new InvalidOperationException(
                $"No Release folder found for: \"{folder}\"");
        }

        if (folders.Count > 1)
        {
            throw new InvalidOperationException($"Ambiguous folder query: \"{folder}\", {folders.Count} matches found.");
        }

        return folders[0].Path;
    }
}
