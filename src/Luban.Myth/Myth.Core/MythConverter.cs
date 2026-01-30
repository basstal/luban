using Luban;
using Luban.Defs;
using Luban.Datas;
using NLog;

namespace Myth;

public class MythConverter
{
    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 验证结果
    /// </summary>
    public class ValidationResult
    {
        public bool IsEnum { get; set; }
        public bool IsTable { get; set; }
        public string CodeExpression { get; set; } = string.Empty;
    }

    /// <summary>
    /// 验证上下文信息，用于错误报告
    /// </summary>
    public class ValidationContext
    {
        public string? TableName { get; set; }
        public string? Content { get; set; }
        public string? FieldName { get; set; }
        public int? RowIndex { get; set; }

        public string GetContextInfo()
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(TableName))
            {
                parts.Add($"表: {TableName}");
            }
            if (RowIndex.HasValue)
            {
                parts.Add($"数据行: {RowIndex.Value + 1}"); // 从1开始显示
            }
            if (!string.IsNullOrEmpty(FieldName))
            {
                parts.Add($"字段: {FieldName}");
            }
            if (!string.IsNullOrEmpty(Content))
            {
                parts.Add($"内容: {Content}");
            }
            return parts.Count > 0 ? $"[{string.Join(", ", parts)}] " : "";
        }
    }

    /// <summary>
    /// 根据 LubanTypeReference 在 Context 中查找并验证值，支持枚举和表两种类型
    /// 支持递归查找，lubanTypeReference 可以是 "TableName" 或 "TableName.FieldName" 格式
    /// </summary>
    /// <param name="lubanTypeReference">Luban 类型引用，可以是 "TableName" 或 "TableName.FieldName"</param>
    /// <param name="rawValue">原始值</param>
    /// <param name="ctx">生成上下文</param>
    /// <param name="isGolang">是否为 Golang 代码生成</param>
    /// <param name="validationContext">验证上下文信息，用于错误报告</param>
    /// <param name="depth">递归深度，防止无限递归</param>
    /// <returns>验证结果，包含生成的代码表达式</returns>
    public static ValidationResult ValidateLubanTypeReference(string lubanTypeReference, string rawValue, GenerationContext ctx, bool isGolang = false, ValidationContext? validationContext = null, int depth = 0)
    {
        const int MAX_DEPTH = 10;
        string contextPrefix = validationContext?.GetContextInfo() ?? "";

        if (depth > MAX_DEPTH)
        {
            s_logger.Error($"{contextPrefix}递归查找深度超过限制: {depth}");
            throw new NotImplementedException($"{contextPrefix}Recursive lookup depth exceeded: {depth}");
        }
        s_logger.Debug($"{contextPrefix}开始验证 LubanTypeReference (深度 {depth}): {lubanTypeReference}, 原始值: {rawValue}");

        // 1. 首先尝试查找枚举定义
        var enumDef = ctx.ExportEnums.Find(defEnum => defEnum.FullName == lubanTypeReference);
        if (enumDef != null)
        {
            s_logger.Debug($"{contextPrefix}找到枚举定义: {enumDef.FullName}");
            var enumItem = enumDef.Items.Find(item => item.Name == rawValue || item.Alias == rawValue);
            if (enumItem == null)
            {
                string errorMsg = $"{contextPrefix}未找到枚举项: {rawValue}，在枚举 {lubanTypeReference} 中。可选项: [{string.Join(", ", enumDef.Items.Select(item => item.Name))}]";
                s_logger.Error(errorMsg);
                throw new NotImplementedException($"{contextPrefix}Enum item {rawValue} not found in enum {lubanTypeReference}\n可选的枚举值有：[{string.Join(", ", enumDef.Items.Select(item => item.Name))}]");
            }

            string codeExpression = isGolang
                ? $"{enumItem.Value}"
                : $"{enumDef.FullNameWithTopModule}.{enumItem.Name}";

            s_logger.Debug($"生成枚举表达式: {codeExpression}");
            return new ValidationResult
            {
                IsEnum = true,
                CodeExpression = codeExpression
            };
        }

        // 2. 解析 lubanTypeReference，判断是否包含字段名（格式：TableName.FieldName）
        string tableFullName = lubanTypeReference;
        string? specifiedFieldName = null;

        int dotIndex = lubanTypeReference.LastIndexOf('.');
        if (dotIndex > 0 && dotIndex < lubanTypeReference.Length - 1)
        {
            // 可能是 "TableName.FieldName" 格式
            string potentialTableName = lubanTypeReference.Substring(0, dotIndex);
            string potentialFieldName = lubanTypeReference.Substring(dotIndex + 1);

            // 检查是否是表名（先尝试完整匹配，再尝试部分匹配）
            var testTable = ctx.Tables.FirstOrDefault(t => t.FullName == potentialTableName);
            if (testTable != null)
            {
                // 验证字段是否存在
                if (testTable.ValueTType?.DefBean?.TryGetField(potentialFieldName, out var _, out _) == true)
                {
                    tableFullName = potentialTableName;
                    specifiedFieldName = potentialFieldName;
                    s_logger.Debug($"{contextPrefix}解析出表名和字段名: 表={tableFullName}, 字段={specifiedFieldName}");
                }
            }
        }

        // 3. 查找表定义
        s_logger.Debug($"{contextPrefix}查找表定义: {tableFullName}");
        var tableDef = ctx.Tables.FirstOrDefault(t => t.FullName == tableFullName);
        if (tableDef == null)
        {
            string errorMsg = $"{contextPrefix}未找到枚举或表定义: {lubanTypeReference}";
            s_logger.Error(errorMsg);
            throw new NotImplementedException($"{contextPrefix}Enum or Table {lubanTypeReference} not found in export enums and tables");
        }

        s_logger.Debug($"{contextPrefix}找到表定义: {tableDef.FullName}");

        // 4. 获取表数据
        if (!ctx.RecordsByTables.TryGetValue(tableDef.FullName, out var tableDataInfo))
        {
            string errorMsg = $"{contextPrefix}表 {tableFullName} 数据未加载";
            s_logger.Error(errorMsg);
            throw new NotImplementedException($"{contextPrefix}Table {tableFullName} data not loaded");
        }

        var records = tableDataInfo.FinalRecords;
        s_logger.Debug($"表 {tableFullName} 共有 {records.Count} 条记录");

        // 5. 确定要查找的字段（优先使用指定的字段，否则使用主键字段）
        DefField? searchField = null;
        string searchFieldName = string.Empty;

        if (!string.IsNullOrEmpty(specifiedFieldName))
        {
            // 使用指定的字段
            if (tableDef.ValueTType?.DefBean?.TryGetField(specifiedFieldName, out var field, out _) == true)
            {
                searchField = field;
                searchFieldName = specifiedFieldName;
                s_logger.Debug($"使用指定的字段进行查找: {searchFieldName}");
            }
            else
            {
                s_logger.Error($"表 {tableFullName} 中未找到指定字段: {specifiedFieldName}");
                throw new NotImplementedException($"Field {specifiedFieldName} not found in table {tableFullName}");
            }
        }
        else if (tableDef.IndexField != null)
        {
            // 使用主键字段
            searchField = tableDef.IndexField;
            searchFieldName = searchField.Name;
            s_logger.Debug($"使用主键字段进行查找: {searchFieldName}");
        }
        else if (tableDef.ValueTType?.DefBean?.HierarchyFields != null && tableDef.ValueTType.DefBean.HierarchyFields.Count > 0)
        {
            // 如果没有主键，使用第一个字段
            searchField = tableDef.ValueTType.DefBean.HierarchyFields[0];
            searchFieldName = searchField.Name;
            s_logger.Debug($"使用第一个字段进行查找: {searchFieldName}");
        }
        else
        {
            s_logger.Error($"表 {tableFullName} 没有可用的查找字段");
            throw new NotImplementedException($"Table {tableFullName} has no searchable field");
        }

        // 6. 检查字段类型，如果是表引用类型，需要递归查找
        DefTable? refTable = null;
        if (searchField.CType is Luban.Types.TBean tBean && tBean.DefBean != null)
        {
            // 字段类型是 Bean，检查是否是表引用
            refTable = ctx.Tables.FirstOrDefault(t => t.ValueTType?.DefBean?.FullName == tBean.DefBean.FullName);
            if (refTable != null)
            {
                s_logger.Debug($"{contextPrefix}字段 {searchFieldName} 是表引用类型，递归查找表: {refTable.FullName}");
                // 递归查找，传递上下文信息
                return ValidateLubanTypeReference(refTable.FullName, rawValue, ctx, isGolang, validationContext, depth + 1);
            }
        }

        // 7. 在表中查找匹配的记录
        bool found = false;
        object? foundValue = null;

        foreach (var record in records)
        {
            var fieldValue = record.Data.GetField(searchFieldName);
            if (fieldValue == null)
            {
                continue;
            }

            // 比较字段值与原始值
            bool matches = false;
            switch (fieldValue)
            {
                case DInt dInt:
                    if (int.TryParse(rawValue, out var intVal) && dInt.Value == intVal)
                    {
                        matches = true;
                        foundValue = dInt.Value;
                    }
                    break;
                case DLong dLong:
                    if (long.TryParse(rawValue, out var longVal) && dLong.Value == longVal)
                    {
                        matches = true;
                        foundValue = dLong.Value;
                    }
                    break;
                case DString dString:
                    if (dString.Value == rawValue)
                    {
                        matches = true;
                        foundValue = dString.Value;
                    }
                    break;
                case DEnum dEnum:
                    if (dEnum.Value.ToString() == rawValue || dEnum.StrValue == rawValue)
                    {
                        matches = true;
                        foundValue = dEnum.Value;
                    }
                    break;
                case DBean dBean:
                    // 如果字段值是 Bean，可能需要递归查找
                    // 这里简化处理：如果 Bean 有主键字段，尝试在主键字段中查找
                    if (refTable != null && refTable.IndexField != null)
                    {
                        var refFieldValue = dBean.GetField(refTable.IndexField.Name);
                        if (refFieldValue != null)
                        {
                            string refValue = refFieldValue.ToString() ?? "";
                            if (refValue == rawValue)
                            {
                                matches = true;
                                foundValue = refValue;
                            }
                        }
                    }
                    break;
            }

            if (matches)
            {
                found = true;
                s_logger.Debug($"在表 {tableDef.InputFiles[0]} 的字段 {searchFieldName} 中找到匹配值: {rawValue}");
                break;
            }
        }

        if (!found)
        {
            // 获取所有可能的值用于错误提示
            var availableValues = new List<string>();
            foreach (var record in records.Take(10)) // 只取前10个用于提示
            {
                var fieldValue = record.Data.GetField(searchFieldName);
                if (fieldValue != null)
                {
                    availableValues.Add(fieldValue.ToString() ?? "");
                }
            }

            string errorMsg = $"{contextPrefix}未在表 {tableDef.InputFiles[0]} 的字段 {searchFieldName} 中找到值: {rawValue}。";
            s_logger.Error(errorMsg);
            throw new NotImplementedException($"{contextPrefix}在表 {tableDef.InputFiles[0]} 的字段 {searchFieldName} 中未找到值: {rawValue}\n");
        }

        // 8. 生成代码表达式（对于表，使用字符串格式，因为验证已经通过）
        string tableCodeExpression = isGolang
            ? $"\"{rawValue}\""
            : $"\"{rawValue}\""; // C# 中使用字符串格式

        s_logger.Debug($"{contextPrefix}生成表验证表达式: {tableCodeExpression}");
        return new ValidationResult
        {
            IsTable = true,
            CodeExpression = tableCodeExpression
        };
    }
    public static string CompareOpToString(MythCompareOp op)
    {
        switch (op)
        {
            case MythCompareOp.Equal:
                return "==";
            case MythCompareOp.NotEqual:
                return "!=";
            case MythCompareOp.Greater:
                return ">";
            case MythCompareOp.GreaterEqual:
                return ">=";
            case MythCompareOp.Less:
                return "<";
            case MythCompareOp.LessEqual:
                return "<=";
        }

        return "/*UNKNOWN*/";
    }

    public static string ArithmeticOpToString(MythArithmeticOp op)
    {
        switch (op)
        {
            case MythArithmeticOp.Add:
                return "+";
            case MythArithmeticOp.Subtract:
                return "-";
            case MythArithmeticOp.Multiply:
                return "*";
            case MythArithmeticOp.Divide:
                return "/";
            default:
                throw new NotSupportedException($"unknown arithmetic op:{op}");
        }
    }


    public static string GetEvalFunctionByFunctionSignature(FunctionSignature functionSignature)
    {
        var returnType = functionSignature.ReturnType;
        var haveParams = functionSignature.Parameters.Count > 0;
        switch (returnType)
        {
            case MythValueType.Int:
            case MythValueType.IntTenThousandth:
            case MythValueType.Float:
            {
                if (!haveParams)
                {
                    return "GetInt";
                }

                return "EvalFunctionReturnInt";
            }
            case MythValueType.Bool:
            {
                if (!haveParams)
                {
                    return "GetBool";
                }

                return "EvalFunctionReturnBool";
            }
            case MythValueType.Long:
            {
                if (!haveParams)
                {
                    return "GetLong";
                }

                return "EvalFunctionReturnLong";
            }
        }

        throw new NotImplementedException($"GetEvalFunctionByFunctionSignature for return type:{returnType} failed!");
    }
}
