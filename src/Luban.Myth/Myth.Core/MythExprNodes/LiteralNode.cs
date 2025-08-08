namespace Myth;

/// <summary>
/// 统一的“值”节点，可表示数字、布尔字面量、字符串字面量，
/// 也可表示 “标识符”(LitType=Unknown, RawValue=符号名)。
/// </summary>
public class LiteralNode : MythExprNode
{
    // 节点的原始文本，比如 "123"、"true"、"房间翻新" 等
    public string RawValue;

    // 当前推断或声明的类型
    private MythValueType _litType;

    public override MythValueType ValueType => _litType;
    // public FunctionSignature NoArgumentFunctionSignature { get; set; }

    public LiteralNode(string rawValue, MythValueType litType)
    {
        RawValue = rawValue;
        _litType = litType;
    }

    /// <summary>
    /// 允许语义分析阶段更新类型
    /// </summary>
    public void SetType(MythValueType newType)
    {
        _litType = newType;
    }

    // public static bool IsEqual(MythValueType a, MythValueType b)
    // {
    //     if (a == MythValueType.IntTenThousandth || a == MythValueType.Float)
    //     {
    //         return b == MythValueType.IntTenThousandth || b == MythValueType.Float;
    //     }
    //     if (a == MythValueType.Int || a == MythValueType.IntTenThousandth)
    //     {
    //         return b == MythValueType.Int || b == MythValueType.IntTenThousandth;
    //     }
    //     return a == b;
    // }

    public static bool CanConvert(MythValueType from, MythValueType to)
    {
        if (from == to)
        {
            return true;
        }
        switch (from)
        {
            case MythValueType.IntTenThousandth:
                return to == MythValueType.Float || to == MythValueType.Int;
            case MythValueType.Float:
                return to == MythValueType.IntTenThousandth;
            case MythValueType.Int:
                return to == MythValueType.IntTenThousandth || to == MythValueType.Float;
        }
        return false;
    }

    public void ConvertToType(MythValueType to)
    {
        switch (_litType)
        {
            case MythValueType.IntTenThousandth:
                if (to == MythValueType.Int)
                {
                    throw new NotImplementedException();
                }
                if (to == MythValueType.Float)
                {
                    // do nothing
                }
                break;
            case MythValueType.Float:
                if (to == MythValueType.IntTenThousandth)
                {
                    // do nothing
                }
                break;
            case MythValueType.Int:
                if (to == MythValueType.IntTenThousandth)
                {
                    // do nothing
                }
                if (to == MythValueType.Float)
                {
                    _litType = MythValueType.Float;
                }
                break;
        }
    }
}
