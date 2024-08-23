using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// ReSharper disable CollectionNeverQueried.Global

public class RoslynExpressionProcessor
{
    public ExpressionProcessResult ProcessExpressions(List<ExpressionInfo> expressions)
    {
        ExpressionProcessResult result = new ExpressionProcessResult();
        result.constDefinitions = new HashSet<string>();
        result.constValues = new Dictionary<string, List<string>>(); // 存储常量值
        result.methods = new List<string>();
        // result.mappings = new List<string>();
        int constIndex = 0;
        var valueCallMappings = new StringBuilder();
        var functionWrappers = new Dictionary<string, string>();
        // var methodDicts = new Dictionary<int, List<string>>();
        var delegateTypesMapping = new Dictionary<string, (string, int, StringBuilder, ParameterType)>();
        foreach (var expInfo in expressions)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText($"bool dummy = {expInfo.expression};");
            var root = syntaxTree.GetRoot();

            var literals = root.DescendantNodes()
                .OfType<LiteralExpressionSyntax>()
                .Where(l => l.IsKind(SyntaxKind.NumericLiteralExpression))
                .Select(l => l.Token.ValueText)
                .Distinct()
                .ToList();

            // 筛选出作为函数调用的标识符
            var functionNames = root.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Select(invocation => invocation.Expression)
                .OfType<IdentifierNameSyntax>()
                .Select(identifier => identifier.Identifier.ValueText)
                .Distinct()
                .ToList();

            // 查找所有标识符，排除常量和函数名
            var identifiers = root.DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .Select(id => id.Identifier.ValueText)
                .Distinct()
                .Except(literals)
                .Except(functionNames)
                .ToList();

            string modifiedExpression = expInfo.expression;
            if (!result.constValues.ContainsKey(expInfo.functionName))
            {
                result.constValues.Add(expInfo.functionName, new List<string>()); // 存储常量值
            }

            foreach (var literal in literals)
            {
                string constName = $"CONST_{constIndex++}";
                result.constDefinitions.Add($"const int {constName} = {literal};");
                modifiedExpression = modifiedExpression.Replace(literal, constName);
                result.constValues[expInfo.functionName].Add(constName);
            }

            // Identify and wrap function calls
            var invocationExpressions = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
            foreach (var invokeExpr in invocationExpressions)
            {
                var identifier = invokeExpr.Expression as IdentifierNameSyntax;
                if (identifier != null)
                {
                    string parameterList = string.Join(", ", invokeExpr.ArgumentList.Arguments.Select(arg => arg.ToString()));
                    if (!functionWrappers.TryGetValue(identifier.Identifier.ValueText, out var wrapperName))
                    {
                        wrapperName = $"ValueCall_{functionWrappers.Count}";
                        functionWrappers[identifier.Identifier.ValueText] = wrapperName;
                        string wrapperFunction = $@"
        public static int {wrapperName}(List<int> {parameterList})
        {{
            return {identifier.Identifier.ValueText}({parameterList});
        }}";
                        result.methods.Add(wrapperFunction);
                    }

                    modifiedExpression = modifiedExpression.Replace($"{identifier.Identifier.ValueText}({parameterList})", $"{wrapperName}({parameterList})");
                    valueCallMappings.AppendLine($"        {{ \"{expInfo.functionName}\", {wrapperName} }},");
                }
            }

            var parameterType = expInfo.parameterType == ParameterType.Integer ? "int" : "List<int>";
            string parameters = string.Join(", ", identifiers.Select(id => $"{parameterType} {id}"));
            int paramCount = identifiers.Count;

            string method = $@"
    public static bool {expInfo.functionName}({parameters})
    {{
        return {modifiedExpression};
    }}";
            result.methods.Add(method);

            var key = $"Func<{string.Join(", ", Enumerable.Repeat(parameterType, paramCount).Append("bool"))}>";
            if (!delegateTypesMapping.ContainsKey(key))
            {
                var builder = new StringBuilder();
                delegateTypesMapping[key] = new($"{paramCount}{expInfo.parameterType}", paramCount, builder, expInfo.parameterType);
            }

            delegateTypesMapping[key].Item3.AppendLine($"{{\"{expInfo.functionName}\", {expInfo.functionName}}},");
        }

        result.delegateTypesMapping = new Dictionary<string, DelegateType>();
        foreach (var (key, value) in delegateTypesMapping)
        {
            result.delegateTypesMapping.Add(key, new DelegateType { item1 = value.Item1, item2 = value.Item2, item3 = value.Item3.ToString(), item4 = value.Item4 });
        }

        result.valueCallMappings = valueCallMappings.ToString();
        return result;
    }
}
