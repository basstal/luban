using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Newtonsoft.Json.Linq;

// ReSharper disable CollectionNeverQueried.Global

public class RoslynExpressionProcessor
{
    public class ExpressionModifier : CSharpSyntaxRewriter
    {
        private readonly Dictionary<LiteralExpressionSyntax, string> _literalToConstName;

        public ExpressionModifier(Dictionary<LiteralExpressionSyntax, string> literalToConstName)
        {
            _literalToConstName = literalToConstName;
        }

        public override SyntaxNode VisitLiteralExpression(LiteralExpressionSyntax node)
        {
            // 检查字面量类型
            if (node.IsKind(SyntaxKind.NumericLiteralExpression) ||
                node.IsKind(SyntaxKind.TrueLiteralExpression) ||
                node.IsKind(SyntaxKind.FalseLiteralExpression) ||
                node.IsKind(SyntaxKind.StringLiteralExpression) ||
                node.IsKind(SyntaxKind.CharacterLiteralExpression) ||
                node.IsKind(SyntaxKind.NullLiteralExpression))
            {
                // 查找常量名称
                if (_literalToConstName.TryGetValue(node, out var constName))
                {
                    return SyntaxFactory.IdentifierName(constName);
                }

                // 如果字面量没有在字典中找到对应的常量名称，抛出异常
                throw new Exception($"Literal value {node.Token.ValueText} not found in the dictionary.");
            }

            // 如果不是我们关注的字面量，继续递归访问子节点
            return base.VisitLiteralExpression(node);
        }
    }

    // // 提取修改后的表达式部分
    // static string ExtractExpression(SyntaxNode root)
    // {
    //     // 获取赋值语句节点（检查是否为 AssignmentExpressionSyntax）
    //     var assignmentExpression = root.DescendantNodes()
    //         .OfType<AssignmentExpressionSyntax>()
    //         .FirstOrDefault();
    //
    //     if (assignmentExpression != null)
    //     {
    //         // 提取右侧的表达式部分，即 "1 + 2 * 3" 或修改后的表达式
    //         var expression = assignmentExpression.Right;
    //
    //         // 返回表达式字符串
    //         return expression.ToString();
    //     }
    //
    //     return string.Empty;
    // }


    public ExpressionProcessResult ProcessExpressions(List<ExpressionInfo> expressions, JObject safeReferenceMethodSignatures)
    {
        ExpressionProcessResult result = new ExpressionProcessResult();
        result.constDefinitions = new HashSet<string>();
        result.constValues = new Dictionary<ExpressionCategory, List<(string, string, string, string)>>(); // 存储常量值
        result.constValueGetters = new Dictionary<string, (string, string, bool, string)>();
        result.methods = new List<string>();
        // result.mappings = new List<string>();
        int constIndex = 0;
        var valueCallMappings = new StringBuilder();
        var functionWrappers = new Dictionary<string, string>();
        // var methodDicts = new Dictionary<int, List<string>>();
        var delegateTypesMapping = new Dictionary<string, (string, int, StringBuilder, ParameterType[])>();
        foreach (var expInfo in expressions)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText($"return {expInfo.expression};");
            var root = syntaxTree.GetRoot();

            // 查找所有常量数据，包括数字字面值和布尔字面值
            var distinctLiterals = root.DescendantNodes()
                .OfType<LiteralExpressionSyntax>()
                // .Select(l => l.Token.ValueText)
                .Distinct()
                .ToArray();

            var distinctLiteralsText = distinctLiterals.Select(l => l.Token.ValueText);

            // 筛选出作为函数调用的标识符
            var functionNames = root.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Select(invocation => invocation.Expression)
                .OfType<IdentifierNameSyntax>()
                .Select(identifier => identifier.Identifier.ValueText)
                .Distinct()
                .ToArray();

            // 查找所有标识符，排除常量和函数名
            var identifiers = root.DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .Select(id => id.Identifier.ValueText)
                .Distinct()
                .Except(distinctLiteralsText)
                .Except(functionNames)
                .ToArray();

            var parameterCSharpTypes = expInfo.parameterTypes.Select(TypeMapping.ParameterTypeToCSharpType).ToArray();
            var parameterEnumTypes = expInfo.parameterTypes.Select(type => type.ToString()).ToArray();

            Dictionary<LiteralExpressionSyntax, string> literalToConstName = new Dictionary<LiteralExpressionSyntax, string>();
            foreach (var literal in distinctLiterals)
            {
                string constName = $"CONST_{constIndex++}";

                if (literal.IsKind(SyntaxKind.NumericLiteralExpression))
                {
                    // 处理数字字面量
                    result.constDefinitions.Add($"const int {constName} = {literal.Token.ValueText};");
                }
                else if (literal.IsKind(SyntaxKind.TrueLiteralExpression) || literal.IsKind(SyntaxKind.FalseLiteralExpression))
                {
                    // 处理布尔字面量
                    bool value = literal.Kind() == SyntaxKind.TrueLiteralExpression;
                    result.constDefinitions.Add($"const bool {constName} = {value.ToString().ToLower()};");
                }
                else if (literal.IsKind(SyntaxKind.StringLiteralExpression))
                {
                    // 处理字符串字面量
                    result.constDefinitions.Add($"const string {constName} = \"{literal.Token.ValueText}\";");
                }
                else if (literal.IsKind(SyntaxKind.CharacterLiteralExpression))
                {
                    // 处理字符字面量
                    result.constDefinitions.Add($"const char {constName} = '{literal.Token.ValueText}';");
                }
                else if (literal.IsKind(SyntaxKind.NullLiteralExpression))
                {
                    // 处理 null 字面量
                    result.constDefinitions.Add($"const object {constName} = null;");
                }
                else
                {
                    // 如果遇到未处理的类型，抛出异常
                    throw new NotImplementedException($"{literal.Kind().ToString()} not supported yet.");
                }

                literalToConstName.Add(literal, constName);
            }

            // 获取 expression 中的常量数据，以及常量数据的类型，根据这个类型和数据数量，来构造 result.constValues 的 key
            var expressionCategory = ExpressionCategorizer.CategorizeExpressions(syntaxTree);
            if (!result.constValues.ContainsKey(expressionCategory))
            {
                result.constValues.Add(expressionCategory, new List<(string, string, string, string)>()); // 存储常量值
            }


            string concatKeyType = TypeMapping.ToConcatTypeKey(expressionCategory.ConstantTypesOrder);
            string constDictNamePostfix = TypeMapping.ToConcatTypeForPostName(expressionCategory.ConstantTypesOrder);
            string tupleValues = literalToConstName.Values.Count > 1 ? $"({string.Join(",", literalToConstName.Values.ToArray())})" : literalToConstName.Values.First();
            result.constValues[expressionCategory].Add((concatKeyType, constDictNamePostfix, tupleValues, expInfo.functionName));
            if (!result.constValueGetters.ContainsKey(concatKeyType))
            {
                // TODO:这里 expInfo.parameterTypes 可能少于 expressionCategory.ConstantTypesOrder 这种情况下一般是一个变量对应多个常量，但是也可能是特殊的对应关系，现在先处理一个变量对应多个常量，特殊对应关系不处理
                var defaultValueTypeList = expInfo.parameterTypes.ToList();
                while (expressionCategory.ConstantTypesOrder.Count > defaultValueTypeList.Count)
                {
                    defaultValueTypeList.Add(defaultValueTypeList[^1]);
                }

                var defaultValues = defaultValueTypeList.Select(TypeMapping.ParameterTypeToDefaultValue).ToList();
                result.constValueGetters.Add(concatKeyType,
                    (concatKeyType, constDictNamePostfix, true, defaultValues.Count > 1 ? $"({string.Join(",", defaultValues)})" : defaultValues[0]));
            }


            var expressionModifer = new ExpressionModifier(literalToConstName);
            var newRoot = expressionModifer.Visit(root);
            // // 通过遍历语法树，提取修改后的表达式部分
            // var modifiedExpression = ExtractExpression(newRoot);

            var modifiedExpression = newRoot.GetText(Encoding.UTF8).ToString();
            // // 输出修改后的表达式
            // Console.WriteLine($"modifiedExpression : {modifiedExpression}");

            // Identify and wrap function calls
            var invocationExpressions = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
            foreach (var invokeExpr in invocationExpressions)
            {
                var identifier = invokeExpr.Expression as IdentifierNameSyntax;
                if (identifier != null && TypeMapping.HaveValueCallInvocations.Contains(identifier.ToString()))
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

            string parameters = string.Join(", ", identifiers.Select((id, i) => $"{TypeMapping.ParameterTypeToCSharpType(expInfo.parameterTypes[i])} {id}"));
            int paramCount = identifiers.Length;

            string method = $@"
    public static bool {expInfo.functionName}({parameters})
    {{
        {modifiedExpression}
    }}";
            result.methods.Add(method);

            var functionType = $"Func<{string.Join(",", parameterCSharpTypes)}, bool>"; // 返回值为 bool
            if (!delegateTypesMapping.ContainsKey(functionType))
            {
                var builder = new StringBuilder();
                delegateTypesMapping[functionType] = new(
                    $"{paramCount}{string.Join("", parameterEnumTypes)}",
                    paramCount,
                    builder,
                    expInfo.parameterTypes
                );
            }

            delegateTypesMapping[functionType].Item3.AppendLine($"{{\"{expInfo.functionName}\", {expInfo.functionName}}},");
        }

        result.delegateTypesMapping = new Dictionary<string, DelegateType>();
        foreach (var (key, value) in delegateTypesMapping)
        {
            // item1 ，映射 Dictionary 字段的后缀
            result.delegateTypesMapping.Add(key, new DelegateType
            {
                item1 = value.Item1,
                item2 = value.Item2,
                item3 = value.Item3.ToString(),
                // Evaluate 传参
                item4 = string.Join(", ", value.Item4.Select((item, index) => $"{TypeMapping.ParameterTypeToCSharpType(item)} v{index}")),
                // Evaluate Function call 参数
                item5 = string.Join(", ", value.Item4.Select((_, index) => $"v{index}")),
                is_empty = false
            });
        }

        // 确保 Safe Reference Method 都会被生成
        var methods = (JArray)safeReferenceMethodSignatures["methods"]!;
        foreach (var method in methods)
        {
            var parameters = (JArray)method["parameters"]!;
            var parameterTypes = parameters.Select(token => Enum.Parse<ParameterType>(token.ToString())).ToArray();
            var parameterCSharpTypes = parameterTypes.Select(TypeMapping.ParameterTypeToCSharpType).ToArray();
            var functionType = $"Func<{string.Join(",", parameterCSharpTypes)}, bool>"; // 返回值为 bool
            if (!result.delegateTypesMapping.ContainsKey(functionType))
            {
                result.delegateTypesMapping.Add(functionType, new DelegateType
                {
                    // Evaluate 传参
                    item4 = string.Join(", ", parameterTypes.Select((item, index) => $"{TypeMapping.ParameterTypeToCSharpType(item)} v{index}")), is_empty = true,
                });
            }
        }

        var constants = (JArray)safeReferenceMethodSignatures["constants"]!;
        foreach (var constant in constants)
        {
            var constantTypes = (JArray)constant["types"]!;
            var parameterTypes = constantTypes.Select(token => Enum.Parse<ParameterType>(token.ToString())).ToArray();
            var types = parameterTypes.Select(TypeMapping.ParameterTypeToCSharpTypeReflection).ToList();
            var defaultValues = parameterTypes.Select(TypeMapping.ParameterTypeToDefaultValue).ToList();
            var concatKeyType = TypeMapping.ToConcatTypeKey(types);
            var constDictNamePostfix = TypeMapping.ToConcatTypeForPostName(types);
            if (!result.constValueGetters.ContainsKey(concatKeyType))
            {
                result.constValueGetters.Add(concatKeyType,
                    (concatKeyType, constDictNamePostfix, false, defaultValues.Count > 1 ? $"({string.Join(",", defaultValues)})" : defaultValues[0]));
            }
        }

        result.valueCallMappings = valueCallMappings.ToString();
        return result;
    }
}
