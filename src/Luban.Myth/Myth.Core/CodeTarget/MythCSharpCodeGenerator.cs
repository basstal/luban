using Luban.Defs;
using Luban.Utils;
using Luban;
using NLog;

namespace Myth
{
    public class MythCSharpCodeGenerator : IMythCodeGenerator
    {
        private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();
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
                case MythValueType.Float:
                case MythValueType.Bool:
                case MythValueType.Long:
                {
                    if (string.IsNullOrEmpty(parameters))
                    {
                        return $"ctx.{evalFunction}(\"{functionSignature.Name}\")";
                    }

                    return $"ctx.{evalFunction}(\"{functionSignature.Name}\", {parameters})";
                }
            }

            throw new NotImplementedException($"GetEvalContextByFunctionSignature for return type:{returnType} failed!");
        }

        /// <summary>
        /// 根据AST，生成可执行C#表达式代码
        /// 假设我们用 ctx 作为 IConditionContext 的变量名
        /// </summary>
        public string GenerateExpressionCode(MythExprNode node, MythExprNode parent = null, int nodeIndexFromParent = -1)
        {
            s_logger.Debug($"开始生成表达式代码，节点类型: {node.GetType().Name}, 父节点: {(parent != null ? parent.GetType().Name : "null")}, 节点索引: {nodeIndexFromParent}");

            if (node is LiteralNode ln)
            {
                s_logger.Debug($"处理字面量节点，类型: {ln.ValueType}, 原始值: {ln.RawValue}");

                switch (ln.ValueType)
                {
                    case MythValueType.Int:
                    {
                        // 验证整数有效性
                        if (!int.TryParse(ln.RawValue, out _))
                        {
                            string contextInfo = m_validationContext?.GetContextInfo() ?? "";
                            s_logger.Error($"{contextInfo}无效的整数值: {ln.RawValue}");
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


                        s_logger.Debug($"生成整数表达式: {ln.RawValue}");
                        return ln.RawValue;
                    }
                    case MythValueType.Long:
                    {
                        // 验证长整数有效性
                        if (!long.TryParse(ln.RawValue, out _))
                        {
                            string contextInfo = m_validationContext?.GetContextInfo() ?? "";
                            s_logger.Error($"{contextInfo}无效的长整数值: {ln.RawValue}");
                            throw new ArgumentException($"{contextInfo}无效的长整数值: {ln.RawValue}，必须是可以转换为长整数的有效数值");
                        }
                        s_logger.Debug($"生成长整数表达式: {ln.RawValue}");
                        return ln.RawValue;
                    }
                    case MythValueType.Variable:
                    {
                        // 变量类型，假设是有效的变量名，不进行数值校验
                        s_logger.Debug($"生成变量表达式: {ln.RawValue}");
                        return ln.RawValue;
                    }
                    case MythValueType.Float:
                    {
                        // 验证浮点数有效性
                        if (!float.TryParse(ln.RawValue, out var floatVal))
                        {
                            string contextInfo = m_validationContext?.GetContextInfo() ?? "";
                            s_logger.Error($"{contextInfo}无效的浮点数值: {ln.RawValue}");
                            throw new ArgumentException($"{contextInfo}无效的浮点数值: {ln.RawValue}，必须是可以转换为浮点数的有效数值");
                        }
                        // 转成万分位整数
                        var result = ((int)(floatVal * 10000)).ToString();
                        s_logger.Debug($"将浮点数 {ln.RawValue} 转换为万分位整数: {result}");
                        return result;
                    }
                    case MythValueType.IntTenThousandth:
                    {
                        // 验证万分位整数有效性
                        if (!float.TryParse(ln.RawValue, out var floatVal))
                        {
                            string contextInfo = m_validationContext?.GetContextInfo() ?? "";
                            s_logger.Error($"{contextInfo}无效的万分位整数值: {ln.RawValue}");
                            throw new ArgumentException($"{contextInfo}无效的万分位整数值: {ln.RawValue}，必须是可以转换为浮点数的有效数值");
                        }

                        if (FunctionSignature.ShouldCastToTenThousandth(ln, parent)) // 转成万分位整数
                        {
                            var result = ((int)(floatVal * 10000)).ToString();
                            s_logger.Debug($"将万分位整数 {ln.RawValue} 转换为整数: {result}");
                            return result;
                        }

                        s_logger.Error($"万分位整数转换失败: CastToTenThousandth 仅支持 ComparisonNode 父节点");
                        throw new NotImplementedException("CastToTenThousandth only support ComparisonNode parent!");
                    }

                    case MythValueType.Bool:
                    {
                        // 验证布尔值有效性
                        var lowerValue = ln.RawValue.ToLower();
                        if (lowerValue != "true" && lowerValue != "false")
                        {
                            string contextInfo = m_validationContext?.GetContextInfo() ?? "";
                            s_logger.Error($"{contextInfo}无效的布尔值: {ln.RawValue}");
                            throw new ArgumentException($"{contextInfo}无效的布尔值: {ln.RawValue}，必须是 true 或 false");
                        }
                        s_logger.Debug($"生成布尔值表达式: {lowerValue}");
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
                                s_logger.Debug($"处理Luban类型引用: {paramterInfo.LubanTypeReference}, 原始值: {ln.RawValue}");

                                // 使用统一的验证函数
                                var validationResult = MythConverter.ValidateLubanTypeReference(
                                    paramterInfo.LubanTypeReference,
                                    ln.RawValue,
                                    m_generationContext,
                                    isGolang: false,
                                    m_validationContext);

                                s_logger.Debug($"验证完成，类型: {(validationResult.IsEnum ? "枚举" : "表")}, 表达式: {validationResult.CodeExpression}");
                                return validationResult.CodeExpression;
                            }
                        }

                        var stringResult = $"\"{ln.RawValue}\"";
                        s_logger.Debug($"生成字符串表达式: {stringResult}");
                        return stringResult;
                    }
                    default:
                    {
                        s_logger.Error($"不支持的值类型: {ln.ValueType}, 原始值: {ln.RawValue}");
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
                s_logger.Debug($"处理函数调用节点，函数名: {fn.FunctionSignature.Name}, 参数数量: {fn.Arguments.Count}");

                // 演示直接调 ctx.EvalFunction(...)
                // 需要把参数也生成C#表达式
                var argCodes = fn.Arguments
                    .Select((argument, index) =>
                    {
                        s_logger.Debug($"生成函数参数 {index} 的表达式代码");
                        return GenerateExpressionCode(argument, fn, index);
                    })
                    .ToArray();

                s_logger.Debug($"函数 {fn.FunctionSignature.Name} 的参数代码: [{string.Join(", ", argCodes)}]");

                // 简化：把所有参数先转成 string[] 传进去
                // 真实项目可能要区分 int/bool/string
                var result = GetEvalContextByFunctionSignature(fn.FunctionSignature, argCodes);
                s_logger.Debug($"生成函数调用表达式: {result}");
                return result;
            }
            else if (node is ComparisonNode cn)
            {
                s_logger.Debug($"处理比较节点，操作符: {cn.Operator}");
                var left = GenerateExpressionCode(cn.Left, cn);
                var right = GenerateExpressionCode(cn.Right, cn);
                string op = MythConverter.CompareOpToString(cn.Operator);
                var result = $"({left} {op} {right})";
                s_logger.Debug($"生成比较表达式: {result}");
                return result;
            }
            else if (node is LogicalNode ln2)
            {
                s_logger.Debug($"处理逻辑节点，操作符: {ln2.Operator}");
                var left = GenerateExpressionCode(ln2.Left, ln2);
                var right = GenerateExpressionCode(ln2.Right, ln2);
                string op = ln2.Operator == MythLogicalOp.And ? "&&" : "||";
                var result = $"({left} {op} {right})";
                s_logger.Debug($"生成逻辑表达式: {result}");
                return result;
            }
            else if (node is ArithmeticNode an)
            {
                s_logger.Debug($"处理算术节点，操作符: {an.Op}");
                var left = GenerateExpressionCode(an.Left, an);
                var right = GenerateExpressionCode(an.Right, an);
                string op = MythConverter.ArithmeticOpToString(an.Op);
                var result = $"({left} {op} {right})";
                s_logger.Debug($"生成算术表达式: {result}");
                return result;
            }
            else if (node is PlaceHolderNode pn)
            {
                var result = string.IsNullOrEmpty(pn.OutputValue) ? pn.Name : pn.OutputValue;
                s_logger.Debug($"处理占位符节点，名称: {pn.Name}, 输出值: {result}");
                return result;
            }
            else if (node is DeclarationExpressionNode den)
            {
                s_logger.Debug($"处理声明表达式节点，类型: {den.TypeIdentifier.RawValue}, 变量名: {den.VariableIdentifier.RawValue}");
                var type = MythTypeUtil.ParseType(den.TypeIdentifier.RawValue);
                var result = $"{MythTypeUtil.MythValueTypeToCSharp(type)} {den.VariableIdentifier.RawValue}";
                s_logger.Debug($"生成声明表达式: {result}");
                return result;
            }
            else if (node is AssignmentNode assignmentNode)
            {
                s_logger.Debug($"处理赋值节点");
                var target = GenerateExpressionCode(assignmentNode.Target, assignmentNode);
                var value = GenerateExpressionCode(assignmentNode.Value, assignmentNode);
                var result = $"{target} = {value}";
                s_logger.Debug($"生成赋值表达式: {result}");
                return result;
            }
            else if (node is ReturnNode returnNode)
            {
                s_logger.Debug($"处理返回节点");
                var value = GenerateExpressionCode(returnNode.Value, returnNode);
                var result = $"return {value}";
                s_logger.Debug($"生成返回表达式: {result}");
                return result;
            }
            else if (node is CastExpressionNode cen)
            {
                s_logger.Debug($"处理类型转换节点，目标类型: {cen.TargetType}");
                var exprCode = GenerateExpressionCode(cen.Expression, cen);
                var typeCode = MythTypeUtil.MythValueTypeToCSharp(cen.TargetType);
                var result = $"(({typeCode}){exprCode})";
                s_logger.Debug($"生成类型转换表达式: {result}");
                return result;
            }
            else if (node is ConditionalExpressionNode conditionalExpressionNode)
            {
                s_logger.Debug($"处理条件表达式节点（三元运算符）");
                var condition = GenerateExpressionCode(conditionalExpressionNode.Condition, conditionalExpressionNode);
                var thenExpr = GenerateExpressionCode(conditionalExpressionNode.ThenExpr, conditionalExpressionNode);
                var elseExpr = GenerateExpressionCode(conditionalExpressionNode.ElseExpr, conditionalExpressionNode);
                var result = $"({condition} ? {thenExpr} : {elseExpr})";
                s_logger.Debug($"生成条件表达式: {result}");
                return result;
            }

            s_logger.Warn($"遇到未知节点类型: {node.GetType().Name}");
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
public static bool {methodName}({interfaceName} ctx)
{{
    return {exprCode};
}}";
        }
    }
}
