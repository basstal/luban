namespace Myth;

public class LogicalNode : MythExprNode
{
    public MythExprNode? Left;
    public MythLogicalOp Operator;
    public MythExprNode? Right;

    // 逻辑运算返回 bool
    public override MythValueType ValueType => MythValueType.Bool;

    public LogicalNode(MythExprNode left, MythLogicalOp @operator, MythExprNode right)
    {
        Left = left;
        Operator = @operator;
        Right = right;
    }
}
