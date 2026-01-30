using Luban.Defs;
using Luban;

namespace Myth
{
    public class MythGolangCodeGenerator : IMythCodeGenerator
    {
        public string GolangTopModuleName { get; set; }
        private GenerationContext m_generationContext;
        private MythConverter.ValidationContext? m_validationContext;
        public string GetEvalContextByFunctionSignature(FunctionSignature functionSignature, string[] argCodes)
        {
            var returnType = functionSignature.ReturnType;
            var evalFunction = MythConverter.GetEvalFunctionByFunctionSignature(functionSignature);
            var parameters = string.Join(",", argCodes);
            switch (returnType)
            {
                case MythValueType.Int:
                case MythValueType.IntTenThousandth:
                case MythValueType.Bool:
                case MythValueType.Long:
                    if (string.IsNullOrEmpty(parameters))
                    {
                        return $"ctx.{evalFunction}(\"{functionSignature.Name}\")";
                    }

                    return $"ctx.{evalFunction}(\"{functionSignature.Name}\", {parameters})";
            }

            throw new NotImplementedException($"GetEvalContextByFunctionSignature for return type:{returnType} failed!");
        }

        /// <summary>
        /// 根据AST，生成可执行golang表达式代码
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
                        // 验证整数有效性
                        if (!int.TryParse(ln.RawValue, out _))
                        {
                            string contextInfo = m_validationContext?.GetContextInfo() ?? "";
                            throw new ArgumentException($"{contextInfo}无效的整数值: {ln.RawValue}，必须是可以转换为整数的有效数值");
                        }
                        if (parent is FunctionCallNode functionCallNode)
                        {
                            var functionSignature = functionCallNode.FunctionSignature;
                            var paramterInfo = nodeIndexFromParent != -1 && nodeIndexFromParent < functionSignature.Parameters.Count ? functionSignature.Parameters[nodeIndexFromParent] : null;
                            if (paramterInfo != null && !string.IsNullOrEmpty(paramterInfo.LubanTypeReference))
                            {
                                // 使用统一的验证函数
                                var validationResult = MythConverter.ValidateLubanTypeReference(
                                    paramterInfo.LubanTypeReference,
                                    ln.RawValue,
                                    m_generationContext,
                                    isGolang: true,
                                    m_validationContext);

                                return validationResult.CodeExpression;
                            }
                        }

                        return ln.RawValue; // 直接输出数字
                    }
                    case MythValueType.Variable:
                    {
                        // 变量类型，假设是有效的变量名，不进行数值校验
                        return ln.RawValue;
                    }
                    case MythValueType.Float:
                    {
                        // 验证浮点数有效性
                        if (!float.TryParse(ln.RawValue, out var floatVal))
                        {
                            string contextInfo = m_validationContext?.GetContextInfo() ?? "";
                            throw new ArgumentException($"{contextInfo}无效的浮点数值: {ln.RawValue}，必须是可以转换为浮点数的有效数值");
                        }
                        // 转成万分位整数
                        return ((int)(floatVal * 10000)).ToString();
                    }
                    case MythValueType.IntTenThousandth:
                    {
                        // 验证万分位整数有效性
                        if (!float.TryParse(ln.RawValue, out var floatVal))
                        {
                            string contextInfo = m_validationContext?.GetContextInfo() ?? "";
                            throw new ArgumentException($"{contextInfo}无效的万分位整数值: {ln.RawValue}，必须是可以转换为浮点数的有效数值");
                        }

                        if (FunctionSignature.ShouldCastToTenThousandth(ln, parent)) // 转成万分位整数
                        {
                            return ((int)(floatVal * 10000)).ToString();
                        }

                        throw new NotImplementedException("CastToTenThousandth only support ComparisonNode parent!");
                    }

                    case MythValueType.Bool:
                    {
                        // 验证布尔值有效性
                        var lowerValue = ln.RawValue.ToLower();
                        if (lowerValue != "true" && lowerValue != "false")
                        {
                            string contextInfo = m_validationContext?.GetContextInfo() ?? "";
                            throw new ArgumentException($"{contextInfo}无效的布尔值: {ln.RawValue}，必须是 true 或 false");
                        }
                        return lowerValue; // "true"/"false"
                    }
                    case MythValueType.String:
                    case MythValueType.Enum:
                    {
                        if (parent is FunctionCallNode functionCallNode)
                        {
                            var functionSignature = functionCallNode.FunctionSignature;
                            var paramterInfo = nodeIndexFromParent != -1 && nodeIndexFromParent < functionSignature.Parameters.Count ? functionSignature.Parameters[nodeIndexFromParent] : null;
                            if (paramterInfo != null && !string.IsNullOrEmpty(paramterInfo.LubanTypeReference))
                            {
                                // 使用统一的验证函数
                                var validationResult = MythConverter.ValidateLubanTypeReference(
                                    paramterInfo.LubanTypeReference,
                                    ln.RawValue,
                                    m_generationContext,
                                    isGolang: true,
                                    m_validationContext);

                                return validationResult.CodeExpression;
                            }
                        }

                        return $"\"{ln.RawValue}\"";
                    }
                    case MythValueType.Long:
                    {
                        // 验证长整数有效性
                        if (!long.TryParse(ln.RawValue, out _))
                        {
                            string contextInfo = m_validationContext?.GetContextInfo() ?? "";
                            throw new ArgumentException($"{contextInfo}无效的长整数值: {ln.RawValue}，必须是可以转换为长整数的有效数值");
                        }
                        return $"int64({ln.RawValue})";
                    }
                    default:
                        throw new NotImplementedException($"Unknown ValueType or {ln.ValueType} is not supported yet");
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
                return $"{den.VariableIdentifier.RawValue} := {MythTypeUtil.MythValueTypeToGoString(type)}";
            }
            else if (node is AssignmentNode assignmentNode)
            {
                var target = GenerateExpressionCode(assignmentNode.Target, assignmentNode);
                var value = GenerateExpressionCode(assignmentNode.Value, assignmentNode);
                return $"{target}({value})";
            }
            else if (node is ReturnNode returnNode)
            {
                if (returnNode.Value is ConditionalExpressionNode returnConditionalExpressionNode)
                {
                    var condition = GenerateExpressionCode(returnConditionalExpressionNode.Condition, returnConditionalExpressionNode);
                    var thenExpr = GenerateExpressionCode(returnConditionalExpressionNode.ThenExpr, returnConditionalExpressionNode);
                    var elseExpr = GenerateExpressionCode(returnConditionalExpressionNode.ElseExpr, returnConditionalExpressionNode);
                    return $"if ({condition}){{\n  return {thenExpr}\n}}\nreturn {elseExpr}\n";
                }
                else
                {
                    var value = GenerateExpressionCode(returnNode.Value, returnNode);
                    return $"return {value}";
                }
            }
            else if (node is CastExpressionNode cen)
            {
                var exprCode = GenerateExpressionCode(cen.Expression, cen);
                var typeCode = MythTypeUtil.MythValueTypeToGoString(cen.TargetType);
                return $"({typeCode}({exprCode}))";
            }
            else if (node is ConditionalExpressionNode conditionalExpressionNode)
            {
                var condition = GenerateExpressionCode(conditionalExpressionNode.Condition, conditionalExpressionNode);
                var thenExpr = GenerateExpressionCode(conditionalExpressionNode.ThenExpr, conditionalExpressionNode);
                var elseExpr = GenerateExpressionCode(conditionalExpressionNode.ElseExpr, conditionalExpressionNode);
                return $"if ({condition}){{\n  {thenExpr}\n}}\n{elseExpr}\n";
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
        public string GenerateMethodCode(string methodName, string interfaceName, MythExprNode node, GenerationContext ctx, MythConverter.ValidationContext? validationContext = null)
        {
            m_generationContext = ctx;
            m_validationContext = validationContext;
            var exprCode = GenerateExpressionCode(node);
            return $@"
func {methodName}(ctx {GolangTopModuleName}.{interfaceName}) bool {{
    return {exprCode};
}}";
        }
    }
}
