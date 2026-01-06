using Luban.Defs;
using Luban;
using System.Collections.Generic;
using System.Linq;
using Luban.Types;

namespace LocalBridge;

public class TypeOption
{
    public string Value { get; set; }
    public string Label { get; set; }
    public string Comment { get; set; }
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
        else if (type.IsEnum && type is TEnum tenum)
        {
            return _providers.Any(p => p.CanHandle(tenum.DefEnum.FullName));
        }
        return false;
    }

    public static string GetTypeFullName(TType type)
    {
        if (type == null)
        {
            return string.Empty;
        }
        else if (type.IsEnum && type is TEnum tenum)
        {
            return tenum.DefEnum.FullName;
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

