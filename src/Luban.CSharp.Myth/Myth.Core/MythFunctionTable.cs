using Myth;

namespace Myth
{
    public static class MythFunctionTable
    {
        public static Dictionary<string, FunctionSignature> Signatures
            = new Dictionary<string, FunctionSignature>();

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
            MythFunctionTable.Signatures.Clear();

            foreach (var line in File.ReadLines(filePath))
            {
                var trimmed = line.Trim();
                // 跳过空行或注释行(若需要)
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("//"))
                    continue;

                // 去掉行末分号
                trimmed = trimmed.TrimEnd(';').Trim();

                // 期望格式形如:
                // [返回类型] [函数名]( [param列表] )
                // 例如: int 房间翻新等级(params string)
                //      bool 房间升级完成(int, bool)
                //      bool 某个函数()

                try
                {
                    // 1) 解析返回类型
                    int firstSpaceIndex = trimmed.IndexOf(' ');
                    if (firstSpaceIndex < 0)
                        throw new Exception("无法解析: 缺少返回类型和函数名之间的空格");

                    string returnTypeStr = trimmed.Substring(0, firstSpaceIndex).Trim();
                    MythValueType returnType = ParseType(returnTypeStr);

                    // 2) 剩余部分(含函数名和括号)
                    string rest = trimmed.Substring(firstSpaceIndex).Trim();
                    int lParenIndex = rest.IndexOf('(');
                    if (lParenIndex < 0)
                        throw new Exception("缺少 '('");

                    string funcName = rest.Substring(0, lParenIndex).Trim();

                    int rParenIndex = rest.IndexOf(')', lParenIndex + 1);
                    if (rParenIndex < 0)
                        throw new Exception("缺少 ')'");

                    // 括号内部
                    string insideParen = rest.Substring(lParenIndex + 1, rParenIndex - (lParenIndex + 1)).Trim();
                    // 可能为 "", "params string", "int", "int, bool", etc.

                    // 构造签名
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
                            if (chunk.StartsWith("params"))
                            {
                                // 形如 "params string"
                                signature.IsParams = true;
                                // 取出 "string"
                                var afterParams = chunk.Substring("params".Length).Trim();
                                MythValueType t = ParseType(afterParams);
                                signature.ParamTypes.Add(t);
                            }
                            else
                            {
                                // 普通单参数
                                signature.IsParams = false;
                                MythValueType t = ParseType(chunk);
                                signature.ParamTypes.Add(t);
                            }
                        }
                        else
                        {
                            // 多参数，不允许 params
                            signature.IsParams = false;

                            foreach (var chunk in paramChunks)
                            {
                                if (chunk.StartsWith("params"))
                                {
                                    // 不允许
                                    throw new Exception($"多参数模式下不允许使用 'params': {chunk}");
                                }

                                // 普通类型
                                MythValueType t = ParseType(chunk);
                                signature.ParamTypes.Add(t);
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
                case "int": return MythValueType.Int;
                case "bool": return MythValueType.Bool;
                case "string": return MythValueType.String;
                case "enum": return MythValueType.Enum;
                default: return MythValueType.Unknown;
            }
        }
    }
}
