namespace SmithyParser.Models.Types;

[TypeName("operation")]
public class Operation : Shape
{
    public Operation(string shapeId) : base(shapeId)
    {
    }

    public Structure Input { get; set; }
    public Structure Output { get; set; }

    public Dictionary<string, Structure> Events { get; set; }
}