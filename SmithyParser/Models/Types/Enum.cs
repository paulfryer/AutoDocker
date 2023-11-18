using System.Security.Cryptography.X509Certificates;

namespace SmithyParser.Models.Types;

[TypeName("enum")]
public class Enum : Shape
{
    public Enum(string shapeId) : base(shapeId)
    {
        
    }

    public List<string> Members = new();
}