namespace Myth
{
    public class DeclarationExpressionNode : MythExprNode
    {
        public LiteralNode TypeIdentifier { get; set; }
        public LiteralNode VariableIdentifier { get; set; }

        public DeclarationExpressionNode(LiteralNode type, LiteralNode variable)
        {
            TypeIdentifier = type;
            VariableIdentifier = variable;
        }

        public override MythValueType ValueType => MythValueType.Unknown;
    }
}
