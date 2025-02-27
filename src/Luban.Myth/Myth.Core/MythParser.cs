namespace Myth;

public class MythParser
{
    private List<MythToken> _tokens;
    private int _index;

    public MythParser(List<MythToken> tokens)
    {
        _tokens = tokens;
        _index = 0;
    }

    public MythExprNode ParseExpressionAndAnalyzeAST()
    {
        // 对应 Expression -> OrExpr
        var result = ParseOrExpr();
        // 分析 AST
        result = MythSemanticAnalyzer.AnalyzeAST(result);

        return result!;
    }

    public MythExprNode ParseExpression()
    {
        // 对应 Expression -> OrExpr
        return ParseOrExpr();
    }

    // 解析 Or
    private MythExprNode ParseOrExpr()
    {
        var left = ParseAndExpr();
        while (Match(MythTokenType.OrOr))
        {
            Consume(MythTokenType.OrOr); // 消耗 ||
            var right = ParseAndExpr();
            left = new LogicalNode(left, MythLogicalOp.Or, right);
        }

        return left;
    }

    // 解析 And
    private MythExprNode ParseAndExpr()
    {
        var left = ParsePrimaryExpr();
        while (Match(MythTokenType.AndAnd))
        {
            Consume(MythTokenType.AndAnd); // 消耗 &&
            var right = ParsePrimaryExpr();
            left = new LogicalNode(left, MythLogicalOp.And, right);
        }

        return left;
    }

    // 解析比较 / 括号优先级
    private MythExprNode ParsePrimaryExpr()
    {
        // 括号优先
        if (Match(MythTokenType.LParen))
        {
            Consume(MythTokenType.LParen);
            var expr = ParseExpression();
            Consume(MythTokenType.RParen);
            return expr;
        }

        // 否则解析Comparison
        return ParseComparison();
    }

    private MythExprNode ParseComparison()
    {
        // 先解析 leftTerm
        var left = ParseValueExpr();
        // 看下一个 Token 是否是比较运算符
        if (Match(MythTokenType.Equal, MythTokenType.NotEqual, MythTokenType.Greater,
                MythTokenType.GreaterEq, MythTokenType.Less, MythTokenType.LessEq))
        {
            var opToken = Peek();
            var op = TokenToCompareOp(opToken.Type);
            Advance();
            var right = ParseValueExpr();
            return new ComparisonNode(left, op, right);
        }

        return left;
    }

    // 解析标识符/函数调用/字面值
    private MythExprNode ParseValueExpr()
    {
        var token = Peek();
        if (token.Type == MythTokenType.Identifier)
        {
            // 有可能是函数调用
            return ParseFunctionOrIdentifier();
        }
        else if (token.Type == MythTokenType.IntLiteral)
        {
            Advance();
            return new LiteralNode(token.Text, MythValueType.Int);
        }
        else if (token.Type == MythTokenType.StringLiteral)
        {
            Advance();
            return new LiteralNode(token.Text, MythValueType.String);
        }
        else if (token.Type == MythTokenType.BoolLiteral)
        {
            Advance();
            return new LiteralNode(token.Text, MythValueType.Bool);
        }
        else if (token.Type == MythTokenType.FloatLiteral)
        {
            Advance();
            return new LiteralNode(token.Text, MythValueType.Float);
        }

        // 如果都不是，返回一个空节点(简化处理)
        Advance();
        return null;
    }


    private MythExprNode ParseFunctionOrIdentifier()
    {
        // // Load function mapping
        // var functionCallMappingFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Myth.Core/FunctionCallMapping.txt");
        // var functionMapping = LoadFunctionMapping(functionCallMappingFilePath);

        // 先拿下标识符
        var idToken = Peek();
        var tokenText = idToken.Text;
        Advance();

        // 看下一个是否是 "("
        if (Match(MythTokenType.LParen))
        {
            // 函数调用
            Consume(MythTokenType.LParen);
            var funcNode = new FunctionCallNode(tokenText);

            if (!Match(MythTokenType.RParen))
            {
                // 解析参数列表
                do
                {
                    var argExpr = ParseExpression();
                    funcNode.Arguments.Add(argExpr);
                } while (Match(MythTokenType.Comma) && Consume(MythTokenType.Comma) != null);
            }

            Consume(MythTokenType.RParen);

            // // 根据函数名从映射中获取参数类型和返回类型
            // if (functionMapping.TryGetValue(funcName, out var funcInfo))
            // {
            //     funcNode.ReturnType = funcInfo.ReturnType;
            //     // Optionally, validate the argument types
            //     // if (funcNode.Arguments.Count != funcInfo.ParamTypes.Count)
            //     // {
            //     //     throw new Exception($"Function {funcName} expects {funcInfo.ParamTypes.Count} arguments, but got {funcNode.Arguments.Count}");
            //     // }
            //
            //     for (int i = 0; i < funcNode.Arguments.Count; i++)
            //     {
            //         if (funcNode.Arguments[i].ValueType != funcInfo.ParamTypes[i])
            //         {
            //             throw new Exception($"Argument {i + 1} of function {funcName} expects type {funcInfo.ParamTypes[i]}, but got {funcNode.Arguments[i].ValueType}");
            //         }
            //     }
            // }
            // else
            // {
            //     throw new Exception($"Function {funcName} not found in mapping");
            // }

            return funcNode;
        }

        // 普通标识符
        return new LiteralNode(tokenText, MythValueType.Unknown);
    }

    private MythCompareOp TokenToCompareOp(MythTokenType type)
    {
        switch (type)
        {
            case MythTokenType.Equal: return MythCompareOp.Equal;
            case MythTokenType.NotEqual: return MythCompareOp.NotEqual;
            case MythTokenType.Greater: return MythCompareOp.Greater;
            case MythTokenType.GreaterEq: return MythCompareOp.GreaterEqual;
            case MythTokenType.Less: return MythCompareOp.Less;
            case MythTokenType.LessEq: return MythCompareOp.LessEqual;
            default:
                throw new Exception("Unknown compare op token");
        }
    }

    // 工具函数
    private bool Match(params MythTokenType[] types)
    {
        if (IsEnd()) return false;
        var currentType = _tokens[_index].Type;
        return types.Contains(currentType);
    }

    private MythToken Consume(MythTokenType type)
    {
        if (Match(type))
        {
            var t = _tokens[_index];
            _index++;
            return t;
        }

        return null;
    }

    private MythToken Peek(int offset = 0)
    {
        if (_index + offset < _tokens.Count)
            return _tokens[_index + offset];
        return _tokens[_tokens.Count - 1]; // END
    }

    private void Advance() => _index++;
    private bool IsEnd() => _index >= _tokens.Count || _tokens[_index].Type == MythTokenType.End;
}
