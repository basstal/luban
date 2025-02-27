// namespace Myth
// {
//     public class IdentifierNode : MythExprNode
//     {
//         // 标识符名称
//         public string Name { get; }
//
//         // 初始的类型是 Unknown
//         // 我们用一个私有字段来存储当前“已解析/推断”的类型
//         private MythValueType _resolvedType = MythValueType.Unknown;
//
//         // 重写基类的 ValueType，让外部读到的是“当前解析出来的类型”
//         public override MythValueType ValueType => _resolvedType;
//
//         public IdentifierNode(string name)
//         {
//             Name = name;
//         }
//
//         // 暴露一个方法，用于在语义分析阶段写回最终类型
//         public void SetResolvedType(MythValueType newType)
//         {
//             _resolvedType = newType;
//         }
//     }
// }
