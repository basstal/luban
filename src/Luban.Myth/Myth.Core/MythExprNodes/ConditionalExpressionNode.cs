namespace Myth;

public class ConditionalExpressionNode : MythExprNode
{
    public MythExprNode Condition { get; set; }
    public MythExprNode ThenExpr { get; set; }
    public MythExprNode ElseExpr { get; set; }

    public override MythValueType ValueType => MythValueType.Unknown;

    public ConditionalExpressionNode(MythExprNode condition, MythExprNode thenExpr, MythExprNode elseExpr)
    {
        Condition = condition;
        ThenExpr = thenExpr;
        ElseExpr = elseExpr;
    }
}
