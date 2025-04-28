using System;
using System.Collections.Generic;
using System.Linq;
using Luban.Defs;

namespace Myth
{
    public class MythMetadata
    {
        // public string ConditionKey; // 唯一标识
        // public string OriginalText; // 原始文本
        // public string MethodName = string.Empty; // 生成的方法名
        public List<string> Identifiers = new List<string>(); // 收集到的所有标识符
        public string Function = string.Empty; // 收集到的所有函数名
        public List<string> FunctionParameters = new List<string>(); // 函数入参
        public List<MythValueType> FunctionParameterTypes = new List<MythValueType>(); // 函数参数类型
        public List<string> IntLiterals = new List<string>(); // int 常量
        public List<string> StringLiterals = new List<string>(); // string 常量
        public List<string> BoolLiterals = new List<string>(); // bool 常量
        public List<string> EnumLiterals = new List<string>(); // 枚举常量
        public MythCompareOp Operator = MythCompareOp.Unknown;
        public MythValueType FunctionReturnType = MythValueType.Unknown;
        public string CompareToLiteralValue = string.Empty;
        public bool IsCompareLiteralLeftSide;
        public string EvaluateType = string.Empty;
        public bool IsParams { get; set; }

        // ... 你也可以再加更多字段，比如运算符信息、参数列表信息等
    }

    public static class MythMetadataCollector
    {
        public static List<MythMetadata> Collect(MythExprNode node, List<DefEnum> exportEnums)
        {
            var metaList = new List<MythMetadata>();
            Traverse(node, metaList, exportEnums);
            return metaList;
        }

        private static void Traverse(MythExprNode node, List<MythMetadata> metaList, List<DefEnum> exportEnums)
        {
            if (node == null)
            {
                return;
            }

            if (node is LogicalNode ln2)
            {
                var leftMeta = new MythMetadata();
                var rightMeta = new MythMetadata();
                Traverse(ln2.Left, new List<MythMetadata> { leftMeta }, exportEnums);
                Traverse(ln2.Right, new List<MythMetadata> { rightMeta }, exportEnums);
                metaList.Add(leftMeta);
                metaList.Add(rightMeta);
            }
            else
            {
                var meta = new MythMetadata();
                CollectMetadata(node, meta, exportEnums);
                metaList.Add(meta);
            }
        }

        public static void CollectMetadata(MythExprNode node, MythMetadata meta, List<DefEnum> exportEnums)
        {
            if (node == null)
            {
                return;
            }

            switch (node)
            {
                case LiteralNode ln:
                    if (ln.ValueType == MythValueType.Int)
                    {
                        meta.IntLiterals.Add(ln.RawValue);
                        meta.CompareToLiteralValue = ln.RawValue;
                    }

                    if (ln.ValueType == MythValueType.IntTenThousandth)
                    {
                        var value = ((int)(float.Parse(ln.RawValue) * 10000)).ToString();
                        meta.IntLiterals.Add(value);
                        meta.CompareToLiteralValue = value;
                    }

                    if (ln.ValueType == MythValueType.String)
                    {
                        meta.StringLiterals.Add(ln.RawValue);
                        meta.CompareToLiteralValue = ln.RawValue;
                    }

                    if (ln.ValueType == MythValueType.Bool)
                    {
                        meta.BoolLiterals.Add(ln.RawValue);
                        meta.CompareToLiteralValue = ln.RawValue;
                    }

                    if (ln.ValueType == MythValueType.Enum)
                    {
                        meta.EnumLiterals.Add(ln.RawValue);
                        meta.CompareToLiteralValue = ln.RawValue;
                    }

                    break;

                case FunctionCallNode fn:
                    meta.Function = fn.FuncName;
                    meta.IsParams = fn.FunctionSignature.IsParams;
                    meta.FunctionReturnType = fn.ReturnType;
                    meta.FunctionParameterTypes = fn.FunctionSignature.ParamTypes;
                    meta.EvaluateType = MythConverter.GetEvalFunctionByFunctionSignature(fn.FunctionSignature);
                    for (int i = 0; i < fn.Arguments.Count; i++)
                    {
                        var arg = fn.Arguments[i];
                        CollectMetadata(arg, meta, exportEnums);
                        if (arg is LiteralNode ln2)
                        {
                            if (ln2.ValueType == MythValueType.IntTenThousandth)
                            {
                                var value = ((int)(float.Parse(ln2.RawValue) * 10000)).ToString();
                                meta.FunctionParameters.Add(value);
                            }
                            else if (ln2.ValueType == MythValueType.Enum)
                            {
                                if (fn.FunctionSignature.ParamActualTypeDict.TryGetValue(i, out var actualTypeStr))
                                {
                                    // 从 luban 的 enum 定义中反射获取 actualTypeStr 对应的 DefEnum，并使用 DefEnum 反射中文的 RawValue 到对应的 Enum 内容。
                                    var enumDef = exportEnums.Find(defEnum => defEnum.FullName == actualTypeStr);
                                    if (enumDef == null)
                                    {
                                        throw new NotImplementedException($"Enum {actualTypeStr} not found in export enums");
                                    }

                                    var enumItem = enumDef.Items.Find(item => item.Name == ln2.RawValue || item.Alias == ln2.RawValue);
                                    if (enumItem == null)
                                    {
                                        throw new NotImplementedException($"Enum item {ln2.RawValue} not found in enum {actualTypeStr}");
                                    }

                                    meta.FunctionParameters.Add(enumItem.IntValue.ToString());
                                }
                                else
                                {
                                    throw new NotImplementedException($"函数 {fn.FuncName} 的第 {i + 1} 个参数类型不是 枚举 ？但是调用的参数却传入了 枚举 类型？");
                                }
                            }
                            else
                            {
                                meta.FunctionParameters.Add(ln2.RawValue);
                            }
                        }
                    }

                    break;

                case ComparisonNode cn:
                    meta.Operator = cn.Operator;
                    // 目前 metadata 仅支持单边为字面值常量
                    meta.IsCompareLiteralLeftSide = cn.Left is LiteralNode;
                    CollectMetadata(cn.Left, meta, exportEnums);
                    CollectMetadata(cn.Right, meta, exportEnums);
                    break;
            }
        }
    }
}
