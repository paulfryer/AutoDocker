namespace SmithyParser.Models;

public class TypeName : Attribute
{
    public string Name { get; }

    public TypeName(string name)
    {
        Name = name;
    }
}