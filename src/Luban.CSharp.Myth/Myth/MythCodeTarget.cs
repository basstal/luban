using Luban;
using Luban.CodeTarget;
using Luban.CSharp.CodeTarget;
using Luban.Defs;
using Luban.Utils;
using Scriban.Runtime;

[CodeTarget("myth")]
public class MythCodeTarget : CsharpCodeTargetBase
{
    public OutputFile GenerateMyth(GenerationContext ctx, ExpressionProcessResult result, DefBean bean)
    {
        var writer = new CodeWriter();
        var template = GetTemplate("MythTemplate");
        var tplCtx = CreateTemplateContext(template);
        var extraEnvs = new ScriptObject
        {
            { "__ctx", ctx },
            { "__top_module", ctx.Target.TopModule },
            { "__manager_name", ctx.Target.Manager },
            { "__manager_name_with_top_module", TypeUtil.MakeFullName(ctx.TopModule, ctx.Target.Manager) },
            { "__name", bean.Name },
            { "__namespace", bean.Namespace },
            { "__namespace_with_top_module", bean.NamespaceWithTopModule },
            { "__full_name_with_top_module", bean.FullNameWithTopModule },
            { "__bean", bean },
            { "__this", bean },
            { "__export_fields", bean.ExportFields },
            { "__hierarchy_export_fields", bean.HierarchyExportFields },
            { "__parent_def_type", bean.ParentDefType },
            { "__code_style", CodeStyle },
            { "__methods", result.methods },
            { "__constDefinitions", result.constDefinitions },
            { "__valueCallMappings", result.valueCallMappings },
            { "__delegateTypesMapping", result.delegateTypesMapping },
            { "__constValues", result.constValues },
        };
        tplCtx.PushGlobal(extraEnvs);
        writer.Write(template.Render(tplCtx));
        return new OutputFile() { File = $"{GetFileNameWithoutExtByTypeName(bean.FullName)}.Myth.{FileSuffixName}", Content = writer.ToResult(FileHeader) };
    }
}
