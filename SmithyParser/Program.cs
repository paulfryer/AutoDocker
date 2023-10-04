using System.Diagnostics;
using System.Dynamic;
using System.IO.Compression;
using System.Reflection;
using Amazon.CodeArtifact;
using Amazon.CodeArtifact.Model;
using Newtonsoft.Json;

internal partial class Program
{
    private static async Task Main()
    {
        // Set test file if no argument passed from command line.
        var smithyFileLocation = "C:\\Users\\Administrator\\Projects\\smithy-source\\commerce.smithy";


        /*
        var codeArtifact = new AmazonCodeArtifactClient();

        codeArtifact.GetPackageVersionAssetAsync(new GetPackageVersionAssetRequest
        {
            
        })
        */


        var start = new DateTime(2023, 10, 01);

        var days = Convert.ToInt16(DateTime.UtcNow.Subtract(start).TotalDays);
        var minutes = Convert.ToInt32(DateTime.UtcNow.Subtract(start).TotalMinutes);


        var smithy = ParseSmithyDocument(smithyFileLocation);
        await smithy.BuildAndPublishPackage("C#", new Version(days, minutes, 0, 0));
    }


    private static void CallSmithyCLIBuild(string smithyFileLocation)
    {
        string smithyCommand = $"smithy build {smithyFileLocation}"; // Replace "your-argument" with the actual argument

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

            // Write the "smithy" command to the standard input
            process.StandardInput.WriteLine(smithyCommand);
            process.StandardInput.Flush();
            process.StandardInput.Close();

            // Read the output and error streams
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

            process.WaitForExit();

            Console.WriteLine("Smithy CLI Output:");
            Console.WriteLine(output);

            if (!string.IsNullOrWhiteSpace(error))
            {
                Console.WriteLine("Smithy CLI Error:");
                Console.WriteLine(error);
            }

            Console.WriteLine("Smithy CLI process completed.");
        }
    }


    private static Smithy ParseSmithyDocument(string smithyFileLocation)
    {
        var smithySource = File.ReadAllText(smithyFileLocation);



        // Build it here with CLI..

        CallSmithyCLIBuild(smithyFileLocation);



        var buildJson = File.ReadAllText("build/smithy/source/build-info/smithy-build-info.json");
        var modelJson = File.ReadAllText("build/smithy/source/model/model.json");


        var b = JsonConvert.DeserializeObject<dynamic>(buildJson);
        var m = JsonConvert.DeserializeObject<dynamic>(modelJson);

        var info = new SmithyBuildInfo();

        foreach (var o in b.operationShapeIds) info.OperationShapeIds.Add((string)o);
        foreach (var r in b.resourceShapeIds) info.ResourceShapeIds.Add((string)r);
        foreach (var s in b.serviceShapeIds) info.ServiceShapeIds.Add((string)s);

        var smithy = new Smithy
        {
            Version = m.smithy
        };

        var templateFileName = smithyFileLocation.Split('\\').Last();
        smithy.SetName(templateFileName);

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

                if (inputShapObj != null)
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
                if (outputShapeObj != null)
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

    private static dynamic GetProperties(dynamic obj)
    {
        var properties = new ExpandoObject() as IDictionary<string, object>;
        if (obj == null)
            return properties;

        foreach (var propertyInfo in obj.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            properties[propertyInfo.Name] = propertyInfo.GetValue(obj);

        return properties;
    }
}

public class SmithyBuildInfo
{
    public List<string> OperationShapeIds = new();

    public List<string> ResourceShapeIds = new();

    public List<string> ServiceShapeIds = new();
}


public class Smithy
{
    public void SetName(string smithyTemplateFileName)
    {
        Name = smithyTemplateFileName.Replace(".smithy", string.Empty);
    }

    public string Namespace => Services.First().Namespace;


    public string Name { get; set; }

    public List<Service> Services = new();
    public string Version { get; set; }
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
    public List<Operation> Operations = new();

    public Service()
    {
    }

    public Service(string shapeId) : base(shapeId)
    {
    }
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
    public Dictionary<string, Type> Members = new();

    public Structure()
    {
    }

    public Structure(string shapeId) : base(shapeId)
    {
    }
}