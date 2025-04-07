namespace Myth
{
    public static class MythFunctionTable
    {
        public static Dictionary<string, FunctionSignature> Signatures = new Dictionary<string, FunctionSignature>();

        /// <summary>
        /// 从文本文件加载函数签名到 MythFunctionTable.Signatures
        /// 支持以下规则：
        /// 1) 单参数时可用 params
        /// 2) 多参数时不可用 params
        /// 3) 可无参数
        /// 4) 返回类型只支持 int, bool, string
        /// </summary>
        public static void LoadFromFile(string filePath)
        {
            // 加载前清空，避免累加
            Signatures.Clear();

            foreach (var lineRaw in File.ReadLines(filePath))
            {
                var line = CharMappingPreprocess.Preprocess(lineRaw);
                var trimmed = line.Trim();
                // 跳过空行或注释行(若需要)
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("//"))
                {
                    continue;
                }

                // 去掉行末分号
                trimmed = trimmed.TrimEnd(';').Trim();

                // 期望格式形如:
                // [返回类型] [函数名]( [param列表] )
                // 例如: int 房间翻新等级(params string)
                //      bool 房间升级完成(int, bool)
                //      bool 某个函数()

                try
                {
                    // 1) Parse return type
                    int firstSpaceIndex = trimmed.IndexOf(' ');
                    if (firstSpaceIndex < 0)
                    {
                        throw new Exception("缺少返回类型，或者返回类型与函数名中缺少空格");
                    }

                    string returnTypeStr = trimmed.Substring(0, firstSpaceIndex).Trim();
                    MythValueType returnType = ParseType(returnTypeStr);

                    // 2) Remaining part (including function name and parentheses)
                    string rest = trimmed.Substring(firstSpaceIndex).Trim();
                    int lParenIndex = rest.IndexOf('(');

                    string funcName;
                    string insideParen = "";

                    if (lParenIndex < 0)
                    {
                        // No parentheses, only function name
                        funcName = rest;
                    }
                    else
                    {
                        funcName = rest.Substring(0, lParenIndex).Trim();
                        int rParenIndex = rest.IndexOf(')', lParenIndex + 1);
                        if (rParenIndex < 0)
                        {
                            throw new Exception("缺少 ')'");
                        }

                        // Inside parentheses
                        insideParen = rest.Substring(lParenIndex + 1, rParenIndex - (lParenIndex + 1)).Trim();
                    }

                    // Construct signature
                    var signature = new FunctionSignature(funcName, returnType);

                    // 3) 解析参数列表
                    if (string.IsNullOrEmpty(insideParen))
                    {
                        // 无参
                        signature.IsParams = false;
                        signature.ParamTypes.Clear(); //空
                    }
                    else
                    {
                        // 按逗号分割
                        var paramChunks = insideParen.Split(',')
                            .Select(s => s.Trim())
                            .Where(s => !string.IsNullOrEmpty(s))
                            .ToList();

                        // 如果只有1个参数
                        if (paramChunks.Count == 1)
                        {
                            var chunk = paramChunks[0];
                            // 新增逻辑: 若包含 '@' 则切割成定义类型与实际类型
                            string definitionTypeStr = chunk;
                            string actualTypeStr = null;
                            if (chunk.Contains("@"))
                            {
                                var parts = chunk.Split(new char[] { '@' }, 2);
                                definitionTypeStr = parts[0].Trim();
                                actualTypeStr = parts[1].Trim();
                            }

                            if (definitionTypeStr.StartsWith("多个"))
                            {
                                // 形如 "多个字符串" 或 "多个字符串@string"
                                signature.IsParams = true;
                                // 取出定义类型中 "多个" 后面的字符串
                                var afterParams = definitionTypeStr.Substring("多个".Length).Trim();
                                MythValueType t = ParseType(afterParams);
                                signature.ParamTypes.Add(t);

                                // 保存实际类型
                                if (actualTypeStr != null)
                                {
                                    signature.ParamActualTypeDict[signature.ParamTypes.Count - 1] = actualTypeStr;
                                }
                            }
                            else
                            {
                                // 普通单参数
                                signature.IsParams = false;
                                MythValueType t = ParseType(definitionTypeStr);
                                signature.ParamTypes.Add(t);

                                // 保存实际类型
                                if (actualTypeStr != null)
                                {
                                    signature.ParamActualTypeDict[signature.ParamTypes.Count - 1] = actualTypeStr;
                                }
                            }
                        }
                        else
                        {
                            // 多参数，不允许 params
                            signature.IsParams = false;

                            foreach (var chunk in paramChunks)
                            {
                                // 新增逻辑: 如果包含 '@' 则切割成定义类型与实际类型
                                string definitionTypeStr = chunk;
                                string actualTypeStr = null;
                                if (chunk.Contains("@"))
                                {
                                    var parts = chunk.Split(new char[] { '@' }, 2);
                                    definitionTypeStr = parts[0].Trim();
                                    actualTypeStr = parts[1].Trim();
                                }

                                if (definitionTypeStr.StartsWith("多个"))
                                {
                                    // 多参数模式下，不允许使用 "多个" 关键字
                                    throw new Exception($"多参数模式下不允许使用 \"多个\" 关键字: {chunk}");
                                }

                                // 普通类型
                                MythValueType t = ParseType(definitionTypeStr);
                                signature.ParamTypes.Add(t);

                                // 保存实际类型
                                if (actualTypeStr != null)
                                {
                                    signature.ParamActualTypeDict[signature.ParamTypes.Count - 1] = actualTypeStr;
                                }
                            }
                        }
                    }

                    // 4) 注册到字典
                    MythFunctionTable.Signatures[funcName] = signature;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"解析行失败: [{trimmed}] - {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 将字符串 "int","bool","string" 转成 MythValueType，否则 Unknown
        /// </summary>
        private static MythValueType ParseType(string typeStr)
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
                default:
                    return MythValueType.Unknown;
            }
        }
    }
}
