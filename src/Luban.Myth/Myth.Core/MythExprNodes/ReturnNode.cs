namespace Myth
{
    public class ReturnNode : MythExprNode
    {
        public MythExprNode Value { get; set; } // can be null

        public ReturnNode(MythExprNode value)
        {
            Value = value;
        }

        public override MythValueType ValueType => Value?.ValueType ?? MythValueType.Void;
    }
}
