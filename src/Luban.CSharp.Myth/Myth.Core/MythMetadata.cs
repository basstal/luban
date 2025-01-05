namespace Myth
{
    public class MythMetadata
    {
        public string ConditionKey; // 唯一标识
        public string OriginalText; // 原始文本
        public string MethodName; // 生成的方法名
        public List<string> Identifiers = new List<string>(); // 收集到的所有标识符
        public List<string> Functions = new List<string>(); // 收集到的所有函数名
        public List<string> IntLiterals = new List<string>(); // int 常量
        public List<string> StringLiterals = new List<string>(); // string 常量
        public List<string> BoolLiterals = new List<string>(); // bool 常量
        public List<string> EnumLiterals = new List<string>(); // 枚举常量

        // ... 你也可以再加更多字段，比如运算符信息、参数列表信息等
    }

    public static class MythMetadataCollector
    {
        public static MythMetadata Collect(MythExprNode node, string originalText, string conditionKey, string methodName)
        {
            var meta = new MythMetadata { ConditionKey = conditionKey, OriginalText = originalText, MethodName = methodName };
            Traverse(node, meta);
            return meta;
        }

        private static void Traverse(MythExprNode node, MythMetadata meta)
        {
            if (node == null) return;
            switch (node)
            {
                case LiteralNode ln:
                    if (ln.ValueType == MythValueType.Int) meta.IntLiterals.Add(ln.RawValue);
                    if (ln.ValueType == MythValueType.String) meta.StringLiterals.Add(ln.RawValue);
                    if (ln.ValueType == MythValueType.Bool) meta.BoolLiterals.Add(ln.RawValue);
                    if (ln.ValueType == MythValueType.Enum) meta.EnumLiterals.Add(ln.RawValue);
                    if (ln.ValueType == MythValueType.NoArgumentFunctionCall) meta.Functions.Add(ln.RawValue);
                    break;
                // case IdentifierNode idn:
                //     meta.Identifiers.Add(idn.Name);
                //     break;
                case FunctionCallNode fn:
                    meta.Functions.Add(fn.FuncName);
                    foreach (var arg in fn.Arguments)
                        Traverse(arg, meta);
                    break;
                case ComparisonNode cn:
                    Traverse(cn.Left, meta);
                    Traverse(cn.Right, meta);
                    break;
                case LogicalNode ln2:
                    Traverse(ln2.Left, meta);
                    Traverse(ln2.Right, meta);
                    break;
            }
        }
    }
}
