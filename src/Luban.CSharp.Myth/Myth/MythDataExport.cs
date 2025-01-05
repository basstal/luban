using Luban;
using Luban.Datas;
using Luban.DataTarget;
using Luban.Defs;
using Luban.OutputSaver;
using Luban.Types;
using Luban.Utils;
using Myth;

[DataExporter("myth")]
public class MythDataExport : DataExporterBase
{
    public override void Handle(GenerationContext ctx, IDataTarget dataTarget, OutputFileManifest manifest)
    {
        // 基础的表格数据导出
        base.Handle(ctx, dataTarget, manifest);


        // 从表格数据中读取需要导出 Myth 代码的数据，并导出 Myth 代码和 Myth 元数据
        HandleMyth(ctx);
    }

    private void HandleMyth(GenerationContext ctx)
    {
        // Dictionary<DefEnum, Dictionary<int, ParameterType>> enumToParameterType = new Dictionary<DefEnum, Dictionary<int, ParameterType>>();
        // 找到所有的 MythBean
        Dictionary<DefBean, int> mythBeans = new Dictionary<DefBean, int>();
        foreach (var defBean in ctx.ExportBeans)
        {
            if (defBean.HasTag("IsMythBean"))
            {
                int mythContentFieldIndex = -1;
                // int caseIndex = -1;
                for (int i = 0; i < defBean.HierarchyExportFields.Count; ++i)
                {
                    var exportField = defBean.HierarchyExportFields[i];
                    // 一个 MythBean 目前应该只有一个 MythContent
                    if (exportField.HasTag("IsMythContent"))
                    {
                        mythContentFieldIndex = i;
                        break;
                    }
                    //
                    // if (enumType != null) 
                    // {
                    //     continue;
                    // }
                    //
                    // if (exportField.CType is TEnum && exportField.CType.HasTag("MythEnum"))
                    // {
                    //     enumType = (TEnum)exportField.CType;
                    // }
                    // else if (exportField.CType is TString && exportField.HasTag("MythEnum"))
                    // {
                    //     var defEnum = ctx.ExportEnums.Find(@enum => @enum.FullName == exportField.GetTag("MythEnum"));
                    //     enumType = TEnum.Create(false, defEnum, defEnum!.Tags);
                    // }
                    //
                    // if (enumType != null)
                    // {
                    //     var intToType = new Dictionary<int, ParameterType>();
                    //     foreach (var item in enumType.DefEnum.Items)
                    //     {
                    //         if (item.GetTag("MythEnumParameterType") == "ListInteger")
                    //         {
                    //             intToType.Add(item.IntValue, ParameterType.ListInteger);
                    //         }
                    //         else if (item.GetTag("MythEnumParameterType") == "Bool")
                    //         {
                    //             intToType.Add(item.IntValue, ParameterType.Bool);
                    //         }
                    //         else
                    //         {
                    //             intToType.Add(item.IntValue, ParameterType.Integer);
                    //         }
                    //     }
                    //
                    //     enumToParameterType.Add(enumType.DefEnum, intToType);
                    //     caseIndex = i;
                    // }
                }

                if (mythContentFieldIndex == -1)
                {
                    throw new InvalidOperationException("IsMythBean 必须定义有一个 IsMythContent 字段");
                }

                mythBeans.Add(defBean, mythContentFieldIndex);
            }
        }

        // 记录需要导出 Myth 代码的表，以及它们需要导出的字段
        Dictionary<DefTable, List<int>> exportMythTables = new Dictionary<DefTable, List<int>>();
        foreach (var defTable in ctx.ExportTables)
        {
            var defTableValueBeanType = defTable.ValueTType;
            var count = defTableValueBeanType.DefBean.ExportFields.Count;
            for (int index = 0; index < count; ++index)
            {
                var defField = defTableValueBeanType.DefBean.ExportFields[index];
                // TODO:现在暂不支持直接在表的 Bean 定义中使用定义基础字段类型为 MythContent
                if (!(defField.CType is TBean beanType))
                {
                    continue;
                }

                if (mythBeans.ContainsKey(beanType.DefBean))
                {
                    if (!exportMythTables.TryGetValue(defTable, out var container))
                    {
                        container = new List<int>();
                        exportMythTables.Add(defTable, container);
                    }

                    container.Add(index);
                }
            }
        }

        var outputManifest = new OutputFileManifest("myth", OutputType.Code);
        // var safeReferenceMethodSignatures = ReadSafeReferenceMethodsFromFile();
        string interfaceName = "IMythConditionContext";
        var mythDataExport = new MythCodeTarget();
        // 每一张需要生成 Myth 代码的表
        foreach (var (mythTable, mythFieldIndices) in exportMythTables)
        {
            // var roslynExpressionProcessor = new RoslynExpressionProcessor();
            // var expressions = new List<ExpressionInfo>();
            var result = new Dictionary<string, (string, MythMetadata)>();
            // 每一行数据
            foreach (var record in ctx.GetTableExportDataList(mythTable))
            {
                // 每一个需要转为 expression 的列
                foreach (var mythFieldIndex in mythFieldIndices)
                {
                    // 找到 Table 中列定义，列名称
                    var defField = mythTable.ValueTType.DefBean.HierarchyExportFields[mythFieldIndex];
                    // var indexInfos = mythTable.IndexList;
                    // var getterInfo = new GetterInfo()
                    // {
                    //     getterKeyTypes = indexInfos.Select(indexInfo =>
                    //     {
                    //         // 如果 getter 的参数为 enum 类型，直接使用 enum 对应的枚举类型
                    //         if (indexInfo.IndexField.CType is TEnum enumType)
                    //         {
                    //             return enumType.DefEnum.Name;
                    //         }
                    //
                    //         return indexInfo.Type.TypeName;
                    //     }).ToArray(),
                    //     getterKeyNames = indexInfos.Select(indexInfo => indexInfo.IndexField.Name).ToArray(),
                    //     tableExportDefField = tableMythField,
                    //     fieldName = TypeUtil.ToCsStyleName(tableMythField.Name)
                    // };
                    // getterInfos.Add(getterInfo);
                    // 找到列对应的数据类型
                    var dBeanField = (DBean)record.Data.Fields[mythFieldIndex];

                    // Console.WriteLine($"dBeanField : {dBeanField}");
                    var mythContentIndex = mythBeans[dBeanField.Type];
                    var dValueMythContent = dBeanField.Fields[mythContentIndex];
                    if (dValueMythContent is DString dStringMythContent)
                    {
                        if (string.IsNullOrEmpty(dStringMythContent.Value))
                        {
                            continue;
                        }

                        MythLexer lexer = new MythLexer(dStringMythContent.Value);
                        var tokens = lexer.Tokenize();

                        // 2. 语法分析 -> AST
                        var parser = new MythParser(tokens);
                        MythExprNode ast = parser.ParseExpressionAndAnalyzeAST();

                        // 3. 生成 C# 代码
                        string methodName = CreateMythMethodName(mythTable, record, defField);
                        string code = MythCodeGenerator.GenerateMethodCode(methodName, interfaceName, ast);
                        var metadata = MythMetadataCollector.Collect(ast, dStringMythContent.Value, methodName, methodName);
                        if (result.ContainsKey(methodName))
                        {
                            throw new InvalidOperationException($"生成的方法名 {methodName} 重复");
                        }

                        result.Add(methodName, (code, metadata));
                    }
                    else
                    {
                        throw new NotImplementedException($"暂不支持的类型 {dValueMythContent}");
                    }
                    // List<DEnum> mythParameterTypeEnumValue = new List<DEnum>();
                    // var caseIndex = mythBeans[dBeanField.Type].Item3;
                    // if (dType is DString dString)
                    // {
                    //     var (parsingResult, _) = MythParameterParsing.ParseParameter(ctx, mythTEnum, dString.Value, null);
                    //     mythParameterTypeEnumValue.AddRange(parsingResult);
                    // }
                    // else if (dType is DEnum dEnum)
                    // {
                    //     mythParameterTypeEnumValue.Add(dEnum);
                    // }
                    // else
                    // {
                    //     throw new NotImplementedException($"暂不支持的类型 {dType}");
                    // }


                    // var mythElementIndices = mythBeans[dBeanField.Type].Item1;

                    // // 根据数据列下标，提取数据
                    // foreach (var mythElementIndex in mythElementIndices)
                    // {
                    //     // TODO:这里有几个强制类型转换可能以后要注意一下扩展性
                    //     var mythValueType = (DString)dBeanField.Fields[mythElementIndex];
                    //     // Console.Write($" field: {field}");
                    //     if (!string.IsNullOrEmpty(mythValueType.Value))
                    //     {
                    //         var parameterTypes = mythParameterTypeEnumValue.Select(enumValue => enumToParameterType[enumValue.Type.DefEnum][enumValue.Value])
                    //             .ToArray();
                    //         var functionName = TypeUtil.ToCsStyleName($"{mythFieldIndex.Name}_{recordIndexValue}");
                    //         expressions.Add(new ExpressionInfo() { expression = mythValueType.Value, functionName = functionName, parameterTypes = parameterTypes, });
                    //     }
                    // }
                }
            }

            // getterInfos = getterInfos.ToList();
            // var result = roslynExpressionProcessor.ProcessExpressions(expressions, safeReferenceMethodSignatures);
            var outputFile = mythDataExport.GenerateMyth(ctx, result, mythTable.ValueTType.DefBean, interfaceName);
            // Console.WriteLine($"outputFile :{outputFile.Content}");
            outputManifest.AddFile(outputFile);
        }

        var interfaceFile = mythDataExport.GenerateMythInterface(ctx, interfaceName);
        outputManifest.AddFile(interfaceFile);

        string outputSaverName = EnvManager.Current.GetOptionOrDefault(outputManifest.TargetName, BuiltinOptionNames.OutputSaver, true, "myth");
        var saver = OutputSaverManager.Ins.GetOutputSaver(outputSaverName);
        saver.Save(outputManifest);
    }

    private string CreateMythMethodName(DefTable mythTable, Record record, DefField defField)
    {
        var recordIndexDTypes = mythTable.IndexList.Select(indexInfo => record.Data.Fields[indexInfo.IndexFieldIdIndex]);
        var recordIndexValue = string.Join("_", recordIndexDTypes.Select(recordIndexDType =>
        {
            if (recordIndexDType is DEnum dEnum)
            {
                // 枚举值转为字符串
                var tEnum = dEnum.Type;
                var item = tEnum.DefEnum.Items.Find(item => item.IntValue == dEnum.Value);
                return item!.Name;
            }

            if (recordIndexDType is DString recordDString)
            {
                return recordDString.Value;
            }

            if (recordIndexDType is DInt recordDInt)
            {
                return recordDInt.Value.ToString();
            }

            throw new NotImplementedException($"构造函数名时存在不支持的 表索引 类型 {recordIndexDType}");
        }));
        // var parameterTypes = mythParameterTypeEnumValue.Select(enumValue => enumToParameterType[enumValue.Type.DefEnum][enumValue.Value])
        //     .ToArray();
        var functionName = TypeUtil.ToCsStyleName($"{defField.Name}_{recordIndexValue}");

        return functionName;
    }

    // private JObject ReadSafeReferenceMethodsFromFile()
    // {
    //     // 读取 JSON 文件
    //     string jsonData = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "SafeReferenceMethodSignatures.json"));
    //
    //     // 解析 JSON 数据
    //     JObject jsonObject = JObject.Parse(jsonData);
    //
    //     // // 访问数据
    //     // ReadJsonData(jsonObject);
    //     return jsonObject;
    // }
}
