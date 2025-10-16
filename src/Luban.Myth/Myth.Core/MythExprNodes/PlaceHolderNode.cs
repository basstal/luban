namespace Myth
{
    public class PlaceHolderNode : MythExprNode
    {
        // 标识符名称
        public string Name { get; }

        private MythValueType m_resolvedType = MythValueType.Variable;

        // 重写基类的 ValueType，让外部读到的是“当前解析出来的类型”
        public override MythValueType ValueType => m_resolvedType;

        public PlaceHolderNode(string name, string inOriginalValue)
        {
            Name = name;
            OriginalValue = inOriginalValue;
        }

        public string OutputValue { get; set; }
        public string OriginalValue { get; private set; }

        public bool IsPlaceholder => OriginalValue != null;
    }
}
