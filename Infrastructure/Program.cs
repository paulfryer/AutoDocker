using System.IO.Compression;
using Amazon.CDK;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.CodeCommit;
using Amazon.CodeCommit.Model;
using Amazon.IdentityManagement;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Environment = Amazon.CDK.Environment;
using File = System.IO.File;

namespace Infrastructure;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var cloudFormation = new AmazonCloudFormationClient();
        var codeCommit = new AmazonCodeCommitClient();
        var s3 = new AmazonS3Client();
        var iam = new AmazonIdentityManagementServiceClient();
        var sts = new AmazonSecurityTokenServiceClient();


        var repoName = "myreponame";

        var solutionName = "OptionsArb";
        var projectNames = new List<string>
        {
            "QuotePump",
            "ArbitragePump",
            "ContractIndexer"
        };


        var identity = await sts.GetCallerIdentityAsync(new GetCallerIdentityRequest());


        var env = new Environment
        {
            Account = identity.Account,
            Region = "us-west-2"
        };


        var app = new App();
        var stack = new AutoDockerBuildStack(identity.Account, solutionName, projectNames, app, "myautodockerapp",
            new StackProps
            {
                Env = env
            });


        var stackId = await app.DeployAsync();
        Thread.Sleep(10000);


        var resources = await cloudFormation.DescribeStackResourcesAsync(new DescribeStackResourcesRequest
        {
            StackName = "myautodockerapp"
        });

        var codeCommitResource = resources.StackResources.Single(r => r.ResourceType == "AWS::CodeCommit::Repository");
        var bucketResource = resources.StackResources.Single(r => r.ResourceType == "AWS::S3::Bucket");


        var userName = "temp-git-user"; // iam.GetUserAsync().Result.User.UserName;


        var region = codeCommit.Config.RegionEndpoint.SystemName;

        var soluitonDirectory = "C:\\Users\\Administrator\\source\\repos\\OptionsArb";

        var tempFolderName = "temp-source";


        // if (Directory.Exists(tempFolderName))
        //    Directory.Delete(tempFolderName, true);


        var tempSource = Directory.CreateDirectory("temp-source");
        var localRepoUrl = tempSource.FullName;


        var remoteRepoUrl = $"https://git-codecommit.{region}.amazonaws.com/v1/repos/{repoName}";


        CopyDirectory(soluitonDirectory, localRepoUrl, codeCommit, null);

        ZipFile.CreateFromDirectory("temp-source", "source.zip");

        var fileStream = File.OpenRead("source.zip");
        var memoryStream = new MemoryStream();
        fileStream.CopyTo(memoryStream);
        memoryStream.Seek(0, SeekOrigin.Begin);


        var putResp = await codeCommit.PutFileAsync(new PutFileRequest
        {
            RepositoryName = "myreponame",
            BranchName = "master",
            CommitMessage = "Checking in source code with generated Dockerfiles",
            FileContent = memoryStream,
            FilePath = "source.zip"
        });

        memoryStream.Dispose();
        fileStream.Dispose();


        /*
        var getCredsResp = await iam.ListServiceSpecificCredentialsAsync(new Amazon.IdentityManagement.Model.ListServiceSpecificCredentialsRequest
        {
            ServiceName = "codecommit.amazonaws.com",
            UserName = userName
        });




            var c = await iam.CreateServiceSpecificCredentialAsync(new Amazon.IdentityManagement.Model.CreateServiceSpecificCredentialRequest
            {
                ServiceName = "codecommit.amazonaws.com",
                UserName = userName
            });


        var repositoryUrl = $"https://{c.ServiceSpecificCredential.ServiceUserName}:{c.ServiceSpecificCredential.ServicePassword}@git-codecommit.{region}.amazonaws.com/v1/repos/{repoName}";
        LibGit2Sharp.Repository.Clone(repositoryUrl, "temp");








        LibGit2Sharp.Repository.Init(localRepoUrl);



        using (var localRepo = new LibGit2Sharp.Repository(localRepoUrl))
        {


            Commands.Stage(localRepo, "*");
            var signature = new Signature("autodocker", "autodocker@example.com", DateTimeOffset.Now);
            var commit = localRepo.Commit("Local copy", signature, signature);



            localRepo.Network.Remotes.Add("origin", remoteRepoUrl);

            localRepo.Branches.Update(localRepo.Branches["master"],
               b => b.Remote = "origin",
               b => b.UpstreamBranch = "refs/heads/master");


            var remote = localRepo.Network.Remotes.Single();

            var credentials = new UsernamePasswordCredentials
            {
                Username = c.ServiceSpecificCredential.ServiceUserName,
                Password = c.ServiceSpecificCredential.ServicePassword
            };

            var options = new PushOptions {
                CredentialsProvider = (_url, _user, _cred) => credentials
            };


            //localRepo.Network.Push(remote, @"refs/head/master", options);



            localRepo.Network.Push(localRepo.Branches["master"], options);

        }






       */


        Thread.Sleep(10000);


        // This is the cleanup part.

        var listResp = await s3.ListObjectsV2Async(new ListObjectsV2Request
        {
            BucketName = bucketResource.PhysicalResourceId
        });

        var deleteKeys = listResp.S3Objects.Select(o => new KeyVersion
        {
            Key = o.Key
        }).ToList();

        if (listResp.S3Objects.Any())
            await s3.DeleteObjectsAsync(new DeleteObjectsRequest
            {
                BucketName = bucketResource.PhysicalResourceId,
                Objects = deleteKeys
            });
    }


    private static void CopyDirectory(string sourceDir, string destDir, AmazonCodeCommitClient codeCommit,
        string parentCommitId)
    {
        var excludePattern = ".";
        var folderName = sourceDir.Split('\\').Last();
        if (folderName.StartsWith(excludePattern) || folderName == "bin" || folderName == "obj")
            return;

        // Create the destination directory if it doesn't exist
        if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);

        // Copy all files
        var files = Directory.GetFiles(sourceDir);
        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            var destFile = Path.Combine(destDir, fileName);
            File.Copy(file, destFile, true);


            using (var fileStream = new FileStream(file, FileMode.Open, FileAccess.Read))
            {
                var memoryStream = new MemoryStream();

                // Copy the file's contents to the MemoryStream
                fileStream.CopyTo(memoryStream);

                /*
                var result = codeCommit.PutFileAsync(new Amazon.CodeCommit.Model.PutFileRequest
                {
                    ParentCommitId = parentCommitId,
                    BranchName = "master",
                    RepositoryName = "myreponame",
                    Email = "test@test.com",
                    FilePath = file,
                    FileContent = memoryStream // new MemoryStream(Encoding.UTF8.GetBytes("Source code.."))
                }).Result;
                */
                // parentCommitId = result.CommitId;
            }
        }

        // Copy all subdirectories recursively
        var subdirectories = Directory.GetDirectories(sourceDir);
        foreach (var subdir in subdirectories)
        {
            var subdirName = new DirectoryInfo(subdir).Name;
            var destSubdir = Path.Combine(destDir, subdirName);
            CopyDirectory(subdir, destSubdir, codeCommit, parentCommitId);
        }
    }
}