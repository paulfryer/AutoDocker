﻿using System.ComponentModel;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Reflection;
using System.Security;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using Newtonsoft.Json;
using SmithyParser.Models;
using SmithyParser.Models.Traits;
using SmithyParser.Models.Types;

namespace SmithyParser.CodeGen;

public class TypeScriptCodeGenerator : ICodeGenerator
{
    public string GenerateCode(SmithyModel model)
    {
        throw new NotImplementedException();
    }
}

internal class CSharpCodeGenerator : ICodeGenerator
{
    public bool UseServiceNameInRoute = true;
    public string GenerateCode(SmithyModel model)
    {
        var sb = new StringBuilder();

        sb.AppendLine("using System;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.ComponentModel;");
        sb.AppendLine("using System.Globalization;");
        sb.AppendLine("using System.ComponentModel.DataAnnotations;");
        sb.AppendLine("using Xunit;");

        if (model.Services.Any(s => s.Namespace == model.Name))
            sb.AppendLine("using Microsoft.AspNetCore.Mvc;");

        foreach (var usedModel in model.Using)
            sb.AppendLine($"using {usedModel.Key};");

        sb.AppendLine($"namespace {model.Name} {{");

        /*
        foreach (var st in model.SimpleTypes.Where(s => s.Namespace == model.Name))
        {
            var dotnetType = GetDotNetTypeForSimpleType(st.Type);

            sb.AppendLine($"public class {st.Name}TypeConverter : TypeConverter {{");
            sb.AppendLine(@"public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
                            {
                                return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
                            }");
            sb.AppendLine(
                $"public override object ConvertFrom({nameof(ITypeDescriptorContext)} context, {nameof(CultureInfo)} culture, object value) {{");
            sb.AppendLine($"if (value is {dotnetType} refVal) return new {st.Name} {{ Value = refVal }};");
            sb.AppendLine("return base.ConvertFrom(context, culture, value); }");
            sb.AppendLine("}");

            sb.AppendLine($"[TypeConverter(typeof({st.Name}TypeConverter))]");
            sb.AppendLine($"public struct {st.Name} {{");
            if (st.Traits.Any(t => t.Key == "smithy.api#pattern"))
            {
                var regEx = st.Traits["smithy.api#pattern"];
                sb.AppendLine($"[RegularExpression({regEx})]");
            }

            sb.AppendLine($"public {dotnetType} Value {{get; set;}}");
            sb.AppendLine("}");
        }
        */

        foreach (var e in model.Enums.Where(s => s.Namespace == model.Name))
        {
            sb.AppendLine($"public enum {e.Name} {{");
            sb.Append(string.Join(",\n", e.Members));
            sb.AppendLine("}");
        }

        foreach (var list in model.Lists.Where(s => s.Namespace == model.Name))
        {
            string listOfType;
            if (list.Target.StartsWith("smithy.api"))
            {
                var targetStructure = new Structure(list.Target);
                listOfType = GetDotNetTypeForSmithyType(targetStructure.Name);
            }
            else
            {
                var listTarget = model.Shapes.Single(s => s.ShapeId == list.Target);
                listOfType = listTarget.Name;
            }

            sb.AppendLine($"public class {list.Name} : List<{listOfType}> {{}}");
        }

        foreach (var s in model.Structures.Where(s => s.Namespace == model.Name))
        {
            var isError = s.Traits.Any(t => t.Key.ShapeId == "smithy.api#error");

            if (isError)
            {
            }

            sb.AppendLine($"public class {s.Name} {(isError ? ": Exception " : string.Empty)}{{");

            foreach (var m in s.Members)
            {
                foreach (var trait in m.Traits)
                    if (trait.Key.ShapeId == "smithy.api#required")
                        sb.AppendLine("[Required]");
     
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
                        if (simpleType.Traits.Any(t => t.Key == "smithy.api#pattern"))
                        {
                            var regEx = simpleType.Traits["smithy.api#pattern"];
                            sb.AppendLine($"[RegularExpression({regEx})]");
                        }

                        var dotNetType = GetDotNetTypeForSimpleType(simpleType.Type);
                        sb.AppendLine($"public {dotNetType} {m.Name} {{get;set;}}");


                    }


                    var subStructure = model.Structures.SingleOrDefault(s => s.ShapeId == m.Target);
                    if (subStructure != null) sb.AppendLine($"public {subStructure.Name}? {m.Name} {{ get; set; }}");

                    var list = model.Lists.SingleOrDefault(l => l.ShapeId == m.Target);
                    if (list != null) sb.AppendLine($"public {list.Name}? {m.Name} {{ get; set; }}");
                }
            }


            sb.AppendLine("}");
        }

        // Services
        foreach (var service in model.Services.Where(s => s.Namespace == model.Name))
        {
            var containsDocumentation = service.Traits.Any(t => t.Key.ShapeId == "smithy.api#documentation");
            if (containsDocumentation)
            {
                var documentationTrait = service.Traits.Single(t => t.Key.ShapeId == "smithy.api#documentation");
                var documentation = JsonConvert.DeserializeObject<string>(documentationTrait.Value);
                var summaryXml = GenerateXmlDocumentation(documentation);
                sb.AppendLine(summaryXml);
            }

            sb.AppendLine($"[Description(\"{service.Namespace}.{service.Name}\")]");
            sb.AppendLine($"public interface I{service.Name}Service {{");
            foreach (var operation in model.Operations.Where(s => s.Namespace == model.Name))
            {
                var outputStructure = model.Structures.Single(s => s.ShapeId == operation.Output);
                var inputStructure = model.Structures.Single(s => s.ShapeId == operation.Input);


                foreach (var errorShapeId in operation.Errors)
                {
                    var error = model.Shapes.Single(s => s.ShapeId == errorShapeId);
                    sb.AppendLine($"/// <exception cref=\"{error.Namespace}.{error.Name}\"></exception>");
                }

                sb.AppendLine(
                    $"    public Task<{outputStructure.Name}> {operation.Name}({inputStructure.Name} input);");
            }

            sb.AppendLine("}");

            // This is the API Controller part.
            if (UseServiceNameInRoute)
                sb.AppendLine($"[Route(\"{service.Name.ToLower()}\")]");
            sb.AppendLine("[ApiController]");
            sb.AppendLine($"public class {service.Name}ServiceController: ControllerBase {{");

            sb.AppendLine($"private readonly I{service.Name}Service service;");

            sb.AppendLine($"public {service.Name}ServiceController(I{service.Name}Service service) {{");
            sb.AppendLine("     this.service = service;");
            sb.AppendLine("}");

            foreach (var operation in model.Operations.Where(s => s.Namespace == model.Name))
                if (operation.Traits.Any(t => t.Key.ShapeId == "smithy.api#http"))
                {
                    var httpTraitJson = operation.Traits.Single(t => t.Key.ShapeId == "smithy.api#http");
                    var httpTrait = JsonConvert.DeserializeObject<HttpTrait>((string)httpTraitJson.Value);
                    var attributeName = ConvertToAttribute(httpTrait.Method);
                    var outputStructure = model.Structures.Single(s => s.ShapeId == operation.Output);
                    var inputStructure = model.Structures.Single(s => s.ShapeId == operation.Input);
                    var operationRoute = UseServiceNameInRoute ? httpTrait.Uri.TrimStart('/') : httpTrait.Uri;

                    sb.AppendLine($"[{attributeName}(\"{operationRoute}\")]");
                    sb.Append($"public async Task<{outputStructure.Name}> {operation.Name}(");
                    var i = 0;
                    foreach (var member in inputStructure.Members)
                    {
                        if (member.Traits.Any(t =>
                                t.Key.ShapeId == "smithy.api#httpLabel" || t.Key.ShapeId == "smithy.api#httpQuery"))
                        {
                            var dotNetType = "";

                            var targetSimpleType = new SimpleType(member.Target);
                            if (targetSimpleType.Namespace == "smithy.api")
                            {
                                dotNetType = GetDotNetTypeForSmithyType(targetSimpleType.Name);
                            }
                            else
                            {
                                var simpleType = model.SimpleTypes.SingleOrDefault(st => st.ShapeId == member.Target);
                                if (simpleType != null)
                                {
                                    dotNetType = GetDotNetTypeForSimpleType(simpleType.Type);
                                }
                                else
                                {
                                    var listType = model.Lists.SingleOrDefault(l => l.ShapeId == member.Target);
                                    if (listType != null)
                                        dotNetType = listType.Name;
                                }

                                //dotNetType = simpleType.Name;
                            }

                            if (i > 0)
                                sb.Append(", ");
                            sb.Append($"{dotNetType} {member.Name}");
                        }

                        i++;
                    }

                    sb.AppendLine(") {");

                    sb.AppendLine($"var input = new {inputStructure.Name}();");

                    foreach (var member in inputStructure.Members)
                    {
                        var simpleType = model.SimpleTypes.SingleOrDefault(st => st.ShapeId == member.Target);
                        // if the type is a simple type, we have to convert it to a struct.
                        if (simpleType != null)
                            sb.AppendLine(
                                $"input.{member.Name} = {member.Name};");
                        else
                            sb.AppendLine($"input.{member.Name} = {member.Name};");
                    }

                    sb.AppendLine($"var output = await service.{operation.Name}(input);");
                    sb.AppendLine("return output;");

                    sb.AppendLine("}");
                }

            sb.AppendLine("}");


            // Generate a mock.
            sb.AppendLine($"public class Mock{service.Name}Service : I{service.Name}Service {{");
            sb.AppendLine("private readonly Random random = new();");
            foreach (var operation in model.Operations.Where(s => s.Namespace == model.Name))
            {
                var outputStructure = model.Structures.Single(s => s.ShapeId == operation.Output);
                var inputStructure = model.Structures.Single(s => s.ShapeId == operation.Input);


                foreach (var errorShapeId in operation.Errors)
                {
                    var error = model.Shapes.Single(s => s.ShapeId == errorShapeId);
                    sb.AppendLine($"/// <exception cref=\"{error.Namespace}.{error.Name}\"></exception>");
                }

                sb.AppendLine(
                    $"    public async Task<{outputStructure.Name}> {operation.Name}({inputStructure.Name} input) {{");

                sb.AppendLine("// Simulate processing time.");
                sb.AppendLine("await Task.Delay(random.Next(500));");
                
                sb.AppendLine($"var output = new {outputStructure.Name} {{");

                SetMockProperties(model, sb, outputStructure);

                sb.AppendLine("};");
                sb.AppendLine("return output;");

                sb.AppendLine("}");
            }
            sb.AppendLine("}");

            // Generate tests
            sb.AppendLine($"public abstract class {service.Name}ServiceTests {{");
            sb.AppendLine("private readonly Random random = new();");
            sb.AppendLine($"private readonly I{service.Name}Service {service.Name}Service;");


            sb.AppendLine($"protected {service.Name}ServiceTests(I{service.Name}Service service) {{");
            sb.AppendLine($"{service.Name}Service = service;");
            sb.AppendLine("}");

            foreach (var operation in model.Operations.Where(s => s.Namespace == model.Name))
            {
                var inputStructure = model.Structures.Single(s => s.ShapeId == operation.Input);
                var outputStructure = model.Structures.Single(s => s.ShapeId == operation.Output);
                sb.AppendLine("[Fact]");
                sb.AppendLine($"public async Task {operation.Name}() {{");
                sb.AppendLine($"var input = {operation.Name}Input;");
                sb.AppendLine($"Validate{inputStructure.Name}(input);");
                sb.AppendLine($"var output  = await {service.Name}Service.{operation.Name}({operation.Name}Input);");
                sb.AppendLine($"Validate{outputStructure.Name}(output);");
                sb.AppendLine("}");

                sb.AppendLine($"public virtual {inputStructure.Name} {operation.Name}Input => new() {{");
                SetMockProperties(model, sb, inputStructure);
                sb.AppendLine("};");
            }

            foreach (var list in model.Lists.Where(s => s.Namespace == model.Name))
            {
                var listTarget = new Structure(list.Target);

                // Skip things like lists of strings.
                if (listTarget.Namespace == "smithy.api")
                    continue;

                sb.AppendLine($"public virtual void Validate{list.Name}({list.Name} list) {{");
                    sb.AppendLine("foreach (var item in list) {");
                    sb.AppendLine($"Validate{listTarget.Name}(item);");
                    sb.AppendLine("}");
                sb.AppendLine("}");
            }

            foreach (var s in model.Structures.Where(s => s.Namespace == model.Name))
            {
                sb.AppendLine($"public virtual void Validate{s.Name}({s.Name} structure) {{");
                foreach (var m in s.Members)
                {
                    if (m.Traits.Any(t => t.Key.ShapeId == "smithy.api#required"))
                    {
                        sb.AppendLine($"Assert.True(structure.{m.Name} != null, $\"The property {{nameof(structure.{m.Name})}} on type {{nameof({s.Name})}} is required, but was null.\");");
                    }

                    var listType = model.Lists.SingleOrDefault(l => l.ShapeId == m.Target);
                    if (listType != null)
                    {
                        // Skip lists that are things like a list of Strings.
                        if (!listType.Target.StartsWith("smithy.api"))
                        {
                            sb.AppendLine($"if (structure.{m.Name} is not null) Validate{listType.Name}(structure.{m.Name});");
                        }

                    }

                    var subType = model.Structures.SingleOrDefault(s => s.ShapeId == m.Target);
                    if (subType != null)
                    {
                        
                        sb.AppendLine($"if (structure.{m.Name} is not null) Validate{subType.Name}(structure.{m.Name});");
                    }
                }
                sb.AppendLine("}");
            }

            sb.AppendLine("}");

            //sb.AppendLine(
            //    $"public sealed class Mock{service.Name}ServiceTests : WeatherServiceTests<Mock{service.Name}Service> {{}}");

        }

        // close the namespace.
        sb.AppendLine("}");

        var sourceCode = sb.ToString();
        var formattedSourceCode = FormatCode(sourceCode);


        return formattedSourceCode;
    }

    public void SetMockProperties(SmithyModel model, StringBuilder sb, Shape shape)
    {
        switch (shape)
        {
            case Structure structure:
                var i = 0;
 
                foreach (var member in structure.Members)
                {
                    if (i > 0)
                        sb.Append(",");
                    string dotNetType = null;
                    if (member.Target.StartsWith("smithy.api#"))
                    {
                        var smithyType = member.Target.Split('#')[1];
                        dotNetType = GetDotNetTypeForSmithyType(smithyType);
                    }
                    else
                    {
                        var simpleType = model.SimpleTypes.SingleOrDefault(st => st.ShapeId == member.Target);
                        if (simpleType != null)
                        {
                            dotNetType = GetDotNetTypeForSimpleType(simpleType.Type);
                        }
                    }
                    if (dotNetType != null)
                        switch (dotNetType)
                        {
                            case "string?":
                                sb.AppendLine($"{member.Name} = \"Mocked value for {member.Name}\"");
                                break;
                            case "DateTime?":
                                sb.AppendLine($"{member.Name} = DateTime.UtcNow");
                                break;
                            case "float?":
                                sb.AppendLine($"{member.Name} = random.NextSingle()");
                                break;
                            case "bool?":
                                sb.AppendLine($"{member.Name} = random.NextDouble() > 0.5");
                                break;
                            case "int?":
                                sb.AppendLine($"{member.Name} = random.Next()");
                                break;
                            default:
                                throw new Exception(
                                    $"Mocked implementation for Dot Net Type: {dotNetType} not implemented.");
                        }


                    var subStructure = model.Structures.SingleOrDefault(s => s.ShapeId == member.Target);
                    if (subStructure != null)
                    {
                        var targetName = member.Target.Split('#')[1];
                        sb.AppendLine($"{member.Name} = new {targetName} {{");
                        SetMockProperties(model, sb, subStructure);
                        sb.AppendLine("}");
                    }


                    var list = model.Lists.SingleOrDefault(l => l.ShapeId == member.Target);

                    if (list != null)
                    {                        
                        sb.AppendLine($"{member.Name} = new {list.Name} {{");

                        for (var l = 0; l < 5; l++)
                        {
                            // Don't build lists of recursive items, else you'll get an endless loop in unit test.
                            var listTarget = model.Shapes.SingleOrDefault(s => s.ShapeId == list.Target);
                            if (listTarget != null && listTarget.ShapeId == shape.ShapeId)
                                continue;

                            if (l > 0) sb.Append(',');

                            if (list.Target.StartsWith("smithy.api"))
                            {
                                var smithyTypeName = list.Target.Split('#')[1];
                                dotNetType = GetDotNetTypeForSmithyType(smithyTypeName);

                                switch (dotNetType)
                                {
                                    case "string?":
                                        sb.AppendLine($"\"Mocked value for {member.Name}\"");
                                        break;
                                    case "DateTime?":
                                        sb.AppendLine($"DateTime.UtcNow");
                                        break;
                                    case "float?":
                                        sb.AppendLine($"random.NextSingle()");
                                        break;
                                    case "bool?":
                                        sb.AppendLine($"random.NextDouble() > 0.5");
                                        break;
                                    case "int?":
                                        sb.AppendLine($"random.Next()");
                                        break;
                                    default:
                                        throw new Exception(
                                            $"Mocked implementation for Dot Net Type: {dotNetType} not implemented.");
                                }
                            }
                            else
                            {

                                sb.AppendLine($"new {listTarget.Name} {{");

                                // Avoid infinite recursion
                                if (listTarget.ShapeId == shape.ShapeId)
                                {
                                    goto skip;
                                }
                                SetMockProperties(model, sb, listTarget);

                                skip:                
                                sb.AppendLine("}");
                            }


                        }

                        sb.AppendLine("}");
                    }



                    i++;
                }

                break;
        }

    }

    private string FormatCode(string sourceCode)
    {
        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = tree.GetRoot().NormalizeWhitespace();
        var workspace = new AdhocWorkspace();
        var formattedRoot = Formatter.Format(root, workspace);
        return formattedRoot.ToFullString();
    }

    public string GenerateXmlDocumentation(string summary)
    {
        var lines = summary.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var formattedSummary = "/// <summary>\n";
        foreach (var line in lines) formattedSummary += $"/// {SecurityElement.Escape(line)}\n";
        formattedSummary += "/// </summary>";
        return formattedSummary;
    }
    

    private static string GetDotNetTypeForSmithyType(string smithyType)
    {


        var map = new Dictionary<string, string>
        {
            { "String", "string?" },
            { "Float", "float?" },
            { "Timestamp", "DateTime?" },
            { "Integer", "int?" },
            { "Boolean", "bool?" }
        };

        if (!map.ContainsKey(smithyType))
            throw new Exception($"Could not find a .net mapping for smithy type: {smithyType}");
        
        return map[smithyType];
    }


    private static string GetDotNetTypeForSimpleType(string simpleType)
    {
        var map = new Dictionary<string, string>
        {
            { "string", "string?" },
            { "boolean", "bool?" },
            { "document", "string?" },
            { "integer", "int?" },
            { "long", "long?" },
            { "bigDecimal", "decimal?" }
        };
        if (!map.ContainsKey(simpleType))
            throw new Exception($"Could not find a .net mapping for simple type: {simpleType}");
        return map[simpleType];
    }

    public static string ConvertToAttribute(string httpMethod)
    {
        switch (httpMethod.ToUpper())
        {
            case "GET":
                return "HttpGet";
            case "POST":
                return "HttpPost";
            case "DELETE":
                return "HttpDelete";
            case "PUT":
                return "HttpPut";
            case "PATCH":
                return "HttpPatch";
            default:
                throw new ArgumentException("Invalid HTTP method");
        }
    }
}