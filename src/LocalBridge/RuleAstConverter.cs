using System;
using System.Collections.Generic;
using System.Linq;
using Myth;

namespace LocalBridge
{
    public class RuleAstConverter
    {
        public static object? Convert(MythExprNode? node)
        {
            if (node == null)
            {
                return null;
            }

            if (node is LogicalNode logical)
            {
                return new
                {
                    id = GenerateId(),
                    type = "Group",
                    op = logical.Operator == MythLogicalOp.And ? "&&" : "||",
                    children = FlattenLogicalNode(logical)
                };
            }

            if (node is ComparisonNode comparison)
            {
                if (comparison.Left is FunctionCallNode funcCall)
                {
                    return new
                    {
                        id = GenerateId(),
                        type = "Condition",
                        funcKey = funcCall.FuncName,
                        args = funcCall.Arguments.Select(a => ConvertArg(a)).ToArray(),
                        returnType = MapValueType(funcCall.ReturnType),
                        compare = new
                        {
                            op = MapCompareOp(comparison.Operator),
                            value = ExtractNumericValue(comparison.Right)
                        }
                    };
                }
                // Fallback if left is not a function call, though the target structure seems to expect it
            }

            if (node is FunctionCallNode functionCall)
            {
                return new
                {
                    id = GenerateId(),
                    type = "Condition",
                    funcKey = functionCall.FuncName,
                    args = functionCall.Arguments.Select(a => ConvertArg(a)).ToArray(),
                    returnType = MapValueType(functionCall.ReturnType),
                    compare = (object?)null
                };
            }

            if (node is LiteralNode literal && literal.ValueType == MythValueType.Unknown)
            {
                // 无参函数在语义分析前可能被解析为 Unknown 类型的 LiteralNode
                return new
                {
                    id = GenerateId(),
                    type = "Condition",
                    funcKey = literal.RawValue,
                    args = Array.Empty<object>(),
                    returnType = "Bool", // 默认为 Bool
                    compare = (object?)null
                };
            }

            return null;
        }

        private static List<object> FlattenLogicalNode(LogicalNode node)
        {
            var children = new List<object>();
            CollectChildren(node, node.Operator, children);
            return children;
        }

        private static void CollectChildren(MythExprNode? node, MythLogicalOp op, List<object> children)
        {
            if (node is LogicalNode logical && logical.Operator == op)
            {
                CollectChildren(logical.Left, op, children);
                CollectChildren(logical.Right, op, children);
            }
            else
            {
                var converted = Convert(node);
                if (converted != null)
                {
                    children.Add(converted);
                }
            }
        }

        private static string MapValueType(MythValueType type)
        {
            return type switch
            {
                MythValueType.Int => "Int",
                MythValueType.IntTenThousandth => "RatioInt",
                MythValueType.Bool => "Bool",
                _ => "Int" // Default
            };
        }

        private static string MapCompareOp(MythCompareOp op)
        {
            return op switch
            {
                MythCompareOp.Equal => "==",
                MythCompareOp.NotEqual => "==", // Not in user list, fallback
                MythCompareOp.Greater => ">",
                MythCompareOp.GreaterEqual => ">=",
                MythCompareOp.Less => "<",
                MythCompareOp.LessEqual => "<=",
                _ => "=="
            };
        }

        private static object? ConvertArg(MythExprNode? arg)
        {
            if (arg is LiteralNode literal)
            {
                if ((literal.ValueType == MythValueType.Int || literal.ValueType == MythValueType.IntTenThousandth) && int.TryParse(literal.RawValue, out int i))
                {
                    return i;
                }
                if (literal.ValueType == MythValueType.Float && float.TryParse(literal.RawValue, out float f))
                {
                    return f;
                }
                if (literal.ValueType == MythValueType.Bool && bool.TryParse(literal.RawValue, out bool b))
                {
                    return b;
                }
                return literal.RawValue;
            }
            return arg?.ToString();
        }

        private static double ExtractNumericValue(MythExprNode? node)
        {
            if (node is LiteralNode literal && double.TryParse(literal.RawValue, out double d))
            {
                return d;
            }
            return 0;
        }

        private static string GenerateId()
        {
            return Guid.NewGuid().ToString("n").Substring(0, 8) + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString("x");
        }

        public static string Serialize(System.Text.Json.JsonElement node)
        {
            return SerializeInternal(node, true);
        }

        private static string SerializeInternal(System.Text.Json.JsonElement node, bool isRoot)
        {
            string? type = node.GetProperty("type").GetString();
            if (type == "Group")
            {
                string? op = node.GetProperty("op").GetString();
                var children = node.GetProperty("children").EnumerateArray()
                    .Select(c => SerializeInternal(c, false))
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();

                if (children.Count == 0)
                {
                    return "";
                }
                if (children.Count == 1)
                {
                    return children[0];
                }

                string content = string.Join($" {op} ", children);
                return isRoot ? content : $"({content})";
            }
            else if (type == "Condition")
            {
                string? funcKey = node.GetProperty("funcKey").GetString();
                var argsArray = node.GetProperty("args").EnumerateArray().ToList();

                string dsl;
                if (argsArray.Count > 0)
                {
                    var args = argsArray.Select(SerializeArg).ToList();
                    dsl = $"{funcKey}({string.Join(", ", args)})";
                }
                else
                {
                    dsl = funcKey ?? "";
                }

                if (node.TryGetProperty("compare", out var compare) && compare.ValueKind != System.Text.Json.JsonValueKind.Null)
                {
                    string? compareOp = compare.GetProperty("op").GetString();
                    var compareValue = compare.GetProperty("value");
                    dsl += $" {compareOp} {compareValue}";
                }
                return dsl;
            }
            return "";
        }

        private static string SerializeArg(System.Text.Json.JsonElement arg)
        {
            switch (arg.ValueKind)
            {
                case System.Text.Json.JsonValueKind.String:
                    // 根据用户要求，函数参数不需要用 "" 包装
                    return arg.GetString() ?? "";
                case System.Text.Json.JsonValueKind.True:
                    return "true";
                case System.Text.Json.JsonValueKind.False:
                    return "false";
                case System.Text.Json.JsonValueKind.Number:
                    return arg.GetRawText();
                default:
                    return arg.GetRawText();
            }
        }
    }
}
