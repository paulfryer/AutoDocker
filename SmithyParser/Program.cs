using System.Diagnostics;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using SmithyParser.CodeGen;
using SmithyParser.Models;

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
        var smithyModel = ParseSmithyDocument(smithyFileLocation);

        var versionProvider = new CodeArtifactPackageVersionProvider();
        var packageName = $"{smithyModel.Namespace}.{smithyModel.Name}";
        var buildVersion = await versionProvider.GetBuildVersion(packageName, repositoryName, domain);

        await smithyModel.BuildAndPublishPackage("C#", buildVersion, domain, repositoryName);
    }


    private static SmithyModel ParseSmithyDocument(string smithyFileLocation)
    {
        var smithySource = File.ReadAllText(smithyFileLocation);

        if (Directory.Exists("build"))
            Directory.Delete("build", true);

        CallSmithyCLIBuild(smithyFileLocation);


        var buildJson = File.ReadAllText("build/smithy/source/build-info/smithy-build-info.json");
        var modelJson = File.ReadAllText("build/smithy/source/model/model.json");


        var b = JsonConvert.DeserializeObject<dynamic>(buildJson);
        var m = JsonConvert.DeserializeObject<dynamic>(modelJson);


        var smithyModel = new SmithyModel(m);
        return smithyModel;
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