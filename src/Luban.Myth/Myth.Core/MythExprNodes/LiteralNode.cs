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
}
