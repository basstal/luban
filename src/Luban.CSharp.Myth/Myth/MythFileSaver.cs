using Luban;
using Luban.OutputSaver;

[OutputSaver("myth")]
public class MythFileSaver : LocalFileSaver
{
    protected override string GetOutputDir(OutputFileManifest manifest)
    {
        return EnvManager.Current.GetOption($"{manifest.TargetName}", "outputMythDir", true);
    }
}
