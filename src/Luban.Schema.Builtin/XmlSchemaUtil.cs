using System.Diagnostics;
using System.Text;
using System.Xml.Linq;
using Luban.Defs;
using Luban.RawDefs;
using Luban.Utils;
using NLog.Fluent;

namespace Luban.Schema.Builtin;

public static class XmlSchemaUtil
{
    public static void ValidAttrKeys(string defineFile, XElement e, List<string> optionKeys, List<string> requireKeys)
    {
        foreach (var k in e.Attributes())
        {
            var name = k.Name.LocalName;
            // Console.Write($"ValidAttrKeys name :{name}\n{new StackTrace()}");
            if (!requireKeys.Contains(name) && optionKeys != null && !optionKeys.Contains(name))
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("[Required]:");
                requireKeys.ForEach(key => sb.Append(key).Append(","));
                sb.Append("[Optional]:");
                optionKeys.ForEach(key => sb.Append(key).Append(","));
                throw new LoadDefException($"定义文件:{defineFile} 定义:{e} 包含未知属性 attr:{name}\n合法的属性：{sb}");
            }
        }
        foreach (var k in requireKeys)
        {
            if (e.Attribute(k) == null)
            {
                throw new LoadDefException($"定义文件:{defineFile} 定义:{e} 缺失属性 attr:{k}");
            }
        }
    }

    private static readonly List<string> _refGroupRequireAttrs = new() { "name", "ref" };

    public static RawRefGroup CreateRefGroup(string fileName, XElement e)
    {
        ValidAttrKeys(fileName, e, null, _refGroupRequireAttrs);

        return new RawRefGroup()
        {
            Name = XmlUtil.GetRequiredAttribute(e, "name"),
            Refs = XmlUtil.GetRequiredAttribute(e, "ref").Split(',').Select(s => s.Trim()).ToList(),
        };
    }
}
