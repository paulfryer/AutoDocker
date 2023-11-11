using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using SmithyParser.Models.Types;

namespace SmithyParser.Models;

public class SmithyModel
{
    public List<Shape> Shapes = new();

    public string Namespace { get; set; }

    public SmithyModel(dynamic json)
    {
        Version = (string)json.smithy;
        foreach (JToken shapeToken in json.shapes)
        {
            var shapeId = Regex.Match((string)shapeToken.Path, @"'([^']*)'").Groups[1].Value;
            var shapeProperty = ((JProperty)shapeToken).First;

            var shapeType = (string)shapeProperty["type"];

            Console.WriteLine($"Parsing shape: {shapeType} {shapeId}");
            switch (shapeType)
            {
                default: throw new NotImplementedException(shapeType);

                case "string":
                    var simpleType = new SimpleType(shapeId)
                    {
                        Type = shapeType
                    };

                    var traits = shapeProperty["traits"];
                    if (traits != null)
                        foreach (JProperty trait in traits)
                        {
                            var traitShapeId = trait.Name;
                            var traitValue = (string)trait.Value;
                            simpleType.Traits.Add(traitShapeId, traitValue);
                        }


                    Shapes.Add(simpleType);
                    break;

                case "list":
                    var list = new List(shapeId);
                    list.Target = (string)shapeProperty["member"]["target"];

                    Shapes.Add(list);
                    break;

                case "structure":
                    var structure = new Structure(shapeId);

                    foreach (JProperty memberProperty in shapeProperty["members"])
                    {
                        var memberName = memberProperty.Name;
                        var propertyValue = memberProperty.Value;
                        var target = (string)propertyValue["target"];
                        var member = new Member
                        {
                            Name = memberName,
                            Target = target
                        };

                        if (propertyValue["traits"] != null)
                            foreach (JProperty traitProperty in propertyValue["traits"])
                            {
                                var traitShapeId = traitProperty.Name;
                                var trait = new Trait(traitShapeId);

                                var v = traitProperty.Value;
                                Console.WriteLine(JsonConvert.SerializeObject(v));

                                // TODO: parse the trait values.
                                member.Traits.Add(trait, null);
                            }

                        structure.Members.Add(member);
                    }

                    var structureTraits = shapeProperty["traits"];
                    if (structureTraits != null)
                    {
                        foreach (JProperty structureTrait in structureTraits)
                        {
                            var traitId = structureTrait.Name;
                            var traitValue = structureTrait.Value;
                            var valueJson = JsonConvert.SerializeObject(traitValue);
                            // Note: not sure if we should try to switch on the trait id and actually parse these.
                            // for now we'll just serialize them as JSON.


                            structure.Traits.Add(new Trait(traitId), valueJson);
                        }
                    }

                    Shapes.Add(structure);
                    break;

                case "resource":
                    var resource = new Resource(shapeId);

                    var identifiers = shapeProperty["identifiers"];
                    if (identifiers != null)
                        foreach (JProperty identifier in identifiers)
                        {
                            var identifierName = identifier.Name;
                            var identifierTarget = (string)identifier.Value["target"];
                            resource.Identifiers.Add(identifierName, identifierTarget);
                        }

                    var resources = shapeProperty["resources"];
                    if (resources != null)
                        foreach (var r in resources)
                        {
                            var target = (string)r["target"];

                            resource.Resources.Add(target);
                        }

                    if (shapeProperty["read"] != null) resource.Read = (string)shapeProperty["read"]["target"];
                    if (shapeProperty["list"] != null) resource.List = (string)shapeProperty["list"]["target"];


                    Shapes.Add(resource);

                    break;
                case "operation":
                    var operation = new Operation(shapeId);

                    if (shapeProperty["input"] != null) operation.Input = (string)shapeProperty["input"]["target"];
                    if (shapeProperty["output"] != null) operation.Output = (string)shapeProperty["output"]["target"];

                    var errors = shapeProperty["errors"];
                    if (errors != null)
                    {
                        foreach (var error in errors)
                        {
                            operation.Errors.Add((string)error["target"]);
                        }
                    }

                    var operationTraits = shapeProperty["traits"];
                    if (operationTraits != null)
                    {
                        foreach (JProperty operationTrait in operationTraits)
                        {
                            var traitId = operationTrait.Name;
                            var traitValue = operationTrait.Value;
                            var valueJson = JsonConvert.SerializeObject(traitValue);
                            operation.Traits.Add(new Trait(traitId), valueJson);
                        }

                    }

                    Shapes.Add(operation);
                    break;
                case "service":
                    var service = new Service(shapeId);

                    var version = shapeProperty["version"];
                    if (version != null)
                    {
                        service.Version = (string)version;
                    }

                    var operations = shapeProperty["operations"];
                    if (operations != null)
                    {
                        foreach (var op in operations)
                        {
                            service.Operations.Add((string)op["target"]);
                        }
                    }

                    var serviceResources = shapeProperty["resources"];
                    if (serviceResources != null)
                    {
                        foreach (var res in serviceResources)
                        {
                            service.Resources.Add((string)res["target"]);
                        }
                    }

                    var serviceTraits = shapeProperty["traits"];
                    if (serviceTraits != null)
                    {
                        foreach (JProperty st in serviceTraits)
                        {
                            var traitId = st.Name;
                            var traitValue = st.Value;
                            var valueJson = JsonConvert.SerializeObject(traitValue);
                            service.Traits.Add(new Trait(traitId), valueJson);
                        }
                    }

                    Shapes.Add(service);
                    break;
            }
        }

        Namespace = Services.First().Namespace;
    }


    public string Version { get; set; }

    public IEnumerable<Resource> Resources => Shapes.OfType<Resource>();

    public IEnumerable<Service> Services => Shapes.OfType<Service>();

    public IEnumerable<Operation> Operations => Shapes.OfType<Operation>();

    public IEnumerable<Structure> Structures => Shapes.OfType<Structure>();

    public IEnumerable<List> Lists => Shapes.OfType<List>();

    public IEnumerable<SimpleType> SimpleTypes => Shapes.OfType<SimpleType>();



    public string ToCSharp()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"namespace {Namespace} {{");

        foreach (var list in Lists)
        {
            var listTarget = Structures.Single(s => s.ShapeId == list.Target);

            sb.AppendLine($"public class {list.Name} : List<{listTarget.Name}> {{}}");
        }

        foreach (var s in Structures)
        {
            BuildStructure(sb, s);
        }

        // Services
        if (Services.Count() > 1) throw new Exception("Parser was designed to only handle 1 service per smithy model.");
        var service = Services.First();

        var containsDocumentation = service.Traits.Any(t => t.Key.ShapeId == "smithy.api#documentation");
        if (containsDocumentation)
        {
            var documentationTrait = service.Traits.Single(t => t.Key.ShapeId == "smithy.api#documentation");
            var documentation = JsonConvert.DeserializeObject<string>(documentationTrait.Value);
            sb.AppendLine($"/// <summary>{documentation}</summary>");
        }

        sb.AppendLine($"public interface I{service.Name}Service {{");
        foreach (var operation in Operations)
        {
            var outputStructure = Structures.Single(s => s.ShapeId == operation.Output);
            var inputStructure = Structures.Single(s => s.ShapeId == operation.Input);


            foreach (var errorShapeId in operation.Errors)
            {
                var error = Shapes.Single(s => s.ShapeId == errorShapeId);
                sb.AppendLine($"/// <exception cref=\"{error.Name}\"></exception>");
            }
            sb.AppendLine($"    public Task<{outputStructure.Name}> {operation.Name}({inputStructure.Name} input);");
        }
        sb.AppendLine("}");


        // close the namespace.
        sb.AppendLine("}");

        return sb.ToString();
    }

    public void BuildStructure(StringBuilder sb, Structure structure)
    {

        var isError = structure.Traits.Any(t => t.Key.ShapeId == "smithy.api#error");


        sb.AppendLine($"public class {structure.Name} {(isError ? ": Exception " : string.Empty)}{{");




        foreach (var m in structure.Members)
        {
            if (m.Target.StartsWith("smithy.api#"))
            {
                var smithyType = m.Target.Split('#')[1];
                var dotNetType = GetDotNetTypeForSmithyType(smithyType);
                sb.AppendLine($"    public {dotNetType} {m.Name} {{ get; set; }}");
            }
            else
            {
                var simpleType = SimpleTypes.SingleOrDefault(s => s.ShapeId == m.Target);
                if (simpleType != null)
                {
                    var dotNetType = SimpleTypeToDotNetTypeMap[simpleType.Type]; 
                    sb.AppendLine($"    public {dotNetType} {m.Name} {{ get; set; }}");
                } 
                
                var subStructure = Structures.SingleOrDefault(s => s.ShapeId == m.Target);
                if (subStructure != null)
                {
                    sb.AppendLine($"    public {subStructure.Name} {m.Name} {{ get; set; }}");
                }

                var list = Lists.SingleOrDefault(l => l.ShapeId == m.Target);
                if (list != null)
                {
                    sb.AppendLine($"    public {list.Name} {m.Name} {{ get; set; }}");
                }

            }
        }
        sb.AppendLine("}");
    }

    private string GetDotNetTypeForSmithyType(string smithyType)
    {
        var map = new Dictionary<string, string>()
        {
            { "String", "string" },
            { "Float", "float" },
            { "Timestamp", "DateTime" },
            {"Integer", "int"}
        };

        if (!map.ContainsKey(smithyType))
            throw new Exception($"Could not find a .net mapping for smithy type: {smithyType}");
        return map[ smithyType ];
    }

    private Dictionary<string, string> SimpleTypeToDotNetTypeMap => new()
    {
        { "string", "string" }
    };

}