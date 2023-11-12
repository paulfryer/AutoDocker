namespace SmithyParser.Models.Types;

public class SimpleType : Shape
{
    public Dictionary<string, string> Traits = new();

    public SimpleType(string shapeId) : base(shapeId)
    {
    }

    public string Type { get; set; }
}