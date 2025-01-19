namespace Myth
{
    public class MythCodeGenerator
    {
        public static string GetEvalContextByFunctionSignature(FunctionSignature functionSignature, string[] argCodes)
        {
            var returnType = functionSignature.ReturnType;
            var evalFunction = GetEvalFunctionByFunctionSignature(functionSignature);
            var parameters = string.Join(",", argCodes);
            switch (returnType)
            {
                case MythValueType.Int:
                case MythValueType.IntTenThousandth:
                    if (string.IsNullOrEmpty(parameters))
                    {
                        return $"ctx.{evalFunction}(\"{functionSignature.Name}\")";
                    }

                    return $"ctx.{evalFunction}(\"{functionSignature.Name}\", {parameters})";
                case MythValueType.Bool:
                    if (string.IsNullOrEmpty(parameters))
                    {
                        return $"ctx.{evalFunction}(\"{functionSignature.Name}\")";
                    }

                    return $"ctx.{evalFunction}(\"{functionSignature.Name}\", {parameters})";
            }

            throw new NotImplementedException("GetEvalContextByFunctionSignature failed!");
        }

        public static string GetEvalFunctionByFunctionSignature(FunctionSignature functionSignature)
        {
            var returnType = functionSignature.ReturnType;
            var haveParams = functionSignature.ParamTypes.Count > 0;
            switch (returnType)
            {
                case MythValueType.Int:
                case MythValueType.IntTenThousandth:
                    if (!haveParams)
                    {
                        return "GetInt";
                    }

                    return "EvalFunction";
                case MythValueType.Bool:
                    if (!haveParams)
                    {
                        return "GetBool";
                    }

                    return "EvalFunctionReturnBool";
            }

            throw new NotImplementedException("GetEvalFunctionByFunctionSignature failed!");
        }

        /// <summary>
        /// 根据AST，生成可执行的C#表达式代码
        /// 假设我们用 ctx 作为 IConditionContext 的变量名
        /// </summary>
        public static string GenerateExpressionCode(MythExprNode node, MythExprNode parent)
        {
            if (node is LiteralNode ln)
            {
                switch (ln.ValueType)
                {
                    case MythValueType.Int:
                    {
                        return ln.RawValue; // 直接输出数字
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
                        return $"\"{ln.RawValue}\"";
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
                    .Select(argument => GenerateExpressionCode(argument, fn))
                    .ToArray();
                // 简化：把所有参数先转成 string[] 传进去
                // 真实项目可能要区分 int/bool/string
                return GetEvalContextByFunctionSignature(fn.FunctionSignature, argCodes);
            }
            else if (node is ComparisonNode cn)
            {
                var left = GenerateExpressionCode(cn.Left, cn);
                var right = GenerateExpressionCode(cn.Right, cn);
                string op = CompareOpToString(cn.Operator);
                return $"({left} {op} {right})";
            }
            else if (node is LogicalNode ln2)
            {
                var left = GenerateExpressionCode(ln2.Left, ln2);
                var right = GenerateExpressionCode(ln2.Right, ln2);
                string op = ln2.Operator == MythLogicalOp.And ? "&&" : "||";
                return $"({left} {op} {right})";
            }

            return "/*UNKNOWN*/";
        }

        

        private static string CompareOpToString(MythCompareOp op)
        {
            switch (op)
            {
                case MythCompareOp.Equal: return "==";
                case MythCompareOp.NotEqual: return "!=";
                case MythCompareOp.Greater: return ">";
                case MythCompareOp.GreaterEqual: return ">=";
                case MythCompareOp.Less: return "<";
                case MythCompareOp.LessEqual: return "<=";
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
        public static string GenerateMethodCode(string methodName, string interfaceName, MythExprNode node)
        {
            var exprCode = GenerateExpressionCode(node, null);
            return $@"
public static bool {methodName}({interfaceName} ctx)
{{
    return {exprCode};
}}";
        }
    }
}
