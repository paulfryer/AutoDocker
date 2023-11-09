using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NuGet.Packaging;
using NuGet.Versioning;

public static class Extensions
{
    public static async Task BuildAndPublishPackage(this Smithy smithy, string language, Version version, string domain,
        string repositoryName)
    {
        var packageFileName = smithy.BuildCodePackage(language, version);
        await smithy.PublishPackage(language, packageFileName, domain, repositoryName);
    }

    public static async Task PublishPackage(this Smithy smithy, string language, string packageFileName, string domain,
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

    [Obsolete]
    public static string BuildCodePackage(this Smithy smithy, string language, Version newVersion)
    {
        var sourceCode = smithy.GenerateSourceCode(language);

        if (language != "C#") throw new NotImplementedException(language);


        BuildDotNetProject(smithy, sourceCode);


        var outputPath = ".";
        var packageId = smithy.Name;
        var version =
            new NuGetVersion(
                $"{newVersion.Major}.{newVersion.Minor}.{newVersion.Build}"); // Get this from previous build...
        var description = $"{smithy.Name} generated Nuget Package.";


        var packageBuilder = new PackageBuilder
        {
            Id = packageId,
            Version = version,
            Description = description
        };

        packageBuilder.Authors.Add("Build Server");

        // Define package dependencies

        /*
        var dependencies = new PackageDependencyGroup(
            NuGetFramework.Parse("net6.0"),
            new List<PackageDependency>
            {
                new PackageDependency("System.Runtime", VersionRange.Parse("6.0.0"))
            }
        );
        packageBuilder.DependencyGroups.Add(dependencies);
        */

        // Set Source Link
        // packageBuilder.packa = new Uri("https://example.com/source-link-repository");


        // Set Compiler Flags
        //packageBuilder.TargetFrameworks.Add(NuGetFramework.Parse("net6.0")); // = "Release";
        //  packageBuilder.TargetFrameworks.Add(NuGetFramework.AnyFramework);


        // Define package dependencies
        /*
        var dependencies = new List<PackageDependencyGroup>
        {
            new PackageDependencyGroup(NuGetFramework.AnyFramework,
                new List<PackageDependency>
                {
                    new PackageDependency("System.Private.CoreLib", VersionRange.Parse("6.0.0.0")),
                   // new PackageDependency("DependencyPackage2", VersionRange.Parse("2.0.0"))
                })
        };
        packageBuilder.DependencyGroups.AddRange(dependencies);
        */


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

    // Helper method to add the [assembly: AssemblyVersion] attribute to the syntax tree
    private static SyntaxTree AddAssemblyVersionAttribute(SyntaxTree syntaxTree, Version version)
    {
        var root = syntaxTree.GetRoot();

        // Create the [assembly: AssemblyVersion] attribute with the specified version
        var assemblyVersionAttribute = SyntaxFactory.Attribute(
            SyntaxFactory.ParseName("System.Reflection.AssemblyVersion"),
            SyntaxFactory.AttributeArgumentList(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.AttributeArgument(
                        SyntaxFactory.LiteralExpression(
                            SyntaxKind.StringLiteralExpression,
                            SyntaxFactory.Literal(version.ToString()))))));

        // Add the attribute to the compilation unit
        var compilationUnit = (CompilationUnitSyntax)root;
        compilationUnit = compilationUnit.AddAttributeLists(
            SyntaxFactory.AttributeList(
                SyntaxFactory.SingletonSeparatedList(assemblyVersionAttribute)));

        return syntaxTree.WithRootAndOptions(compilationUnit, syntaxTree.Options);
    }

    public static string GenerateSourceCode(this Smithy smithy, string language)
    {
        if (language != "C#") throw new NotImplementedException(language);


        // Create a compilation unit
        var root = SyntaxFactory.CompilationUnit();


        // Add using statements
        root = root.AddUsings(
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System")),
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Threading.Tasks"))
        ); // Add using for System.Threading.Tasks


        var namespaceDeclaration = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName(smithy.Namespace));


        foreach (var service in smithy.Services)
        {
            // Create the namespace
            // var namespaceDeclaration = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName(service.Namespace));

            // Create the interface
            var interfaceDeclaration = SyntaxFactory.InterfaceDeclaration($"I{service.Name}Service")
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword));


            foreach (var operation in service.OperationsOLD)
            {
                // Create the method declaration
                var methodDeclaration = SyntaxFactory.MethodDeclaration(
                        SyntaxFactory.GenericName(SyntaxFactory.Identifier("Task"))
                            .WithTypeArgumentList(SyntaxFactory.TypeArgumentList(
                                SyntaxFactory.SingletonSeparatedList(
                                    SyntaxFactory.ParseTypeName(operation.OutputOLD.Name)))),
                        operation.Name)
                    .WithParameterList(
                        SyntaxFactory.ParameterList(
                            SyntaxFactory.SingletonSeparatedList(
                                SyntaxFactory.Parameter(
                                        SyntaxFactory.Identifier("input"))
                                    .WithType(SyntaxFactory.ParseTypeName(operation.InputOLD.Name)))))
                    .WithSemicolonToken(
                        SyntaxFactory.Token(SyntaxKind.SemicolonToken)); // Add a semicolon to indicate no method body

                // Add the method to the interface
                interfaceDeclaration = interfaceDeclaration.AddMembers(methodDeclaration);


                // Create the input class
                var inputClass = SyntaxFactory.ClassDeclaration(operation.InputOLD.Name)
                    .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword));

                foreach (var inputMember in operation.InputOLD.MembersOLD)
                {
                    var typeName = inputMember.Value.Name;

                    inputClass = inputClass.AddMembers(
                        SyntaxFactory.PropertyDeclaration(
                                SyntaxFactory.ParseTypeName(typeName), inputMember.Key)
                            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                            .WithAccessorList(
                                SyntaxFactory.AccessorList(
                                    SyntaxFactory.List(new[]
                                    {
                                        SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                                            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
                                        SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                                            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
                                    })
                                )
                            ));
                }


                // output class
                var outputClass = SyntaxFactory.ClassDeclaration(operation.OutputOLD.Name)
                    .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword));

                foreach (var outputMember in operation.OutputOLD.MembersOLD)
                {
                    var typeName = outputMember.Value.Name;

                    outputClass = outputClass.AddMembers(
                        SyntaxFactory.PropertyDeclaration(
                                SyntaxFactory.ParseTypeName(typeName), outputMember.Key)
                            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                            .WithAccessorList(
                                SyntaxFactory.AccessorList(
                                    SyntaxFactory.List(new[]
                                    {
                                        SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                                            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
                                        SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                                            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
                                    })
                                )
                            ));
                }


                // Add the classes to the namespace
                namespaceDeclaration = namespaceDeclaration.AddMembers(inputClass, outputClass);
            }

            namespaceDeclaration = namespaceDeclaration.AddMembers(interfaceDeclaration);
        }

        // Add the namespace to the compilation unit
        root = root.AddMembers(namespaceDeclaration);
        // Convert the compilation unit to a string
        var generatedCode = root.NormalizeWhitespace().ToFullString();

        Console.WriteLine(generatedCode);

        return generatedCode;
    }

    public static void BuildDotNetProject(Smithy smithy, string sourceCode)
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

            // Generate the project file content for a library
            var projectFileContent = $@"
                <Project Sdk=""Microsoft.NET.Sdk"">
                    <PropertyGroup>
                        <TargetFramework>net6.0</TargetFramework>
                        <OutputType>Library</OutputType>
                        <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
                    </PropertyGroup>
                    <ItemGroup>
                        <Compile Include=""{smithy.Name}.cs"" />
                    </ItemGroup>
                </Project>
            ";

            // Save the project file (csproj)
            var projectFilePath = Path.Combine(projectDirectory, $"{smithy.Name}.csproj");
            File.WriteAllText(projectFilePath, projectFileContent, Encoding.UTF8);

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