namespace SmithyParser.Models.Types;



[TypeName("resource")]
public class Resource : Shape
{
    public Resource(string shapeId) : base(shapeId)
    {
    }

    public Dictionary<string, string> Identifiers = new();

    public string Read { get; set; }
    public string List { get; set; }

    public List<string> Resources = new();
}