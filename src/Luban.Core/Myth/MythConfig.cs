public class MythConfig
{
    public string FileLocation { get; set; }
    public string CodeTarget { get; set; }
    public string MythFunctionDefineFilePath { get; set; }
    public string OutputMythDir { get; set; }
    public string ImportPrefix { get; set; }

    public string GetOutputSuffixByCodeTarget()
    {
        switch (CodeTarget)
        {
            case "csharp":
                return "cs";
            case "golang":
                return "go";
            default:
                throw new InvalidOperationException($"Unsupported code target: {CodeTarget}");
        }
    }

    public void PostProcessRelativePath(string mythConfigFile)
    {
        if (string.IsNullOrEmpty(mythConfigFile))
        {
            throw new InvalidOperationException("mythConfigFile is null or empty");
        }

        // 检查所有路径不是绝对路径的值，将它的相对路径使用 mythConfigFile 所在的目录进行转换
        var mythConfigDir = Path.GetDirectoryName(mythConfigFile);
        if (!string.IsNullOrEmpty(FileLocation) && !Path.IsPathRooted(FileLocation))
        {
            FileLocation = Path.Combine(mythConfigDir, FileLocation);
        }

        if (!string.IsNullOrEmpty(MythFunctionDefineFilePath) && !Path.IsPathRooted(MythFunctionDefineFilePath))
        {
            MythFunctionDefineFilePath = Path.Combine(mythConfigDir, MythFunctionDefineFilePath);
        }

        if (!string.IsNullOrEmpty(OutputMythDir) && !Path.IsPathRooted(OutputMythDir))
        {
            OutputMythDir = Path.Combine(mythConfigDir, OutputMythDir);
        }
    }

    public override string ToString()
    {
        return $"fileLocation: {FileLocation}, codeTarget: {CodeTarget}, mythFunctionDefineFilePath: {MythFunctionDefineFilePath}, outputMythDir: {OutputMythDir}";
    }
}
