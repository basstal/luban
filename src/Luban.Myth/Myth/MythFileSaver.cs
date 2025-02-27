using Luban;
using Luban.OutputSaver;
using Luban.Myth;

[OutputSaver("myth")]
public class MythFileSaver : LocalFileSaver
{
    public override string GetOutputDir(OutputFileManifest manifest)
    {
        return MythManager.Ins.MythConfig.OutputMythDir;
    }
}
