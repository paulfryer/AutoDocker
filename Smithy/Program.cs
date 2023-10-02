using System;
using System.Dynamic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using Amazon.CodeArtifact;
using Amazon.CodeArtifact.Model;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Newtonsoft.Json;

partial class Program
{
    static async Task Main()
    {
        //Extract();
        var smithy = BuildSmithy();

        GenerateCode(smithy);


        var codeArtifact = new AmazonCodeArtifactClient();
        var sts = new AmazonSecurityTokenServiceClient();


        var identity = await sts.GetCallerIdentityAsync(new GetCallerIdentityRequest());

     
        var publishResult = await codeArtifact.PublishPackageVersionAsync(new PublishPackageVersionRequest
        {
            Format = PackageFormat.Nuget,
            Domain = "services",
            DomainOwner = identity.Account,
            Repository = "Services",
            Namespace = "defaultnamespace",
            AssetName = "name",
            PackageVersion = "1.0.0", // TODO: Get the current version then bump it.
            AssetContent = new MemoryStream(File.ReadAllBytes("/package.nuget"))
        });
        


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

