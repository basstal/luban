namespace Myth
{
    public class FunctionSignature
    {
        public string Name; // 函数名
        public MythValueType ReturnType; // 返回类型

        // 将原有的参数列表和实际类型字典整合为 ParameterInfo 列表
        public List<ParameterInfo> Parameters { get; set; } = new List<ParameterInfo>();

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

    public class ParameterInfo
    {
        public MythValueType Type { get; set; }
        public string LubanTypeReference { get; set; } // The string after '@'
        public string VariableSignature { get; set; }

        public ParameterInfo(MythValueType type, string inLubanTypeReference, string inVariableSignature)
        {
            Type = type;
            LubanTypeReference = inLubanTypeReference;
            VariableSignature = inVariableSignature;
        }
    }
}
