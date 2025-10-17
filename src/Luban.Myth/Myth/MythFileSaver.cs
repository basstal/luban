using Luban;
using Luban.OutputSaver;
using Luban.Myth;

[OutputSaver("MythCode")]
public class MythFileSaver : LocalFileSaver
{
    public override string GetOutputDir(OutputFileManifest manifest)
    {
        return MythManager.Ins.MythConfig.OutputMythCodeDir;
    }
}


[OutputSaver("MythExpression")]
public class MythExpressionFileSaver : LocalFileSaver
{
    public override string GetOutputDir(OutputFileManifest manifest)
    {
        return MythManager.Ins.MythConfig.OutputMythExpressionDir;
    }
}