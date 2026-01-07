using System;
using System.Collections.Generic;
using System.Linq;
using Myth;

namespace LocalBridge
{
    public class RuleAstConverter
    {
        public static object Convert(MythExprNode node)
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
                        args = funcCall.Arguments.Select(ConvertArg).ToArray(),
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
                    args = functionCall.Arguments.Select(ConvertArg).ToArray(),
                    returnType = MapValueType(functionCall.ReturnType),
                    compare = (object)null
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

        private static void CollectChildren(MythExprNode node, MythLogicalOp op, List<object> children)
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

        private static object ConvertArg(MythExprNode arg)
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

        private static double ExtractNumericValue(MythExprNode node)
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
    }
}

