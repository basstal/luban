
using Luban;
using Luban.Defs;

public interface IMythCodeTarget
{
    public OutputFile GenerateMyth(GenerationContext ctx, Dictionary<string, (string, string)> result, DefBean bean, string interfaceName);
    public OutputFile GenerateMythInterface(GenerationContext ctx, string interfaceName);
}
