using Luban.Defs;
using Luban;
using System.Collections.Generic;
using System.Linq;
using Luban.Types;
using Myth;

namespace LocalBridge;

/// <summary>
/// 验证器参数信息
/// </summary>
public class PayloadValidatorParameter
{
    /// <summary>
    /// 值类型
    /// </summary>
    public MythValueType ValueType { get; set; }

    /// <summary>
    /// 引用类型（枚举或表的全名）
    /// </summary>
    public string ReferenceType { get; set; } = string.Empty;

    /// <summary>
    /// 是否为参数（可变参数）
    /// </summary>
    public bool IsParameter { get; set; }
}

/// <summary>
/// 验证器信息
/// </summary>
public class PayloadValidatorInfo
{
    /// <summary>
    /// 验证器参数列表
    /// </summary>
    public List<PayloadValidatorParameter> Parameters { get; set; } = new();
}

public class TypeOption
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;

    /// <summary>
    /// Payload 验证器信息（如果存在）
    /// </summary>
    public PayloadValidatorInfo? PayloadValidator { get; set; }
}

public interface ITypeOptionsProvider
{
    bool CanHandle(string type);
    List<TypeOption> GetOptions(string type);
}

public class EnumOptionsProvider : ITypeOptionsProvider
{
    public bool CanHandle(string type)
    {
        if (GenerationContext.Current == null)
        {
            return false;
        }
        var defType = FindDefType(type);
        return defType is DefEnum;
    }

    public List<TypeOption> GetOptions(string type)
    {
        var defType = FindDefType(type);
        if (defType is DefEnum defEnum)
        {
            var payloadValidators = MythGenerationContextEnhance.BuildPayloadValidators(defEnum);
            return defEnum.Items.Select(item =>
            {
                var option = new TypeOption
                {
                    Value = item.Name,
                    Label = item.AliasOrName,
                    Comment = item.Comment,
                };

                // 如果存在 payloadValidators，填充验证器信息
                if (payloadValidators != null && payloadValidators.TryGetValue(item.IntValue, out var validatorList))
                {
                    option.PayloadValidator = new PayloadValidatorInfo
                    {
                        Parameters = validatorList.Select(v => new PayloadValidatorParameter
                        {
                            ValueType = v.Item1,
                            ReferenceType = v.Item2,
                            IsParameter = v.Item3
                        }).ToList()
                    };
                }

                return option;
            }).ToList();
        }
        return new List<TypeOption>();
    }

    private DefTypeBase FindDefType(string type)
    {
        var assembly = GenerationContext.Current.Assembly;
        var defType = assembly.GetDefType(type);
        if (defType == null)
        {
            defType = assembly.GetDefType(GenerationContext.Current.TopModule, type);
        }
        return defType;
    }
}

public class TypeOptionsManager
{
    private static readonly List<ITypeOptionsProvider> _providers = new()
    {
        new EnumOptionsProvider(),
        // Future providers like FunctionOptionsProvider can be added here
    };

    public static bool IsOptionType(TType type)
    {
        if (type == null)
        {
            return false;
        }

        if (type.IsEnum && type is TEnum tenum)
        {
            return _providers.Any(p => p.CanHandle(tenum.DefEnum.FullName));
        }

        if (type is TArray array)
        {
            return IsOptionType(array.ElementType);
        }

        if (type is TList list)
        {
            return IsOptionType(list.ElementType);
        }

        if (type is TSet set)
        {
            return IsOptionType(set.ElementType);
        }

        if (type is TMap map)
        {
            return IsOptionType(map.KeyType) || IsOptionType(map.ValueType);
        }

        return false;
    }

    public static bool IsContainerType(TType type)
    {
        if (type == null)
        {
            return false;
        }
        return type is TArray || type is TList || type is TSet;
    }

    public static string GetTypeFullName(TType type)
    {
        if (type == null)
        {
            return string.Empty;
        }

        if (type.IsEnum && type is TEnum tenum)
        {
            return tenum.DefEnum.FullName;
        }

        if (type is TArray array)
        {
            return GetTypeFullName(array.ElementType);
        }

        if (type is TList list)
        {
            return GetTypeFullName(list.ElementType);
        }

        if (type is TSet set)
        {
            return GetTypeFullName(set.ElementType);
        }

        if (type is TMap map)
        {
            if (IsOptionType(map.ValueType))
            {
                return GetTypeFullName(map.ValueType);
            }
            if (IsOptionType(map.KeyType))
            {
                return GetTypeFullName(map.KeyType);
            }
        }

        return type.TypeName;
    }

    public static List<TypeOption> GetOptions(string type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return new List<TypeOption>();
        }

        foreach (var provider in _providers)
        {
            if (provider.CanHandle(type))
            {
                return provider.GetOptions(type);
            }
        }
        return new List<TypeOption>();
    }
}

