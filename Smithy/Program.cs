using System;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using Amazon.CodeArtifact;
using Amazon.CodeArtifact.Model;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Newtonsoft.Json;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

partial class Program
{
    static async Task Main()
    {
        //Extract();
        var smithy = BuildSmithy();

        var sourceCode = GenerateCode(smithy);

        BuildGeneratedCode(sourceCode);
        var nugetFileName = CreateNuGetPackage();
        //await PublishNugetPackage(nugetFileName);


        var sts = new AmazonSecurityTokenServiceClient();
        var identity = await sts.GetCallerIdentityAsync(new GetCallerIdentityRequest());


        var domain = "services";
        var repositoryName = "Services";

        var nugetUrl = $"https://{domain}-{identity.Account}.d.codeartifact.{sts.Config.RegionEndpoint.SystemName}.amazonaws.com/nuget/{repositoryName}/v3/index.json";

        //await PublishNugetPackageViaNuget(nugetUrl, nugetFileName);
        PublishNugetPackageViaCLI(nugetUrl, nugetFileName);

    }

    static void GenerateSourceCode()
    {

    }

    static void BuildGeneratedCode(string sourceCode)
    {

        // Create a Roslyn syntax tree from the source code
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

        // Create a compilation with the syntax tree and necessary references
        MetadataReference[] references = new MetadataReference[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
        };

        CSharpCompilation compilation = CSharpCompilation.Create(
            "MyLibrary.dll", // Output assembly name
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)); // DLL output

        // Create a memory stream to store the compiled DLL
        using (MemoryStream ms = new MemoryStream())
        {
            EmitResult result = compilation.Emit(ms);

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
                {
                    Console.WriteLine(diagnostic);
                }
            }
        }
    }

    static void PublishNugetPackageViaCLI(string nugetUrl, string nugetFileName)
    {
        string packagePath = $"{nugetFileName}"; // Path to your NuGet package
        string apiKey = "your-api-key"; // Your NuGet API key
        //string sourceUrl = "https://nuget.example.com/nuget"; // URL of your NuGet server

        // Define the NuGet CLI command
        string nugetCommand = $"nuget push \"{packagePath}\" -Source {nugetUrl} -ApiKey {apiKey}";

        // Create a process to run the NuGet CLI command
        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = "cmd",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using (Process process = new Process { StartInfo = psi })
        {
            process.Start();

            // Write the NuGet CLI command to the standard input
            process.StandardInput.WriteLine(nugetCommand);
            process.StandardInput.Flush();
            process.StandardInput.Close();

            // Read the output and error streams
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

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

    static async Task PublishNugetPackageViaNuget(string sourceUrl, string nugetFileName)
    {
        string packagePath = $"{nugetFileName}"; // Path to your NuGet package
        string apiKey = "your-api-key"; // Your NuGet API key
       // string sourceUrl = "https://nuget.example.com/nuget"; // URL of your NuGet server

        ILogger logger = NullLogger.Instance; // Use NullLogger as a simpler alternative
        //SourceCacheContext sourceCacheContext = new SourceCacheContext();

        PackageSource packageSource = new PackageSource(sourceUrl);
        SourceRepository sourceRepository = Repository.Factory.GetCoreV3(packageSource);

        var resource = await sourceRepository.GetResourceAsync<PackageUpdateResource>();

        using (var sourceCacheContext = new SourceCacheContext())
        {
            var packageUpdateResource = await sourceRepository.GetResourceAsync<PackageUpdateResource>();
            var packageBytes = File.ReadAllBytes(packagePath);

  

            await packageUpdateResource.Push(
                packagePath: packagePath,
                symbolSource: null,
                timeoutInSecond: 600,
                disableBuffering: false,
                getApiKey: (a) => null,
                log: logger,
                getSymbolApiKey: s => null,
                noServiceEndpoint: false,
                skipDuplicate: false,
                symbolPackageUpdateResource: null);

            Console.WriteLine("Package published successfully.");
        }

    }

    static async Task PublishNugetPackageViaCodeArtifiactAPI(string account, string nugetFileName)
    {
        var codeArtifact = new AmazonCodeArtifactClient();


        var publishResult = await codeArtifact.PublishPackageVersionAsync(new PublishPackageVersionRequest
        {
            Package = "PackageName",
            Format = PackageFormat.Nuget,
            Domain = "services",
            DomainOwner = account,
            Repository = "Services",
            Namespace = "defaultnamespace",
            AssetName = "AssetName",
            PackageVersion = "1.0.1", // TODO: Get the current version then bump it.
            AssetContent = new MemoryStream(File.ReadAllBytes(nugetFileName))
        });

    }



    static string CreateNuGetPackage()
    {
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
            new ManifestFile
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





    static void Extract()
    {
       string zipFilePath = "example-service/build.zip";
        string extractPath = "example-service";

        try
        {
            // Create the directory if it doesn't exist
            Directory.CreateDirectory(extractPath);

            // Extract the zip file
            ZipFile.ExtractToDirectory(zipFilePath, extractPath);

            Console.WriteLine("Zip file extracted successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    static Smithy BuildSmithy()
    {
        var buildJson = File.ReadAllText("example-service/build/smithy/source/build-info/smithy-build-info.json");
        var modelJson = File.ReadAllText("example-service/build/smithy/source/model/model.json");


        var b = JsonConvert.DeserializeObject<dynamic>(buildJson);
        var m = JsonConvert.DeserializeObject<dynamic>(modelJson);

        var info = new SmithyBuildInfo();

        foreach (var o in b.operationShapeIds) info.OperationShapeIds.Add((string)o);
        foreach (var r in b.resourceShapeIds) info.ResourceShapeIds.Add((string)r);
        foreach(var s in b.serviceShapeIds) info.ServiceShapeIds.Add((string)s);

        var smithy = new Smithy
        {
            Version = m.smithy
        };


        foreach (var serviceShapeId in info.ServiceShapeIds)
        {
            var s = m.shapes[serviceShapeId];

            var service = new Service(serviceShapeId);

            foreach (var o in s.operations)
            {
                var target = (string)o.target;
                var operationObj = m.shapes[target];

                var operation = new Operation(target);

                var inputShapeId = (string)operationObj.input.target;
                var outputShapeId = (string)operationObj.output.target;
                var inputShapObj = m.shapes[inputShapeId];
                var outputShapeObj = m.shapes[outputShapeId];


                operation.Input = new Structure(inputShapeId);
                operation.Output = new Structure(outputShapeId);

            
                foreach (var inputMember in inputShapObj.members)
                {
                    var memberName = (string)inputMember.Name;
                    var inputMemberTarget = (string)inputMember.First.target;

                    switch (inputMemberTarget)
                    {
                        case "smithy.api#String":
                            operation.Input.Members.Add(memberName, typeof(string));
                            break;
                        case "smithy.api#Double":
                            operation.Input.Members.Add(memberName, typeof(double));
                            break;
                        case "smithy.api#Integer":
                            operation.Input.Members.Add(memberName, typeof(int));
                            break;
                        default:
                            Console.WriteLine($"Unsupported target: {inputMemberTarget}");
                            break;
                    }
                }
                // TODO: make a single loop that updates both input and output.
                foreach (var outputMember in outputShapeObj.members)
                {
                    var memberName = (string)outputMember.Name;
                    var memberTarget = (string)outputMember.First.target;

                    switch (memberTarget)
                    {
                        case "smithy.api#String":
                            operation.Output.Members.Add(memberName, typeof(string));
                            break;
                        case "smithy.api#Double":
                            operation.Output.Members.Add(memberName, typeof(double));
                            break;
                        case "smithy.api#Integer":
                            operation.Output.Members.Add(memberName, typeof(int));
                            break;
                        default:
                            Console.WriteLine($"Unsupported target: {memberTarget}");
                            break;
                    }
                }


                service.Operations.Add(operation);

            }




            smithy.Services.Add(service);
        }





        return smithy;

    }

    static dynamic GetProperties(dynamic obj)
    {
        var properties = new ExpandoObject() as IDictionary<string, object>;
        if (obj == null)
            return properties;

        foreach (var propertyInfo in obj.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            properties[propertyInfo.Name] = propertyInfo.GetValue(obj);
        }

        return properties;
    }
}

public class SmithyBuildInfo
{
    public List<string> OperationShapeIds = new List<string>();

    public List<string> ResourceShapeIds = new List<string>();

    public List<string> ServiceShapeIds = new List<string>();
}


public class Smithy
{
    public string Version { get; set; }

    public List<Service> Services = new List<Service>();

}

public abstract class Shape
{
    public Shape()
    {
        
    }

    public Shape(string shapeId)
    {
        Namespace = shapeId.Split('#')[0];
        Name = shapeId.Split('#')[1];
    }

    public string Namespace { get; set; }

    public string Name { get; set; }
}

public class Resource : Shape
{

}

public class Service : Shape
{
    public Service()
    {
        
    }

    public Service(string shapeId) : base(shapeId) { }

    public List<Operation> Operations = new List<Operation>();
}

public class Operation : Shape
{

    public Operation()
    {
        
    }
    public Operation(string shapeId) : base(shapeId)
    {
        
    }
    public Structure Input { get; set; }
    public Structure Output { get; set; }
}

public class Structure : Shape
{
    public Structure()
    {
        
    }

    public Structure(string shapeId) : base(shapeId)
    {

}


    public Dictionary<string, Type> Members = new Dictionary<string, Type>();
}

