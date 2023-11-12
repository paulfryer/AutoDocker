namespace SmithyParser.Models.Types;

[TypeName("service")]
public class Service : Shape
{
    public List<string> Operations = new();
    public List<Operation> OperationsOLD = new();

    public List<string> Resources = new();

    public Dictionary<Trait, string> Traits = new();

    public Service(string shapeId) : base(shapeId)
    {
    }

    public string Version { get; set; }
}