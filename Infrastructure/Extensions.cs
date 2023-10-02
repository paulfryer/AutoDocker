using Amazon.CDK;
using Amazon.CDK.CXAPI;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;

namespace Infrastructure;

public static class Extensions
{
    public static async Task<string> DeployAsync(this App app)
    {
        var cloudAssembly = app.Synth();

        var stackArtifact =
            (CloudFormationStackArtifact)cloudAssembly.Artifacts.Single(a => a is CloudFormationStackArtifact);

        var cloudFormationJson = File.ReadAllText(stackArtifact.TemplateFullPath);

        var cloudFormation = new AmazonCloudFormationClient();

        var resp = await cloudFormation.CreateStackAsync(new CreateStackRequest
        {
            StackName = stackArtifact.StackName,
            TemplateBody = cloudFormationJson,
            Capabilities = new List<string> { "CAPABILITY_IAM", "CAPABILITY_NAMED_IAM" }
        });

        return resp.StackId;
    }
}