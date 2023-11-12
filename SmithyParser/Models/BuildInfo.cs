using SmithyParser.Models.Types;

namespace SmithyParser.Models;

public class BuildInfo
{
    public List<Trait> DefTraits = new();
    public List<Operation> Operations = new();

    public List<Resource> Resources = new();

    public List<Service> Services = new();

    public List<Trait> Traits = new();
}