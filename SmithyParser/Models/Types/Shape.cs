namespace SmithyParser.Models.Types;

public abstract class Shape
{
    protected Shape(string shapeId)
    {
        ShapeId = shapeId;
    }

    public string ShapeId { get; set; }

    public string Namespace => ShapeId.Split('#')[0];

    public string Name => ShapeId.Split('#')[1];
}