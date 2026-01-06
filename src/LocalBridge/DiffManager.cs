using System.Diagnostics;
using Luban;
using Luban.DataTarget;
using Luban.Defs;
using Luban.OutputSaver;
using Luban.Pipeline;
using Luban.Utils;
using NLog;
using Excel2TextDiff;

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

    public static async Task RunExcelDiff(string xlsxPath)
    {
        if (!File.Exists(xlsxPath))
        {
            throw new Exception($"File not found: {xlsxPath}");
        }

        string baselineXlsx = Path.Combine(Path.GetTempPath(), "LubanDiff", $"{Guid.NewGuid()}_baseline.xlsx");
        Directory.CreateDirectory(Path.GetDirectoryName(baselineXlsx)!);

        bool hasBaseline = false;
        try
        {
            // 1. 尝试获取 SVN BASE 版本
            ProcessStartInfo svnInfo = new ProcessStartInfo
            {
                FileName = "svn",
                Arguments = $"info \"{xlsxPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var infoProcess = Process.Start(svnInfo);
            infoProcess?.WaitForExit();

            if (infoProcess?.ExitCode == 0)
            {
                // 是 SVN 受控文件，尝试 cat BASE
                ProcessStartInfo svnCat = new ProcessStartInfo
                {
                    FileName = "svn",
                    Arguments = $"cat -r BASE \"{xlsxPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var catProcess = Process.Start(svnCat);
                if (catProcess != null)
                {
                    using var fs = File.Create(baselineXlsx);
                    await catProcess.StandardOutput.BaseStream.CopyToAsync(fs);
                    catProcess.WaitForExit();
                    if (catProcess.ExitCode == 0)
                    {
                        hasBaseline = true;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            s_logger.Warn(ex, "Failed to get SVN BASE version, fallback to empty baseline");
        }

        if (!hasBaseline)
        {
            // 如果获取失败（如新增文件），创建一个空的 excel 文件或走 Mode A 逻辑
            // 这里简单处理：如果没有 baseline，则 Excel2TextDiff 可能会报错或 diff 一个空文件
            // 为了稳定，如果没有 baseline，我们可以创建一个空的 Excel 文件，或者提示用户
            s_logger.Warn("No SVN baseline found for {0}", xlsxPath);
            // 这里可以根据需求决定是否继续。如果继续，Excel2TextDiff 需要处理一个不存在的文件。
            // 既然用户提到 baseline empty，我们就在这里保证文件存在
            if (!File.Exists(baselineXlsx))
            {
                File.WriteAllBytes(baselineXlsx, Array.Empty<byte>()); // 极简处理
            }
        }

        try
        {
            // 2. 使用 Excel2TextWriter 将两个 excel 转换为文本进行比较
            var writer = new Excel2TextWriter();
            string baselineTxt = Path.Combine(Path.GetTempPath(), "LubanDiff", $"{Guid.NewGuid()}_baseline.txt");
            string currentTxt = Path.Combine(Path.GetTempPath(), "LubanDiff", $"{Guid.NewGuid()}_current.txt");

            s_logger.Info($"Transforming baseline: {baselineXlsx} -> {baselineTxt}");
            writer.TransformToTextAndSave(baselineXlsx, baselineTxt);

            s_logger.Info($"Transforming current: {xlsxPath} -> {currentTxt}");
            writer.TransformToTextAndSave(xlsxPath, currentTxt);

            // 3. 调用 Beyond Compare 比较生成的文本文件
            string bcomparePath = EnvManager.Current.GetOptionOrDefault("diff", "bcomparePath", false, "bcompare.exe");

            s_logger.Info($"Comparing current:{currentTxt} with baseline:{baselineTxt} using Beyond Compare");

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = bcomparePath,
                Arguments = $"\"{baselineTxt}\" \"{currentTxt}\"",
                UseShellExecute = true
            };

            Process.Start(startInfo);
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            s_logger.Error(ex, "Failed to run excel diff");
            throw;
        }
        finally
        {
            // 临时生成的 xlsx 文件可以删除，但 txt 文件最好留着直到 BCompare 关闭 (或者由用户手动清理)
            if (File.Exists(baselineXlsx))
            {
                try { File.Delete(baselineXlsx); } catch { /* ignore */ }
            }
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
