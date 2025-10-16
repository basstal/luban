namespace Myth
{
    public static class MythSemanticAnalyzer
    {
        /// <summary>
        /// 对整棵AST做语义分析，并填充类型信息(如 FunctionCallNode.ReturnType)。
        /// 如果有类型不匹配等错误，可抛出异常或做别的处理。
        /// </summary>
        public static MythExprNode AnalyzeAST(MythExprNode root)
        {
            return AnalyzeNode(root, null)!;
        }

        public static MythExprNode AnalyzeASTWithFunctionSignature(MythExprNode node, FunctionSignature signature)
        {
            return AnalyzeASTWithFunctionSignature(node, signature, new Dictionary<string, string>());
        }

        public static MythExprNode AnalyzeASTWithFunctionSignature(MythExprNode node, FunctionSignature signature, Dictionary<string, string> placeholders)
        {
            var result = ResolvePlaceholders(node, placeholders)!;
            result = AnalyzeNode(result, null)!;
            result = EnhanceWithFunctionSignature(result, signature);
            return result;
        }

        private static MythExprNode? ResolvePlaceholders(MythExprNode? node, Dictionary<string, string> placeholders)
        {
            if (node == null || placeholders.Count == 0)
            {
                return node;
            }

            if (node is LiteralNode ln && ln.ValueType == MythValueType.Unknown)
            {
                if (placeholders.TryGetValue(ln.RawValue, out var originalValue))
                {
                    var placeholderNode = new PlaceHolderNode(ln.RawValue, originalValue);
                    return placeholderNode;
                }
            }

            switch (node)
            {
                case FunctionCallNode fn:
                    for (int i = 0; i < fn.Arguments.Count; i++)
                    {
                        fn.Arguments[i] = ResolvePlaceholders(fn.Arguments[i], placeholders)!;
                    }
                    break;
                case ComparisonNode cn:
                    cn.Left = ResolvePlaceholders(cn.Left, placeholders);
                    cn.Right = ResolvePlaceholders(cn.Right, placeholders);
                    break;
                case LogicalNode ln2:
                    ln2.Left = ResolvePlaceholders(ln2.Left, placeholders);
                    ln2.Right = ResolvePlaceholders(ln2.Right, placeholders);
                    break;
                case ArithmeticNode an:
                    an.Left = ResolvePlaceholders(an.Left, placeholders);
                    an.Right = ResolvePlaceholders(an.Right, placeholders);
                    break;
                case ListNode list:
                    for (int i = 0; i < list.Elements.Count; i++)
                    {
                        list.Elements[i] = ResolvePlaceholders(list.Elements[i], placeholders)!;
                    }
                    break;
            }

            return node;
        }

        private static MythExprNode? EnhanceWithFunctionSignature(MythExprNode node, FunctionSignature signature)
        {
            if (node == null)
            {
                return null;
            }
            switch (node)
            {
                case LiteralNode ln:
                {
                    if (ln.ValueType == MythValueType.Unknown && signature.Parameters.Any(parameterInfo => parameterInfo.VariableSignature == ln.RawValue))
                    {
                        ln.SetType(MythValueType.Variable);
                    }
                    break;
                }
                case ComparisonNode cn:
                    cn.Left = EnhanceWithFunctionSignature(cn.Left, signature);
                    cn.Right = EnhanceWithFunctionSignature(cn.Right, signature);
                    break;

                case LogicalNode ln2:
                    ln2.Left = EnhanceWithFunctionSignature(ln2.Left, signature);
                    ln2.Right = EnhanceWithFunctionSignature(ln2.Right, signature);
                    break;
                case ArithmeticNode an:
                    an.Left = EnhanceWithFunctionSignature(an.Left, signature);
                    an.Right = EnhanceWithFunctionSignature(an.Right, signature);
                    break;
                case PlaceHolderNode pn:
                    break;
            }
            return node;
        }

        private static MythExprNode? AnalyzeNode(MythExprNode? node, MythExprNode? parent)
        {
            if (node == null)
            {
                return null;
            }

            switch (node)
            {
                case LiteralNode ln:
                    if (TryGetFunctionSignature(ln, out var functionSignature))
                    {
                        var newNode = new FunctionCallNode(ln.RawValue);
                        newNode.ReturnType = functionSignature!.ReturnType;
                        newNode.FunctionSignature = functionSignature;
                        return newNode;
                    }

                    if (FunctionSignature.ShouldCastToTenThousandth(ln, parent))
                    {
                        ln.SetType(MythValueType.IntTenThousandth);
                    }

                    break;

                case FunctionCallNode fn:
                    if (MythFunctionTable.Signatures.TryGetValue(fn.FuncName, out var signature))
                    {
                        fn.FunctionSignature = signature;
                        fn.ReturnType = signature.ReturnType;

                        var isParams = signature.IsParams;
                        if (!isParams)
                        {
                            if (fn.Arguments.Count != signature.Parameters.Count)
                            {
                                throw new Exception($"函数[{fn.FuncName}]要求{signature.Parameters.Count}个参数, 但实参个数为{fn.Arguments.Count}");
                            }
                        }


                        for (int i = 0; i < fn.Arguments.Count; i++)
                        {
                            fn.Arguments[i] = AnalyzeNode(fn.Arguments[i], fn);
                            if (fn.Arguments[i] is LiteralNode ln)
                            {
                                ln.SetType(isParams ? signature.Parameters[0].Type : signature.Parameters[i].Type);
                            }
                        }
                    }
                    else
                    {
                        throw new Exception($"未知的函数调用: {fn.FuncName}，没有定义函数签名吗？");
                    }

                    break;

                case ComparisonNode cn:
                    cn.Left = AnalyzeNode(cn.Left, cn);
                    cn.Right = AnalyzeNode(cn.Right, cn);
                    break;

                case LogicalNode ln2:
                    ln2.Left = AnalyzeNode(ln2.Left, ln2);
                    ln2.Right = AnalyzeNode(ln2.Right, ln2);
                    break;
                case ArithmeticNode an:
                    an.Left = AnalyzeNode(an.Left, an);
                    an.Right = AnalyzeNode(an.Right, an);
                    break;
                case PlaceHolderNode pn:
                    break;
                case ListNode list:
                {
                    // listNode 内只允许 literalNode
                    foreach (var item in list.Elements)
                    {
                        if (item is not LiteralNode ln)
                        {
                            throw new Exception($"ListNode 内只允许 literalNode");
                        }
                        if (ln.ValueType == MythValueType.Unknown)
                        {
                            ln.SetType(MythValueType.String);
                        }
                    }
                    break;
                }
                default:
                    throw new Exception($"MythSemanticAnalyzer.AnalyzeNode 不支持的节点类型: {node.GetType()}");
            }

            return node;
        }

        private static bool TryGetFunctionSignature(LiteralNode ln, out FunctionSignature? functionSignature)
        {
            if (!MythFunctionTable.Signatures.TryGetValue(ln.RawValue, out functionSignature))
            {
                return false;
            }

            if (functionSignature.Parameters.Count > 0)
            {
                throw new Exception($"函数[{ln.RawValue}]需要参数，但是没有提供参数。请检查可能的错误写法。");
            }

            return true;
        }
    }
}
