namespace SmithyParser.Models.Types;

public class Member
{
    public Dictionary<Trait, object> Traits = new();
    public string Name { get; set; }

    public string Target { get; set; }
}