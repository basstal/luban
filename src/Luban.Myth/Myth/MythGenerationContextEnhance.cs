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
        public DefField defFieldRpnToken;
    }

    private Dictionary<DefBean, MetadataEnhance> _parsingCache = new Dictionary<DefBean, MetadataEnhance>();
    private HashSet<DefBean> _mythBeanCache = new HashSet<DefBean>();
    private DefBean _metadataBean;
    private DefEnum _metadataEvaluateType;
    private DefEnum _metadataOperator;
    private DefEnum _metadataValueType;
    private DefEnum _rpnTokenType;
    private DefEnum _rpnLogicalType;
    private DefBean _rpnTokenBean;
    private RpnBuilder _rpnBuilder;
    public static bool MythGenerationEnabled;

    public void EnhanceScheme(GenerationContext ctx)
    {
        var mythFunctionDefineFilePath = MythManager.Ins.MythConfig.MythFunctionDefineFilePath;
        MythGenerationEnabled = true;
        if (!File.Exists(mythFunctionDefineFilePath))
        {
            Console.WriteLine($"[ERROR] Myth function define file doesn't exist at {mythFunctionDefineFilePath}!");
            MythGenerationEnabled = false;
            return;
        }
        // MythGenerationEnabled = File.Exists(mythFunctionDefineFilePath);
        // if (!MythGenerationEnabled)
        // {
        //     return;
        // }

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

        _rpnTokenBean = ctx.ExportBeans.Find(defBean => defBean.Namespace == "Myth" && defBean.Name == "MythRpnToken")!;
        if (_rpnTokenBean == null)
        {
            throw new FileNotFoundException("DefBean MythRpnToken not found, maybe myth.xml is excluded?");
        }

        _rpnTokenType = ctx.ExportEnums.Find(defEnum => defEnum.Namespace == "Myth" && defEnum.Name == "MythRpnTokenType")!;
        if (_rpnTokenType == null)
        {
            throw new FileNotFoundException("DefEnum MythRpnTokenType not found, maybe myth.xml is excluded?");
        }

        _rpnLogicalType = ctx.ExportEnums.Find(defEnum => defEnum.Namespace == "Myth" && defEnum.Name == "MythLogicalType")!;
        if (_rpnLogicalType == null)
        {
            throw new FileNotFoundException("DefEnum MythLogicalType not found, maybe myth.xml is excluded?");
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
                _mythBeanCache.Add(result.Item1);
            }
        }
        _rpnBuilder = new RpnBuilder(ctx.ExportEnums);
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
                if (tableField.CType is TBean tableFieldBean)
                {
                    if (_parsingCache.TryGetValue(tableFieldBean.DefBean, out var value))
                    {
                        AppendMythMetadata(defTable, records, new[] { i }, value, tableField);
                    }
                    else
                    {
                        var (mythBeanPath, resultDefBean) = MythDataExport.FindMythBeanPath(tableFieldBean.DefBean, _mythBeanCache);
                        if (mythBeanPath != null && _parsingCache.TryGetValue(resultDefBean, out var value1))
                        {
                            AppendMythMetadata(defTable, records, new int[] { i }.Concat(mythBeanPath).ToArray(), value1, tableField);
                        }
                    }
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

        DefField CompileAndAddField(RawField rawField)
        {
            // 标记，以防拿着这个字段去读表
            rawField.Tags = new Dictionary<string, string>() { { "MythMetadata", "" } };
            rawField.NotNameValidation = false;
            rawField.Groups = new List<string>();
            rawField.Comment = string.Empty;
            var defField = new DefField(bean, rawField, 0);
            defField.Compile();
            // result.Add(new DefField(bean, rawField, 0));
            bean.Fields.Add(defField);
            bean.HierarchyFields.Add(defField);
            return defField;
        }
        // 附加 myth 元数据到表格定义中，
        var rawFieldMethodName = new RawField()
        {
            Name = $"method_name",
            Type = $"string",
        };
        var defFieldMethodName = CompileAndAddField(rawFieldMethodName);
        // 这里直接附加 luban 定义好的 bean，名字是唯一的 "MythExpressionMetadata"
        var rawField = new RawField()
        {
            Name = $"metadata",
            Type = $"(list#sep=,),{_metadataBean.FullName}",
        };
        var defFieldMetadata = CompileAndAddField(rawField);
        // 附加RPN执行单元
        var rawFieldRpnToken = new RawField()
        {
            Name = $"rpn_token",
            Type = $"(list#sep=,),{_rpnTokenBean.FullName}",
        };
        var defFieldRpnToken = CompileAndAddField(rawFieldRpnToken);

        return (bean, new MetadataEnhance()
        {
            targetParsingFieldIndex = targetParsingFieldIndex,
            defFieldMetadata = defFieldMetadata,
            defFieldMethodName = defFieldMethodName,
            defFieldRpnToken = defFieldRpnToken
        });
    }


    private void AppendMythMetadata(DefTable mythTable, List<Record> records, int[] targetTableDefBeanFieldPath, MetadataEnhance metadataEnhance, DefField defFieldInTable)
    {
        var targetParsingFieldIndex = metadataEnhance.targetParsingFieldIndex;
        var WalkByPath = (DBean data, int[] path) =>
        {
            var current = (DType)data;
            foreach (var index in path)
            {
                if (current is DBean dBean)
                {
                    current = dBean.Fields[index];
                }
                else
                {
                    throw new Exception("Invalid path");
                }
            }

            return current;
        };
        // var enumType = TEnum.Create(false, parsingToEnum, parsingToEnum!.Tags);
        foreach (var record in records)
        {
            var dType = WalkByPath(record.Data, targetTableDefBeanFieldPath);
            if (dType is DBean dBean && dBean.Fields[targetParsingFieldIndex] is DString rawData)
            {
                if (!string.IsNullOrEmpty(rawData.Value))
                {
                    MythLexer lexer = new MythLexer(rawData.Value);
                    var tokens = lexer.Tokenize();

                    // 2. 语法分析 -> AST
                    var parser = new MythParser(tokens);
                    MythExprNode ast = parser.ParseExpressionAndAnalyzeAST();

                    // 3. 收集元数据
                    string methodName = MythDataExport.CreateMythMethodName(mythTable, record, defFieldInTable);
                    // var metadataList = MythMetadataCollector.Collect(ast);
                    _rpnBuilder.Clear();
                    _rpnBuilder.Build(methodName, ast);


                    dBean.Fields.Add(DString.ValueOf(metadataEnhance.defFieldMethodName.CType, methodName));
                    dBean.Fields.Add(ConvertMetadataToRawBean(_rpnBuilder.Metas, metadataEnhance));
                    dBean.Fields.Add(ConvertRpnTokenToRawBean(_rpnBuilder.Tokens, metadataEnhance));
                }
                else
                {
                    dBean.Fields.Add(DString.ValueOf(metadataEnhance.defFieldMethodName.CType, string.Empty));
                    dBean.Fields.Add(new DList((TList)metadataEnhance.defFieldMetadata.CType, new List<DType>()));
                    dBean.Fields.Add(new DList((TList)metadataEnhance.defFieldRpnToken.CType, new List<DType>()));
                }
            }
        }
    }

    private DType ConvertRpnTokenToRawBean(List<Token> tokens, MetadataEnhance metadataEnhance)
    {
        List<DType> tokensResultList = new List<DType>();

        var rpnTokenBean = TBean.Create(true, _rpnTokenBean, new Dictionary<string, string>());
        var rpnTokenTypeTEnum = TEnum.Create(true, _rpnTokenType, _rpnTokenType.Tags);
        var rpnTokenLogicTypeTEnum = TEnum.Create(true, _rpnLogicalType, _rpnLogicalType.Tags);
        foreach (var token in tokens)
        {
            List<DType> fields = new List<DType>();
            // 参考 myth.xml，这里顺序是写死的，没有做检查，意味着 myth.xml 的定义顺序也不可变更
            fields.Add(new DEnum(rpnTokenTypeTEnum, token.Kind.ToString()));
            fields.Add(DInt.ValueOf(token.MetaIndex));
            fields.Add(new DEnum(rpnTokenLogicTypeTEnum, token.LogicSymbol.ToString()));
            var bean = new DBean(rpnTokenBean, _rpnTokenBean!, fields);
            tokensResultList.Add(bean);
        }

        return new DList((TList)metadataEnhance.defFieldRpnToken.CType, tokensResultList);
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
        // var metadataTString4 = TString.Create(true, new Dictionary<string, string>());
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
            fields.Add(new DArray(metadataTArrayParameterType, metadata.FunctionParameterTypes.Select(parameterType => (DType)new DEnum(metadataValueTypeTEnum, parameterType.ToString())).ToList()));
            fields.Add(new DEnum(metadataValueTypeTEnum, metadata.FunctionReturnType.ToString()));
            // 字面值
            fields.Add(DString.ValueOf(metadataTString3, metadata.CompareToLiteralValue));
            fields.Add(DBool.ValueOf(metadata.IsCompareLiteralLeftSide));
            var bean = new DBean(metadataTBean, _metadataBean!, fields);
            metadataResultList.Add(bean);
        }

        return new DList((TList)metadataEnhance.defFieldMetadata.CType, metadataResultList);
    }
}
