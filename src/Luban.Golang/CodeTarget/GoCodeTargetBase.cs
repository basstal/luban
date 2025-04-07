using Luban.CodeFormat;
using Luban.CodeTarget;
using Luban.Defs;
using Luban.Golang.TemplateExtensions;
using Luban.Golang.TypeVisitors;
using Luban.Utils;
using Scriban;
using Scriban.Runtime;

namespace Luban.Golang.CodeTarget;

public abstract class GoCodeTargetBase : TemplateCodeTargetBase
{
    public override string FileHeader => CommonFileHeaders.AUTO_GENERATE_C_LIKE;

    protected override string FileSuffixName => "go";

    protected override ICodeStyle DefaultCodeStyle => CodeFormatManager.Ins.GoDefaultCodeStyle;

    private static readonly HashSet<string> s_preservedKeyWords = new()
    {
        // go preserved key words 
        //"break", "default", "func", "interface", "select", "case", "defer", "go", "map", "struct", "chan", "else", "goto", "package", "switch", "const", "fallthrough", "if", "range", "continue", "for", "import", "return", "var"
    };

    protected override IReadOnlySet<string> PreservedKeyWords => s_preservedKeyWords;

    protected override void OnCreateTemplateContext(TemplateContext ctx)
    {
        ctx.PushGlobal(new GoCommonTemplateExtension());
        string lubanModuleName = EnvManager.Current.GetOption(Name, "lubanGoModule", true);
        ctx.PushGlobal(new ScriptObject() { { "__luban_module_name", lubanModuleName }, });
    }

    public override void GenerateEnum(GenerationContext ctx, DefEnum @enum, CodeWriter writer)
    {
        var template = GetTemplate("enum");
        var tplCtx = CreateTemplateContext(template);
        var editableContent = GetEditableContent(@enum);
        var extraEnvs = new ScriptObject
        {
            { "__ctx", ctx },
            { "__name", @enum.Name },
            { "__namespace", @enum.Namespace },
            { "__top_module", ctx.Target.TopModule },
            { "__namespace_with_top_module", @enum.NamespaceWithTopModule },
            { "__full_name_with_top_module", @enum.FullNameWithTopModule },
            { "__enum", @enum },
            { "__this", @enum },
            { "__code_style", CodeStyle },
            { "__editable_content", editableContent },
            { "__generate_alias_mapper", @enum.HasTag("GenerateAliasMapper") },
            { "__go_enum_name", UnderlyingDeclaringTypeNameVisitor.DefEnumToName(@enum) }
        };
        tplCtx.PushGlobal(extraEnvs);
        writer.Write(template.Render(tplCtx));
    }
}
