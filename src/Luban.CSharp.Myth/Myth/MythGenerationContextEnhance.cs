using Luban;
using Luban.Datas;
using Luban.Defs;
using Luban.Myth;
using Luban.RawDefs;
using Luban.Schema;
using Luban.Types;
using Luban.Utils;
using Luban.Validator;

[MythGenerationContext("default")]
public class MythGenerationContextEnhance : IMythGenerationContextEnhance
{
    public struct EnumParsingField
    {
        public int targetParsingFieldIndex;
        public DefEnum enumType;
        public DefField field;
        public DefField fieldParameters;
    }

    private Dictionary<DefBean, EnumParsingField> _parsingCache = new Dictionary<DefBean, EnumParsingField>();

    public void EnhanceScheme(GenerationContext ctx)
    {
        foreach (var defBean in ctx.ExportBeans)
        {
            var result = CreateMythEnumParsingFields(ctx, defBean);
            if (result.Item1 != null)
            {
                _parsingCache.Add(result.Item1, result.Item2);
            }
        }
    }

    public void EnhanceLoadDatasAndValidate(GenerationContext genCtx)
    {
        var v = new DataValidatorContext(genCtx.Assembly);
        var visitor = new DataValidatorVisitor(v);
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
                    var targetParsingFieldIndex = value.targetParsingFieldIndex;
                    var enumType = value.enumType;
                    var field = value.field;
                    var fieldParameters = value.fieldParameters;
                    AddMythEnumParsingDataToRecords(genCtx, records, i, targetParsingFieldIndex, enumType, field, fieldParameters, visitor);
                }
            }
        }
    }

    public (DefBean?, EnumParsingField) CreateMythEnumParsingFields(GenerationContext ctx, DefBean bean)
    {
        DefEnum? enumType = null;
        int targetParsingFieldIndex = -1;
        for (int i = 0; i < bean.Fields.Count; ++i)
        {
            var checkField = bean.Fields[i];
            // 找到 bean 的字段中 tags 中包含有 MythEnum 的 field，这里理论上应该唯一，并且类型应该为 string
            if (checkField.Tags.TryGetValue("MythEnum", out var enumTypeFullName))
            {
                if (checkField.CType is TString)
                {
                    targetParsingFieldIndex = i;
                    enumType = ctx.ExportEnums.Find(@enum => @enum.FullName == enumTypeFullName);
                    break;
                }

                throw new Exception($"MythEnum field {checkField.Name} must be string type");
            }
        }

        if (enumType == null)
        {
            return (null, default);
        }

        // 找到 enumType.Name 除第一个字母以外的大写字母，在大写字母前加入一个 _ 下划线
        var name = TypeUtil.AddUnderscoreBeforeUppercase(enumType.Name);
        // 附加序列化数据到表格中，这里表示有哪些函数
        var rawField = new RawField()
        {
            Name = $"{name}_array",
            Type = $"(list#sep=;),{enumType.FullName}",
            Comment = "",
            // 标记，以防拿着这个字段去读表
            Tags = new Dictionary<string, string>() { { "MythEnumAdded", "" } },
            NotNameValidation = false,
            Groups = new List<string>()
        };
        var field = new DefField(bean, rawField, 0);
        field.Compile();
        // result.Add(new DefField(bean, rawField, 0));
        bean.Fields.Add(field);
        bean.HierarchyFields.Add(field);
        // 附加序列化数据到表格中，这里表示有函数的对应参数值
        var rawFieldParameters = new RawField()
        {
            Name = $"{name}_parameters",
            Type = $"(list#sep=;),string",
            Comment = "",
            // 标记，以防拿着这个字段去读表
            Tags = new Dictionary<string, string>() { { "MythEnumAdded", "" } },
            NotNameValidation = false,
            Groups = new List<string>()
        };
        var fieldParameters = new DefField(bean, rawFieldParameters, 0);
        fieldParameters.Compile();
        bean.Fields.Add(fieldParameters);
        bean.HierarchyFields.Add(fieldParameters);
        // result.Add();
        // foreach (var field in result)
        // {
        //     field.Compile();
        // }

        return (bean, new EnumParsingField() { targetParsingFieldIndex = targetParsingFieldIndex, enumType = enumType, field = field, fieldParameters = fieldParameters });
    }


    private void AddMythEnumParsingDataToRecords(GenerationContext ctx, List<Record> records, int targetTableDefBeanFieldIndex, int targetParsingFieldIndex, DefEnum parsingToEnum,
        DefField fieldArray,
        DefField fieldParameters, DataValidatorVisitor visitor)
    {
        var enumType = TEnum.Create(false, parsingToEnum, parsingToEnum!.Tags);

        foreach (var record in records)
        {
            var dType = record.Data.Fields[targetTableDefBeanFieldIndex];
            if (dType is DBean dBean)
            {
                var rawData = dBean.Fields[targetParsingFieldIndex] as DString;
                var (resultDEnums, resultDParameters) = MythParameterParsing.ParseParameter(ctx, enumType, rawData.Value, visitor);
                dBean.Fields.Add(new DList(fieldArray.CType as TList, resultDEnums.Select(d => (DType)d).ToList()));
                dBean.Fields.Add(new DList(fieldParameters.CType as TList, resultDParameters.Select(d => (DType)d).ToList()));
            }
        }
    }
}
