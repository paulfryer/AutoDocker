using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using SmithyParser.CodeGen;
using SmithyParser.Models;

public static class Extensions
{
    public static async Task BuildAndPublishPackage(this SmithyModel smithy, string language, Version version,
        string domain,
        string repositoryName)
    {
        var packageFileName = smithy.BuildCodePackage(language, version);
        await smithy.PublishPackage(language, packageFileName, domain, repositoryName);
    }

    public static async Task PublishPackage(this SmithyModel smithy, string language, string packageFileName,
        string domain,
        string repositoryName)
    {
        if (language != "C#") throw new NotImplementedException(language);

        var sts = new AmazonSecurityTokenServiceClient();
        var identity = await sts.GetCallerIdentityAsync(new GetCallerIdentityRequest());
        var nugetUrl =
            $"https://{domain}-{identity.Account}.d.codeartifact.{sts.Config.RegionEndpoint.SystemName}.amazonaws.com/nuget/{repositoryName}/v3/index.json";


        var packagePath = $"{packageFileName}"; // Path to your NuGet package

        // Define the NuGet CLI command
        var nugetCommand = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? $"nuget push \"{packagePath}\" -Source {nugetUrl}"
            : $"mono /usr/local/bin/nuget.exe push \"{packagePath}\" -Source {nugetUrl}";

        Console.WriteLine("About to run the following nuget command:");
        Console.WriteLine(nugetCommand);

        // Create a process to run the NuGet CLI command
        var psi = new ProcessStartInfo
        {
            FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd" : "/bin/bash",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (var process = new Process { StartInfo = psi })
        {
            process.Start();

            // Write the NuGet CLI command to the standard input
            process.StandardInput.WriteLine(nugetCommand);
            process.StandardInput.Flush();
            process.StandardInput.Close();

            // Read the output and error streams
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();

            process.WaitForExit();

            Console.WriteLine("NuGet CLI OutputOLD:");
            Console.WriteLine(output);

            if (!string.IsNullOrWhiteSpace(error))
            {
                Console.WriteLine("NuGet CLI Error:");
                Console.WriteLine(error);
            }

            Console.WriteLine("Package publishing process completed.");
        }
    }

    public static string BuildCodePackage(this SmithyModel smithy, string language, Version newVersion)
    {
        var csharpGenerator = new CSharpCodeGenerator();
        var sourceCode = csharpGenerator.GenerateCode(smithy);

        Console.WriteLine("===== START GENERATED CODE =====");
        Console.Write(sourceCode);
        Console.WriteLine("===== END GENERATED CODE =====");

        if (language != "C#") throw new NotImplementedException(language);


        BuildDotNetProject(smithy, sourceCode, newVersion);


        var outputPath = ".";
        var packageId = $"{smithy.Name}";
        var version =
            new NuGetVersion(
                $"{newVersion.Major}.{newVersion.Minor}.{newVersion.Build}");
        var description = $"{smithy.Name} generated Nuget Package.";


        var packageBuilder = new PackageBuilder
        {
            Id = packageId,
            Version = version,
            Description = description
        };

        packageBuilder.Authors.Add("Build Server");

        

        var packageContents = new List<ManifestFile>
        {
            new()
            {
                Exclude = "False",
                Source = $"dynamic-directory\\bin\\Release\\net6.0\\{smithy.Name}.dll",
                Target = $"lib\\net6.0\\{smithy.Name}.dll"
            }
        };

        packageBuilder.PopulateFiles(".", packageContents);


        if (smithy.Using.Any())
        {
            var packageDependencies = smithy.Using.Select(usedModel => 
                new PackageDependency(usedModel.Key, 
                    new VersionRange(new 
                        NuGetVersion(usedModel.Value.Major, usedModel.Value.Minor, usedModel.Value.Build)))).ToList();

            // Add a dependency
            var dependencySet = new PackageDependencyGroup(
                NuGetFramework.Parse("net6.0"),
                packageDependencies
            );
            packageBuilder.DependencyGroups.Add(dependencySet);
        }

        // remove invalid characters that show in linux env.
        packageBuilder.Id = packageBuilder.Id.Replace("./", string.Empty);

        var packageFileName = $"{packageBuilder.Id}.{packageBuilder.Version}.nupkg";
        var packagePath = Path.Combine(outputPath, packageFileName);

        using (var packageStream = File.Create(packagePath))
        {
            packageBuilder.Save(packageStream);
        }

        Console.WriteLine($"NuGet package saved to: {packagePath}");

        return packageFileName;
    }

    public static void BuildDotNetProject(SmithyModel smithy, string sourceCode, Version version)
    {
        try
        {
            if (Directory.Exists("dynamic-build"))
                Directory.Delete("dynamic-build");

            Directory.CreateDirectory("dynamic-directory");


            // Specify the path to your project directory
            var projectDirectory = Path.GetFullPath(@"dynamic-directory");


            // Save the source code to a file
            var sourceFilePath = Path.Combine(projectDirectory, $"{smithy.Name}.cs");
            File.WriteAllText(sourceFilePath, sourceCode, Encoding.UTF8);

            string includeXml = "";
            foreach (var usedModel in smithy.Using)
                includeXml += $"<PackageReference Include=\"{usedModel.Key}\" Version=\"{usedModel.Value.Major}.{usedModel.Value.Minor}.{usedModel.Value.Build}\" />";

            string includeMvcXml = "";
            if (smithy.Services.Any(s => s.Namespace == smithy.Name))
                includeMvcXml = "<PackageReference Include=\"Microsoft.AspNetCore.Mvc\" Version=\"2.2.0\" />";

            // Generate the project file content for a library
            var projectFileContent = $@"
                <Project Sdk=""Microsoft.NET.Sdk"">
                    <PropertyGroup>
                        <AssemblyVersion>{version.Major}.{version.Minor}.{version.Build}.{version.Revision}</AssemblyVersion>
                        <TargetFramework>net6.0</TargetFramework>
                        <OutputType>Library</OutputType>
                        <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
                    </PropertyGroup>
                    <ItemGroup>
                        <Compile Include=""{smithy.Name}.cs"" />
                        {includeMvcXml}
                        {includeXml}
                    </ItemGroup>
                </Project>
            ";

            // Save the project file (csproj)
            var projectFilePath = Path.Combine(projectDirectory, $"{smithy.Name}.csproj");
            File.WriteAllText(projectFilePath, projectFileContent, Encoding.UTF8);



            // NUGET RESTORE......

            // Create a new process start info
            var processStartInfo1 = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"restore --no-cache \"{projectFilePath}\"",
                WorkingDirectory = projectDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Start the process
            using (var process = Process.Start(processStartInfo1))
            {
                // Read the output (standard and error)
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();

                // Wait for the process to exit
                process.WaitForExit();

                // Handle the results
                Console.WriteLine("Output:");
                Console.WriteLine(output);
                if (!string.IsNullOrEmpty(error))
                {
                    Console.WriteLine("Error:");
                    Console.WriteLine(error);
                }
            }









            // Build the library using dotnet
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"build -c Release \"{projectFilePath}\"",
                WorkingDirectory = projectDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = new Process())
            {
                process.StartInfo = processStartInfo;
                process.OutputDataReceived += (sender, e) => Console.WriteLine(e.Data);
                process.ErrorDataReceived += (sender, e) => Console.WriteLine(e.Data);

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                process.WaitForExit();

                if (process.ExitCode == 0)
                    Console.WriteLine("Build succeeded.");
                else
                    Console.WriteLine($"Build failed with exit code {process.ExitCode}.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }
}