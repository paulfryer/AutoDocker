namespace SmithyParser.Models.Types;

[TypeName("list")]
public class List : Shape
{
    public List(string shapeId) : base(shapeId)
    {
    }

    public string Target { get; set; }
}