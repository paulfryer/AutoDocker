namespace SmithyParser.Models.Types;

[TypeName("resource")]
public class Resource : Shape
{
    public Dictionary<string, string> Identifiers = new();

    public List<string> Resources = new();

    public Resource(string shapeId) : base(shapeId)
    {
    }

    public string Read { get; set; }
    public string List { get; set; }
}