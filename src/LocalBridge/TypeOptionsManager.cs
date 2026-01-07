using Luban.Defs;
using Luban;
using System.Collections.Generic;
using System.Linq;
using Luban.Types;

namespace LocalBridge;

public class TypeOption
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
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
            return defEnum.Items.Select(item => new TypeOption
            {
                Value = item.Name,
                Label = item.AliasOrName,
                Comment = item.Comment
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

