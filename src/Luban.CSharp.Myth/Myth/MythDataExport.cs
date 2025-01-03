using Luban;
using Luban.Datas;
using Luban.DataTarget;
using Luban.Defs;
using Luban.OutputSaver;
using Luban.Types;
using Luban.Utils;
using Newtonsoft.Json.Linq;

[DataExporter("myth")]
public class MythDataExport : DataExporterBase
{
    public override void Handle(GenerationContext ctx, IDataTarget dataTarget, OutputFileManifest manifest)
    {
        base.Handle(ctx, dataTarget, manifest);
        // Console.WriteLine("hello");
        Dictionary<DefBean, (List<int>, TEnum, int)> mythBeans = new Dictionary<DefBean, (List<int>, TEnum, int)>();
        Dictionary<DefEnum, Dictionary<int, ParameterType>> enumToParameterType = new Dictionary<DefEnum, Dictionary<int, ParameterType>>();
        foreach (var defBean in ctx.ExportBeans)
        {
            if (defBean.HasTag("Myth"))
            {
                List<int> mythElementIndices = new List<int>();
                // 没有定义 tag MythEnum 情况下 enumType 为 null
                TEnum? enumType = null;
                int caseIndex = -1;
                for (int i = 0; i < defBean.HierarchyExportFields.Count; ++i)
                {
                    var exportField = defBean.HierarchyExportFields[i];
                    if (exportField.HasTag("MythElement"))
                    {
                        mythElementIndices.Add(i);
                    }

                    if (enumType != null) // 一个 DefBean 仅能有一个 MythEnum 的字段
                    {
                        continue;
                    }

                    if (exportField.CType is TEnum && exportField.CType.HasTag("MythEnum"))
                    {
                        enumType = (TEnum)exportField.CType;
                    }
                    else if (exportField.CType is TString && exportField.HasTag("MythEnum"))
                    {
                        var defEnum = ctx.ExportEnums.Find(@enum => @enum.FullName == exportField.GetTag("MythEnum"));
                        enumType = TEnum.Create(false, defEnum, defEnum!.Tags);
                    }

                    if (enumType != null)
                    {
                        var intToType = new Dictionary<int, ParameterType>();
                        foreach (var item in enumType.DefEnum.Items)
                        {
                            if (item.GetTag("MythEnumParameterType") == "ListInteger")
                            {
                                intToType.Add(item.IntValue, ParameterType.ListInteger);
                            }
                            else if (item.GetTag("MythEnumParameterType") == "Bool")
                            {
                                intToType.Add(item.IntValue, ParameterType.Bool);
                            }
                            else
                            {
                                intToType.Add(item.IntValue, ParameterType.Integer);
                            }
                        }

                        enumToParameterType.Add(enumType.DefEnum, intToType);
                        caseIndex = i;
                    }
                }

                mythBeans.Add(defBean, (mythElementIndices, enumType, caseIndex)!);
            }
        }

        Dictionary<DefTable, List<int>> mythTables = new Dictionary<DefTable, List<int>>();
        foreach (var defTable in ctx.ExportTables)
        {
            // if (defTable.Mode != TableMode.MAP) // 暂不支持没有主键的表
            // {
            //     Console.WriteLine($"暂不支持没有主键的表，表名字：{defTable.Name}, mode {defTable.Mode}");
            //     continue;
            // }

            var defTableValueBeanType = defTable.ValueTType;
            var count = defTableValueBeanType.DefBean.ExportFields.Count;
            for (int index = 0; index < count; ++index)
            {
                var defField = defTableValueBeanType.DefBean.ExportFields[index];
                if (!(defField.CType is TBean beanType))
                {
                    continue;
                }

                if (mythBeans.ContainsKey(beanType.DefBean))
                {
                    if (!mythTables.TryGetValue(defTable, out var container))
                    {
                        container = new List<int>();
                        mythTables.Add(defTable, container);
                    }

                    container.Add(index);
                }
            }
        }

        var outputManifest = new OutputFileManifest("myth", OutputType.Code);
        var safeReferenceMethodSignatures = ReadSafeReferenceMethodsFromFile();

        // 每一张需要生成 Myth 代码的表
        foreach (var (mythTable, mythFieldIndices) in mythTables)
        {
            var roslynExpressionProcessor = new RoslynExpressionProcessor();
            var expressions = new List<ExpressionInfo>();
            var getterInfos = new HashSet<GetterInfo>();
            // 每一行数据
            foreach (var record in ctx.GetTableExportDataList(mythTable))
            {
                // 每一个需要转为 expression 的列
                foreach (var index in mythFieldIndices)
                {
                    // 找到 Table 中列定义，列名称
                    var tableMythField = mythTable.ValueTType.DefBean.HierarchyExportFields[index];
                    var indexInfos = mythTable.IndexList;
                    var getterInfo = new GetterInfo()
                    {
                        getterKeyTypes = indexInfos.Select(indexInfo =>
                        {
                            // 如果 getter 的参数为 enum 类型，直接使用 enum 对应的枚举类型
                            if (indexInfo.IndexField.CType is TEnum enumType)
                            {
                                return enumType.DefEnum.Name;
                            }

                            return indexInfo.Type.TypeName;
                        }).ToArray(),
                        getterKeyNames = indexInfos.Select(indexInfo => indexInfo.IndexField.Name).ToArray(),
                        tableExportDefField = tableMythField,
                        fieldName = TypeUtil.ToCsStyleName(tableMythField.Name)
                    };
                    getterInfos.Add(getterInfo);
                    // 找到列对应的数据类型
                    var dBeanField = (DBean)record.Data.Fields[index];

                    var mythTEnum = mythBeans[dBeanField.Type].Item2;
                    List<DEnum> mythParameterTypeEnumValue = new List<DEnum>();
                    var caseIndex = mythBeans[dBeanField.Type].Item3;
                    var dType = dBeanField.Fields[caseIndex];
                    if (dType is DString dString)
                    {
                        var (parsingResult, _) = MythParameterParsing.ParseParameter(ctx, mythTEnum, dString.Value, null);
                        mythParameterTypeEnumValue.AddRange(parsingResult);
                    }
                    else if (dType is DEnum dEnum)
                    {
                        mythParameterTypeEnumValue.Add(dEnum);
                    }
                    else
                    {
                        throw new NotImplementedException($"暂不支持的类型 {dType}");
                    }


                    var recordIndexDTypes = indexInfos.Select(indexInfo => record.Data.Fields[indexInfo.IndexFieldIdIndex]);
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
                    var mythElementIndices = mythBeans[dBeanField.Type].Item1;

                    // 根据数据列下标，提取数据
                    foreach (var mythElementIndex in mythElementIndices)
                    {
                        // TODO:这里有几个强制类型转换可能以后要注意一下扩展性
                        var mythValueType = (DString)dBeanField.Fields[mythElementIndex];
                        // Console.Write($" field: {field}");
                        if (!string.IsNullOrEmpty(mythValueType.Value))
                        {
                            var parameterTypes = mythParameterTypeEnumValue.Select(enumValue => enumToParameterType[enumValue.Type.DefEnum][enumValue.Value])
                                .ToArray();
                            var functionName = TypeUtil.ToCsStyleName($"{tableMythField.Name}_{recordIndexValue}");
                            expressions.Add(new ExpressionInfo() { expression = mythValueType.Value, functionName = functionName, parameterTypes = parameterTypes, });
                        }
                    }
                }
            }

            // getterInfos = getterInfos.ToList();
            var result = roslynExpressionProcessor.ProcessExpressions(expressions, safeReferenceMethodSignatures);
            var mythDataExport = new MythCodeTarget();
            var outputFile = mythDataExport.GenerateMyth(ctx, result, mythTable.ValueTType.DefBean, getterInfos);
            // Console.WriteLine($"outputFile :{outputFile.Content}");
            outputManifest.AddFile(outputFile);
        }

        string outputSaverName = EnvManager.Current.GetOptionOrDefault(outputManifest.TargetName, BuiltinOptionNames.OutputSaver, true, "myth");
        var saver = OutputSaverManager.Ins.GetOutputSaver(outputSaverName);
        saver.Save(outputManifest);
    }

    private JObject ReadSafeReferenceMethodsFromFile()
    {
        // 读取 JSON 文件
        string jsonData = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "SafeReferenceMethodSignatures.json"));

        // 解析 JSON 数据
        JObject jsonObject = JObject.Parse(jsonData);

        // // 访问数据
        // ReadJsonData(jsonObject);
        return jsonObject;
    }
}
