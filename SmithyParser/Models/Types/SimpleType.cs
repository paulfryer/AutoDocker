namespace SmithyParser.Models.Types;


public class SimpleType : Shape
{
    public SimpleType(string shapeId) : base(shapeId)
    {
    }
    public string Type { get; set; }
    public Dictionary<string, string> Traits = new();
}