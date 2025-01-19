namespace Myth
{
    public class FunctionSignature
    {
        public string Name; // 函数名
        public MythValueType ReturnType; // 返回类型

        // 多个参数类型，如 (int, bool, string)
        // 如果有 params，则放在列表中某一个位置，也可以做更多规则
        public List<MythValueType> ParamTypes = new List<MythValueType>();

        // 如果函数声明了 params 关键字，IsParams = true
        // (本示例假设只有一个 params，用于最后一个参数，或简化理解)
        public bool IsParams;

        public FunctionSignature(string name, MythValueType ret)
        {
            Name = name;
            ReturnType = ret;
        }

        public static bool ShouldCastToTenThousandth(LiteralNode ln, MythExprNode parent)
        {
            if (!(parent is ComparisonNode comparisonNode))
            {
                return false;
            }

            MythExprNode node = comparisonNode.Left == ln ? comparisonNode.Right! : comparisonNode.Left!;
            return node is FunctionCallNode fn && fn.ReturnType == MythValueType.IntTenThousandth;
        }
    }
}
