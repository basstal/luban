using Luban.Defs;
using Luban.Utils;

namespace Myth
{
    public class MythCSharpCodeGenerator : IMythCodeGenerator
    {
        private List<DefEnum> m_exportEnums;
        public string GetEvalContextByFunctionSignature(FunctionSignature functionSignature, string[] argCodes)
        {
            var returnType = functionSignature.ReturnType;
            var evalFunction = MythConverter.GetEvalFunctionByFunctionSignature(functionSignature);
            var parameters = string.Join(",", argCodes);
            switch (returnType)
            {
                case MythValueType.Int:
                case MythValueType.IntTenThousandth:
                case MythValueType.Float:
                {
                    if (string.IsNullOrEmpty(parameters))
                    {
                        return $"ctx.{evalFunction}(\"{functionSignature.Name}\")";
                    }

                    return $"ctx.{evalFunction}(\"{functionSignature.Name}\", {parameters})";
                }
                case MythValueType.Bool:
                {
                    if (string.IsNullOrEmpty(parameters))
                    {
                        return $"ctx.{evalFunction}(\"{functionSignature.Name}\")";
                    }

                    return $"ctx.{evalFunction}(\"{functionSignature.Name}\", {parameters})";
                }
            }

            throw new NotImplementedException("GetEvalContextByFunctionSignature failed!");
        }

        /// <summary>
        /// 根据AST，生成可执行C#表达式代码
        /// 假设我们用 ctx 作为 IConditionContext 的变量名
        /// </summary>
        public string GenerateExpressionCode(MythExprNode node, MythExprNode parent = null, int nodeIndexFromParent = -1)
        {
            if (node is LiteralNode ln)
            {
                switch (ln.ValueType)
                {
                    case MythValueType.Int:
                    {
                        return ln.RawValue; // 直接输出数字
                    }
                    case MythValueType.Float:
                    {
                        // 转成万分位整数
                        return ((int)(float.Parse(ln.RawValue) * 10000)).ToString();
                    }
                    case MythValueType.IntTenThousandth:
                    {
                        if (FunctionSignature.ShouldCastToTenThousandth(ln, parent)) // 转成万分位整数
                        {
                            return ((int)(float.Parse(ln.RawValue) * 10000)).ToString();
                        }

                        throw new NotImplementedException("CastToTenThousandth only support ComparisonNode parent!");
                    }

                    case MythValueType.Bool:
                        return ln.RawValue.ToLower(); // "true"/"false" 
                    case MythValueType.String:
                    case MythValueType.Enum:
                    {
                        var functionCallNode = (FunctionCallNode)parent;
                        var functionSignature = functionCallNode.FunctionSignature;
                        var paramterInfo = nodeIndexFromParent != -1 && nodeIndexFromParent < functionSignature.Parameters.Count ? functionSignature.Parameters[nodeIndexFromParent] : null;
                        if (paramterInfo != null && !string.IsNullOrEmpty(paramterInfo.LubanTypeReference))
                        {
                            // 从 luban 的 enum 定义中反射获取 actualTypeStr 对应的 DefEnum，并使用 DefEnum 反射中文的 RawValue 到对应的 Enum 内容。
                            var enumDef = m_exportEnums.Find(defEnum => defEnum.FullName == paramterInfo.LubanTypeReference);
                            if (enumDef == null)
                            {
                                throw new NotImplementedException($"Enum {paramterInfo.LubanTypeReference} not found in export enums");
                            }

                            var enumItem = enumDef.Items.Find(item => item.Name == ln.RawValue || item.Alias == ln.RawValue);
                            if (enumItem == null)
                            {
                                throw new NotImplementedException($"Enum item {ln.RawValue} not found in enum {paramterInfo.LubanTypeReference}\n可选的枚举值有：[{string.Join(", ", enumDef.Items.Select(item => item.Name))}]");
                            }


                            return $"{enumDef.FullNameWithTopModule}.{enumItem.Name}";
                        }

                        return $"\"{ln.RawValue}\"";
                    }
                    case MythValueType.Variable:
                    {
                        return ln.RawValue;
                    }
                    default:
                    {
                        throw new NotImplementedException($"Unknown ValueType or {ln.ValueType} : {ln.RawValue} is not supported yet");
                    }
                }
            }
            // else if (node is IdentifierNode idn)
            // {
            //     // 假设标识符都是 int 类型
            //     // 这里可以改成 "ctx.GetInt(\"idn.Name\")" 或分支情况
            //     return $"ctx.GetInt(\"{idn.Name}\")";
            // }
            else if (node is FunctionCallNode fn)
            {
                // 演示直接调 ctx.EvalFunction(...)
                // 需要把参数也生成C#表达式
                var argCodes = fn.Arguments
                    .Select((argument, index) => GenerateExpressionCode(argument, fn, index))
                    .ToArray();
                // 简化：把所有参数先转成 string[] 传进去
                // 真实项目可能要区分 int/bool/string
                return GetEvalContextByFunctionSignature(fn.FunctionSignature, argCodes);
            }
            else if (node is ComparisonNode cn)
            {
                var left = GenerateExpressionCode(cn.Left, cn);
                var right = GenerateExpressionCode(cn.Right, cn);
                string op = MythConverter.CompareOpToString(cn.Operator);
                return $"({left} {op} {right})";
            }
            else if (node is LogicalNode ln2)
            {
                var left = GenerateExpressionCode(ln2.Left, ln2);
                var right = GenerateExpressionCode(ln2.Right, ln2);
                string op = ln2.Operator == MythLogicalOp.And ? "&&" : "||";
                return $"({left} {op} {right})";
            }
            else if (node is ArithmeticNode an)
            {
                var left = GenerateExpressionCode(an.Left, an);
                var right = GenerateExpressionCode(an.Right, an);
                string op = MythConverter.ArithmeticOpToString(an.Op);
                return $"({left} {op} {right})";
            }
            else if (node is PlaceHolderNode pn)
            {
                return string.IsNullOrEmpty(pn.OutputValue) ? pn.Name : pn.OutputValue;
            }
            else if (node is DeclarationExpressionNode den)
            {
                var type = MythTypeUtil.ParseType(den.TypeIdentifier.RawValue);
                return $"{MythTypeUtil.MythValueTypeToCSharp(type)} {den.VariableIdentifier.RawValue}";
            }
            else if (node is AssignmentNode assignmentNode)
            {
                var target = GenerateExpressionCode(assignmentNode.Target, assignmentNode);
                var value = GenerateExpressionCode(assignmentNode.Value, assignmentNode);
                return $"{target} = {value}";
            }
            else if (node is ReturnNode returnNode)
            {
                var value = GenerateExpressionCode(returnNode.Value, returnNode);
                return $"return {value}";
            }
            else if (node is CastExpressionNode cen)
            {
                var exprCode = GenerateExpressionCode(cen.Expression, cen);
                var typeCode = MythTypeUtil.MythValueTypeToCSharp(cen.TargetType);
                return $"(({typeCode}){exprCode})";
            }
            return "/*UNKNOWN*/";
        }





        /// <summary>
        /// 把最终的表达式包装成一个可执行的方法字符串，比如:
        /// 
        /// bool Condition001(IConditionContext ctx) {
        ///     return (...); 
        /// }
        /// 
        /// </summary>
        public string GenerateMethodCode(string methodName, string interfaceName, MythExprNode node, List<DefEnum> exportEnums)
        {
            m_exportEnums = exportEnums;
            var exprCode = GenerateExpressionCode(node);
            return $@"
public static bool {methodName}({interfaceName} ctx)
{{
    return {exprCode};
}}";
        }
    }
}
