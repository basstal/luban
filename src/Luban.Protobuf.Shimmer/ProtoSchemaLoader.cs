using Luban.DataLoader;
using Luban.Datas;
using Luban.Defs;
using Luban.RawDefs;
using Luban.Schema;
using Luban.Types;
using Luban.Utils;

namespace Luban.Protobuf.Shimmer;

[SchemaLoader("pb", "pb")]
public class ProtoSchemaLoader : SchemaLoaderBase
{
    public override void Load(string fileName)
    {
        var defTableRecordType = new DefBean(new RawBean()
        {
            Namespace = "__intern__",
            Name = "__TableRecord__",
            Parent = "",
            Alias = "",
            IsValueType = false,
            Sep = "",
            Fields = new List<RawField>
            {
                new() { Name = "full_name", Type = "string" },
                new() { Name = "value_type", Type = "string" },
                new() { Name = "index", Type = "string" },
                new() { Name = "mode", Type = "string" },
                new() { Name = "group", Type = "string" },
                new() { Name = "comment", Type = "string" },
                new() { Name = "read_schema_from_file", Type = "bool" },
                new() { Name = "input", Type = "string" },
                new() { Name = "output", Type = "string" },
                new() { Name = "tags", Type = "string" },
            }
        })
        {
            Assembly = new DefAssembly(new RawAssembly()
            {
                Targets = new List<RawTarget> { new() { Name = "default", Manager = "Tables" } },
            }, "default", new List<string>()),
        };
        defTableRecordType.PreCompile();
        defTableRecordType.Compile();
        defTableRecordType.PostCompile();
        var tableRecordType = TBean.Create(false, defTableRecordType, null);

        // (var actualFile, var sheetName) = FileUtil.SplitFileAndSheetName(FileUtil.Standardize(fileName));
        var records = DataLoaderManager.Ins.LoadTableFile(tableRecordType, fileName, "*", new Dictionary<string, string>());
        // 这里每一个 record 应该代表的是一个 table 的定义
        foreach (var r in records)
        {
            DBean data = r.Data;
            //s_logger.Info("== read text:{}", r.Data);
            string fullName = (data.GetField("full_name") as DString).Value.Trim();
            string name = TypeUtil.GetName(fullName);
            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(name))
            {
                throw new Exception($"定义了一个空的table类名");
            }
            string module = TypeUtil.GetNamespace(fullName);
            string valueType = (data.GetField("value_type") as DString).Value.Trim();
            string index = (data.GetField("index") as DString).Value.Trim();
            string mode = (data.GetField("mode") as DString).Value.Trim();
            string group = (data.GetField("group") as DString).Value.Trim();
            string comment = (data.GetField("comment") as DString).Value.Trim();
            bool readSchemaFromFile = (data.GetField("read_schema_from_file") as DBool).Value;
            string inputFile = (data.GetField("input") as DString).Value.Trim();
            // string patchInput = (data.GetField("patch_input") as DString).Value.Trim();
            string tags = (data.GetField("tags") as DString).Value.Trim();
            string outputFile = (data.GetField("output") as DString).Value.Trim();
            // string options = (data.GetField("options") as DString).Value.Trim(); 
            var table = SchemaLoaderUtil.CreateTable(fileName, name, module, valueType, index, mode, group, comment, readSchemaFromFile, inputFile, tags, outputFile);
            Collector.Add(table);
        };
    }
}
