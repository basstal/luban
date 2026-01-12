using Luban.Defs;
using Luban.Types;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LocalBridge;

public class SchemaInfoManager
{
    public static object GetTableInfo(DefTable table)
    {
        return new
        {
            fullName = table.FullName,
            name = table.Name,
            @namespace = table.Namespace,
            mode = table.Mode.ToString(),
            inputFiles = table.InputFiles,
            fields = GetFields(table.ValueTType.DefBean, 0)
        };
    }

    private static List<object> GetFields(DefBean bean, int depth)
    {
        // 限制递归深度，防止循环引用导致栈溢出
        if (depth > 10)
        {
            return new List<object>();
        }

        return bean.HierarchyFields.Select(f => GetFieldInfo(f, depth)).ToList();
    }

    private static object GetFieldInfo(DefField f, int depth)
    {
        var type = f.CType;
        var fieldInfo = new Dictionary<string, object?>
        {
            ["name"] = f.Name,
            ["comment"] = f.Comment,
            ["typeFullName"] = TypeOptionsManager.GetTypeFullName(type),
            ["isOptionType"] = TypeOptionsManager.IsOptionType(type),
            ["isRefType"] = TypeRefManager.IsRefType(type),
            ["refTableName"] = TypeRefManager.GetRefTableFullName(type),
            ["isContainerType"] = TypeOptionsManager.IsContainerType(type),
            ["isMythContent"] = f.HasTag("IsMythContent"),
            ["isMythNoRpn"] = f.HasTag("MythNoRpn"),
        };

        // 尝试获取 Bean 类型（直接是 Bean 或 容器内的元素是 Bean）
        TBean? beanType = GetBeanType(type);
        if (beanType != null)
        {
            fieldInfo["isBean"] = true;
            fieldInfo["fields"] = GetFields(beanType.DefBean, depth + 1);
        }
        else
        {
            fieldInfo["isBean"] = false;
        }

        return fieldInfo;
    }

    private static TBean? GetBeanType(TType type)
    {
        if (type is TBean bean)
        {
            return bean;
        }

        if (type is TArray array && array.ElementType is TBean arrayBean)
        {
            return arrayBean;
        }

        if (type is TList list && list.ElementType is TBean listBean)
        {
            return listBean;
        }

        if (type is TSet set && set.ElementType is TBean setBean)
        {
            return setBean;
        }

        if (type is TMap map && map.ValueType is TBean mapBean)
        {
            return mapBean;
        }

        return null;
    }
}

