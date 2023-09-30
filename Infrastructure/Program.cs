


using Amazon.Auth.AccessControlPolicy.ActionIdentifiers;
using Amazon.CDK;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.S3.Assets;
using Amazon.CDK.CloudAssembly.Schema;
using Amazon.S3.Model;
using System.Linq;
using System.Text;

namespace AutoDocker
{

    public static class Program
    {
        public static async Task Main(string[] args)
        {


            var app = new App();
            var stack = new AutoDockerBuildStack(app, "myautodockerapp");
               
            var stackId = await app.DeployAsync();
            Thread.Sleep(10000);

            var cloudFormation = new Amazon.CloudFormation.AmazonCloudFormationClient();
            var codeCommit = new Amazon.CodeCommit.AmazonCodeCommitClient();
            var s3 = new Amazon.S3.AmazonS3Client();
   

            var resources = await cloudFormation.DescribeStackResourcesAsync(new Amazon.CloudFormation.Model.DescribeStackResourcesRequest
            {
                StackName = "myautodockerapp"
            });

            var codeCommitResource = resources.StackResources.Single(r => r.ResourceType == "AWS::CodeCommit::Repository");
            var bucketResource = resources.StackResources.Single(r => r.ResourceType == "AWS::S3::Bucket");

            var sb = new StringBuilder();
            sb.AppendLine("Source code...");


            /*
            var putResp = await codeCommit.PutFileAsync(new Amazon.CodeCommit.Model.PutFileRequest
            {
                RepositoryName = "myreponame",
                BranchName = "master",
                CommitMessage = "First checkin.",
                FileContent = new MemoryStream(Encoding.UTF8.GetBytes("Source code..")),
                FilePath = "initail.txt",
                

            });
            */




            Thread.Sleep(10000);




            // This is the cleanup part.

            var listResp = await s3.ListObjectsV2Async(new Amazon.S3.Model.ListObjectsV2Request {
                BucketName = bucketResource.PhysicalResourceId
            });

            var deleteKeys = listResp.S3Objects.Select(o => new KeyVersion
            {
                Key = o.Key
            }).ToList();

            if (listResp.S3Objects.Any())
                await s3.DeleteObjectsAsync(new Amazon.S3.Model.DeleteObjectsRequest
                {
                    BucketName = bucketResource.PhysicalResourceId,
                    Objects = deleteKeys
                });
            

        }
    }

    



}