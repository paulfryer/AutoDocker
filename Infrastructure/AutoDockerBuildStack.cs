using Amazon.CDK;
using Amazon.CDK.AWS.CodeBuild;
using Amazon.CDK.AWS.CodeCommit;
using Amazon.CDK.AWS.CodePipeline;
using Amazon.CDK.AWS.CodePipeline.Actions;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.S3.Assets;

namespace AutoDocker
{
    public class AutoDockerBuildStack : Stack
    {
        public AutoDockerBuildStack(string solutionName, List<string> projectNames, App scope, string id, IStackProps? props = null) : base(scope, id, props)
        {
           /*
            var gitUser = new User(this, "temp-git-user", new UserProps
            {
                UserName = "temp-git-user"
            });
            gitUser.AddManagedPolicy(ManagedPolicy.FromAwsManagedPolicyName("AWSCodeCommitFullAccess"));
           */

            foreach (var projectName in projectNames)
            {
                var ecr = new Amazon.CDK.AWS.ECR.Repository(this, projectName, new Amazon.CDK.AWS.ECR.RepositoryProps
                {
                    RepositoryName = $"{solutionName.ToLower()}/{projectName.ToLower()}"
                }) ;

            }




            var buildProject = new PipelineProject(this, "myproject",
                new PipelineProjectProps
                {

                    BuildSpec = BuildSpec.FromSourceFilename("buildspec.yml"),
                    Environment = new BuildEnvironment
                    {
                        ComputeType = ComputeType.SMALL,
                        Privileged = true,
                        BuildImage = LinuxBuildImage.FromCodeBuildImageId("aws/codebuild/standard:7.0")
                    },
                    
                });


            

            var codeCommit = new Repository(this, "myrepo", new RepositoryProps
            {
                RepositoryName = "myreponame",
                
               // Code = Code.FromAsset(sourceCodeAsset, "master"),

            });

            //  codeCommit.RepositoryCloneUrlHttp


            var artifactBucket = new Bucket(this, "mycodebucket123456", new BucketProps
            {
                //BucketName = "somecodebucketnamehere",

                Encryption = BucketEncryption.S3_MANAGED,
                //AutoDeleteObjects = true,

                RemovalPolicy = RemovalPolicy.DESTROY,

            });


            var codePipeline = new Pipeline(this, "mycodepipeline", new PipelineProps
            {
                
                ArtifactBucket = artifactBucket,
                Stages = new[]
                {
                    new Amazon.CDK.AWS.CodePipeline.StageProps
                    {
                        StageName = "SourceStage",
                        
                        Actions = new[]
                        {
                            new CodeCommitSourceAction(new CodeCommitSourceActionProps
                            {
                                //CodeBuildCloneOutput = true,
                                Trigger = CodeCommitTrigger.NONE,
                                Branch = "master",
                                ActionName = "SourceAction",
                                Repository = codeCommit,
                                Output = Artifact_.Artifact("SourceArtifact")
                            })
                        }
                    },
                    new Amazon.CDK.AWS.CodePipeline.StageProps
                    {
                        StageName = "BuildStage",
                        Actions = new[]
                        {
                            new CodeBuildAction(new CodeBuildActionProps
                            {
                                ActionName = "BuildCodeAction",
                                Project = buildProject,
                                Input = Artifact_.Artifact("SourceArtifact")
                            })
                        }
                    }
                }
            });

        }
    }

    



}