using System;
using System.IO;
using System.IO.Compression;
using Newtonsoft.Json;

partial class Program
{
    static void Main()
    {

        var smithy = BuildSmithy();

        GenerateCode(smithy);

      


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


                service.Operations.Add(operation);

            }




            smithy.Services.Add(service);
        }





        return smithy;

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
}