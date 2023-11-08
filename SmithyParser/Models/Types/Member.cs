namespace SmithyParser.Models.Types;

public class Member
{
    public string Name { get; set; }

    public string Target { get; set; }

    public Dictionary<Trait, object> Traits = new();
}