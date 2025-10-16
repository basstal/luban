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
        public DefField? defFieldRpnToken;
        public Dictionary<int, List<(MythValueType, string, bool)>>? payloadValidators;
        public int payloadValidatorFieldIndex;
    }
    private static readonly NLog.Logger s_logger = NLog.LogManager.GetCurrentClassLogger();

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

    private List<DefEnum> m_exportEnums;

    public void EnhanceScheme(GenerationContext ctx)
    {
        var mythFunctionDefineFilePath = MythManager.Ins.MythConfig.MythFunctionDefineFilePath;
        MythGenerationEnabled = true;
        if (!File.Exists(mythFunctionDefineFilePath))
        {
            s_logger.Warn($"[WARNING] Myth function define file doesn't exist at {mythFunctionDefineFilePath}!");
            MythGenerationEnabled = false;
            return;
        }
        // MythGenerationEnabled = File.Exists(mythFunctionDefineFilePath);
        // if (!MythGenerationEnabled)
        // {
        //     return;
        // }

        MythFunctionTable.ClearAndLoadSignaturesFromFile(mythFunctionDefineFilePath);
        MythFunctionTable.ClearAndLoadFunctionAndBodyFromFile(MythManager.Ins.MythConfig.MythExpressionFilePath);

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
        m_exportEnums = ctx.ExportEnums;
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
        var AppendMythMetadataForTBean = (DefTable defTable, List<Record> records, TBean tableFieldBean, int i, DefField tableField) =>
        {
            if (_parsingCache.TryGetValue(tableFieldBean.DefBean, out var value))
            {
                AppendMythMetadata(defTable, records, new[] { i }, value, tableField, genCtx);
            }
            else
            {
                var (mythBeanPath, resultDefBean) = MythDataExport.FindMythBeanPath(tableFieldBean.DefBean, _mythBeanCache);
                if (mythBeanPath != null && _parsingCache.TryGetValue(resultDefBean, out var value1))
                {
                    AppendMythMetadata(defTable, records, new int[] { i }.Concat(mythBeanPath).ToArray(), value1, tableField, genCtx);
                }
            }
        };
        foreach (var defTable in genCtx.Tables)
        {
            var records = genCtx.GetTableAllDataList(defTable);

            // 往 records 后面增加数据，数据解析自 targetCheckField 的字符串
            // 首先要检查 table 的 defBean 是传参的 defBean
            // if (defTable.Name.Contains("CharacterStandalone"))
            // {
            //     s_logger.Info("111");
            // }
            for (int i = 0; i < defTable.ValueTType.DefBean.ExportFields.Count; ++i)
            {
                var tableField = defTable.ValueTType.DefBean.ExportFields[i];
                if (tableField.CType is TBean tableFieldBean)
                {
                    AppendMythMetadataForTBean(defTable, records, tableFieldBean, i, tableField);
                }
                else if (tableField.CType is TArray tableFieldArray && tableFieldArray.ElementType is TBean tableFieldArrayBean)
                {
                    AppendMythMetadataForTBean(defTable, records, tableFieldArrayBean, i, tableField);
                }
                else if (tableField.CType is TList tableFieldList && tableFieldList.ElementType is TBean tableFieldListBean)
                {
                    AppendMythMetadataForTBean(defTable, records, tableFieldListBean, i, tableField);
                }
            }
        }
    }

    public (DefBean?, MetadataEnhance) CreateMythMetadataField(GenerationContext ctx, DefBean bean)
    {
        // DefEnum? enumType = null;
        int targetParsingFieldIndex = -1;
        int payloadValidatorFieldIndex = -1;
        bool noRpnExpression = false;
        DefField? payloadValidatorField = null;
        for (int i = 0; i < bean.Fields.Count; ++i)
        {
            var checkField = bean.Fields[i];
            // 找到 bean 的字段中 tags 中包含有 MythEnum 的 field，这里理论上应该唯一，并且类型应该为 string
            if (checkField.HasTag("IsMythContent"))
            {
                noRpnExpression = checkField.HasTag("MythNoRpn");
                targetParsingFieldIndex = i;
                var fieldNameOfPayloadValidator = checkField.GetTag("MythPayloadValidatorField");
                if (!string.IsNullOrEmpty(fieldNameOfPayloadValidator))
                {
                    // 目前 Validator 只支持枚举类型
                    payloadValidatorFieldIndex = bean.Fields.FindIndex(field => field.Name == fieldNameOfPayloadValidator && field.CType is TEnum);
                    if (payloadValidatorFieldIndex != -1)
                    {
                        payloadValidatorField = bean.Fields[payloadValidatorFieldIndex];
                    }
                }
                break;
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
        DefField? defFieldRpnToken = null;
        if (!noRpnExpression)
        {
            // 附加RPN执行单元
            var rawFieldRpnToken = new RawField()
            {
                Name = $"rpn_token",
                Type = $"(list#sep=,),{_rpnTokenBean.FullName}",
            };
            defFieldRpnToken = CompileAndAddField(rawFieldRpnToken);
        }

        Dictionary<int, List<(MythValueType, string, bool)>>? payloadValidators = null;
        if (payloadValidatorField != null)
        {
            payloadValidators = new Dictionary<int, List<(MythValueType, string, bool)>>();
            if (payloadValidatorField.CType is TEnum tEnum)
            {
                var defEnum = tEnum.DefEnum;
                foreach (var item in defEnum.Items)
                {
                    var validatorContent = item.GetTag("MythPayloadValidator");
                    var isValidatorIsParameter = item.HasTag("MythPayloadValidatorIsParams");
                    if (!string.IsNullOrEmpty(validatorContent))
                    {
                        var validatorContentList = validatorContent.Split(',');
                        var mythValueTypes = new List<(MythValueType, string, bool)>();
                        foreach (var oneParameter in validatorContentList)
                        {
                            // 提取 oneParameter 中类型和值，其中类型在后缀中以 [] 包裹
                            var typeAndValue = oneParameter.Split('[');
                            var type = string.Empty;
                            var referenceDefType = string.Empty;
                            if (typeAndValue.Length == 2)
                            {
                                type = typeAndValue[0];
                                referenceDefType = typeAndValue[1].TrimEnd(']');
                            }
                            else
                            {
                                type = oneParameter;
                                referenceDefType = string.Empty;
                            }
                            if (!Enum.TryParse(type, out MythValueType mythValueType) || mythValueType == MythValueType.Unknown)
                            {
                                s_logger.Error($"[ERROR] PayloadValidator tag {oneParameter} is not valid, item {item.Name} in enum {defEnum.Name}");
                                continue;
                            }
                            mythValueTypes.Add((mythValueType, referenceDefType, false));
                        }
                        mythValueTypes[^1] = (mythValueTypes[^1].Item1, mythValueTypes[^1].Item2, isValidatorIsParameter);
                        payloadValidators.Add(item.IntValue, mythValueTypes);
                    }
                }
            }
        }

        return (bean, new MetadataEnhance()
        {
            targetParsingFieldIndex = targetParsingFieldIndex,
            defFieldMetadata = defFieldMetadata,
            defFieldMethodName = defFieldMethodName,
            defFieldRpnToken = defFieldRpnToken,
            payloadValidators = payloadValidators,
            payloadValidatorFieldIndex = payloadValidatorFieldIndex,
        });
    }

    // 新增：校验单个literal节点类型和表引用
    private void ModifyAndValidateSingleLiteralNode(LiteralNode literalNode, (MythValueType, string, bool) validator, string displayRawData, GenerationContext genCtx, int index = -1)
    {
        if (!string.IsNullOrEmpty(validator.Item2))
        {
            if (validator.Item1 == MythValueType.Enum)
            {
                var findEnum = m_exportEnums.Find(defEnum => defEnum.FullName == validator.Item2);
                if (findEnum == null)
                {
                    throw new Exception($"{displayRawData}，表 [{validator.Item2}] 中不存在引用值 [{literalNode.RawValue}]");
                }
                var enumItem = findEnum.Items.Find(item => item.Name == literalNode.RawValue || item.Alias == literalNode.RawValue);
                if (enumItem == null)
                {
                    throw new Exception($"{displayRawData}，表 [{validator.Item2}] 中不存在引用值 [{literalNode.RawValue}]");
                }
                literalNode.SetType(MythValueType.Enum);
                literalNode.RawValue = enumItem.IntValue.ToString();
            }
            else
            {
                var findTableDef = genCtx.Tables.Find(defTable => defTable.FullName == validator.Item2);
                if (findTableDef != null)
                {
                    var keyType = findTableDef.ValueTType.DefBean.ExportFields[findTableDef.IndexFieldIdIndex];
                    var tableRecords = genCtx.GetTableAllDataList(findTableDef);
                    var referenceRecord = tableRecords.FindIndex(record =>
                    {
                        var dType = record.Data.GetField(keyType.Name);
                        if (dType is DString dString)
                        {
                            return dString.Value == literalNode.RawValue;
                        }
                        else if (dType is DInt dInt)
                        {
                            return dInt.Value == int.Parse(literalNode.RawValue);
                        }
                        return false;
                    });
                    if (referenceRecord == -1)
                    {
                        throw new Exception($"{displayRawData}，表 [{validator.Item2}] 中不存在引用值 [{literalNode.RawValue}]");
                    }
                }
            }
        }

        if (literalNode.ValueType == MythValueType.Unknown && !string.IsNullOrEmpty(literalNode.RawValue))
        {
            literalNode.SetType(MythValueType.String);
        }

        if (!LiteralNode.CanConvert(literalNode.ValueType, validator.Item1))
        {
            string idxMsg = index >= 0 ? $" 第 {index + 1} 个值" : " 单个值";
            throw new Exception($"{displayRawData}，{idxMsg}应该是 {validator} 类型，但是解析类型为 {literalNode.ValueType} ??");
        }
        else
        {
            literalNode.ConvertToType(validator.Item1);
        }
    }

    void PayloadModifyAndValidation(MythExprNode ast, Dictionary<int, List<(MythValueType, string, bool)>> payloadValidators, DType payloadValidatorField, string displayRawData, GenerationContext genCtx)
    {
        var payloadValidatorFieldEnum = payloadValidatorField as DEnum;
        if (!payloadValidators.TryGetValue(payloadValidatorFieldEnum.Value, out var validatorContext))
        {
            // 如果没有填校验器的定义则不校验
            return;
            // throw new Exception($"payloadValidate 的 enum 值 {payloadValidatorFieldEnum.Value} 不存在，配置可能存在问题");
        }
        // if (validatorContext.Any(entry => entry.Item2 == "Dialogue.TbDialogueEndingReward"))
        // {

        //     s_logger.Warn($"validator : {string.Join(",", validatorContext.Select(type => type.ToString()))}, displayRawData : {displayRawData}");
        // }
        if (ast is ListNode listNode)
        {
            if (listNode.Elements.Count != validatorContext.Count && !validatorContext[^1].Item3)
            {
                throw new Exception($"{displayRawData}，与 [{payloadValidatorFieldEnum.Type.DefEnum.FullName}] 枚举类型 [{payloadValidatorFieldEnum.StrValue}] 需要的参数数量不匹配，需要参数数量为 {validatorContext.Count}, 类型分别为 [{string.Join(",", validatorContext.Select(type => type.ToString()))}]");
            }
            for (int i = 0; i < validatorContext.Count; ++i)
            {

                var validator = validatorContext[i];
                if (validator.Item3) // 用同样的 validator 验证所有的参数
                {
                    for (int j = 0; j < listNode.Elements.Count; ++j)
                    {
                        if (listNode.Elements[j] is LiteralNode literalNode)
                        {
                            ModifyAndValidateSingleLiteralNode(literalNode, validator, displayRawData, genCtx, j);
                        }
                    }
                }
                else if (listNode.Elements[i] is LiteralNode literalNode)
                {
                    ModifyAndValidateSingleLiteralNode(literalNode, validator, displayRawData, genCtx, i);
                }
            }
        }
        else if (ast is LiteralNode ln)
        {
            if (validatorContext.Count != 1)
            {
                throw new Exception($"{displayRawData}，与 [{payloadValidatorFieldEnum.Type.DefEnum.FullName}] 枚举类型 [{payloadValidatorFieldEnum.StrValue}] 需要的参数数量不匹配，需要参数数量为 {validatorContext.Count}, 类型分别为 [{string.Join(",", validatorContext.Select(type => type.ToString()))}]");
            }
            ModifyAndValidateSingleLiteralNode(ln, validatorContext[0], displayRawData, genCtx);
        }
        else
        {
            throw new Exception($"{displayRawData}, payloadValidate 只不支持的Myth表达式类型{ast.ValueType}，配置可能存在问题");
        }
    }

    private void AppendMythMetadata(DefTable mythTable, List<Record> records, int[] targetTableDefBeanFieldPath, MetadataEnhance metadataEnhance, DefField defFieldInTable, GenerationContext genCtx)
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
        var ProcessDBean = (DBean dBean, Record record, DString rawData) =>
        {
            var payloadValidatorFieldData = metadataEnhance.payloadValidatorFieldIndex != -1 ? dBean.Fields[metadataEnhance.payloadValidatorFieldIndex] : null;
            if (!string.IsNullOrEmpty(rawData.Value))
            {
                MythLexer lexer = new MythLexer(rawData.Value);
                var tokens = lexer.Tokenize();

                // 2. 语法分析 -> AST
                var parser = new MythParser(tokens);
                MythExprNode ast = parser.ParseExpression();
                ast = MythSemanticAnalyzer.AnalyzeAST(ast);

                string methodName = MythDataExport.CreateMythMethodName(mythTable, record, defFieldInTable);
                if (metadataEnhance.defFieldRpnToken != null)
                {
                    try
                    {
                        // 3. 收集元数据
                        // var metadataList = MythMetadataCollector.Collect(ast);
                        _rpnBuilder.Clear();
                        _rpnBuilder.Build(methodName, ast);


                        dBean.Fields.Add(DString.ValueOf(metadataEnhance.defFieldMethodName.CType, methodName));
                        dBean.Fields.Add(ConvertMetadataToRawBean(_rpnBuilder.Metas, metadataEnhance));
                        dBean.Fields.Add(ConvertRpnTokenToRawBean(_rpnBuilder.Tokens, metadataEnhance));
                    }
                    catch (System.Exception)
                    {
                        s_logger.Error($"[ERROR] 表 [{record.Source}] 的 [{defFieldInTable.Name}] 列存在错误");
                        throw;
                    }
                }
                else
                {
                    if (metadataEnhance.payloadValidators != null)
                    {
                        var displayRawData = $"表 [{record.Source}] 的 [{defFieldInTable.Name}] 列存在数据校验 [{rawData.Value}] 错误";
                        PayloadModifyAndValidation(ast, metadataEnhance.payloadValidators, payloadValidatorFieldData, displayRawData, genCtx);
                    }
                    try
                    {
                        // 3. 收集元数据
                        var metadataList = MythMetadataCollector.Collect(ast, m_exportEnums);
                        if (metadataList.Count == 1 && metadataList[0].FunctionParameters.Count == 0 && ast is LiteralNode)
                        {
                            metadataList[0].FunctionParameters.Add(metadataList[0].CompareToLiteralValue);
                            metadataList[0].FunctionParameterTypes.Add(metadataList[0].CompareToLiteralValueType);
                        }
                        dBean.Fields.Add(DString.ValueOf(metadataEnhance.defFieldMethodName.CType, methodName));
                        dBean.Fields.Add(ConvertMetadataToRawBean(metadataList, metadataEnhance));
                    }
                    catch (System.Exception)
                    {
                        s_logger.Error($"[ERROR] 表 [{record.Source}] 的 [{defFieldInTable.Name}] 列存在错误");
                        throw;
                    }
                }
            }
            else
            {
                if (payloadValidatorFieldData is DEnum dEnum && dEnum.Value != 0)
                {
                    var defineField = dBean.TType.DefBean.Fields[metadataEnhance.payloadValidatorFieldIndex];
                    s_logger.Error($"[ERROR] 表 [{record.Source}] 的 [{defFieldInTable.Name}] 的 {defineField.Name} 列存在非 None(0) 枚举值 {dEnum.StrValue}({dEnum.Value})，但是对应数据列值为空？？");
                }
                dBean.Fields.Add(DString.ValueOf(metadataEnhance.defFieldMethodName.CType, string.Empty));
                dBean.Fields.Add(new DList((TList)metadataEnhance.defFieldMetadata.CType, new List<DType>()));
                if (metadataEnhance.defFieldRpnToken != null)
                {
                    dBean.Fields.Add(new DList((TList)metadataEnhance.defFieldRpnToken.CType, new List<DType>()));
                }
            }
        };
        // var enumType = TEnum.Create(false, parsingToEnum, parsingToEnum!.Tags);
        foreach (var record in records)
        {
            var dType = WalkByPath(record.Data, targetTableDefBeanFieldPath);
            if (dType is DArray dArray)
            {
                if (dArray.Datas.Count == 0)
                {
                    throw new Exception($"表 [{record.Source}] 的 [{defFieldInTable.Name}] 列存在数据校验 [{dArray.Datas}] 错误");
                }
                foreach (var dElementType in dArray.Datas)
                {
                    if (dElementType is DBean dElementBean && dElementBean.Fields[targetParsingFieldIndex] is DString rawElementData)
                    {
                        ProcessDBean(dElementBean, record, rawElementData);
                    }
                }
            }
            else if (dType is DList dList)
            {
                if (dList.Datas.Count == 0)
                {
                    throw new Exception($"表 [{record.Source}] 的 [{defFieldInTable.Name}] 列存在数据校验 [{dList.Datas}] 错误");
                }
                foreach (var dElementType in dList.Datas)
                {
                    if (dElementType is DBean dElementBean && dElementBean.Fields[targetParsingFieldIndex] is DString rawElementData)
                    {
                        ProcessDBean(dElementBean, record, rawElementData);
                    }
                }
            }
            else if (dType is DBean dBean && dBean.Fields[targetParsingFieldIndex] is DString rawData)
            {
                ProcessDBean(dBean, record, rawData);
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

        return new DList((TList)metadataEnhance.defFieldRpnToken!.CType, tokensResultList);
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
