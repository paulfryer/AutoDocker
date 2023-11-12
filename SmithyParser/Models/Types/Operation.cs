namespace SmithyParser.Models.Types;

[TypeName("operation")]
public class Operation : Shape
{
    public List<string> Errors = new();

    public Dictionary<Trait, object> Traits = new();

    public Operation(string shapeId) : base(shapeId)
    {
    }

    public string Input { get; set; }
    public string Output { get; set; }


    public Structure InputOLD { get; set; }
    public Structure OutputOLD { get; set; }
    public Dictionary<string, Structure> EventsOLD { get; set; }
}