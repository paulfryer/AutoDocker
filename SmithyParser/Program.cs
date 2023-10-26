using System.Diagnostics;
using System.Dynamic;
using System.Reflection;
using System.Runtime.InteropServices;
using Newtonsoft.Json;

internal class Program
{
    public static async Task Main(string[] args)
    {
        string smithySourceDirectory;
        if (args.Any())
            smithySourceDirectory = args[0];
        else smithySourceDirectory = Environment.GetEnvironmentVariable("SMITHY_SOURCE");
        
        if (smithySourceDirectory == null)
            smithySourceDirectory = ".";

        var smithyFiles = Directory.GetFiles(smithySourceDirectory, "*.smithy", SearchOption.AllDirectories);

        foreach (var smithyFile in smithyFiles)
            await Generate(smithyFile);
    }

    private static async Task Generate(string smithyFileLocation)
    {
        var start = new DateTime(2023, 10, 01);

        var days = Convert.ToInt16(DateTime.UtcNow.Subtract(start).TotalDays);
        var minutes = Convert.ToInt32(DateTime.UtcNow.Subtract(start).TotalMinutes);


        var smithy = ParseSmithyDocument(smithyFileLocation);
        await smithy.BuildAndPublishPackage("C#", new Version(days, minutes, 0, 0));
    }


    private static void CallSmithyCLIBuild(string smithyFileLocation)
    {
        var smithyCommand = $"smithy build {smithyFileLocation}"; // Replace "your-argument" with the actual argument

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

            // Write the "smithy" command to the standard input
            process.StandardInput.WriteLine(smithyCommand);
            process.StandardInput.Flush();
            process.StandardInput.Close();

            // Read the output and error streams
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();

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

        if (Directory.Exists("build"))
            Directory.Delete("build", true);


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

                                var customType = m.shapes[memberTarget];

                                if (customType != null)
                                {
                                    if (customType["traits"] != null && customType["traits"]["smithy.api#streaming"] != null &&  customType["type"] == "union")
                                    {
                                        operation.Events = new Dictionary<string, Structure>();

                                        foreach (var member in customType["members"])
                                        {
                                            var eventName = (string)member.Name;
                                            var eventTypeKey = (string)member.First["target"];
                                            var targetType = m.shapes[eventTypeKey];

                                            var eventStructure = new Structure(eventTypeKey);

                                            foreach (var mem in targetType.members)
                                            {
                                                var memName = (string)mem.Name;
                                                var memTarget = (string)mem.First.target;
                                                switch (memTarget)
                                                {
                                                    case "smithy.api#String":
                                                        eventStructure.Members.Add(memName, typeof(string));
                                                        break;
                                                    case "smithy.api#Double":
                                                        eventStructure.Members.Add(memName, typeof(double));
                                                        break;
                                                    case "smithy.api#Integer":
                                                        eventStructure.Members.Add(memName, typeof(int));
                                                        break;
                                                    default: throw new NotImplementedException();
                                                }
                                            }

                                            operation.Events.Add(eventName, eventStructure);
                                        }
                                        
                                    }
                                }
                                else 
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
    public List<Service> Services = new();

    public string Namespace => Services.First().Namespace;


    public string Name { get; set; }
    public string Version { get; set; }

    public void SetName(string smithyTemplateFileName)
    {
        Name = smithyTemplateFileName.Replace(".smithy", string.Empty);
    }
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

    public Dictionary<string, Structure> Events { get; set; }
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
