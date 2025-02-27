namespace Myth
{
    public class MythMetadata
    {
        // public string ConditionKey; // 唯一标识
        // public string OriginalText; // 原始文本
        // public string MethodName = string.Empty; // 生成的方法名
        public List<string> Identifiers = new List<string>(); // 收集到的所有标识符
        public string Function = string.Empty; // 收集到的所有函数名
        public List<string> FunctionParameters = new List<string>(); // 函数入参
        public List<MythValueType> FunctionParameterTypes = new List<MythValueType>(); // 函数参数类型
        public List<string> IntLiterals = new List<string>(); // int 常量
        public List<string> StringLiterals = new List<string>(); // string 常量
        public List<string> BoolLiterals = new List<string>(); // bool 常量
        public List<string> EnumLiterals = new List<string>(); // 枚举常量
        public MythCompareOp Operator = MythCompareOp.Unknown;
        public MythValueType FunctionReturnType = MythValueType.Unknown;
        public string CompareToLiteralValue = string.Empty;
        public string CompareLiteralContext = string.Empty;
        public string EvaluateType = string.Empty;
        public bool IsParams { get; set; }

        // ... 你也可以再加更多字段，比如运算符信息、参数列表信息等
    }

    public static class MythMetadataCollector
    {
        public static List<MythMetadata> Collect(MythExprNode node)
        {
            var metaList = new List<MythMetadata>();
            Traverse(node, metaList);
            return metaList;
        }

        private static void Traverse(MythExprNode node, List<MythMetadata> metaList)
        {
            if (node == null) return;

            if (node is LogicalNode ln2)
            {
                var leftMeta = new MythMetadata();
                var rightMeta = new MythMetadata();
                Traverse(ln2.Left, new List<MythMetadata> { leftMeta });
                Traverse(ln2.Right, new List<MythMetadata> { rightMeta });
                metaList.Add(leftMeta);
                metaList.Add(rightMeta);
            }
            else
            {
                var meta = new MythMetadata();
                CollectMetadata(node, meta);
                metaList.Add(meta);
            }
        }

        private static void CollectMetadata(MythExprNode node, MythMetadata meta)
        {
            if (node == null) return;

            switch (node)
            {
                case LiteralNode ln:
                    if (ln.ValueType == MythValueType.Int)
                    {
                        meta.IntLiterals.Add(ln.RawValue);
                        meta.CompareToLiteralValue = ln.RawValue;
                    }

                    if (ln.ValueType == MythValueType.IntTenThousandth)
                    {
                        var value = ((int)(float.Parse(ln.RawValue) * 10000)).ToString();
                        meta.IntLiterals.Add(value);
                        meta.CompareToLiteralValue = value;
                    }

                    if (ln.ValueType == MythValueType.String)
                    {
                        meta.StringLiterals.Add(ln.RawValue);
                        meta.CompareToLiteralValue = ln.RawValue;
                    }

                    if (ln.ValueType == MythValueType.Bool)
                    {
                        meta.BoolLiterals.Add(ln.RawValue);
                        meta.CompareToLiteralValue = ln.RawValue;
                    }

                    if (ln.ValueType == MythValueType.Enum)
                    {
                        meta.EnumLiterals.Add(ln.RawValue);
                        meta.CompareToLiteralValue = ln.RawValue;
                    }

                    break;

                case FunctionCallNode fn:
                    meta.Function = fn.FuncName;
                    meta.IsParams = fn.FunctionSignature.IsParams;
                    meta.FunctionReturnType = fn.ReturnType;
                    meta.FunctionParameterTypes = fn.FunctionSignature.ParamTypes;
                    meta.EvaluateType = MythConverter.GetEvalFunctionByFunctionSignature(fn.FunctionSignature);
                    foreach (var arg in fn.Arguments)
                    {
                        CollectMetadata(arg, meta);
                        if (arg is LiteralNode ln2)
                        {
                            meta.FunctionParameters.Add(ln2.RawValue);
                        }
                    }

                    break;

                case ComparisonNode cn:
                    meta.Operator = cn.Operator;
                    // 目前 metadata 仅支持单边为字面值常量
                    if (cn.Left is LiteralNode)
                    {
                        meta.CompareLiteralContext = "Left";
                    }
                    else
                    {
                        meta.CompareLiteralContext = "Right";
                    }

                    CollectMetadata(cn.Left, meta);
                    CollectMetadata(cn.Right, meta);
                    break;
            }
        }
    }
}
