using System.Reflection;

namespace Luban.OutputSaver;

public abstract class OutputSaverBase : IOutputSaver
{
    public virtual string Name => GetType().GetCustomAttribute<OutputSaverAttribute>().Name;

    public virtual string GetOutputDir(OutputFileManifest manifest)
    {
        return GetOutputDir(manifest.OutputType, manifest.TargetName);
    }

    public virtual string GetOutputDir(OutputType outputType, string targetName)
    {
        string optionName = outputType == OutputType.Code
            ? BuiltinOptionNames.OutputCodeDir
            : BuiltinOptionNames.OutputDataDir;
        return EnvManager.Current.GetOption($"{targetName}", optionName, true);
    }

    protected virtual void BeforeSave(OutputFileManifest outputFileManifest, string outputDir)
    {
    }

    protected virtual void PostSave(OutputFileManifest outputFileManifest, string outputDir)
    {
    }

    public virtual void Save(OutputFileManifest outputFileManifest)
    {
        string outputDir = GetOutputDir(outputFileManifest);
        BeforeSave(outputFileManifest, outputDir);
        var tasks = new List<Task>();
        foreach (var outputFile in outputFileManifest.DataFiles)
        {
            tasks.Add(Task.Run(() =>
            {
                SaveFile(outputFileManifest, outputDir, outputFile);
            }));
        }

        Task.WaitAll(tasks.ToArray());
        PostSave(outputFileManifest, outputDir);
    }

    public abstract void SaveFile(OutputFileManifest fileManifest, string outputDir, OutputFile outputFile);
}
