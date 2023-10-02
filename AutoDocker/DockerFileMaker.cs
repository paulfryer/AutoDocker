using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace AutoDocker;

// TODO: provide functions to make the docker files, then make the 

public static class DockerFileMaker

{
    // Consider automatically building a pipeline for a Solution?
    // upon checkin, iterate through each project, see if anything has changed. If so build it and deploy it.
    // You could do a Hash of all the files in the project folder. You could just store the last hash as an env variable or tag.
    public static string ComputeHash(Project project)
    {
        var md5 = MD5.Create();
        var ms = new MemoryStream();

        var projectNames = new List<string>();
        GetAllProjectNames(project, projectNames);

        foreach (var projectName in projectNames)
        {
            var projectFolder = $"{project.SolutionDirectory}/{projectName}";

            var projectDirectory = new DirectoryInfo(projectFolder);

            foreach (var file in projectDirectory.EnumerateFiles())
            {
                //Console.WriteLine(file.FullName);
                var bytes = Encoding.UTF8.GetBytes(file.OpenText().ReadToEnd());
                ms.Write(bytes);
            }
        }

        ms.Position = 0;
        var hash = md5.ComputeHash(ms);
        ms.Dispose();

        return Convert.ToBase64String(hash);
    }

    public static List<Project> GetProjectsInSolution(FileInfo solutionFile)
    {
        Console.WriteLine($"Trying to find projects in soluiton file: {solutionFile.FullName}");
        var projects = new List<Project>();
        var solutionSource = File.OpenText(solutionFile.FullName).ReadToEnd();
        var projectNames = new List<string>();
        LoadProjectName(solutionSource, projectNames);
        foreach (var projectName in projectNames)
            projects.Add(GetProject(solutionFile.Directory, projectName));
        return projects;
    }

    public static void LoadProjectName(string solutionSource, List<string> projectNames)
    {
        var projectStartToken = "Project(";
        var projectEndToken = "EndProject";
        if (solutionSource.Contains(projectStartToken))
        {
            var projectIndex = solutionSource.IndexOf(projectStartToken);
            var projectPart = solutionSource.Substring(projectIndex, solutionSource.Length - projectIndex);
            var parts = projectPart.Split('"');
            var projectName = parts[3];
            projectNames.Add(projectName);

            var projectEndIndex = solutionSource.IndexOf(projectEndToken) + projectEndToken.Length;
            solutionSource = solutionSource.Substring(projectEndIndex, solutionSource.Length - projectEndIndex);
            LoadProjectName(solutionSource, projectNames);
        }
    }

    public static FileInfo GetSolutionFile(DirectoryInfo directory)
    {
        foreach (var file in directory.EnumerateFiles())
            if (file.Name.EndsWith(".sln"))
                return file;
        throw new Exception("No solution file found in directory.");
    }

    // Next steps
    // 1. reference ECR, automatically create a Repository for the project if it doesn't exist.
    // 2. automatically create a Task in ECS if it doesn't exist. (could do this with a step function).
    // 3. The stepfunction could also force a new deployment to services that use the task that was updated.
    // The solution will also need 2 build projects, one for X86 and one for ARM.
    public static void Main(string[] args)
    {
        var solutionDirectoryLocation = ".";
        if (args.Length > 1)
            throw new Exception("Only 1 argument is expected: the solution directory");
        if (args.Length == 1)
            solutionDirectoryLocation = args[0];

        var solutionDirectory = new DirectoryInfo(solutionDirectoryLocation);

        var solutionFile = GetSolutionFile(solutionDirectory);
        var projects = GetProjectsInSolution(solutionFile);

        foreach (var project in projects.Where(p => p.OutputType == "Exe"))
        {
            var hash = ComputeHash(project);
            Console.WriteLine($"Hash: {hash}");

            var dockerSource = GetDockerSource(project);
            //Console.Write(dockerSource);


            using (var writer = new StreamWriter(project.DockerFileLocation))
            {
                writer.Write(dockerSource);
            }


            //Console.ReadKey();

            // TODO: Consider sending these in as command line args.
            var x86LinuxImageName = $"{project.ProjectName}-Linux-X86";
            var armLinuxImageName = $"{project.ProjectName}-Linux-ARM";
            var x86WindowsImageName = $"{project.ProjectName}-Windows-X86";
        }
    }


    public static string GetDockerSource(Project project)
    {
        var projectNames = new List<string>();
        GetAllProjectNames(project, projectNames);

        if (project.OutputType != "Exe")
            throw new Exception($"Only Exe project type is supported. Found: {project.OutputType}");
        if (project.TargetFramework != "net6.0")
            throw new Exception($"Only net6.0 target framework is supported. Found: {project.TargetFramework}");

        var baseImage = "mcr.microsoft.com/dotnet/runtime:6.0";
        var buildImage = "mcr.microsoft.com/dotnet/sdk:6.0";

        var sb = new StringBuilder();

        sb.AppendLine($"FROM {baseImage} AS base");
        sb.AppendLine("WORKDIR /app");
        sb.AppendLine();
        sb.AppendLine($"FROM {buildImage} AS build");
        sb.AppendLine("WORKDIR /src");
        foreach (var name in projectNames)
            sb.AppendLine($"COPY [\"{name}/{name}.csproj\", \"{name}/\"]");

        sb.Append(@"RUN dotnet restore ""{projectName}/{projectName}.csproj""
COPY . .
WORKDIR ""/src/{projectName}""
RUN dotnet build ""{projectName}.csproj"" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish ""{projectName}.csproj"" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT [""dotnet"", ""{projectName}.dll""]".Replace("{projectName}", project.ProjectName));


        var dockerSource = sb.ToString();

        return dockerSource;
    }

    public static void GetAllProjectNames(Project project, List<string> projectNames)
    {
        projectNames.Add(project.ProjectName);
        foreach (var referencedProject in project.ReferencedProjects)
            GetAllProjectNames(referencedProject, projectNames);
    }


    public static Project GetProject(DirectoryInfo solutionDirectory, string projectName)
    {
        Console.WriteLine($"Looking for projects in solutionDirectory: {solutionDirectory}");
        var project = new Project { ProjectName = projectName, SolutionDirectory = solutionDirectory };

        var xml = File.OpenText($"{solutionDirectory.FullName}/{projectName}/{projectName}.csproj").ReadToEnd();

        var xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(xml);

        foreach (XmlNode projectNode in xmlDoc.ChildNodes)
        foreach (XmlNode childNode in projectNode.ChildNodes)
            if (childNode.Name == "PropertyGroup")
            {
                var outputTypeNode = childNode.SelectSingleNode("./OutputType");
                if (outputTypeNode == null)
                    project.OutputType = "Lib";
                else
                    project.OutputType = outputTypeNode.InnerText;
                project.TargetFramework = childNode.SelectSingleNode("./TargetFramework").InnerText;
            }
            else
            {
                foreach (XmlNode itemNode in childNode.ChildNodes)
                    if (itemNode.Name == "ProjectReference")
                    {
                        var referencedProjectLocation = itemNode.Attributes["Include"].Value;
                        var locationParts = referencedProjectLocation.Split('\\');
                        var lastPart = locationParts[locationParts.Length - 1];
                        var referencedProjectName = lastPart.Replace(".csproj", string.Empty);
                        project.ReferencedProjects.Add(GetProject(solutionDirectory, referencedProjectName));
                    }
            }

        return project;
    }
}

public class Project
{
    public List<Project> ReferencedProjects = new();

    public string ProjectName { get; set; }
    public string TargetFramework { get; set; }
    public string OutputType { get; set; }

    public DirectoryInfo SolutionDirectory { get; set; }

    public string ProjectDirectory => $"{SolutionDirectory}\\{ProjectName}";

    public string DockerFileLocation => $"{ProjectDirectory}\\Dockerfile";
}