using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmithyParser.Models.Types;
using Enum = SmithyParser.Models.Types.Enum;

namespace SmithyParser.Models;

public class SmithyModel
{
    public List<Shape> Shapes = new();

    public Dictionary<string, Version> Using = new();

    public SmithyModel(string modelName, string modelJson, string smithySource)
    {
        string pattern = @"^use.*$";
        foreach (Match match in Regex.Matches(smithySource, pattern, RegexOptions.Multiline))
        {
            var usedShapeId = match.Value.Trim().Replace("use ", string.Empty);
            var usedShape = new Structure(usedShapeId);
            Using.TryAdd(usedShape.Namespace, null);
        }


        var json = JsonConvert.DeserializeObject<dynamic>(modelJson);

        if (modelName.EndsWith(".smithy"))
            modelName = modelName.Substring(0, modelName.Length - ".smithy".Length);
        Name = modelName;
        

        Version = (string)json.smithy;
        foreach (JToken shapeToken in json.shapes)
        {
            var shapeId = Regex.Match(shapeToken.Path, @"'([^']*)'").Groups[1].Value;
            var shapeProperty = ((JProperty)shapeToken).First;

            var shapeType = (string)shapeProperty["type"];

            Console.WriteLine($"Parsing shape: {shapeType} {shapeId}");
            switch (shapeType)
            {
                default: throw new NotImplementedException(shapeType);

                case "enum":
                    
                    var e = new Enum(shapeId);
                    foreach (JProperty member in shapeProperty["members"])
                    {
                        var name = member.Name;
                        e.Members.Add(name);
                    }
                    Shapes.Add(e);
                    break;
                case "map":
                    var map = new Map(shapeId);
                    map.Key = (string)shapeProperty["key"]["target"];
                    map.Value = (string)shapeProperty["key"]["value"];
                    Shapes.Add(map);
                    break;



                case "timestamp":
                case "short":
                case "long":
                case "integer":
                case "float":
                case "double":
                case "document":
                case "byte":
                case "boolean":
                case "blob":
                case "bigInteger":
                case "bigDecimal":
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
                            var traitValue = JsonConvert.SerializeObject(trait.Value);
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
                        foreach (JProperty structureTrait in structureTraits)
                        {
                            var traitId = structureTrait.Name;
                            var traitValue = structureTrait.Value;
                            var valueJson = JsonConvert.SerializeObject(traitValue);


                            // Note: not sure if we should try to switch on the trait id and actually parse these.
                            // for now we'll just serialize them as JSON.
                            structure.Traits.Add(new Trait(traitId), valueJson);
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
                        foreach (var error in errors)
                            operation.Errors.Add((string)error["target"]);

                    var operationTraits = shapeProperty["traits"];
                    if (operationTraits != null)
                        foreach (JProperty operationTrait in operationTraits)
                        {
                            var traitId = operationTrait.Name;
                            var traitValue = operationTrait.Value;
                            var valueJson = JsonConvert.SerializeObject(traitValue);
                            operation.Traits.Add(new Trait(traitId), valueJson);
                        }

                    Shapes.Add(operation);
                    break;
                case "service":
                    var service = new Service(shapeId);

                    var version = shapeProperty["version"];
                    if (version != null) service.Version = (string)version;

                    var operations = shapeProperty["operations"];
                    if (operations != null)
                        foreach (var op in operations)
                            service.Operations.Add((string)op["target"]);

                    var serviceResources = shapeProperty["resources"];
                    if (serviceResources != null)
                        foreach (var res in serviceResources)
                            service.Resources.Add((string)res["target"]);

                    var serviceTraits = shapeProperty["traits"];
                    if (serviceTraits != null)
                        foreach (JProperty st in serviceTraits)
                        {
                            var traitId = st.Name;
                            var traitValue = st.Value;
                            var valueJson = JsonConvert.SerializeObject(traitValue);
                            service.Traits.Add(new Trait(traitId), valueJson);
                        }

                    Shapes.Add(service);
                    break;
            }
        }

        // If there is a service use that, else use the namespace from any shape.
        //Namespace = Services.Any() ? Services.First().Namespace : 
           //             Shapes.Any() ? Shapes.First().Namespace : string.Empty;
    }

    //public string Namespace { get; set; }

    public string Name { get; set; }

    public string Version { get; set; }

    public IEnumerable<Resource> Resources => Shapes.OfType<Resource>();

    public IEnumerable<Service> Services => Shapes.OfType<Service>();

    public IEnumerable<Operation> Operations => Shapes.OfType<Operation>();

    public IEnumerable<Structure> Structures => Shapes.OfType<Structure>();

    public IEnumerable<List> Lists => Shapes.OfType<List>();

    public IEnumerable<SimpleType> SimpleTypes => Shapes.OfType<SimpleType>();

    public IEnumerable<Enum> Enums => Shapes.OfType<Enum>();
}