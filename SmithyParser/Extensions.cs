using System.Diagnostics;
using System.Reflection;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NuGet.Packaging;
using NuGet.Versioning;

public static class Extensions
{
    public static async Task BuildAndPublishPackage(this Smithy smithy, string language, Version version)
    {
        var packageFileName = smithy.BuildCodePackage(language, version);
        await smithy.PublishPackage(language, packageFileName);
    }

    public static async Task PublishPackage(this Smithy smithy, string language, string packageFileName)
    {
        if (language != "C#") throw new NotImplementedException(language);

        var sts = new AmazonSecurityTokenServiceClient();
        var identity = await sts.GetCallerIdentityAsync(new GetCallerIdentityRequest());


        var domain = "services";
        var repositoryName = "Services";

        var nugetUrl =
            $"https://{domain}-{identity.Account}.d.codeartifact.{sts.Config.RegionEndpoint.SystemName}.amazonaws.com/nuget/{repositoryName}/v3/index.json";


        var packagePath = $"{packageFileName}"; // Path to your NuGet package

        // Define the NuGet CLI command
        var nugetCommand = $"nuget push \"{packagePath}\" -Source {nugetUrl}";

        // Create a process to run the NuGet CLI command
        var psi = new ProcessStartInfo
        {
            FileName = "cmd",
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

            Console.WriteLine("NuGet CLI Output:");
            Console.WriteLine(output);

            if (!string.IsNullOrWhiteSpace(error))
            {
                Console.WriteLine("NuGet CLI Error:");
                Console.WriteLine(error);
            }

            Console.WriteLine("Package publishing process completed.");
        }
    }

    public static string BuildCodePackage(this Smithy smithy, string language, Version newVersion)
    {
        var sourceCode = smithy.GenerateSourceCode(language);

        if (language != "C#") throw new NotImplementedException(language);

        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        syntaxTree = AddAssemblyVersionAttribute(syntaxTree, newVersion);

        var compilationOptions = new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary,
            assemblyIdentityComparer: DesktopAssemblyIdentityComparer.Default);

        var compilation = CSharpCompilation.Create(
            assemblyName: smithy.Name, 
            new[] { syntaxTree },   
            new[] { MetadataReference.CreateFromFile(typeof(object).GetTypeInfo().Assembly.Location) }, // References
            compilationOptions);

        using (var ms = new MemoryStream())
        {
            var result = compilation.Emit(ms);
            if (result.Success)
            {
                File.WriteAllBytes($"{smithy.Name}.dll", ms.ToArray());
                Console.WriteLine($"{smithy.Name}.dll has been created.");
            }
            else
            {
                Console.WriteLine("Compilation failed:");

                foreach (var diagnostic in result.Diagnostics.Where(diagnostic =>
                             diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error))
                    Console.WriteLine(diagnostic);
            }
        }

        var outputPath = ".";
        var packageId = smithy.Name;
        var version = new NuGetVersion($"{newVersion.Major}.{newVersion.Minor}.{newVersion.Revision}"); // Get this from previous build...
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
                Source = $"{smithy.Name}.dll",
                Target = $"{Path.GetFileName($"{smithy.Name}.dll")}"
            }
        };

        packageBuilder.PopulateFiles(".", packageContents);

        var packageFileName = $"{packageBuilder.Id}.{packageBuilder.Version}.nupkg";
        var packagePath = Path.Combine(outputPath, packageFileName);

        using (var packageStream = File.Create(packagePath)) packageBuilder.Save(packageStream);

        Console.WriteLine($"NuGet package saved to: {packagePath}");

        return packageFileName;
    }

    // Helper method to add the [assembly: AssemblyVersion] attribute to the syntax tree
    static SyntaxTree AddAssemblyVersionAttribute(SyntaxTree syntaxTree, Version version)
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
            SyntaxFactory.UsingDirective(
                SyntaxFactory.ParseName("System.Threading.Tasks"))); // Add using for System.Threading.Tasks

        foreach (var service in smithy.Services)
        {
            // Create the namespace
            var namespaceDeclaration = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName(service.Namespace));

            // Create the interface
            var interfaceDeclaration = SyntaxFactory.InterfaceDeclaration($"I{service.Name}Service")
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword));


            foreach (var operation in service.Operations)
            {
                // Create the method declaration
                var methodDeclaration = SyntaxFactory.MethodDeclaration(
                        SyntaxFactory.GenericName(SyntaxFactory.Identifier("Task"))
                            .WithTypeArgumentList(SyntaxFactory.TypeArgumentList(
                                SyntaxFactory.SingletonSeparatedList(
                                    SyntaxFactory.ParseTypeName(operation.Output.Name)))),
                        operation.Name)
                    .WithParameterList(
                        SyntaxFactory.ParameterList(
                            SyntaxFactory.SingletonSeparatedList(
                                SyntaxFactory.Parameter(
                                        SyntaxFactory.Identifier("input"))
                                    .WithType(SyntaxFactory.ParseTypeName(operation.Input.Name)))))
                    .WithSemicolonToken(
                        SyntaxFactory.Token(SyntaxKind.SemicolonToken)); // Add a semicolon to indicate no method body

                // Add the method to the interface
                interfaceDeclaration = interfaceDeclaration.AddMembers(methodDeclaration);


                // Create the input class
                var inputClass = SyntaxFactory.ClassDeclaration(operation.Input.Name)
                    .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword));

                foreach (var inputMember in operation.Input.Members)
                    inputClass = inputClass.AddMembers(
                        SyntaxFactory.PropertyDeclaration(
                                SyntaxFactory.ParseTypeName(inputMember.Value.Name), inputMember.Key)
                            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                            .WithAccessorList(
                                SyntaxFactory.AccessorList(
                                    SyntaxFactory.SingletonList(
                                        SyntaxFactory.AccessorDeclaration(
                                                SyntaxKind.GetAccessorDeclaration)
                                            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)))
                                )
                            ));

                // output class
                var outputClass = SyntaxFactory.ClassDeclaration(operation.Output.Name)
                    .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword));

                foreach (var outputMember in operation.Output.Members)
                    outputClass = outputClass.AddMembers(
                        SyntaxFactory.PropertyDeclaration(
                                SyntaxFactory.ParseTypeName(outputMember.Value.Name), outputMember.Key)
                            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                            .WithAccessorList(
                                SyntaxFactory.AccessorList(
                                    SyntaxFactory.SingletonList(
                                        SyntaxFactory.AccessorDeclaration(
                                                SyntaxKind.GetAccessorDeclaration)
                                            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)))
                                )
                            ));

                // Add the interface and classes to the namespace
                namespaceDeclaration = namespaceDeclaration.AddMembers(inputClass,
                    outputClass);
            }

            namespaceDeclaration = namespaceDeclaration.AddMembers(interfaceDeclaration);
            // Add the namespace to the compilation unit
            root = root.AddMembers(namespaceDeclaration);
        }


        // Convert the compilation unit to a string
        var generatedCode = root.NormalizeWhitespace().ToFullString();

        Console.WriteLine(generatedCode);

        return generatedCode;
    }
}