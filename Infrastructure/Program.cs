


using Amazon;
using Amazon.Auth.AccessControlPolicy.ActionIdentifiers;
using Amazon.CDK;
using Amazon.CDK.AWS.CodeCommit;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.S3.Assets;
using Amazon.CDK.CloudAssembly.Schema;
using Amazon.CodeCommit;
using Amazon.IdentityManagement.Model;
using Amazon.Runtime.CredentialManagement;
using Amazon.S3.Model;
using LibGit2Sharp;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace AutoDocker
{

    public static class Program
    {
        public static async Task Main(string[] args)
        {
            var cloudFormation = new Amazon.CloudFormation.AmazonCloudFormationClient();
            var codeCommit = new Amazon.CodeCommit.AmazonCodeCommitClient();
            var s3 = new Amazon.S3.AmazonS3Client();
            var iam = new Amazon.IdentityManagement.AmazonIdentityManagementServiceClient();
            var sts = new Amazon.SecurityToken.AmazonSecurityTokenServiceClient();


            var repoName = "myreponame";

            var solutionName = "OptionsArb";
            var projectNames = new List<string>
            {
                "QuotePump",
                "ArbitragePump",
                "ContractIndexer"
            };


            var identity = await sts.GetCallerIdentityAsync(new Amazon.SecurityToken.Model.GetCallerIdentityRequest { });

            

            var env = new Amazon.CDK.Environment
            {
                Account = identity.Account,
                Region = "us-west-2"
            };


            var app = new App();
            var stack = new AutoDockerBuildStack(identity.Account, solutionName, projectNames, app, "myautodockerapp", new StackProps
            {
                Env = env
            });
               
           

          var stackId = await app.DeployAsync();
           Thread.Sleep(10000);


    






            var resources = await cloudFormation.DescribeStackResourcesAsync(new Amazon.CloudFormation.Model.DescribeStackResourcesRequest
            {
                StackName = "myautodockerapp"
            });

            var codeCommitResource = resources.StackResources.Single(r => r.ResourceType == "AWS::CodeCommit::Repository");
            var bucketResource = resources.StackResources.Single(r => r.ResourceType == "AWS::S3::Bucket");



            var userName = "temp-git-user";// iam.GetUserAsync().Result.User.UserName;

            
   

            var region = codeCommit.Config.RegionEndpoint.SystemName;

            var soluitonDirectory = "C:\\Users\\Administrator\\source\\repos\\OptionsArb";

            var tempFolderName = "temp-source";


            // if (Directory.Exists(tempFolderName))
            //    Directory.Delete(tempFolderName, true);


             var tempSource = Directory.CreateDirectory("temp-source");
            var localRepoUrl =  tempSource.FullName;

            
           

            var remoteRepoUrl = $"https://git-codecommit.{region}.amazonaws.com/v1/repos/{repoName}";
            




            

            CopyDirectory(soluitonDirectory, localRepoUrl, codeCommit, null);

            ZipFile.CreateFromDirectory("temp-source", "source.zip");

            FileStream fileStream = File.OpenRead("source.zip");
            MemoryStream memoryStream = new MemoryStream();
            fileStream.CopyTo(memoryStream);
            memoryStream.Seek(0, SeekOrigin.Begin);


            var putResp = await codeCommit.PutFileAsync(new Amazon.CodeCommit.Model.PutFileRequest
            {
                RepositoryName = "myreponame",
                BranchName = "master",
                CommitMessage = "Checking in source code with generated Dockerfiles",
                FileContent = memoryStream,
                FilePath = "source.zip",
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


        static void CopyDirectory(string sourceDir, string destDir, AmazonCodeCommitClient codeCommit, string parentCommitId)
        {
            string excludePattern = ".";
            var folderName = sourceDir.Split('\\').Last();
            if (folderName.StartsWith(excludePattern) || folderName == "bin" || folderName == "obj")
                return;

            // Create the destination directory if it doesn't exist
            if (!Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            // Copy all files
            string[] files = Directory.GetFiles(sourceDir);
            foreach (string file in files)
            {
                string fileName = Path.GetFileName(file);
                string destFile = Path.Combine(destDir, fileName);
                File.Copy(file, destFile, true);


                
                using (FileStream fileStream = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    MemoryStream memoryStream = new MemoryStream();

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
            string[] subdirectories = Directory.GetDirectories(sourceDir);
            foreach (string subdir in subdirectories)
            {
                string subdirName = new DirectoryInfo(subdir).Name;
                string destSubdir = Path.Combine(destDir, subdirName);
                CopyDirectory(subdir, destSubdir, codeCommit, parentCommitId);
            }
        }
    }

 





}