using Luban.Types;
using System.Collections.Generic;

namespace LocalBridge;

public class TypeRefManager
{
    public static bool IsRefType(TType type)
    {
        if (type == null)
        {
            return false;
        }

        if (type.HasTag("ref"))
        {
            return true;
        }

        if (type is TArray array)
        {
            return IsRefType(array.ElementType);
        }

        if (type is TList list)
        {
            return IsRefType(list.ElementType);
        }

        if (type is TSet set)
        {
            return IsRefType(set.ElementType);
        }

        if (type is TMap map)
        {
            return IsRefType(map.KeyType) || IsRefType(map.ValueType);
        }

        return false;
    }

    public static string GetRefTypeName(TType type)
    {
        if (type == null)
        {
            return string.Empty;
        }

        string refValue = type.GetTag("ref");
        if (!string.IsNullOrEmpty(refValue))
        {
            return refValue;
        }

        if (type is TArray array)
        {
            return GetRefTypeName(array.ElementType);
        }

        if (type is TList list)
        {
            return GetRefTypeName(list.ElementType);
        }

        if (type is TSet set)
        {
            return GetRefTypeName(set.ElementType);
        }

        if (type is TMap map)
        {
            string keyRef = GetRefTypeName(map.KeyType);
            if (!string.IsNullOrEmpty(keyRef))
            {
                return keyRef;
            }
            return GetRefTypeName(map.ValueType);
        }

        return string.Empty;
    }

    public static string GetRefTableFullName(TType type)
    {
        string refName = GetRefTypeName(type);
        if (string.IsNullOrEmpty(refName))
        {
            return string.Empty;
        }

        if (Luban.GenerationContext.Current == null)
        {
            return refName.TrimEnd('?');
        }

        // 尝试解析为完整的表名
        var tables = Luban.GenerationContext.Current.Tables;
        var table = tables.FirstOrDefault(t => t.FullName.Equals(refName, StringComparison.OrdinalIgnoreCase));
        if (table == null)
        {
            // 如果全名没匹配上，尝试按短名匹配
            table = tables.FirstOrDefault(t => t.Name.Equals(refName, StringComparison.OrdinalIgnoreCase));
        }

        var result = table?.FullName ?? refName;
        return result.TrimEnd('?');
    }
}

