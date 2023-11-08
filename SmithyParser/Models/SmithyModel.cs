using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmithyParser.Models.Types;

namespace SmithyParser.Models;

public class SmithyModel
{
    public List<Shape> Shapes = new();

    public SmithyModel(dynamic json)
    {
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
                    var simpleType = new SimpleType(shapeId);
                    simpleType.Type = shapeType;

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

                    Shapes.Add(operation);
                    break;
                case "service":
                    var service = new Service(shapeId);
                    Shapes.Add(service);
                    break;
            }
        }
    }


    public string Version { get; set; }

    public IEnumerable<Resource> Resources => Shapes.OfType<Resource>();

    public IEnumerable<Service> Services => Shapes.OfType<Service>();

    public IEnumerable<Operation> Operations => Shapes.OfType<Operation>();

    public IEnumerable<Structure> Structures => Shapes.OfType<Structure>();

    public IEnumerable<List> Lists => Shapes.OfType<List>();

    public IEnumerable<SimpleType> SimpleTypes => Shapes.OfType<SimpleType>();
}