namespace Myth
{
    public class FunctionSignature
    {
        public string Name; // 函数名
        public MythValueType ReturnType; // 返回类型

        // 多个参数类型，如 (int, bool, string)
        // 如果有 params，则放在列表中某一个位置，也可以做更多规则
        public List<MythValueType> ParamTypes = new List<MythValueType>();

        // 新增: 存储实际参数类型映射
        // key: ParamTypes 中的下标；value: 参数的实际类型字符串
        public Dictionary<int, string> ParamActualTypeDict { get; set; } = new Dictionary<int, string>();

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
            if (parent is ComparisonNode comparisonNode)
            {
                MythExprNode node = comparisonNode.Left == ln ? comparisonNode.Right! : comparisonNode.Left!;
                return node is FunctionCallNode fn && (fn.ReturnType == MythValueType.IntTenThousandth || fn.ReturnType == MythValueType.Float);
            }
            if (parent is FunctionCallNode functionCallNode)
            {
                return functionCallNode.Arguments.Contains(ln);
            }

            return false;
        }
    }
}
