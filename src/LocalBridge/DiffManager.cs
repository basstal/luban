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
        string? svnInfoOutput = null;
        string? svnInfoError = null;
        string? svnCatError = null;
        int? svnInfoExitCode = null;
        int? svnCatExitCode = null;

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
            if (infoProcess != null)
            {
                // 异步读取输出和错误，避免死锁
                var outputTask = infoProcess.StandardOutput.ReadToEndAsync();
                var errorTask = infoProcess.StandardError.ReadToEndAsync();
                await infoProcess.WaitForExitAsync();
                svnInfoOutput = await outputTask;
                svnInfoError = await errorTask;
                svnInfoExitCode = infoProcess.ExitCode;
            }

            if (svnInfoExitCode == 0)
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
                    // 异步读取错误输出，避免死锁
                    var errorTask = catProcess.StandardError.ReadToEndAsync();

                    using var fs = File.Create(baselineXlsx);
                    await catProcess.StandardOutput.BaseStream.CopyToAsync(fs);
                    fs.Flush();
                    fs.Close();

                    await catProcess.WaitForExitAsync();

                    // 读取错误输出
                    svnCatError = await errorTask;
                    svnCatExitCode = catProcess.ExitCode;

                    if (catProcess.ExitCode == 0 && string.IsNullOrWhiteSpace(svnCatError))
                    {
                        // 验证文件是否是有效的 Excel 文件（.xlsx 是 ZIP 格式）
                        if (IsValidExcelFile(baselineXlsx))
                        {
                            hasBaseline = true;
                        }
                        else
                        {
                            s_logger.Warn("SVN BASE file is not a valid Excel file, may contain error response");
                            try
                            { File.Delete(baselineXlsx); }
                            catch { /* ignore */ }
                        }
                    }
                    else
                    {
                        // 检查是否是新增文件（没有 BASE 版本）
                        bool isNewFile = svnCatError != null &&
                            (svnCatError.Contains("has no pristine version until it is committed") ||
                             svnCatError.Contains("E200009"));

                        // 检查 svn info 输出中是否包含 Schedule: add
                        bool isScheduledAdd = svnInfoOutput != null &&
                            svnInfoOutput.Contains("Schedule: add", StringComparison.OrdinalIgnoreCase);

                        if (isNewFile || isScheduledAdd)
                        {
                            // 新增文件，使用当前工作副本的版本作为 baseline
                            s_logger.Info("File is a new file (Schedule: add), using current working copy as baseline");

                            // 检查文件是否被修改
                            string? statusOutput = null;
                            string? statusError = null;
                            int? statusExitCode = null;

                            try
                            {
                                ProcessStartInfo svnStatus = new ProcessStartInfo
                                {
                                    FileName = "svn",
                                    Arguments = $"status \"{xlsxPath}\"",
                                    RedirectStandardOutput = true,
                                    RedirectStandardError = true,
                                    UseShellExecute = false,
                                    CreateNoWindow = true
                                };
                                using var statusProcess = Process.Start(svnStatus);
                                if (statusProcess != null)
                                {
                                    var statusOutputTask = statusProcess.StandardOutput.ReadToEndAsync();
                                    var statusErrorTask = statusProcess.StandardError.ReadToEndAsync();
                                    await statusProcess.WaitForExitAsync();
                                    statusOutput = await statusOutputTask;
                                    statusError = await statusErrorTask;
                                    statusExitCode = statusProcess.ExitCode;
                                }
                            }
                            catch (Exception ex)
                            {
                                s_logger.Warn(ex, "Failed to check SVN status");
                            }

                            // 如果文件未被修改（status 为空或只有空格），使用当前文件作为 baseline
                            if (statusExitCode == 0 && string.IsNullOrWhiteSpace(statusOutput))
                            {
                                // 文件未被修改，复制当前文件作为 baseline
                                File.Copy(xlsxPath, baselineXlsx, true);
                                if (IsValidExcelFile(baselineXlsx))
                                {
                                    hasBaseline = true;
                                    s_logger.Info("Using current working copy as baseline (file is unmodified)");
                                }
                            }
                            else
                            {
                                // 文件已被修改，对于新增文件，无法获取未修改的版本
                                s_logger.Warn("File is a new file but has been modified. Cannot get unmodified version for comparison.");
                                // 仍然尝试使用当前文件作为 baseline（比对会显示无差异）
                                File.Copy(xlsxPath, baselineXlsx, true);
                                if (IsValidExcelFile(baselineXlsx))
                                {
                                    hasBaseline = true;
                                    s_logger.Info("Using current working copy as baseline (file has been modified, comparison will show no difference)");
                                }
                            }
                        }
                        else
                        {
                            s_logger.Warn("SVN cat command failed. ExitCode: {0}, Error: {1}", catProcess.ExitCode, svnCatError);
                            try
                            { File.Delete(baselineXlsx); }
                            catch { /* ignore */ }
                        }
                    }
                }
            }
            else
            {
                // 文件不是 SVN 受控文件
                s_logger.Warn("File is not under SVN control. ExitCode: {0}, Error: {1}", svnInfoExitCode, svnInfoError);
            }
        }
        catch (Exception ex)
        {
            s_logger.Error(ex, "Exception occurred while trying to get SVN BASE version");
            // 确保清理可能创建的不完整文件
            if (File.Exists(baselineXlsx))
            {
                try
                { File.Delete(baselineXlsx); }
                catch { /* ignore */ }
            }
            // 重新抛出异常，包含详细信息
            throw new Exception($"Failed to get SVN BASE version for file: {xlsxPath}. " +
                $"SVN Info ExitCode: {svnInfoExitCode?.ToString() ?? "N/A"}, " +
                $"SVN Info Error: {svnInfoError ?? "N/A"}, " +
                $"SVN Cat ExitCode: {svnCatExitCode?.ToString() ?? "N/A"}, " +
                $"SVN Cat Error: {svnCatError ?? "N/A"}, " +
                $"Exception: {ex.Message}", ex);
        }

        // 如果没有获取到有效的 baseline，抛出详细错误
        if (!hasBaseline)
        {
            var errorDetails = new System.Text.StringBuilder();
            errorDetails.AppendLine($"无法获取 SVN BASE 版本的 Excel 文件: {xlsxPath}");
            errorDetails.AppendLine($"SVN Info 命令退出码: {svnInfoExitCode?.ToString() ?? "未执行"}");
            if (!string.IsNullOrWhiteSpace(svnInfoError))
            {
                errorDetails.AppendLine($"SVN Info 错误输出: {svnInfoError}");
            }
            if (!string.IsNullOrWhiteSpace(svnInfoOutput))
            {
                errorDetails.AppendLine($"SVN Info 标准输出: {svnInfoOutput}");
            }
            errorDetails.AppendLine($"SVN Cat 命令退出码: {svnCatExitCode?.ToString() ?? "未执行"}");
            if (!string.IsNullOrWhiteSpace(svnCatError))
            {
                errorDetails.AppendLine($"SVN Cat 错误输出: {svnCatError}");
            }

            // 检查是否是新增文件的情况
            bool isNewFile = svnCatError != null &&
                (svnCatError.Contains("has no pristine version until it is committed") ||
                 svnCatError.Contains("E200009"));
            bool isScheduledAdd = svnInfoOutput != null &&
                svnInfoOutput.Contains("Schedule: add", StringComparison.OrdinalIgnoreCase);

            if (isNewFile || isScheduledAdd)
            {
                errorDetails.AppendLine($"注意: 这是一个新增文件（Schedule: add），已尝试使用当前工作副本作为 baseline，但仍然失败。");
            }

            // 清理临时文件
            if (File.Exists(baselineXlsx))
            {
                try
                { File.Delete(baselineXlsx); }
                catch { /* ignore */ }
            }

            throw new Exception(errorDetails.ToString());
        }

        try
        {
            // 2. 使用 Excel2TextWriter 将两个 excel 转换为文本进行比较
            var writer = new Excel2TextWriter();
            string baselineTxt = Path.Combine(Path.GetTempPath(), "LubanDiff", $"{Guid.NewGuid()}_baseline.txt");
            string currentTxt = Path.Combine(Path.GetTempPath(), "LubanDiff", $"{Guid.NewGuid()}_current.txt");

            // 处理 baseline 文件（此时 hasBaseline 应该为 true，否则会在上面抛出异常）
            if (File.Exists(baselineXlsx) && IsValidExcelFile(baselineXlsx))
            {
                s_logger.Info($"Transforming baseline: {baselineXlsx} -> {baselineTxt}");
                writer.TransformToTextAndSave(baselineXlsx, baselineTxt);
            }
            else
            {
                throw new Exception($"Baseline Excel file is missing or invalid: {baselineXlsx}");
            }

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
                try
                { File.Delete(baselineXlsx); }
                catch { /* ignore */ }
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

    /// <summary>
    /// 验证文件是否是有效的 Excel 文件（.xlsx 是 ZIP 格式，文件签名应该是 PK）
    /// </summary>
    private static bool IsValidExcelFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return false;
        }

        try
        {
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length == 0)
            {
                return false;
            }

            // .xlsx 文件是 ZIP 格式，文件签名应该是 "PK" (0x50 0x4B)
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            byte[] header = new byte[2];
            int bytesRead = fs.Read(header, 0, 2);

            if (bytesRead < 2)
            {
                return false;
            }

            // ZIP 文件签名：PK (0x50 0x4B)
            return header[0] == 0x50 && header[1] == 0x4B;
        }
        catch
        {
            return false;
        }
    }
}
