using Luban.RawDefs;
using Luban.Schema;
using Luban.Utils;

namespace Luban.Protobuf.Shimmer;

[SchemaCollector("pb")]
public class ProtoSchemaCollector : Luban.Schema.SchemaCollectorBase
{
    private static readonly NLog.Logger s_logger = NLog.LogManager.GetCurrentClassLogger();

    private LubanConfig _config;

    public override void Load(LubanConfig config)
    {
        _config = config;

        // TODO:所有定义都从 proto 中加载
        foreach (var importFile in _config.Imports)
        {
            s_logger.Debug("import schema file:{} type:{}", importFile.FileName, importFile.Type);
            var schemaLoader = SchemaManager.Ins.CreateSchemaLoader(FileUtil.GetExtensionWithDot(importFile.FileName), importFile.Type, this);
            schemaLoader.Load(importFile.FileName);
        }

        LoadTableValueTypeSchemasFromFile();
    }

    public override RawAssembly CreateRawAssembly()
    {
        return CreateRawAssembly(_config);
    }

    private void LoadTableValueTypeSchemasFromFile()
    {
        var tasks = new List<Task>();
        string beanSchemaLoaderName = EnvManager.Current.GetOptionOrDefault(BuiltinOptionNames.SchemaCollectorFamily, "beanSchemaLoader", true, "pb");
        foreach (var table in Tables)
        {
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    string fileName = table.InputFiles[0];
                    IBeanSchemaLoader schemaLoader = SchemaManager.Ins.CreateBeanSchemaLoader(beanSchemaLoaderName);
                    string fullPath = $"{GenerationContext.GetInputDataPath()}/{fileName}";
                    RawBean bean = schemaLoader.Load(fullPath, table.ValueType);
                    Add(bean);
                }
                catch (Exception ex)
                {
                    // Log the error
                    Console.WriteLine($"Error processing {table.Name}: {ex.Message}");
                    // Re-throw the exception to be aggregated
                    throw;
                }
            }));
        }

        try
        {
            Task.WaitAll(tasks.ToArray());
        }
        catch (AggregateException aggEx)
        {
            foreach (var ex in aggEx.Flatten().InnerExceptions)
            {
                Console.WriteLine($"Task error: {ex.Message}");
            }
        }
    }
}
