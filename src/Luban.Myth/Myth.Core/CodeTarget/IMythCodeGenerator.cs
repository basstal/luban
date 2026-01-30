using Luban.Defs;
using Luban;



namespace Myth
{
    public interface IMythCodeGenerator
    {
        public string GetEvalContextByFunctionSignature(FunctionSignature functionSignature, string[] argCodes);
        public string GenerateExpressionCode(MythExprNode node, MythExprNode parent = null, int nodeIndexFromParent = -1);
        public string GenerateMethodCode(string methodName, string interfaceName, MythExprNode node, GenerationContext ctx, MythConverter.ValidationContext? validationContext = null);
    }
}
