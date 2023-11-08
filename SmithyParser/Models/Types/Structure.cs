namespace SmithyParser.Models.Types;

[TypeName("structure")]
public class Structure : Shape
{
    public Dictionary<string, Type> MembersOLD = new();

    public List<Member> Members = new();

    public Structure(string shapeId) : base(shapeId)
    {
    }
}