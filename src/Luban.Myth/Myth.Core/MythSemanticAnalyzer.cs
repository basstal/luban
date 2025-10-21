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
            return AnalyzeNode(root, null, new HashSet<string>())!;
        }

        // public static MythExprNode AnalyzeASTWithFunctionSignature(MythExprNode node, FunctionSignature signature)
        // {
        //     return AnalyzeASTWithFunctionSignature(node, signature, new Dictionary<string, string>());
        // }

        public static MythExprNode AnalyzeASTWithFunctionSignature(MythExprNode node, FunctionSignature signature, Dictionary<string, string> placeholders, HashSet<string> variableDeclarations)
        {
            var result = ResolvePlaceholders(node, placeholders)!;
            result = AnalyzeNode(result, null, variableDeclarations)!;
            result = LocateVariables(result, signature, variableDeclarations);
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
                case AssignmentNode an:
                    an.Target = ResolvePlaceholders(an.Target, placeholders);
                    an.Value = ResolvePlaceholders(an.Value, placeholders);
                    break;
                case ReturnNode rn:
                    rn.Value = ResolvePlaceholders(rn.Value, placeholders);
                    break;
                case CastExpressionNode cen:
                    cen.Expression = ResolvePlaceholders(cen.Expression, placeholders);
                    break;
                case ConditionalExpressionNode conditionExpressionNode:
                    conditionExpressionNode.Condition = ResolvePlaceholders(conditionExpressionNode.Condition, placeholders);
                    conditionExpressionNode.ThenExpr = ResolvePlaceholders(conditionExpressionNode.ThenExpr, placeholders);
                    conditionExpressionNode.ElseExpr = ResolvePlaceholders(conditionExpressionNode.ElseExpr, placeholders);
                    break;
            }

            return node;
        }

        private static MythExprNode? LocateVariables(MythExprNode node, FunctionSignature signature, HashSet<string> variableDeclarations)
        {
            if (node == null)
            {
                return null;
            }
            switch (node)
            {
                case LiteralNode ln:
                {
                    if (ln.ValueType == MythValueType.Unknown)
                    {
                        if (signature.Parameters.Any(parameterInfo => parameterInfo.VariableSignature == ln.RawValue))
                        {
                            ln.SetType(MythValueType.Variable);
                        }
                        else if (variableDeclarations.Contains(ln.RawValue))
                        {
                            ln.SetType(MythValueType.Variable);
                        }
                    }
                    break;
                }
                case ComparisonNode cn:
                    cn.Left = LocateVariables(cn.Left, signature, variableDeclarations);
                    cn.Right = LocateVariables(cn.Right, signature, variableDeclarations);
                    break;

                case LogicalNode ln2:
                    ln2.Left = LocateVariables(ln2.Left, signature, variableDeclarations);
                    ln2.Right = LocateVariables(ln2.Right, signature, variableDeclarations);
                    break;
                case ArithmeticNode an:
                    an.Left = LocateVariables(an.Left, signature, variableDeclarations);
                    an.Right = LocateVariables(an.Right, signature, variableDeclarations);
                    break;
                case PlaceHolderNode pn:
                case DeclarationExpressionNode den:
                    break;
                case AssignmentNode assignmentNode:
                    assignmentNode.Target = LocateVariables(assignmentNode.Target, signature, variableDeclarations);
                    assignmentNode.Value = LocateVariables(assignmentNode.Value, signature, variableDeclarations);
                    break;
                case ReturnNode rn:
                    rn.Value = LocateVariables(rn.Value, signature, variableDeclarations);
                    break;
                case CastExpressionNode cen:
                    cen.Expression = LocateVariables(cen.Expression, signature, variableDeclarations);
                    break;
                case ConditionalExpressionNode conditionExpressionNode:
                    conditionExpressionNode.Condition = LocateVariables(conditionExpressionNode.Condition, signature, variableDeclarations);
                    conditionExpressionNode.ThenExpr = LocateVariables(conditionExpressionNode.ThenExpr, signature, variableDeclarations);
                    conditionExpressionNode.ElseExpr = LocateVariables(conditionExpressionNode.ElseExpr, signature, variableDeclarations);
                    break;
            }
            return node;
        }

        private static MythExprNode? AnalyzeNode(MythExprNode? node, MythExprNode? parent, HashSet<string> variableDeclarations)
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
                            fn.Arguments[i] = AnalyzeNode(fn.Arguments[i], fn, variableDeclarations);
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
                    cn.Left = AnalyzeNode(cn.Left, cn, variableDeclarations);
                    cn.Right = AnalyzeNode(cn.Right, cn, variableDeclarations);
                    break;

                case LogicalNode ln2:
                    ln2.Left = AnalyzeNode(ln2.Left, ln2, variableDeclarations);
                    ln2.Right = AnalyzeNode(ln2.Right, ln2, variableDeclarations);
                    break;
                case ArithmeticNode an:
                    an.Left = AnalyzeNode(an.Left, an, variableDeclarations);
                    an.Right = AnalyzeNode(an.Right, an, variableDeclarations);
                    break;
                case PlaceHolderNode pn:
                    break;
                case DeclarationExpressionNode den:
                    variableDeclarations.Add(den.VariableIdentifier.RawValue);
                    break;
                case ReturnNode rn:
                    rn.Value = AnalyzeNode(rn.Value, rn, variableDeclarations);
                    break;
                case AssignmentNode an:
                    an.Target = AnalyzeNode(an.Target, an, variableDeclarations);
                    an.Value = AnalyzeNode(an.Value, an, variableDeclarations);
                    break;
                case CastExpressionNode cen:
                    cen.Expression = AnalyzeNode(cen.Expression, cen, variableDeclarations);
                    break;
                case ConditionalExpressionNode conditionExpressionNode:
                    conditionExpressionNode.Condition = AnalyzeNode(conditionExpressionNode.Condition, conditionExpressionNode, variableDeclarations);
                    conditionExpressionNode.ThenExpr = AnalyzeNode(conditionExpressionNode.ThenExpr, conditionExpressionNode, variableDeclarations);
                    conditionExpressionNode.ElseExpr = AnalyzeNode(conditionExpressionNode.ElseExpr, conditionExpressionNode, variableDeclarations);
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
                {
                    throw new Exception($"MythSemanticAnalyzer.AnalyzeNode 不支持的节点类型: {node.GetType()}");
                }
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
