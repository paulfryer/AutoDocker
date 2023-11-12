namespace SmithyParser.Models.Types;

[TypeName("structure")]
public class Structure : Shape
{
    public List<Member> Members = new();
    public Dictionary<string, Type> MembersOLD = new();

    // public Dictionary<string, string> Traits = new();
    public Dictionary<Trait, object> Traits = new();

    public Structure(string shapeId) : base(shapeId)
    {
    }
}