namespace Myth
{
    public interface IMythCodeGenerator
    {
        public string GetEvalContextByFunctionSignature(FunctionSignature functionSignature, string[] argCodes);
        public string GenerateExpressionCode(MythExprNode node, MythExprNode parent);

        public string GenerateMethodCode(string methodName, string interfaceName, MythExprNode node);
    }
}
