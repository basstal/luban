namespace Myth
{
    public class CastExpressionNode : MythExprNode
    {
        public MythValueType TargetType { get; set; }
        public MythExprNode Expression { get; set; }

        public CastExpressionNode(MythValueType targetType, MythExprNode expression)
        {
            TargetType = targetType;
            Expression = expression;
        }

        public override MythValueType ValueType => TargetType;
    }
}
