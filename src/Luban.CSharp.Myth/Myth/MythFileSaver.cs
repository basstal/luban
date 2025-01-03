using Luban;
using Luban.OutputSaver;

[OutputSaver("myth")]
public class MythFileSaver : LocalFileSaver
{
    public override string GetOutputDir(OutputFileManifest manifest)
    {
        return EnvManager.Current.GetOption($"{manifest.TargetName}", "outputMythDir", true);
    }
}
