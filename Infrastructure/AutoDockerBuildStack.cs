using Amazon.CDK;
using Amazon.CDK.AWS.CodeBuild;
using Amazon.CDK.AWS.CodeCommit;
using Amazon.CDK.AWS.CodePipeline;
using Amazon.CDK.AWS.CodePipeline.Actions;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.S3.Assets;
using Amazon.Runtime.Internal.Transform;

namespace AutoDocker
{
    public class AutoDockerBuildStack : Stack
    {
        public AutoDockerBuildStack(string account, string solutionName, List<string> projectNames, App scope, string id, IStackProps? props = null) : base(scope, id, props)
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
                    RepositoryName = $"{solutionName.ToLower()}/{projectName.ToLower()}",
                    RemovalPolicy = RemovalPolicy.DESTROY
                }) ;

            }



            var buildProject = new PipelineProject(this, "myproject",
                new PipelineProjectProps
                {

                    BuildSpec = BuildSpec.FromObject(new Dictionary<string, object>
                    {
                        { "version", "0.2" },
                        {"phases", new Dictionary<string, object>
                        {
                            {"build", new Dictionary<string, object>
                            {
                                {"commands", new string[]{
                                    "unzip source.zip",
                                    "cp $PROJECT_NAME/Dockerfile Dockerfile",
                                    "aws ecr get-login-password --region $AWS_REGION | docker login --username AWS --password-stdin $ACCOUNT.dkr.ecr.$AWS_REGION.amazonaws.com",
                                    "docker build -t $REPO_NAME .",
                                    "docker tag $REPO_NAME:latest $ACCOUNT.dkr.ecr.$AWS_REGION.amazonaws.com/$REPO_NAME:latest",
                                    "docker push $ACCOUNT.dkr.ecr.$AWS_REGION.amazonaws.com/$REPO_NAME:latest"
                                }
                            } }
                        } }
                        }
                    }),
                    Environment = new BuildEnvironment
                    {
                        ComputeType = ComputeType.SMALL,
                        Privileged = true,
                        BuildImage = LinuxBuildImage.FromCodeBuildImageId("aws/codebuild/standard:7.0")
                    },
                    
                });

            buildProject.Role.AddManagedPolicy(ManagedPolicy.FromAwsManagedPolicyName("EC2InstanceProfileForImageBuilderECRContainerBuilds"));


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

            var buildStage = new Amazon.CDK.AWS.CodePipeline.StageProps
            {
                StageName = "BuildStage",
            };

            List<IAction> buildStageActions = new List<IAction>();
            foreach (var project in projectNames)
            {
                buildStageActions.Add(
                    new CodeBuildAction(new CodeBuildActionProps
                    {
                        ActionName = $"Build{project}",
                        Project = buildProject,
                        Input = Artifact_.Artifact("SourceArtifact"),
                        EnvironmentVariables = new Dictionary<string, IBuildEnvironmentVariable>
                                {
                                    {"ACCOUNT", new BuildEnvironmentVariable{Type = BuildEnvironmentVariableType.PLAINTEXT,
                                        Value = account} },
                                    {"REPO_NAME", new BuildEnvironmentVariable{ Type = BuildEnvironmentVariableType.PLAINTEXT,
                                        Value = $"{solutionName.ToLower()}/{project.ToLower()}" } },
                                    {"PROJECT_NAME", new BuildEnvironmentVariable{Type = BuildEnvironmentVariableType.PLAINTEXT,
                                        Value = project } }

                                }
                    }));
            }
            buildStage.Actions = buildStageActions.ToArray();

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
                      buildStage
                }
            });



            
        }
    }




}