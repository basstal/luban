using Myth;

namespace Myth
{
    public static class MythSemanticAnalyzer
    {
        // private Dictionary<string, (List<MythValueType> ParamTypes, MythValueType ReturnType)> LoadFunctionMapping(string filePath)
        // {
        //     var mapping = new Dictionary<string, (List<MythValueType>, MythValueType)>();
        //     foreach (var line in File.ReadAllLines(filePath))
        //     {
        //         var parts = line.Split(',');
        //         if (parts.Length >= 3)
        //         {
        //             var funcName = parts[0];
        //             var paramTypes = parts.Skip(1).Take(parts.Length - 2).Select(ParseValueType).ToList();
        //             var returnType = ParseValueType(parts.Last());
        //             mapping[funcName] = (paramTypes, returnType);
        //         }
        //     }
        //
        //     return mapping;
        // }

        // private MythValueType ParseValueType(string type)
        // {
        //     return type switch
        //     {
        //         "int" => MythValueType.Int,
        //         "bool" => MythValueType.Bool,
        //         "string" => MythValueType.String,
        //         _ => throw new Exception($"Unknown value type: {type}")
        //     };
        // }

        // // 可选的变量表：如果 DSL 里还有类似 "房间1", "房间2" 这样的字符串标识符想强行指定成某种类型
        // // 这里仅作演示
        // private static Dictionary<string, MythValueType> _predefinedIdentifiers
        //     = new Dictionary<string, MythValueType>()
        //     {
        //         // 如果有需要可以在此定义
        //         // {"房间1", MythValueType.String},
        //         // {"房间2", MythValueType.String},
        //     };

        /// <summary>
        /// 对整棵AST做语义分析，并填充类型信息(如 FunctionCallNode.ReturnType)。
        /// 如果有类型不匹配等错误，可抛出异常或做别的处理。
        /// </summary>
        public static void AnalyzeAST(MythExprNode root)
        {
            AnalyzeNode(root);
        }

        private static void AnalyzeNode(MythExprNode node)
        {
            if (node == null) return;

            switch (node)
            {
                // case LiteralNode ln:
                //     // 字面值已在构造时确定 LitType，不需要额外处理
                //     break;

                case LiteralNode ln:
                    // 如果我们有预定义的标识符类型表，就查一下
                    if (MythFunctionTable.Signatures.TryGetValue(ln.RawValue, out var knownType))
                    {
                        if (knownType.ParamTypes.Count > 0)
                        {
                            throw new Exception($"函数[{ln.RawValue}]需要参数，但是没有提供。");
                        }

                        ln.SetType(MythValueType.NoArgumentFunctionCall);
                        ln.NoArgumentFunctionSignature = knownType;
                    }
                    else
                    {
                        // 否则保持 Unknown 或做一些缺省处理
                    }

                    break;

                case FunctionCallNode fn:
                    // // 先把子节点(实参)做语义分析
                    // foreach (var arg in fn.Arguments)
                    // {
                    //     AnalyzeNode(arg);
                    // }

                    // 查找函数签名
                    if (MythFunctionTable.Signatures.TryGetValue(fn.FuncName, out var signature))
                    {
                        // 把这个函数节点的返回类型设置
                        fn.ReturnType = signature.ReturnType;

                        // 检查参数类型
                        if (signature.IsParams)
                        {
                            // 全部参数都必须是 signature.ParamType
                            foreach (var arg in fn.Arguments)
                            {
                                if (arg is LiteralNode literalNode)
                                {
                                    literalNode.SetType(signature.ParamTypes[0]);
                                }
                                else
                                {
                                    throw new Exception(
                                        $"函数[{fn.FuncName}]要求参数是[{signature.ParamTypes[0]}], 但实参类型[{arg.ValueType}]不匹配。");
                                }
                                // var argType = GetNodeValueType(arg);
                                // 只要不是 ParamType，就视为类型不匹配
                                // if (argType != signature.ParamType)
                                // {
                                //     throw new Exception(
                                //         $"函数[{fn.FuncName}]要求参数是[{signature.ParamType}], 但实参类型[{argType}]不匹配。");
                                // }
                            }
                        }
                        else
                        {
                            // 如果不是 params，就要精确匹配形参个数和类型
                            if (fn.Arguments.Count != signature.ParamTypes.Count)
                            {
                                throw new Exception($"函数[{fn.FuncName}]要求{signature.ParamTypes.Count}个参数, 但实参个数为{fn.Arguments.Count}");
                            }

                            for (int i = 0; i < fn.Arguments.Count; i++)
                            {
                                if (fn.Arguments[i] is LiteralNode literalNode)
                                {
                                    literalNode.SetType(signature.ParamTypes[i]);
                                }
                                else
                                {
                                    throw new Exception(
                                        $"函数[{fn.FuncName}]的第{i + 1}个参数要求类型为[{signature.ParamTypes[i]}], 但实参类型为[{fn.Arguments[i].ValueType}]");
                                }
                                // var argType = GetNodeValueType(fn.Arguments[i]);
                                // if (argType != signature.ParamTypes[i])
                                // {
                                //     throw new Exception($"函数[{fn.FuncName}]的第{i + 1}个参数要求类型为[{signature.ParamTypes[i]}], 但实参类型为[{argType}]");
                                // }
                            }
                        }
                    }
                    else
                    {
                        // 函数表里找不到这个名字
                        throw new Exception($"未知的函数调用: {fn.FuncName}");
                    }

                    break;

                case ComparisonNode cn:
                    // 分别分析左右表达式
                    AnalyzeNode(cn.Left);
                    AnalyzeNode(cn.Right);
                    // 比较运算返回 bool，但如果左/右为 Unknown，这里也只能先放行或做更多规则
                    break;

                case LogicalNode ln2:
                    // 分别分析左右表达式
                    AnalyzeNode(ln2.Left);
                    AnalyzeNode(ln2.Right);
                    // logical && / || 返回 bool
                    break;
            }
        }

        /// <summary>
        /// 获取节点在语义分析后确定的类型(可能还是 Unknown)。
        /// </summary>
        private static MythValueType GetNodeValueType(MythExprNode node)
        {
            if (node == null) return MythValueType.Unknown;
            return node.ValueType;
            // 如果你对 IdentifierNode 做过 ResolvedType 赋值，就在这里用它
        }
    }
}
