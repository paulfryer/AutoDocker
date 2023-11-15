
using SmithyParser.Models;

namespace SmithyParser.CodeGen;

public interface ICodeGenerator
{
    string GenerateCode(SmithyModel model);
}