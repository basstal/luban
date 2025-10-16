
using Luban;
using Luban.Defs;


public interface IMythCodeTemplateTarget
{
    public OutputFile GenerateMyth(GenerationContext ctx, Dictionary<string, (string, string)> result, DefBean bean, string interfaceName);
    public OutputFile GenerateMythInterface(GenerationContext ctx, string interfaceName);
    public OutputFile GenerateMythExpression(GenerationContext ctx, Myth.IMythCodeGenerator mythCodeGenerator);
}

