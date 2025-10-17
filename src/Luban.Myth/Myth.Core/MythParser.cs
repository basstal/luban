namespace Myth;

public class MythParser
{
    private List<MythToken> m_tokens;
    private int m_index;

    public MythParser(List<MythToken> tokens)
    {
        m_tokens = tokens;
        m_index = 0;
    }

    public MythExprNode ParseStatement()
    {
        if (Match(MythTokenType.KeywordReturn))
        {
            return ParseReturnStatement();
        }
        return ParseExpression();
    }

    private MythExprNode ParseReturnStatement()
    {
        Consume(MythTokenType.KeywordReturn);
        MythExprNode value = null;
        if (!IsEnd() && !Match(MythTokenType.Semicolon))
        {
            value = ParseExpression(false);
        }
        return new ReturnNode(value);
    }

    public MythExprNode ParseExpression(bool allowList = true)
    {
        // Start parsing from the lowest precedence level (assignment)
        return ParseAssignmentExpr(allowList);
    }

    // Precedence 0: Assignment (=) - Right-associative
    private MythExprNode ParseAssignmentExpr(bool allowList)
    {
        var left = ParseOrExpr(allowList); // Parse higher-precedence expression first

        if (Match(MythTokenType.Assign))
        {
            // The target of an assignment must be a valid l-value (e.g., an identifier or a declaration).
            bool isValidLValue = left is PlaceHolderNode ||
                                 left is DeclarationExpressionNode ||
                                 (left is LiteralNode ln && ln.ValueType == MythValueType.Unknown);

            if (!isValidLValue)
            {
                throw new Exception($"Invalid assignment target. Expected an identifier or declaration but got {left.GetType().Name}.");
            }

            Consume(MythTokenType.Assign);
            // Recursively call for right-associativity (e.g., a = b = 5)
            var right = ParseAssignmentExpr(false);
            return new AssignmentNode(left, right);
        }

        return left;
    }

    // Precedence 1: Or (||)
    private MythExprNode ParseOrExpr(bool allowList)
    {
        var left = ParseAndExpr(allowList);
        while (Match(MythTokenType.OrOr))
        {
            Consume(MythTokenType.OrOr);
            // The right-hand side of an operator can't be a list
            var right = ParseAndExpr(false);
            left = new LogicalNode(left, MythLogicalOp.Or, right);
        }
        return left;
    }

    // Precedence 2: And (&&)
    private MythExprNode ParseAndExpr(bool allowList)
    {
        var left = ParseComparison(allowList);
        while (Match(MythTokenType.AndAnd))
        {
            Consume(MythTokenType.AndAnd);
            var right = ParseComparison(false);
            left = new LogicalNode(left, MythLogicalOp.And, right);
        }
        return left;
    }

    // Precedence 3: Comparison (==, !=, >, etc.)
    private MythExprNode ParseComparison(bool allowList)
    {
        var left = ParseAdditiveExpr(allowList);
        // Comparison operators are not associative, so we only parse one
        if (Match(MythTokenType.Equal, MythTokenType.NotEqual, MythTokenType.Greater, MythTokenType.GreaterEq, MythTokenType.Less, MythTokenType.LessEq))
        {
            var opToken = Peek();
            var op = TokenToCompareOp(opToken.Type);
            Advance();
            var right = ParseAdditiveExpr(false);
            return new ComparisonNode(left, op, right);
        }
        return left;
    }

    // Precedence 4: Additive (+, -)
    private MythExprNode ParseAdditiveExpr(bool allowList)
    {
        var left = ParseMultiplicativeExpr(allowList);
        while (Match(MythTokenType.Plus, MythTokenType.Minus))
        {
            var opToken = Peek();
            var op = TokenToArithmeticOp(opToken.Type);
            Advance();
            var right = ParseMultiplicativeExpr(false);
            left = new ArithmeticNode(left, op, right);
        }
        return left;
    }

    // Precedence 5: Multiplicative (*, /)
    private MythExprNode ParseMultiplicativeExpr(bool allowList)
    {
        var left = ParsePrimaryExpr(allowList);
        while (Match(MythTokenType.Asterisk, MythTokenType.Slash))
        {
            var opToken = Peek();
            var op = TokenToArithmeticOp(opToken.Type);
            Advance();
            var right = ParsePrimaryExpr(false);
            left = new ArithmeticNode(left, op, right);
        }
        return left;
    }

    // Precedence 6: Primary (Parentheses, Literals, Functions)
    private MythExprNode ParsePrimaryExpr(bool allowList)
    {
        if (Match(MythTokenType.LParen))
        {
            // Lookahead to check for a cast expression: `(TypeName)expr`
            var tokenInside = Peek(1);
            var tokenAfter = Peek(2);

            if (tokenInside.Type == MythTokenType.Identifier && tokenAfter.Type == MythTokenType.RParen && MythTypeUtil.TryParseType(tokenInside.Text, out var targetType))
            {
                // It's a cast expression
                Consume(MythTokenType.LParen);
                Consume(MythTokenType.Identifier);
                Consume(MythTokenType.RParen);

                // The cast operator has high precedence, so it applies to the next primary expression.
                var expressionToCast = ParsePrimaryExpr(false);
                return new CastExpressionNode(targetType, expressionToCast);
            }
            else
            {
                // It's a regular parenthesized expression
                Consume(MythTokenType.LParen);
                // Restart the precedence chain inside the parentheses
                var expr = ParseExpression(false);
                Consume(MythTokenType.RParen);
                return expr;
            }
        }
        // If not a parenthesis, it must be a value (literal, variable, function call)
        return ParseValueExpr(allowList);
    }

    // Parses literals, identifiers (variables), and function calls
    private MythExprNode ParseValueExpr(bool allowList = false)
    {
        MythExprNode ParseSingleValue()
        {
            var token = Peek();
            switch (token.Type)
            {
                case MythTokenType.Identifier:
                    return ParseFunctionOrIdentifier();
                case MythTokenType.IntLiteral:
                    Advance();
                    return new LiteralNode(token.Text, MythValueType.Int);
                case MythTokenType.StringLiteral:
                    Advance();
                    return new LiteralNode(token.Text, MythValueType.String);
                case MythTokenType.BoolLiteral:
                    Advance();
                    return new LiteralNode(token.Text, MythValueType.Bool);
                case MythTokenType.FloatLiteral:
                    Advance();
                    return new LiteralNode(token.Text, MythValueType.Float);
                default:
                    // This case should ideally not be hit with valid syntax
                    Advance();
                    return null;
            }
        }

        // The allowList logic is for comma-separated values, which can only
        // be parsed at the start of an expression.
        if (!allowList)
        {
            return ParseSingleValue();
        }

        var nodes = new List<MythExprNode>();
        while (true)
        {
            var node = ParseSingleValue();
            if (node != null)
            {
                nodes.Add(node);
            }

            if (Match(MythTokenType.Comma))
            {
                Consume(MythTokenType.Comma);
                continue;
            }
            if (Match(MythTokenType.Semicolon))
            {
                Consume(MythTokenType.Semicolon);
                continue;
            }
            break;
        }

        if (nodes.Count == 1)
        { return nodes[0]; }
        if (nodes.Count > 1)
        { return new ListNode(nodes); }
        return null;
    }

    private MythExprNode ParseFunctionOrIdentifier()
    {
        var idToken = Peek();
        var tokenText = idToken.Text;

        // If `id id`, parse as a declaration fragment
        if (Peek(1).Type == MythTokenType.Identifier)
        {
            Advance(); // Consume type identifier
            var typeIdNode = new LiteralNode(tokenText, MythValueType.Unknown);

            var varToken = Peek();
            Advance(); // Consume variable identifier
            var varIdNode = new LiteralNode(varToken.Text, MythValueType.Unknown);

            return new DeclarationExpressionNode(typeIdNode, varIdNode);
        }

        // Standard identifier or function call parsing
        Advance();

        // If followed by '(', it's a function call
        if (Match(MythTokenType.LParen))
        {
            Consume(MythTokenType.LParen);
            var funcNode = new FunctionCallNode(tokenText);

            if (!Match(MythTokenType.RParen))
            {
                do
                {
                    // Arguments are full expressions themselves
                    var argExpr = ParseExpression(false);
                    funcNode.Arguments.Add(argExpr);
                } while (Match(MythTokenType.Comma) && Consume(MythTokenType.Comma) != null);
            }

            Consume(MythTokenType.RParen);
            return funcNode;
        }

        // Otherwise, it's a variable/identifier
        return new LiteralNode(tokenText, MythValueType.Unknown);
    }

    // Helper methods to convert tokens to AST operator types
    private MythArithmeticOp TokenToArithmeticOp(MythTokenType type)
    {
        return type switch
        {
            MythTokenType.Plus => MythArithmeticOp.Add,
            MythTokenType.Minus => MythArithmeticOp.Subtract,
            MythTokenType.Asterisk => MythArithmeticOp.Multiply,
            MythTokenType.Slash => MythArithmeticOp.Divide,
            _ => throw new Exception($"Unknown arithmetic operator token: {type}")
        };
    }

    private MythCompareOp TokenToCompareOp(MythTokenType type)
    {
        return type switch
        {
            MythTokenType.Equal => MythCompareOp.Equal,
            MythTokenType.NotEqual => MythCompareOp.NotEqual,
            MythTokenType.Greater => MythCompareOp.Greater,
            MythTokenType.GreaterEq => MythCompareOp.GreaterEqual,
            MythTokenType.Less => MythCompareOp.Less,
            MythTokenType.LessEq => MythCompareOp.LessEqual,
            _ => throw new Exception("Unknown compare op token")
        };
    }

    // Utility methods for token stream manipulation
    private bool Match(params MythTokenType[] types)
    {
        if (IsEnd())
        { return false; }
        var currentType = m_tokens[m_index].Type;
        return types.Contains(currentType);
    }

    private MythToken Consume(MythTokenType type)
    {
        if (Match(type))
        {
            var t = m_tokens[m_index];
            m_index++;
            return t;
        }
        return null;
    }

    private MythToken Peek(int offset = 0)
    {
        if (m_index + offset < m_tokens.Count)
        {
            return m_tokens[m_index + offset];
        }
        return m_tokens[m_tokens.Count - 1]; // Return END token
    }

    private void Advance() => m_index++;
    private bool IsEnd() => m_index >= m_tokens.Count || m_tokens[m_index].Type == MythTokenType.End;
}
