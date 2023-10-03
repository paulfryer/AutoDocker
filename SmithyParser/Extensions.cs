using System.Diagnostics;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NuGet.Packaging;
using NuGet.Versioning;

public static class Extensions
{
    public static async Task BuildAndPublishPackage(this Smithy smithy, string language)
    {
        var packageFileName = smithy.BuildCodePackage(language);
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
        var apiKey = "your-api-key"; // Your NuGet API key
        //string sourceUrl = "https://nuget.example.com/nuget"; // URL of your NuGet server

        // Define the NuGet CLI command
        var nugetCommand = $"nuget push \"{packagePath}\" -Source {nugetUrl} -ApiKey {apiKey}";

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

    public static string BuildCodePackage(this Smithy smithy, string language)
    {
        var sourceCode = smithy.GenerateSourceCode(language);

        if (language != "C#") throw new NotImplementedException(language);

        // Create a Roslyn syntax tree from the source code
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

        // Create a compilation with the syntax tree and necessary references
        MetadataReference[] references =
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location)
        };

        var compilation = CSharpCompilation.Create(
            "MyLibrary.dll", // Output assembly name
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)); // DLL output

        // Create a memory stream to store the compiled DLL
        using (var ms = new MemoryStream())
        {
            var result = compilation.Emit(ms);

            if (result.Success)
            {
                // Save the compiled DLL to a file
                File.WriteAllBytes("MyLibrary.dll", ms.ToArray());
                Console.WriteLine("MyLibrary.dll has been created.");
            }
            else
            {
                Console.WriteLine("Compilation failed:");

                foreach (var diagnostic in result.Diagnostics.Where(diagnostic =>
                             diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error))
                    Console.WriteLine(diagnostic);
            }
        }


        // Now build the Nuget package.

        var outputPath = ".";
        var packageId = "MyPackage";
        var version = new NuGetVersion("1.0.0");
        var authors = new HashSet<string> { "Your Name" };
        var description = "A NuGet package generated programmatically.";


        var packageBuilder = new PackageBuilder
        {
            Id = packageId,
            Version = version,
            Description = description
        };

        packageBuilder.Authors.Add("Your name");

        // Define package contents
        var packageContents = new List<ManifestFile>
        {
            new()
            {
                Source = "MyLibrary.dll",
                Target = $"{Path.GetFileName("MyLibrary.dll")}"
            }
        };

        packageBuilder.PopulateFiles(".", packageContents);

        var packageFileName = $"{packageBuilder.Id}.{packageBuilder.Version}.nupkg";
        var packagePath = Path.Combine(outputPath, packageFileName);

        using (var packageStream = File.Create(packagePath))
        {
            packageBuilder.Save(packageStream);
        }

        Console.WriteLine($"NuGet package saved to: {packagePath}");

        return packageFileName;
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