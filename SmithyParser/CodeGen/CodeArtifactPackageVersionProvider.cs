using Amazon.CodeArtifact;
using Amazon.CodeArtifact.Model;

namespace SmithyParser.CodeGen;

public class CodeArtifactPackageVersionProvider //: IPackageVersionProvider
{
    private IAmazonCodeArtifact codeArtifact = new AmazonCodeArtifactClient();

    public async Task<Version> GetBuildVersion(string packageName, string repository, string domain)
    {
       // var domain = Environment.GetEnvironmentVariable("CODE_ARTIFACT_DOMAIN");
       // var repository = Environment.GetEnvironmentVariable("CODE_ARTIFACT_REPOSITORY");
        var format = "nuget";

        try
        {
            var request = new ListPackageVersionsRequest
            {
                Domain = domain,
                Repository = repository,
                Format = format,
                Package = packageName,
                MaxResults = 1, // We only need the latest version
                SortBy = PackageVersionSortType.PUBLISHED_TIME
            };

            var response = await codeArtifact.ListPackageVersionsAsync(request);

            if (response.Versions.Count > 0)
            {
                var latestBuildVersion = response.Versions[0].Version; // Return the latest version
                var buildVersion = new Version(latestBuildVersion);
                var newBuildVersion = IncrementBuild(buildVersion);
                return newBuildVersion;
            }
        }
        catch (Exception ex)
        {
            // Handle exceptions (e.g., package not found, AWS service errors)
            Console.WriteLine($"Error: {ex.Message}");
        }

        return new Version("1.0.0.0");
    }

    public static Version IncrementBuild(Version originalVersion)
    {
        if (originalVersion == null)
        {
            throw new ArgumentNullException(nameof(originalVersion));
        }

        // Increment the build number. If it's -1 (undefined), set it to 0.
        int newBuild = originalVersion.Build != -1 ? originalVersion.Build + 1 : 0;

        // Check if the Revision component is defined.
        if (originalVersion.Revision != -1)
        {
            // If Revision is defined, include it in the new Version.
            return new Version(originalVersion.Major, originalVersion.Minor, newBuild, originalVersion.Revision);
        }
        else
        {
            // If Revision is not defined, create a new Version without the Revision component.
            return new Version(originalVersion.Major, originalVersion.Minor, newBuild, 0);
        }
    }
}