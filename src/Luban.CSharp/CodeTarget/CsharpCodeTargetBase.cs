using System.Text;
using Luban.CodeFormat;
using Luban.CodeTarget;
using Luban.CSharp.TemplateExtensions;
using Luban.Datas;
using Luban.Defs;
using Luban.OutputSaver;
using Luban.RawDefs;
using Luban.Types;
using Luban.Utils;
using Scriban;
using Scriban.Runtime;

namespace Luban.CSharp.CodeTarget;

public abstract class CsharpCodeTargetBase : TemplateCodeTargetBase
{
    public override string FileHeader => CommonFileHeaders.AUTO_GENERATE_C_LIKE;

    protected override string FileSuffixName => "cs";

    protected override ICodeStyle DefaultCodeStyle => CodeFormatManager.Ins.CsharpDefaultCodeStyle;

    private static readonly HashSet<string> s_preservedKeyWords = new()
    {
        "abstract",
        "as",
        "base",
        "bool",
        "break",
        "byte",
        "case",
        "catch",
        "char",
        "checked",
        "class",
        "const",
        "continue",
        "decimal",
        "default",
        "delegate",
        "do",
        "double",
        "else",
        "enum",
        "event",
        "explicit",
        "extern",
        "false",
        "finally",
        "fixed",
        "float",
        "for",
        "foreach",
        "goto",
        "if",
        "implicit",
        "in",
        "int",
        "interface",
        "internal",
        "is",
        "lock",
        "long",
        "namespace",
        "new",
        "null",
        "object",
        "operator",
        "out",
        "override",
        "params",
        "private",
        "protected",
        "public",
        "readonly",
        "ref",
        "return",
        "sbyte",
        "sealed",
        "short",
        "sizeof",
        "stackalloc",
        "static",
        "string",
        "struct",
        "switch",
        "this",
        "throw",
        "true",
        "try",
        "typeof",
        "uint",
        "ulong",
        "unchecked",
        "unsafe",
        "ushort",
        "using",
        "virtual",
        "void",
        "volatile",
        "while"
    };

    protected override IReadOnlySet<string> PreservedKeyWords => s_preservedKeyWords;


    protected override string GetFileNameWithoutExtByTypeName(string name)
    {
        return name.Replace('.', '/');
    }

    protected override void OnCreateTemplateContext(TemplateContext ctx)
    {
        ctx.PushGlobal(new CsharpTemplateExtension());
    }

    public override List<GroupByInfo> GatherGroupByInfo(DefTable table)
    {
        // 获取 DefTable 中的所有字段
        var defBean = table.ValueTType.DefBean;
        var exportFields = defBean.GetExportFields();
        var groupsByTag = new Dictionary<string, List<DefField>>();
        var groupByFields = exportFields.Where(defField =>
                defField.Tags.Any(pair => pair.Key.StartsWith("groupBy")) &&
                (defField.CType.TypeName == "string" || defField.CType.TypeName == "int" || defField.CType.TypeName == "enum"))
            .ToArray();
        foreach (var defField in groupByFields)
        {
            foreach (var pair in defField.Tags)
            {
                if (pair.Key.StartsWith("groupBy"))
                {
                    var groupName = pair.Value;
                    if (!groupsByTag.TryGetValue(groupName, out var list))
                    {
                        list = new List<DefField>();
                        groupsByTag[groupName] = list;
                    }

                    list.Add(defField);
                }
            }
        }


        List<GroupByInfo> groupByInfos = new List<GroupByInfo>();
        foreach (var group in groupsByTag)
        {
            // 将 group.Value DefField 中的 CType 拼为一个元组字符串
            var dictKeyType = CsharpTemplateExtension.DeclaringTypeName(group.Value[0].CType);
            var getterParametersToGroupKey = TypeUtil.ToCsStyleName(group.Value[0].Name);
            if (group.Value.Count > 1)
            {
                dictKeyType = $"({string.Join(", ", group.Value.Select(defField => CsharpTemplateExtension.DeclaringTypeName(defField.CType)))})";
                getterParametersToGroupKey = $"({string.Join(", ", group.Value.Select(defField => TypeUtil.ToCsStyleName(defField.Name)))})";
            }

            var groupName = string.Join("_", group.Value.Select(defField => defField.Name));
            var getterParameters = string.Join(", ",
                group.Value.Select(defField => $"{CsharpTemplateExtension.DeclaringTypeName(defField.CType)} {TypeUtil.ToCsStyleName(defField.Name)}"));
            groupByInfos.Add(new GroupByInfo()
            {
                dict_key_type = dictKeyType,
                group_name = groupName,
                fields = group.Value.ToArray(),
                getter_parameters = getterParameters,
                getter_parameters_to_group_key = getterParametersToGroupKey
            });
        }

        return groupByInfos;
    }

    public override EditableContent GetEditableContent(DefEnum defEnum)
    {
        // 这里定死了 targetName 为 client
        var targetName = "client";
        // 这里复制的 saver 的代码
        string outputSaverName = EnvManager.Current.GetOptionOrDefault(targetName, BuiltinOptionNames.OutputSaver, true, "local");
        var saver = OutputSaverManager.Ins.GetOutputSaver(outputSaverName) as OutputSaverBase;
        var codeOutputDir = saver!.GetOutputDir(OutputType.Code, targetName);

        var outputFilePath = Path.Combine(codeOutputDir, $"{GetFileNameWithoutExtByTypeName(defEnum.FullName)}.{FileSuffixName}");
        // Console.WriteLine($"outputFilePath :{outputFilePath}");
        var begin = "#region 自定义的枚举请填在下面，尽量使用大的枚举值以避免与 luban 转档的枚举值产生冲突";
        var end = "#endregion";
        // 初始化 EditableContent 结构
        var editableContent = new EditableContent { begin = begin, end = end, content = string.Empty };

        if (!File.Exists(outputFilePath))
        {
            return editableContent;
        }

        // try
        // {
        // 读取文件内容
        var lines = File.ReadAllLines(outputFilePath);
        bool inEditableRegion = false;
        StringBuilder contentBuilder = new StringBuilder();

        foreach (var line in lines)
        {
            // Trim the line to ignore leading/trailing spaces
            var trimmedLine = line.Trim();

            // Check if we found the start of the editable region
            if (!inEditableRegion && trimmedLine == begin)
            {
                inEditableRegion = true; // Start capturing content after this line
                continue; // Skip this line, we start capturing from the next line
            }

            // Check if we found the end of the editable region
            if (inEditableRegion && trimmedLine == end)
            {
                break; // Stop capturing content
            }

            // If we're within the editable region, add the line to the content
            if (inEditableRegion)
            {
                contentBuilder.AppendLine(line);
            }
        }

        // Set the content of EditableContent
        editableContent.content = contentBuilder.ToString().TrimEnd();
        // }
        // catch (Exception ex)
        // {
        //     // Handle any file reading or other exceptions
        //     
        // }

        return editableContent;
    }

    public override bool GenerateXlsxOpener(GenerationContext ctx, CodeWriter writer)
    {
        var template = GetTemplate("xlsxOpener");
        var tplCtx = CreateTemplateContext(template);
        string inputDataDir = GenerationContext.GetInputDataPath();
        string unityProjectDir = GenerationContext.GlobalConf.UnityProjectDir;
        var relativePath = Path.GetRelativePath(unityProjectDir, inputDataDir).Replace("\\", "/");
        var xlsxTables = ctx.ExportTables.Select(table =>
        {
            if (table.Tags.TryGetValue("中文名", out var tag))
            {
                // 这为啥是数组？有啥其他规则嘛？
                return (tag, table.ValueTType.DefBean.Name, table.InputFiles[0], relativePath);
            }

            // 这为啥是数组？有啥其他规则嘛？
            return (table.ValueTType.DefBean.Name, table.ValueTType.DefBean.Name, table.InputFiles[0], relativePath);
        });
        var extraEnvs = new ScriptObject
        {
            { "__ctx", ctx },
            { "__top_module", ctx.Target.TopModule },
            { "__manager_name", ctx.Target.Manager },
            { "__manager_name_with_top_module", TypeUtil.MakeFullName(ctx.TopModule, ctx.Target.Manager) },
            { "__code_style", CodeStyle },
            { "__xlsx_tables", xlsxTables }
        };
        tplCtx.PushGlobal(extraEnvs);
        writer.Write(template.Render(tplCtx));
        return true;
    }

    
}
