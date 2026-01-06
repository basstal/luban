using System.Diagnostics;
using Luban;
using Luban.DataTarget;
using Luban.Defs;
using Luban.OutputSaver;
using Luban.Pipeline;
using Luban.Utils;
using NLog;

namespace LocalBridge;

public class DiffManager
{
    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();

    public static async Task RunDiff(string fileName)
    {
        if (GenerationContext.Current == null)
        {
            throw new Exception("GenerationContext not initialized. Please call /health first.");
        }

        // 1. 找到对应的表
        var fileNameWithoutPath = Path.GetFileName(fileName);
        var table = GenerationContext.Current.Tables.FirstOrDefault(t =>
            t.InputFiles.Any(f => Path.GetFileName(f).Equals(fileNameWithoutPath, StringComparison.OrdinalIgnoreCase)));

        if (table == null)
        {
            throw new Exception($"Table for file '{fileName}' not found.");
        }

        // 2. 导出当前版本到临时目录
        string tempExportDir = Path.Combine(Path.GetTempPath(), "LubanDiff", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempExportDir);

        try
        {
            string exportedFilePath = ExportSingleTable(table, tempExportDir);

            // 3. 获取 Baseline 路径
            string baselineDir = EnvManager.Current.GetOptionOrDefault("", "baselineDir", false, "");
            if (string.IsNullOrEmpty(baselineDir))
            {
                baselineDir = EnvManager.Current.GetOptionOrDefault("", "outputDataDir", true, "");
            }

            if (string.IsNullOrEmpty(baselineDir))
            {
                throw new Exception("Baseline directory not configured. Please set 'diff.baselineDir' in xargs.");
            }

            string baselineFilePath = Path.Combine(baselineDir, Path.GetFileName(exportedFilePath));

            // 4. 调用 Beyond Compare
            string bcomparePath = EnvManager.Current.GetOptionOrDefault("diff", "bcomparePath", false, "bcompare.exe");

            s_logger.Info($"Comparing current:{exportedFilePath} with baseline:{baselineFilePath}");

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = bcomparePath,
                Arguments = $"\"{baselineFilePath}\" \"{exportedFilePath}\"",
                UseShellExecute = true
            };

            Process.Start(startInfo);
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            s_logger.Error(ex, "Failed to run diff");
            throw;
        }
    }

    private static string ExportSingleTable(DefTable table, string outputDir)
    {
        var ctx = GenerationContext.Current;
        IDataTarget dataTarget = DataTargetManager.Ins.CreateDataTarget("json");
        var records = ctx.GetTableExportDataList(table);

        // Canonicalize: 确保数据一致性，如果是 MAP 或有索引则进行排序
        if (table.Mode == TableMode.MAP)
        {
            records = GenerationContext.ToSortByKeyDataList(table, records);
        }
        else if (table.Mode == TableMode.LIST && table.IndexList.Count > 0)
        {
            records = GenerationContext.ToSortByKeyDataList(table, records);
        }

        var outputFile = dataTarget.ExportTable(table, records);

        string finalPath = Path.Combine(outputDir, outputFile.File);
        string? dir = Path.GetDirectoryName(finalPath);
        if (dir != null && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllBytes(finalPath, outputFile.GetContentBytes());
        return finalPath;
    }
}
