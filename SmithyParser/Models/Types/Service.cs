namespace SmithyParser.Models.Types;

[TypeName("service")]
public class Service : Shape
{
    public List<Operation> OperationsOLD = new();

    public List<string> Operations = new();

    public string Version { get; set; }

    public List<string> Resources = new();

    public Dictionary<Trait, object> Traits = new ();

    public Service(string shapeId) : base(shapeId)
    {
    }
}