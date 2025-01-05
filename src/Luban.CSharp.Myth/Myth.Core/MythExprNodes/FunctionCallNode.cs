namespace Myth;

public class FunctionCallNode : MythExprNode
{
    public string FuncName;

    public List<MythExprNode> Arguments = new List<MythExprNode>();

    // 假设本示例函数调用只返回 int 或 bool
    public MythValueType ReturnType = MythValueType.Int;

    public override MythValueType ValueType => ReturnType;

    public FunctionCallNode(string funcName)
    {
        FuncName = funcName;
    }
}
