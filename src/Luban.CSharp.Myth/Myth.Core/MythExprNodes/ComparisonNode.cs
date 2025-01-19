namespace Myth;

public class ComparisonNode : MythExprNode
{
    public MythExprNode? Left;
    public MythCompareOp Operator;
    public MythExprNode? Right;

    // 比较运算返回 bool
    public override MythValueType ValueType => MythValueType.Bool;

    public ComparisonNode(MythExprNode left, MythCompareOp op, MythExprNode right)
    {
        Left = left;
        Operator = op;
        Right = right;
    }
}
