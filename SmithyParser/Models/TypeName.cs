namespace SmithyParser.Models;

public class TypeName : Attribute
{
    public TypeName(string name)
    {
        Name = name;
    }

    public string Name { get; }
}