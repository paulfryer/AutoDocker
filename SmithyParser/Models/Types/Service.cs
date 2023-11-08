namespace SmithyParser.Models.Types;

[TypeName("service")]
public class Service : Shape
{
    public List<Operation> Operations = new();

    public Service(string shapeId) : base(shapeId)
    {
    }
}