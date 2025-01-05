// using Luban.Utils;
//
// public class TypeMapping
// {
//     public static string ParameterTypeToCSharpType(ParameterType expInfoParameterType)
//     {
//         return expInfoParameterType switch
//         {
//             ParameterType.Integer => "int",
//             ParameterType.ListInteger => "List<int>",
//             ParameterType.Bool => "bool",
//             _ => throw new ArgumentOutOfRangeException(nameof(expInfoParameterType), expInfoParameterType, null)
//         };
//     }
//
//     public static Type ParameterTypeToCSharpTypeReflection(ParameterType expInfoParameterType)
//     {
//         return expInfoParameterType switch
//         {
//             ParameterType.Integer => typeof(int),
//             ParameterType.ListInteger => typeof(List<int>),
//             ParameterType.Bool => typeof(bool),
//             _ => throw new ArgumentOutOfRangeException(nameof(expInfoParameterType), expInfoParameterType, null)
//         };
//     }
//
//
//     public static string ParameterTypeToDefaultValue(ParameterType expInfoParameterType)
//     {
//         return expInfoParameterType switch
//         {
//             ParameterType.Integer => "int.MinValue",
//             ParameterType.ListInteger => "int.MinValue",
//             ParameterType.Bool => "false",
//             _ => throw new ArgumentOutOfRangeException(nameof(expInfoParameterType), expInfoParameterType, null)
//         };
//     }
//
//     // 定义一个字典，用于存储基础数据类型和它们的对应名称
//     private static readonly Dictionary<Type, string> _primitiveTypeToStringMap = new Dictionary<Type, string>
//     {
//         { typeof(int), "int" },
//         { typeof(double), "double" },
//         { typeof(bool), "bool" },
//         { typeof(decimal), "decimal" },
//         { typeof(long), "long" },
//         { typeof(short), "short" },
//         { typeof(byte), "byte" },
//         { typeof(char), "char" },
//         { typeof(float), "float" }
//     };
//
//     public static List<string> HaveValueCallInvocations = new List<string>() { "Max", "Min", "Average", };
//
//
//     public static string ToConcatTypeForPostName(List<Type> inTypes)
//     {
//         return inTypes.Count == 1
//             ? TypeUtil.ToPascalCase(GetPrimitiveTypeName(inTypes[0]))
//             : TypeUtil.ToPascalCase(string.Join("_", inTypes.Select(GetPrimitiveTypeName)));
//     }
//
//     public static string ToConcatTypeKey(List<Type> inTypes)
//     {
//         return inTypes.Count == 1
//             ? GetPrimitiveTypeName(inTypes[0])
//             : $"({string.Join(",", inTypes.Select(GetPrimitiveTypeName))})";
//     }
//
//     // 获取基础类型的字符串表示
//     public static string GetPrimitiveTypeName(Type type)
//     {
//         // 如果类型是 Nullable<T> 类型，则获取它的基础类型
//         if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
//         {
//             var underlyingType = type.GetGenericArguments()[0];
//             return GetPrimitiveTypeName(underlyingType); // 递归处理，获取基础类型的名称
//         }
//
//         // 如果是基础类型，直接返回对应的名称
//         if (_primitiveTypeToStringMap.TryGetValue(type, out var typeName))
//         {
//             return typeName;
//         }
//
//         // 如果类型不在字典中，返回类型的名字（可能是自定义类型）
//         return type.Name;
//     }
// }
