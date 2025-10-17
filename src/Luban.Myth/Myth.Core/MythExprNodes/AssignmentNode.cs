namespace Myth
{
    public class AssignmentNode : MythExprNode
    {
        public MythExprNode Target { get; set; }
        public MythExprNode Value { get; set; }

        public AssignmentNode(MythExprNode target, MythExprNode value)
        {
            Target = target;
            Value = value;
        }

        public override MythValueType ValueType => Value.ValueType;
    }
}
