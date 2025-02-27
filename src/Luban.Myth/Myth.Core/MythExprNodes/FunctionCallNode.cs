namespace Myth;

public class FunctionCallNode : MythExprNode
{
    public string FuncName;

    public List<MythExprNode?> Arguments = new List<MythExprNode?>();

    public MythValueType ReturnType = MythValueType.Unknown;

    public override MythValueType ValueType => ReturnType;

    public FunctionSignature FunctionSignature { get; set; }

    public FunctionCallNode(string funcName)
    {
        FuncName = funcName;
    }
}
