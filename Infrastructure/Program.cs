


using Amazon.Auth.AccessControlPolicy.ActionIdentifiers;
using Amazon.CDK;
using Amazon.CDK.AWS.CodeBuild;
using Amazon.CDK.AWS.CodeCommit;
using Amazon.CDK.AWS.CodePipeline;
using Amazon.CDK.AWS.CodePipeline.Actions;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.S3.Assets;
using Amazon.CDK.CloudAssembly.Schema;
using System.Linq;

namespace AutoDocker
{

    public class AutoDockerBuildStack : Stack
    {
        public AutoDockerBuildStack(App scope, string id, IStackProps? props = null) : base(scope, id, props)
        {


            var buildProject = new PipelineProject(this, "myproject",
                new PipelineProjectProps
                {

                    BuildSpec = BuildSpec.FromSourceFilename("buildspec.yml"),
                    Environment = new BuildEnvironment
                    {
                        ComputeType = ComputeType.SMALL,
                        Privileged = true,
                        BuildImage = LinuxBuildImage.FromCodeBuildImageId("aws/codebuild/standard:7.0")
                    }
                });

            var codeCommit = new Repository(this, "myrepo", new RepositoryProps
            {
                RepositoryName = "myreponame",
                
                Code = Code.FromZipFile(".", "master"),
                
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
            }) ;

        }
    }

    public class DeployStage : Stage
    {
        public DeployStage(Constructs.Construct scope, string id, Amazon.CDK.IStageProps? props = null) : base(scope, id, props)
        {
        }

        
    }

    public static class Program
    {
        public static async Task Main(string[] args)
        {





            var app = new App();
            var stack = new AutoDockerBuildStack(app, "myautodockerapp");


            

            await app.DeployAsync();

        }
    }

    



}