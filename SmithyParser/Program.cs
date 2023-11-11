using System.Diagnostics;
using System.Dynamic;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using SmithyParser.Models;
using SmithyParser.Models.Types;

internal class Program
{
    private static string domain = "services";
    private static string repositoryName = "Services";

    public static async Task Main(string[] args)
    {
        if (args.Length > 0)
            domain = args[0];
        if (args.Length > 1)
            repositoryName = args[1];


        var smithySourceDirectory = Environment.GetEnvironmentVariable("SMITHY_SOURCE");

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
        await smithy.BuildAndPublishPackage("C#", new Version(days, minutes, 0, 0), domain, repositoryName);
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

            Console.WriteLine("Smithy CLI OutputOLD:");
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


        var smithyModel = new SmithyModel(m);
        var cSharp = smithyModel.ToCSharp();
        Console.Write(cSharp);
        


        /// OLD WAY .....................................
        var info = new SmithyBuildInfo();

        foreach (var o in b.operationShapeIds) info.OperationShapeIds.Add((string)o);
        foreach (var r in b.resourceShapeIds) info.ResourceShapeIds.Add((string)r);
        foreach (var s in b.serviceShapeIds) info.ServiceShapeIds.Add((string)s);

        var smithy = new Smithy
        {
            Version = m.smithy
        };

        var templateFileName = smithyFileLocation.Split('\\').Last();

        foreach (var serviceShapeId in info.ServiceShapeIds)
        {
            var s = m.shapes[serviceShapeId];

            var service = new Service(serviceShapeId);

            //foreach (var o in s.operations)
            foreach (var target in info.OperationShapeIds) //.Select(s => new Operation(s)))
            {
                //var target = (string)o.target;
                var operationObj = m.shapes[target];

                var operation = new Operation(target);

                var inputShapeId = (string)operationObj.input.target;
                var outputShapeId = (string)operationObj.output.target;
                var inputShapObj = m.shapes[inputShapeId];
                var outputShapeObj = m.shapes[outputShapeId];


                operation.InputOLD = new Structure(inputShapeId);
                operation.OutputOLD = new Structure(outputShapeId);


                if (inputShapObj != null)
                    foreach (var inputMember in inputShapObj.members)
                    {
                        var memberName = (string)inputMember.Name;
                        var inputMemberTarget = (string)inputMember.First.target;

                        switch (inputMemberTarget)
                        {
                            case "smithy.api#SimpleType":
                                operation.InputOLD.MembersOLD.Add(memberName, typeof(string));
                                break;
                            case "smithy.api#Double":
                                operation.InputOLD.MembersOLD.Add(memberName, typeof(double));
                                break;
                            case "smithy.api#Integer":
                                operation.InputOLD.MembersOLD.Add(memberName, typeof(int));
                                break;
                            case "smithy.api#Timestamp":
                                operation.InputOLD.MembersOLD.Add(memberName, typeof(DateTime));
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
                            case "smithy.api#SimpleType":
                                operation.OutputOLD.MembersOLD.Add(memberName, typeof(string));
                                break;
                            case "smithy.api#Double":
                                operation.OutputOLD.MembersOLD.Add(memberName, typeof(double));
                                break;
                            case "smithy.api#Integer":
                                operation.OutputOLD.MembersOLD.Add(memberName, typeof(int));
                                break;
                            case "smithy.api#Timestamp":
                                operation.OutputOLD.MembersOLD.Add(memberName, typeof(DateTime));
                                break;
                            default:

                                var customType = m.shapes[memberTarget];

                                if (customType != null)
                                {
                                    if (customType["traits"] != null &&
                                        customType["traits"]["smithy.api#streaming"] != null &&
                                        customType["type"] == "union")
                                    {
                                        operation.EventsOLD = new Dictionary<string, Structure>();

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
                                                    case "smithy.api#SimpleType":
                                                        eventStructure.MembersOLD.Add(memName, typeof(string));
                                                        break;
                                                    case "smithy.api#Double":
                                                        eventStructure.MembersOLD.Add(memName, typeof(double));
                                                        break;
                                                    case "smithy.api#Integer":
                                                        eventStructure.MembersOLD.Add(memName, typeof(int));
                                                        break;
                                                    case "smithy.api#Timestamp":
                                                        eventStructure.MembersOLD.Add(memName, typeof(DateTime));
                                                        break;
                                                    default: throw new NotImplementedException();
                                                }
                                            }

                                            operation.EventsOLD.Add(eventName, eventStructure);
                                        }
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"Unsupported target: {memberTarget}");
                                }

                                break;
                        }
                    }


                service.OperationsOLD.Add(operation);
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


    public string Name => Services.First().Namespace + "." + Services.First().Name;
    public string Version { get; set; }
}