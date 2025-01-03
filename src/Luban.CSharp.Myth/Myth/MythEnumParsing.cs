using Luban;
using Luban.Datas;
using Luban.Types;
using Luban.Validator;

public class MythParameterParsing
{
    public static (List<DEnum>, List<DString>) ParseParameter(GenerationContext ctx, TEnum inEnum, string rawInputContext, DataValidatorVisitor visitor)
    {
        // TODO:validation 的写法不生效
        visitor = null;

        // result 映射到 CheckType，即传入的 inEnum
        var functions = new List<DEnum>();
        // result 映射到 CheckType 的参数，这里因为参数类型不能确定（可能是 int 可能是 string）因此统一使用 string 类型
        var functionsParameter = new List<DString>();
        rawInputContext = rawInputContext.Replace("；", ";");
        var splitRawInputContext = rawInputContext.Split(new[] { ';', '#' }, StringSplitOptions.RemoveEmptyEntries);


        foreach (var splitRawInputFunction in splitRawInputContext)
        {
            var trimmedRawInputFunction = splitRawInputFunction.Trim();

            // 统一替换中文括号和英文括号
            trimmedRawInputFunction = trimmedRawInputFunction.Replace('（', '(').Replace('）', ')');
            string enumItemValue;
            string? parameterInputValue = null;

            // 检查是否包含括号
            if (trimmedRawInputFunction.Contains('(') && trimmedRawInputFunction.Contains(')'))
            {
                // 提取括号外面的部分（例如：家具等级、梦想等级）
                enumItemValue = trimmedRawInputFunction.Substring(0, trimmedRawInputFunction.IndexOf('(')).Trim();

                // 提取括号内的部分（例如：厕所、主厨）
                parameterInputValue = trimmedRawInputFunction
                    .Substring(trimmedRawInputFunction.IndexOf('(') + 1, trimmedRawInputFunction.IndexOf(')') - trimmedRawInputFunction.IndexOf('(') - 1).Trim();
            }
            else
            {
                // 如果没有括号，直接将整个字符串作为 enumType
                enumItemValue = trimmedRawInputFunction;
            }

            // 根据提取的 enumType 查找相应的 Enum 类型
            var defEnum = inEnum.DefEnum.Items.Find(enumItem => enumItem.Alias == enumItemValue || enumItem.Name == enumItemValue);

            if (defEnum == null)
            {
                // 如果找不到 Enum 类型，跳过当前项或抛出异常
                throw new Exception($"在 枚举类型 {inEnum.DefEnum.FullName} 中未找到 '{enumItemValue}' 数据.");
            }


            // 将找到的 Enum 加入到结果列表中
            // 一个枚举类型对应一个功能函数
            functions.Add(new DEnum(inEnum, enumItemValue));


            // 下面处理对应的参数
            if (string.IsNullOrEmpty(parameterInputValue))
            {
                continue;
            }

            var parameterTypeDefValue = defEnum.GetTag("Parameter");
            var parameterTypeRefValue = defEnum.GetTag("ref");
            var compileParameterContext = !string.IsNullOrEmpty(parameterTypeRefValue) ? $"{parameterTypeDefValue}#ref={parameterTypeRefValue}" : parameterTypeDefValue;
            var parameterType = ctx.Assembly.CreateType("Myth", compileParameterContext, true);
            // NLog.LogManager.GetCurrentClassLogger().Info("Parameter Type Value: {0}", parameterTypeValue);

            if (parameterType is TEnum tEnum)
            {
                var enumItem = tEnum.DefEnum.Items.Find(enumItem => enumItem.Alias == parameterInputValue || enumItem.Name == parameterInputValue);

                if (enumItem == null)
                {
                    // 如果找不到 Enum 类型，跳过当前项或抛出异常
                    throw new Exception($"在 枚举类型 {tEnum.DefEnum.Name} 中未找到 '{parameterInputValue}' 数据.");
                }

                functionsParameter.Add(DString.ValueOf(tEnum, enumItem.Value));
            }
            else if (parameterType is TString itemString)
            {
                var value = DString.ValueOf(itemString, parameterInputValue);
                if (visitor != null)
                {
                    itemString.Apply(visitor, value);
                }

                functionsParameter.Add(value);
            }
            else if (parameterType is TList itemList)
            {
                var dList = new DList(itemList, ReadCollectionDatas(itemList, itemList.ElementType, parameterInputValue, visitor));
                functionsParameter.Add(DString.ValueOf(itemList, string.Join(itemList.GetTag("sep"), dList.Datas.Select(dType => ((DString)dType).Value))));
            }
            else
            {
                throw new NotImplementedException($"未处理的 Myth 参数类型 {parameterType}");
            }
        }

        return (functions, functionsParameter);
    }

    private static List<DType> ReadCollectionDatas(TType type, TType elementType, string rawInput, DataValidatorVisitor? visitor)
    {
        var datas = new List<DType>();
        var sep = type.GetTag("sep");
        var splitRawInput = rawInput.Split(sep, StringSplitOptions.RemoveEmptyEntries);

        foreach (var splitRawInputFunction in splitRawInput)
        {
            var trimmedRawInputFunction = splitRawInputFunction.Trim();
            var dString = DString.ValueOf(elementType, trimmedRawInputFunction);
            if (visitor != null)
            {
                elementType.Apply(visitor, dString);
            }

            datas.Add(dString);
        }

        return datas;
    }
}
