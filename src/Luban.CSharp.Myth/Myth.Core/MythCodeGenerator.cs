namespace Myth
{
    public class MythCodeGenerator
    {
        public static string GetEvalFunctionByReturnType(MythValueType returnType)
        {
            switch (returnType)
            {
                case MythValueType.Int:
                    return "EvalFunction";
                case MythValueType.Bool:
                    return "EvalFunctionReturnBool";
            }

            return "EvalFunction";
        }

        /// <summary>
        /// 根据AST，生成可执行的C#表达式代码
        /// 假设我们用 ctx 作为 IConditionContext 的变量名
        /// </summary>
        public static string GenerateExpressionCode(MythExprNode node)
        {
            if (node is LiteralNode ln)
            {
                switch (ln.ValueType)
                {
                    case MythValueType.Int:
                        return ln.RawValue; // 直接输出数字
                    case MythValueType.Bool:
                        return ln.RawValue.ToLower(); // "true"/"false" 
                    case MythValueType.String:
                        return $"\"{ln.RawValue}\"";
                    case MythValueType.Enum:
                        return $"ctx.GetEnum(\"{ln.RawValue}\")";
                    case MythValueType.NoArgumentFunctionCall:
                        return $"ctx.Get{ln.NoArgumentFunctionSignature.ReturnType}(\"{ln.RawValue}\")";
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
                    .Select(GenerateExpressionCode)
                    .ToArray();
                // 简化：把所有参数先转成 string[] 传进去
                // 真实项目可能要区分 int/bool/string
                return $"ctx.{GetEvalFunctionByReturnType(fn.ReturnType)}(\"{fn.FuncName}\", {string.Join(", ", argCodes)})";
            }
            else if (node is ComparisonNode cn)
            {
                var left = GenerateExpressionCode(cn.Left);
                var right = GenerateExpressionCode(cn.Right);
                string op = CompareOpToString(cn.Operator);
                return $"({left} {op} {right})";
            }
            else if (node is LogicalNode ln2)
            {
                var left = GenerateExpressionCode(ln2.Left);
                var right = GenerateExpressionCode(ln2.Right);
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
                case MythCompareOp.GreaterEq: return ">=";
                case MythCompareOp.Less: return "<";
                case MythCompareOp.LessEq: return "<=";
            }

            return "??";
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
            var exprCode = GenerateExpressionCode(node);
            return $@"
public static bool {methodName}({interfaceName} ctx)
{{
    return {exprCode};
}}";
        }
    }
}
