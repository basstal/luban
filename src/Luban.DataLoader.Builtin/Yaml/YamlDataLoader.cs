using Luban.DataLoader.Builtin.DataVisitors;
using Luban.Datas;
using Luban.Defs;
using Luban.Types;
using Luban.Utils;
using YamlDotNet.RepresentationModel;

namespace Luban.DataLoader.Builtin.Yaml;

[DataLoader("yml")]
public class YamlDataLoader : DataLoaderBase
{
    private YamlNode _root;
    public override void Load(string rawUrl, string sheetOrFieldName, Stream stream)
    {
        RawUrl = rawUrl;
        var ys = new YamlStream();
        ys.Load(new StreamReader(stream));
        var rootNode = ys.Documents[0].RootNode;

        // Console.WriteLine($"[YamlDataLoader] 加载Yaml文件: {rawUrl}");
        // Console.WriteLine($"[YamlDataLoader] 根节点类型: {rootNode.GetType().Name}");
        // Console.WriteLine($"[YamlDataLoader] 根节点内容: {rootNode}");

        this._root = rootNode;

        if (!string.IsNullOrEmpty(sheetOrFieldName))
        {
            if (sheetOrFieldName.StartsWith("*"))
            {
                sheetOrFieldName = sheetOrFieldName.Substring(1);
            }
            if (!string.IsNullOrEmpty(sheetOrFieldName))
            {
                foreach (var subField in sheetOrFieldName.Split('.'))
                {
                    this._root = _root[new YamlScalarNode(subField)];
                    // Console.WriteLine($"[YamlDataLoader] 进入子字段: {subField}, 当前节点类型: {_root.GetType().Name}, 内容: {_root}");
                }
            }
        }
    }

    public override List<Record> ReadMulti(TBean type)
    {
        var records = new List<Record>();
        int idx = 0;
        foreach (var ele in (YamlSequenceNode)_root)
        {
            // Console.WriteLine($"[YamlDataLoader] ReadMulti 正在读取第{idx}个元素: {ele}");
            var rec = ReadRecord(ele, type);
            if (rec != null)
            {
                records.Add(rec);
            }
            idx++;
        }
        // Console.WriteLine($"[YamlDataLoader] ReadMulti 读取完成, 总共读取到 {records.Count} 条记录");
        return records;
    }

    private static readonly YamlScalarNode s_tagNameNode = new(FieldNames.TagKey);

    public override Record ReadOne(TBean type)
    {
        return ReadRecord(_root, type);
    }

    private Record ReadRecord(YamlNode yamlNode, TBean type)
    {
        // Console.WriteLine($"[YamlDataLoader] ReadRecord 当前节点: {yamlNode}");
        string tagName;
        if (((YamlMappingNode)yamlNode).Children.TryGetValue(s_tagNameNode, out var tagNode))
        {
            tagName = (string)tagNode;
        }
        else
        {
            tagName = null;
        }
        if (DataUtil.IsIgnoreTag(tagName))
        {
            // Console.WriteLine($"[YamlDataLoader] ReadRecord 忽略tag: {tagName}");
            return null;
        }
        var data = (DBean)type.Apply(YamlDataCreator.Ins, yamlNode, type.DefBean.Assembly);
        var tags = DataUtil.ParseTags(tagName);
        // Console.WriteLine($"[YamlDataLoader] ReadRecord 生成Record, tag: {tagName}, tags: {string.Join(",", tags ?? new List<string>())}, data: {data}");
        return new Record(data, RawUrl, tags);
    }
}
