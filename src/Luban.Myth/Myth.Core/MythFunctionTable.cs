namespace Myth
{
    public static class MythFunctionTable
    {
        public static Dictionary<string, FunctionSignature> Signatures = new Dictionary<string, FunctionSignature>();
        public static Dictionary<string, FunctionBody> FunctionBodies = new Dictionary<string, FunctionBody>();
        private static readonly NLog.Logger s_logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// 从文本文件加载函数签名到 MythFunctionTable.Signatures
        /// 支持以下规则：
        /// 1) 单参数时可用 params
        /// 2) 多参数时不可用 params
        /// 3) 可无参数
        /// 4) 返回类型只支持 int, bool, string
        /// </summary>
        public static void ClearAndLoadSignaturesFromFile(string filePath)
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

                try
                {
                    var signature = ParseSignatureFromLine(trimmed);
                    // 4) 注册到字典
                    MythFunctionTable.Signatures[signature.Name] = signature;
                }
                catch (Exception ex)
                {
                    s_logger.Error($"[ERROR]解析行失败: [{trimmed}] - {ex.Message}");
                }
            }
        }

        public static void ClearAndLoadFunctionAndBodyFromFile(string filePath)
        {
            FunctionBodies.Clear();

            var lines = File.ReadAllLines(filePath).ToList();
            for (int i = 0; i < lines.Count; i++)
            {
                var lineRaw = lines[i];
                var line = CharMappingPreprocess.Preprocess(lineRaw);
                var trimmed = line.Trim();

                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("//"))
                {
                    continue;
                }

                try
                {
                    var signature = ParseSignatureFromLine(trimmed);
                    if (signature == null)
                    {
                        continue;
                    }

                    // find body by indentation
                    var bodyLines = new List<string>();
                    var signatureIndentation = GetIndentation(lineRaw);
                    bool bodyFound = false;
                    int j = i + 1;
                    for (; j < lines.Count; j++)
                    {
                        var nextLine = lines[j];
                        if (string.IsNullOrWhiteSpace(nextLine))
                        {
                            bodyLines.Add(nextLine);
                            continue;
                        }

                        var nextIndentation = GetIndentation(nextLine);
                        if (nextIndentation > signatureIndentation)
                        {
                            bodyLines.Add(nextLine);
                            bodyFound = true;
                        }
                        else
                        {
                            break; // end of body
                        }
                    }

                    if (bodyFound)
                    {
                        var variableDeclarations = new HashSet<string>();
                        var functionBody = new FunctionBody(signature);
                        foreach (var lineContent in bodyLines)
                        {
                            if (string.IsNullOrWhiteSpace(lineContent))
                            {
                                continue;
                            }

                            var (preprocessedLine, placeholders) = PreprocessLine(lineContent);
                            var lexer = new MythLexer(preprocessedLine);
                            var tokens = lexer.Tokenize();
                            var parser = new MythParser(tokens);
                            var expression = parser.ParseStatement();
                            expression = MythSemanticAnalyzer.AnalyzeASTWithFunctionSignature(expression, functionBody.Signature, placeholders, variableDeclarations);
                            functionBody.ParsedBodyLines.Add(expression);
                        }

                        FunctionBodies[signature.Name] = functionBody;
                        i = j - 1; // Continue parsing from after the function body
                    }
                }
                catch (Exception ex)
                {
                    s_logger.Error($"[ERROR]解析函数及其实现时出错: [{trimmed}] - {ex.Message}\n{ex.StackTrace}");
                }
            }
        }


        private static (string, Dictionary<string, string>) PreprocessLine(string line)
        {
            var placeholders = new Dictionary<string, string>();
            if (!line.Contains('@'))
            {
                return (line, placeholders);
            }

            var sb = new System.Text.StringBuilder();
            int placeholderId = 0;
            int currentPos = 0;

            while (currentPos < line.Length)
            {
                int atPos = line.IndexOf('@', currentPos);
                if (atPos == -1)
                {
                    sb.Append(line.Substring(currentPos));
                    break;
                }

                sb.Append(line.Substring(currentPos, atPos - currentPos));

                int endPos = atPos + 1;
                while (endPos < line.Length && (char.IsLetterOrDigit(line[endPos]) || line[endPos] == '_' || line[endPos] == '.'))
                {
                    endPos++;
                }

                string originalContent = line.Substring(atPos + 1, endPos - (atPos + 1));
                if (string.IsNullOrEmpty(originalContent))
                {
                    sb.Append('@');
                    currentPos = atPos + 1;
                    continue;
                }

                string placeholder = $"__placeholder_{placeholderId++}__";
                placeholders[placeholder] = originalContent;
                sb.Append(placeholder);

                currentPos = endPos;
            }

            return (sb.ToString(), placeholders);
        }

        private static int GetIndentation(string str)
        {
            return str.Length - str.TrimStart().Length;
        }

        private static FunctionSignature ParseSignatureFromLine(string line)
        {
            // 去掉行末分号
            var trimmed = line.TrimEnd(';').Trim();

            // 期望格式形如:
            // [返回类型] [函数名]( [param列表] )
            // 例如: int 房间翻新等级(params string)
            //      bool 房间升级完成(int, bool)
            //      bool 某个函数()

            // 1) Parse return type
            int firstSpaceIndex = trimmed.IndexOf(' ');
            if (firstSpaceIndex < 0)
            {
                throw new Exception("缺少返回类型，或者返回类型与函数名中缺少空格");
            }

            string returnTypeStr = trimmed.Substring(0, firstSpaceIndex).Trim();
            MythValueType returnType = MythTypeUtil.ParseType(returnTypeStr);

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
                signature.Parameters.Clear(); //空
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
                    var parameterInfo = ParseParemterInfo(chunk, out var isParams);
                    signature.Parameters.Add(parameterInfo);
                    signature.IsParams = isParams;
                }
                else
                {
                    // 多参数，不允许 params
                    signature.IsParams = false;

                    foreach (var chunk in paramChunks)
                    {
                        var parameterInfo = ParseParemterInfo(chunk, out var isParams, true);
                        signature.Parameters.Add(parameterInfo);
                    }
                }
            }
            return signature;
        }



        private static ParameterInfo ParseParemterInfo(string inChunk, out bool isParams, bool disableMultiParams = false)
        {
            isParams = false;
            var splitByWhiteSpace = inChunk.Split(" ");
            // 新增逻辑: 若包含 '@' 则切割成定义类型与实际类型
            string definitionTypeStr = splitByWhiteSpace[0];
            string variableSignatureStr = splitByWhiteSpace.Length > 1 ? splitByWhiteSpace[1] : "";
            string actualTypeStr = "";
            if (definitionTypeStr.Contains("@"))
            {
                var parts = definitionTypeStr.Split(new char[] { '@' }, 2);
                definitionTypeStr = parts[0].Trim();
                actualTypeStr = parts[1].Trim();
            }

            if (definitionTypeStr.StartsWith("多个"))
            {
                if (disableMultiParams)
                {
                    // 多参数模式下，不允许使用 "多个" 关键字
                    throw new Exception($"多参数模式下不允许使用 \"多个\" 关键字: {inChunk}");
                }
                // 形如 "多个字符串" 或 "多个字符串@string"
                isParams = true;
                // 取出定义类型中 "多个" 后面的字符串
                var afterParams = definitionTypeStr.Substring("多个".Length).Trim();
                MythValueType t = MythTypeUtil.ParseType(afterParams);
                return new ParameterInfo(t, actualTypeStr, variableSignatureStr);

                // // 保存实际类型
                // if (actualTypeStr != null)
                // {
                //     signature.ParamActualTypeDict[signature.ParamTypes.Count - 1] = actualTypeStr;
                // }
            }
            else
            {
                // 普通单参数
                MythValueType t = MythTypeUtil.ParseType(definitionTypeStr);
                return new ParameterInfo(t, actualTypeStr, variableSignatureStr);

                // // 保存实际类型
                // if (actualTypeStr != null)
                // {
                //     signature.ParamActualTypeDict[signature.ParamTypes.Count - 1] = actualTypeStr;
                // }
            }
        }
    }
}
