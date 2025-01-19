using Luban;
using Luban.Datas;
using Luban.Defs;
using Luban.Myth;
using Luban.RawDefs;
using Luban.Schema;
using Luban.Types;
using Myth;

[MythGenerationContext("default")]
public class MythGenerationContextEnhance : IMythGenerationContextEnhance
{
    public struct MetadataEnhance
    {
        public int targetParsingFieldIndex;

        public DefField defFieldMetadata;
        public DefField defFieldMethodName;
    }

    private Dictionary<DefBean, MetadataEnhance> _parsingCache = new Dictionary<DefBean, MetadataEnhance>();
    private DefBean _metadataBean;
    private DefEnum _metadataEvaluateType;
    private DefEnum _metadataOperator;
    private DefEnum _metadataValueType;

    public static bool MythGenerationEnabled;

    public void EnhanceScheme(GenerationContext ctx)
    {
        var mythFunctionDefineFilePath = EnvManager.Current.GetOptionRaw("MythFunctionDefineFilePath");
        MythGenerationEnabled = File.Exists(mythFunctionDefineFilePath);
        if (!MythGenerationEnabled)
        {
            return;
        }

        MythFunctionTable.LoadFromFile(mythFunctionDefineFilePath);

        _metadataBean = ctx.ExportBeans.Find(defBean => defBean.Namespace == "Myth" && defBean.Name == "MythMetadata")!;
        if (_metadataBean == null)
        {
            throw new FileNotFoundException("DefBean MythMetadata not found, maybe myth.xml is excluded?");
        }

        _metadataEvaluateType = ctx.ExportEnums.Find(defEnum => defEnum.Namespace == "Myth" && defEnum.Name == "MythEvaluateType")!;
        if (_metadataEvaluateType == null)
        {
            throw new FileNotFoundException("DefEnum MythEvaluateType not found, maybe myth.xml is excluded?");
        }

        _metadataOperator = ctx.ExportEnums.Find(defEnum => defEnum.Namespace == "Myth" && defEnum.Name == "MythOperatorType")!;
        if (_metadataOperator == null)
        {
            throw new FileNotFoundException("DefEnum MythOperatorType not found, maybe myth.xml is excluded?");
        }

        _metadataValueType = ctx.ExportEnums.Find(defEnum => defEnum.Namespace == "Myth" && defEnum.Name == "MythValueType")!;
        if (_metadataValueType == null)
        {
            throw new FileNotFoundException("DefEnum MythValueType not found, maybe myth.xml is excluded?");
        }

        foreach (var defBean in ctx.ExportBeans)
        {
            if (!defBean.HasTag("IsMythBean"))
            {
                continue;
            }

            var result = CreateMythMetadataField(ctx, defBean);
            if (result.Item1 != null)
            {
                _parsingCache.Add(result.Item1, result.Item2);
            }
        }
    }

    public void EnhanceLoadDatasAndValidate(GenerationContext genCtx)
    {
        if (!MythGenerationEnabled)
        {
            return;
        }

        // var v = new DataValidatorContext(genCtx.Assembly);
        // var visitor = new DataValidatorVisitor(v);
        foreach (var defTable in genCtx.Tables)
        {
            var records = genCtx.GetTableAllDataList(defTable);

            // 往 records 后面增加数据，数据解析自 targetCheckField 的字符串
            // 首先要检查 table 的 defBean 是传参的 defBean
            for (int i = 0; i < defTable.ValueTType.DefBean.ExportFields.Count; ++i)
            {
                var tableField = defTable.ValueTType.DefBean.ExportFields[i];
                if (tableField.CType is TBean fieldBean && _parsingCache.TryGetValue(fieldBean.DefBean, out var value))
                {
                    AppendMythMetadata(defTable, records, i, value, tableField);
                }
            }
        }
    }

    public (DefBean?, MetadataEnhance) CreateMythMetadataField(GenerationContext ctx, DefBean bean)
    {
        // DefEnum? enumType = null;
        int targetParsingFieldIndex = -1;
        for (int i = 0; i < bean.Fields.Count; ++i)
        {
            var checkField = bean.Fields[i];
            // 找到 bean 的字段中 tags 中包含有 MythEnum 的 field，这里理论上应该唯一，并且类型应该为 string
            if (checkField.HasTag("IsMythContent"))
            {
                targetParsingFieldIndex = i;
            }
        }

        if (targetParsingFieldIndex == -1)
        {
            throw new Exception("IsMythContent tag not found in any field, Bean type " + bean.FullName);
        }

        // 附加 myth 元数据到表格定义中，
        var rawFieldMethodName = new RawField()
        {
            Name = $"method_name",
            Type = $"string",
            Comment = "",
            // 标记，以防拿着这个字段去读表
            Tags = new Dictionary<string, string>() { { "MythMetadata", "" } },
            NotNameValidation = false,
            Groups = new List<string>(),
        };
        var defFieldMethodName = new DefField(bean, rawFieldMethodName, 0);
        defFieldMethodName.Compile();
        // result.Add(new DefField(bean, rawField, 0));
        bean.Fields.Add(defFieldMethodName);
        bean.HierarchyFields.Add(defFieldMethodName);
        // 这里直接附加 luban 定义好的 bean，名字是唯一的 "MythExpressionMetadata"
        var rawField = new RawField()
        {
            Name = $"metadata",
            Type = $"(list#sep=,),{_metadataBean.FullName}",
            Comment = "",
            // 标记，以防拿着这个字段去读表
            Tags = new Dictionary<string, string>() { { "MythMetadata", "" } },
            NotNameValidation = false,
            Groups = new List<string>(),
        };
        var defField = new DefField(bean, rawField, 0);
        defField.Compile();
        // result.Add(new DefField(bean, rawField, 0));
        bean.Fields.Add(defField);
        bean.HierarchyFields.Add(defField);

        return (bean, new MetadataEnhance() { targetParsingFieldIndex = targetParsingFieldIndex, defFieldMetadata = defField, defFieldMethodName = defFieldMethodName });
    }


    private void AppendMythMetadata(DefTable mythTable, List<Record> records, int targetTableDefBeanFieldIndex, MetadataEnhance metadataEnhance,
        DefField defFieldInTable)
    {
        var targetParsingFieldIndex = metadataEnhance.targetParsingFieldIndex;
        // var enumType = TEnum.Create(false, parsingToEnum, parsingToEnum!.Tags);
        foreach (var record in records)
        {
            var dType = record.Data.Fields[targetTableDefBeanFieldIndex];
            if (dType is DBean dBean && dBean.Fields[targetParsingFieldIndex] is DString rawData)
            {
                if (!string.IsNullOrEmpty(rawData.Value))
                {
                    MythLexer lexer = new MythLexer(rawData.Value);
                    var tokens = lexer.Tokenize();

                    // 2. 语法分析 -> AST
                    var parser = new MythParser(tokens);
                    MythExprNode ast = parser.ParseExpressionAndAnalyzeAST();

                    // 3. 生成 C# 代码
                    string methodName = MythDataExport.CreateMythMethodName(mythTable, record, defFieldInTable);
                    var metadataList = MythMetadataCollector.Collect(ast);

                    dBean.Fields.Add(DString.ValueOf(metadataEnhance.defFieldMethodName.CType, methodName));
                    dBean.Fields.Add(ConvertMetadataToRawBean(metadataList, metadataEnhance));
                }
                else
                {
                    dBean.Fields.Add(DString.ValueOf(metadataEnhance.defFieldMethodName.CType, string.Empty));
                    dBean.Fields.Add(new DList((TList)metadataEnhance.defFieldMetadata.CType, new List<DType>()));
                }
            }
        }
    }

    private DType ConvertMetadataToRawBean(List<MythMetadata> metadataList, MetadataEnhance metadataEnhance)
    {
        List<DType> metadataResultList = new List<DType>();
        var metadataTBean = TBean.Create(true, _metadataBean, new Dictionary<string, string>());
        var metadataEvaluateTypeTEnum = TEnum.Create(true, _metadataEvaluateType, _metadataEvaluateType.Tags);
        var metadataOperatorTEnum = TEnum.Create(true, _metadataOperator, _metadataOperator.Tags);
        var metadataValueTypeTEnum = TEnum.Create(true, _metadataValueType, _metadataValueType.Tags);
        var metadataTString = TString.Create(true, new Dictionary<string, string>());
        var metadataTString1 = TString.Create(true, new Dictionary<string, string>());
        var metadataTArrayString = TArray.Create(true, new Dictionary<string, string>(), metadataTString1);
        var metadataTString3 = TString.Create(true, new Dictionary<string, string>());
        var metadataTString4 = TString.Create(true, new Dictionary<string, string>());
        var metadataTArrayParameterType = TArray.Create(true, new Dictionary<string, string>(), metadataValueTypeTEnum);

        foreach (var metadata in metadataList)
        {
            List<DType> fields = new List<DType>();
            // 使用 metadata 填充 fields，参考 myth.xml，这里顺序是写死的，没有做检查，意味着 myth.xml 的定义顺序也不可变更
            fields.Add(new DEnum(metadataEvaluateTypeTEnum, string.IsNullOrEmpty(metadata.EvaluateType) ? "Unknown" : metadata.EvaluateType));
            fields.Add(new DEnum(metadataOperatorTEnum, metadata.Operator.ToString()));
            // 函数
            fields.Add(DString.ValueOf(metadataTString, metadata.Function));
            fields.Add(new DArray(metadataTArrayString, metadata.FunctionParameters.Select(parameter => (DType)DString.ValueOf(metadataTString1, parameter)).ToList()));
            fields.Add(DBool.ValueOf(metadata.IsParams));
            fields.Add(new DArray(metadataTArrayParameterType,
                metadata.FunctionParameterTypes.Select(parameterType => (DType)new DEnum(metadataValueTypeTEnum, parameterType.ToString())).ToList()));
            fields.Add(new DEnum(metadataValueTypeTEnum, metadata.FunctionReturnType.ToString()));
            // 字面值
            fields.Add(DString.ValueOf(metadataTString3, metadata.CompareToLiteralValue));
            fields.Add(DString.ValueOf(metadataTString4, metadata.CompareLiteralContext));
            var bean = new DBean(metadataTBean, _metadataBean!, fields);
            metadataResultList.Add(bean);
        }

        return new DList((TList)metadataEnhance.defFieldMetadata.CType, metadataResultList);
    }
}
