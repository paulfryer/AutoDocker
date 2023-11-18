namespace SmithyParser.Models.Types;

public class Map : Shape
{
    public Map(string shapeId) : base(shapeId)
    {
    }

    public string Key { get; set; }
    public string Value { get; set; }
}