using System.ComponentModel.DataAnnotations;

using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace MrRelease;

public class AzureDevOpsOptions
{
    [Required]
    public string Collection { get; set; } = null!;

    [Required]
    public string Project { get; set; } = null!;

    [Required]
    public string PersonalAccessToken { get; set; } = null!;

    public int RefreshSeconds { get; set; } = 10;

    public VssConnection CreateConnection()
    {
        var credentials = new VssBasicCredential("", PersonalAccessToken);
        return new VssConnection(new Uri(Collection), credentials);
    }
}
