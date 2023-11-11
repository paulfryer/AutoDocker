using System.Text;
using Newtonsoft.Json;
using SmithyParser.Models;

namespace SmithyParser.CodeGen;

internal class CSharpCodeGenerator : ICodeGenerator
{


    public string GenerateCode(SmithyModel model)
    {
        var sb = new StringBuilder();

        sb.AppendLine("using System;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using System.Collections.Generic;");

        sb.AppendLine($"namespace {model.Namespace} {{");

        foreach (var list in model.Lists)
        {
            var listTarget = model.Structures.Single(s => s.ShapeId == list.Target);

            sb.AppendLine($"public class {list.Name} : List<{listTarget.Name}> {{}}");
        }

        foreach (var s in model.Structures)
        {
            var isError = s.Traits.Any(t => t.Key.ShapeId == "smithy.api#error");


            sb.AppendLine($"public class {s.Name} {(isError ? ": Exception " : string.Empty)}{{");

            foreach (var m in s.Members)
                if (m.Target.StartsWith("smithy.api#"))
                {
                    var smithyType = m.Target.Split('#')[1];
                    var dotNetType = GetDotNetTypeForSmithyType(smithyType);
                    sb.AppendLine($"    public {dotNetType} {m.Name} {{ get; set; }}");
                }
                else
                {
                    var simpleType = model.SimpleTypes.SingleOrDefault(s => s.ShapeId == m.Target);
                    if (simpleType != null)
                    {
                        var dotNetType = GetDotNetTypeForSimpleType(simpleType.Type);
                        sb.AppendLine($"    public {dotNetType} {m.Name} {{ get; set; }}");
                    }

                    var subStructure = model.Structures.SingleOrDefault(s => s.ShapeId == m.Target);
                    if (subStructure != null) sb.AppendLine($"    public {subStructure.Name} {m.Name} {{ get; set; }}");

                    var list = model.Lists.SingleOrDefault(l => l.ShapeId == m.Target);
                    if (list != null) sb.AppendLine($"    public {list.Name} {m.Name} {{ get; set; }}");
                }

            sb.AppendLine("}");
        }

        // Services
        if (model.Services.Count() > 1)
            throw new Exception("Parser was designed to only handle 1 service per smithy model.");
        var service = model.Services.First();

        var containsDocumentation = service.Traits.Any(t => t.Key.ShapeId == "smithy.api#documentation");
        if (containsDocumentation)
        {
            var documentationTrait = service.Traits.Single(t => t.Key.ShapeId == "smithy.api#documentation");
            var documentation = JsonConvert.DeserializeObject<string>(documentationTrait.Value);
            sb.AppendLine($"/// <summary>{documentation}</summary>");
        }

        sb.AppendLine($"public interface I{service.Name}Service {{");
        foreach (var operation in model.Operations)
        {
            var outputStructure = model.Structures.Single(s => s.ShapeId == operation.Output);
            var inputStructure = model.Structures.Single(s => s.ShapeId == operation.Input);


            foreach (var errorShapeId in operation.Errors)
            {
                var error = model.Shapes.Single(s => s.ShapeId == errorShapeId);
                sb.AppendLine($"/// <exception cref=\"{error.Name}\"></exception>");
            }

            sb.AppendLine($"    public Task<{outputStructure.Name}> {operation.Name}({inputStructure.Name} input);");
        }

        sb.AppendLine("}");


        // close the namespace.
        sb.AppendLine("}");

        return sb.ToString();
    }


    private static string GetDotNetTypeForSmithyType(string smithyType)
    {
        var map = new Dictionary<string, string>
        {
            { "String", "string" },
            { "Float", "float" },
            { "Timestamp", "DateTime" },
            { "Integer", "int" }
        };

        if (!map.ContainsKey(smithyType))
            throw new Exception($"Could not find a .net mapping for smithy type: {smithyType}");
        return map[smithyType];
    }

    private static string GetDotNetTypeForSimpleType(string simpleType)
    {
        var map = new Dictionary<string, string>{ { "string", "string" }};
        if (!map.ContainsKey(simpleType))
            throw new Exception($"Could not find a .net mapping for simple type: {simpleType}");
        return map[simpleType];
    }
}