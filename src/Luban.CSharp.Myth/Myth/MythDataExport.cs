using Luban;
using Luban.Datas;
using Luban.DataTarget;
using Luban.Defs;
using Luban.OutputSaver;
using Luban.Types;
using Luban.Utils;

[DataExporter("myth")]
public class MythDataExport : DataExporterBase
{
    public override void Handle(GenerationContext ctx, IDataTarget dataTarget, OutputFileManifest manifest)
    {
        base.Handle(ctx, dataTarget, manifest);
        // Console.WriteLine("hello");
        Dictionary<DefBean, (List<int>, int)> mythBeans = new Dictionary<DefBean, (List<int>, int)>();
        Dictionary<DefEnum, Dictionary<int, ParameterType>> enumToParameterType = new Dictionary<DefEnum, Dictionary<int, ParameterType>>();
        foreach (var defBean in ctx.ExportBeans)
        {
            if (defBean.HasTag("Myth"))
            {
                List<int> mythElementIndices = new List<int>();
                int caseIndex = -1;
                for (int i = 0; i < defBean.HierarchyExportFields.Count; ++i)
                {
                    var exportField = defBean.HierarchyExportFields[i];
                    if (exportField.HasTag("MythElement"))
                    {
                        mythElementIndices.Add(i);
                    }

                    if (exportField.CType is TEnum enumType && enumType.HasTag("MythCase"))
                    {
                        var intToType = new Dictionary<int, ParameterType>();
                        foreach (var item in enumType.DefEnum.Items)
                        {
                            if (item.GetTag("MythCase") == "ListInteger")
                            {
                                intToType.Add(item.IntValue, ParameterType.ListInteger);
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

                mythBeans.Add(defBean, (mythElementIndices, caseIndex));
            }
        }

        Dictionary<DefTable, List<int>> mythTables = new Dictionary<DefTable, List<int>>();
        foreach (var defTable in ctx.ExportTables)
        {
            if (defTable.Mode != TableMode.MAP) // 暂不支持没有主键的表
            {
                continue;
            }

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


        var roslynExpressionProcessor = new RoslynExpressionProcessor();
        var expressions = new List<ExpressionInfo>();
        // 每一张需要生成 Myth 代码的表
        foreach (var (mythTable, mythFieldIndices) in mythTables)
        {
            // 每一行数据
            foreach (var record in ctx.GetTableExportDataList(mythTable))
            {
                // 每一个需要转为 expression 的列
                foreach (var index in mythFieldIndices)
                {
                    // 找到 Table 中列定义，列名称
                    var tableMythField = mythTable.ValueTType.DefBean.HierarchyExportFields[index];
                    // 找到列对应的数据类型
                    var dBeanField = (DBean)record.Data.Fields[index];

                    var mythElementIndices = mythBeans[dBeanField.Type].Item1;
                    var mythCaseIndex = mythBeans[dBeanField.Type].Item2;
                    var mythParameterTypeEnumValue = (DEnum)dBeanField.Fields[mythCaseIndex];
                    // 根据数据列下标，提取数据
                    foreach (var mythElementIndex in mythElementIndices)
                    {
                        // TODO:这里有几个强制类型转换可能以后要注意一下扩展性
                        var mythValueType = (DString)dBeanField.Fields[mythElementIndex];
                        // Console.Write($" field: {field}");
                        if (!string.IsNullOrEmpty(mythValueType.Value))
                        {
                            var tableIndexType = (DInt)record.Data.Fields[mythTable.IndexFieldIdIndex];
                            expressions.Add(new ExpressionInfo()
                            {
                                expression = mythValueType.Value,
                                functionName = TypeUtil.ToCsStyleName($"{tableMythField.Name}_{tableIndexType.Value}"),
                                parameterType = enumToParameterType[mythParameterTypeEnumValue.Type.DefEnum][mythParameterTypeEnumValue.Value]
                            });
                        }
                    }
                }
            }

            var result = roslynExpressionProcessor.ProcessExpressions(expressions);
            var mythDataExport = new MythCodeTarget();
            var outputFile = mythDataExport.GenerateMyth(ctx, result, mythTable.ValueTType.DefBean);
            // Console.WriteLine($"outputFile :{outputFile.Content}");
            var outputManifest = new OutputFileManifest("myth", OutputType.Code);
            outputManifest.AddFile(outputFile);
            string outputSaverName = EnvManager.Current.GetOptionOrDefault(outputManifest.TargetName, BuiltinOptionNames.OutputSaver, true, "myth");
            var saver = OutputSaverManager.Ins.GetOutputSaver(outputSaverName);
            saver.Save(outputManifest);
        }
    }
}
