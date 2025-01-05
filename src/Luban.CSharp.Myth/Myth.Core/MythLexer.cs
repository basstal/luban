using System.Text;

namespace Myth;

public class MythLexer
{
    private string _input;
    private int _pos;
    private int _length;
    private Dictionary<char, char> _charMapping;

    public MythLexer(string input)
    {
        string charMappingFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Myth.Core/CharMapping.txt");
        _charMapping = LoadCharMapping(charMappingFilePath);
        _input = PreprocessInput(input);
        _length = input.Length;
        _pos = 0;
    }

    private Dictionary<char, char> LoadCharMapping(string filePath)
    {
        var mapping = new Dictionary<char, char>();
        foreach (var line in File.ReadAllLines(filePath))
        {
            var parts = line.Split('=');
            if (parts.Length == 2 && parts[0].Length == 1 && parts[1].Length == 1)
            {
                mapping[parts[0][0]] = parts[1][0];
            }
        }

        return mapping;
    }

    private string PreprocessInput(string input)
    {
        var sb = new StringBuilder(input.Length);
        foreach (var c in input)
        {
            if (_charMapping.TryGetValue(c, out var mappedChar))
            {
                sb.Append(mappedChar);
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    public List<MythToken> Tokenize()
    {
        var tokens = new List<MythToken>();

        while (!IsEnd())
        {
            char c = Peek();
            if (char.IsWhiteSpace(c))
            {
                Advance();
                continue;
            }

            // 1. 标识符或布尔值
            if (char.IsLetter(c) || c == '_')
            {
                tokens.Add(ReadIdentifierOrBool());
                continue;
            }

            // 2. 数字
            if (char.IsDigit(c))
            {
                tokens.Add(ReadNumber());
                continue;
            }

            // 3. 字符串: "xxx"
            if (c == '"')
            {
                tokens.Add(ReadStringLiteral());
                continue;
            }

            // 4. 运算符或符号
            //    例如: ==, !=, >=, <=, &&, ||
            //    还有括号 ( ), 逗号 , 等
            switch (c)
            {
                case '=':
                    if (PeekNext() == '=')
                    {
                        tokens.Add(new MythToken(MythTokenType.Equal, "=="));
                        Advance(2);
                    }
                    else
                    {
                        // 简化，假设单独 '=' 不在本示例中出现
                        Advance();
                    }

                    break;
                case '!':
                    if (PeekNext() == '=')
                    {
                        tokens.Add(new MythToken(MythTokenType.NotEqual, "!="));
                        Advance(2);
                    }
                    else
                    {
                        Advance();
                    }

                    break;
                case '>':
                    if (PeekNext() == '=')
                    {
                        tokens.Add(new MythToken(MythTokenType.GreaterEq, ">="));
                        Advance(2);
                    }
                    else
                    {
                        tokens.Add(new MythToken(MythTokenType.Greater, ">"));
                        Advance();
                    }

                    break;
                case '<':
                    if (PeekNext() == '=')
                    {
                        tokens.Add(new MythToken(MythTokenType.LessEq, "<="));
                        Advance(2);
                    }
                    else
                    {
                        tokens.Add(new MythToken(MythTokenType.Less, "<"));
                        Advance();
                    }

                    break;
                case '&':
                    if (PeekNext() == '&')
                    {
                        tokens.Add(new MythToken(MythTokenType.AndAnd, "&&"));
                        Advance(2);
                    }
                    else
                    {
                        Advance();
                    }

                    break;
                case '|':
                    if (PeekNext() == '|')
                    {
                        tokens.Add(new MythToken(MythTokenType.OrOr, "||"));
                        Advance(2);
                    }
                    else
                    {
                        Advance();
                    }

                    break;
                case '(':
                    tokens.Add(new MythToken(MythTokenType.LParen, "("));
                    Advance();
                    break;
                case ')':
                    tokens.Add(new MythToken(MythTokenType.RParen, ")"));
                    Advance();
                    break;
                case ',':
                    tokens.Add(new MythToken(MythTokenType.Comma, ","));
                    Advance();
                    break;
                default:
                    // 简化：遇到无法识别的字符，就跳过
                    Advance();
                    break;
            }
        }

        // 结束 Token
        tokens.Add(new MythToken(MythTokenType.End, ""));
        return tokens;
    }

    private MythToken ReadIdentifierOrBool()
    {
        int start = _pos;
        while (!IsEnd() && (char.IsLetterOrDigit(Peek()) || Peek() == '_'))
        {
            Advance();
        }

        string text = _input.Substring(start, _pos - start);

        // 如果是 "true" 或 "false"
        if (text == "true" || text == "false")
        {
            return new MythToken(MythTokenType.BoolLiteral, text);
        }

        return new MythToken(MythTokenType.Identifier, text);
    }

    private MythToken ReadNumber()
    {
        int start = _pos;
        while (!IsEnd() && char.IsDigit(Peek()))
        {
            Advance();
        }

        string text = _input.Substring(start, _pos - start);
        return new MythToken(MythTokenType.IntLiteral, text);
    }

    private MythToken ReadStringLiteral()
    {
        // 跳过前导双引号
        Advance(); // skip "
        int start = _pos;

        while (!IsEnd() && Peek() != '"')
        {
            Advance();
        }

        string text = _input.Substring(start, _pos - start);
        // 跳过结束引号
        if (!IsEnd()) Advance();

        return new MythToken(MythTokenType.StringLiteral, text);
    }

    private char Peek(int offset = 0)
    {
        if (_pos + offset < _length)
        {
            return _input[_pos + offset];
        }

        return '\0';
    }

    private char PeekNext()
    {
        return Peek(1);
    }

    private void Advance(int step = 1)
    {
        _pos += step;
    }

    private bool IsEnd()
    {
        return _pos >= _length;
    }
}
