namespace Myth
{
    public class MythTypeUtil
    {

        public static bool TryParseType(string typeStr, out MythValueType outType)
        {
            outType = ParseType(typeStr);
            return outType != MythValueType.Unknown;
        }

        /// <summary>
        /// 将字符串 "int","bool","string" 转成 MythValueType，否则 Unknown
        /// </summary>
        public static MythValueType ParseType(string typeStr)
        {
            switch (typeStr)
            {
                case "整数":
                    return MythValueType.Int;
                case "万分比整数":
                    return MythValueType.IntTenThousandth;
                case "布尔":
                    return MythValueType.Bool;
                case "字符串":
                    return MythValueType.String;
                case "枚举":
                    return MythValueType.Enum;
                case "浮点数":
                    return MythValueType.Float;
                case "长整数":
                    return MythValueType.Long;
                default:
                    return MythValueType.Unknown;
            }
        }

        public static string MythValueTypeToCSharpNoFloat(MythValueType inValueType)
        {
            switch (inValueType)
            {
                case MythValueType.Int:
                case MythValueType.IntTenThousandth:
                case MythValueType.Float:
                    return "int";
                case MythValueType.Bool:
                    return "bool";
                case MythValueType.String:
                    return "string";
                case MythValueType.Enum:
                    return "int";
                default:
                    return "unknown";
            }
        }


        public static string MythValueTypeToCSharp(MythValueType inValueType)
        {
            switch (inValueType)
            {
                case MythValueType.Int:
                case MythValueType.IntTenThousandth:
                    return "int";
                case MythValueType.Float:
                    return "float";
                case MythValueType.Bool:
                    return "bool";
                case MythValueType.String:
                    return "string";
                case MythValueType.Enum:
                    return "int";
                default:
                    return "unknown";
            }
        }

        public static string MythValueTypeToGoStringNoFloat(MythValueType inValueType)
        {
            switch (inValueType)
            {
                case MythValueType.Int:
                case MythValueType.IntTenThousandth:
                case MythValueType.Float:
                    return "int32";
                case MythValueType.Bool:
                    return "bool";
                case MythValueType.String:
                    return "string";
                case MythValueType.Enum:
                    return "int32";
                default:
                    return "interface{}";
            }
        }

        public static string MythValueTypeToGoString(MythValueType inValueType)
        {
            switch (inValueType)
            {
                case MythValueType.Int:
                case MythValueType.IntTenThousandth:
                    return "int32";
                case MythValueType.Float:
                    return "float32";
                case MythValueType.Bool:
                    return "bool";
                case MythValueType.String:
                    return "string";
                case MythValueType.Enum:
                    return "int32";
                default:
                    return "interface{}";
            }
        }
    }
}