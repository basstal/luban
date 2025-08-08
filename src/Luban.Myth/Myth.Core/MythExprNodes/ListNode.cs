namespace Myth;

using System.Collections.Generic;

/// <summary>
/// 表示一组字面量节点（如 0.2,2,haha 这种逗号分隔的字面量序列）
/// </summary>
public class ListNode : MythExprNode
{
    public List<MythExprNode> Elements { get; }

    public ListNode(List<MythExprNode> elements)
    {
        Elements = elements;
    }

    public override MythValueType ValueType => MythValueType.ValueArray;
}