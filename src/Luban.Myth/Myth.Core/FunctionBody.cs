namespace Myth
{
    public class FunctionBody
    {
        public FunctionSignature Signature { get; set; }
        public List<MythExprNode> ParsedBodyLines { get; set; }

        public FunctionBody(FunctionSignature signature)
        {
            Signature = signature;
            ParsedBodyLines = new List<MythExprNode>();
        }
    }
}
