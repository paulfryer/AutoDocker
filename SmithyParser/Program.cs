using System.Diagnostics;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using SmithyParser.CodeGen;
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

        //foreach (var smithyFile in smithyFiles)
        //   await Generate(smithyFile);


        await Generate("C:\\Users\\Administrator\\source\\repos\\AutoDocker\\SmithyParser\\example\\example.weather.smithy");

    }

    private static async Task<Version> Generate(string smithyFileLocation)
    {
        var smithyModel = ParseSmithyDocument(smithyFileLocation);

        // This makes the whole thing recursive.
        foreach (var usedModel in smithyModel.Using)
        {
            var usedModelFileLocation = Path.Combine(smithyFileLocation.Replace(smithyModel.Name, usedModel.Key));
            var usedModelVersion = await Generate(usedModelFileLocation);
            smithyModel.Using[usedModel.Key] = usedModelVersion;
        }


        // If no services are found skip, because we are only interested in services.
        //if (!smithyModel.Services.Any())
        //    return;

        var versionProvider = new CodeArtifactPackageVersionProvider();
        var packageName = $"{smithyModel.Name}";
        var buildVersion = await versionProvider.GetBuildVersion(packageName, repositoryName, domain);

        

        await smithyModel.BuildAndPublishPackage("C#", buildVersion, domain, repositoryName);
        return buildVersion;
    }


    private static SmithyModel ParseSmithyDocument(string smithyFileLocation)
    {
        var smithySource = File.ReadAllText(smithyFileLocation);

        if (Directory.Exists("build"))
            Directory.Delete("build", true);

        CallSmithyCLIBuild(smithyFileLocation);


        var buildJson = File.ReadAllText("build/smithy/source/build-info/smithy-build-info.json");
        var modelJson = File.ReadAllText("build/smithy/source/model/model.json");

       

        var smithyFileName = Path.GetFileName(smithyFileLocation);

        var smithyModel = new SmithyModel(smithyFileName, modelJson, smithySource);


        return smithyModel;
    }

    private static void CallSmithyCLIBuild(string smithyFileLocation)
    {
        var smithyCommand = $"smithy build C:\\Users\\Administrator\\source\\repos\\AutoDocker\\SmithyParser\\example\\*.smithy"; // {smithyFileLocation}"; // Replace "your-argument" with the actual argument

        var psi = new ProcessStartInfo
        {
            FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd" : "/bin/bash",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process();
        process.StartInfo = psi;
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