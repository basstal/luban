using Luban.Defs;
using Luban.RawDefs;
using Luban.TemplateExtensions;
using Luban.Tmpl;
using Luban.Types;
using Luban.Utils;
using Scriban;
using Scriban.Runtime;

namespace Luban.CodeTarget;

public abstract class TemplateCodeTargetBase : CodeTargetBase
{
    protected virtual string CommonTemplateSearchPath => $"common/{FileSuffixName}";

    protected virtual string TemplateDir => Name;

    protected TemplateContext CreateTemplateContext(Template template)
    {
        var ctx = new TemplateContext() { LoopLimit = 0, NewLine = "\n", };
        ctx.PushGlobal(new ContextTemplateExtension());
        ctx.PushGlobal(new TypeTemplateExtension());
        OnCreateTemplateContext(ctx);
        return ctx;
    }

    protected abstract void OnCreateTemplateContext(TemplateContext ctx);

    protected virtual Scriban.Template GetTemplate(string name)
    {
        if (TemplateManager.Ins.TryGetTemplate($"{TemplateDir}/{name}", out var template))
        {
            return template;
        }

        if (!string.IsNullOrWhiteSpace(CommonTemplateSearchPath) && TemplateManager.Ins.TryGetTemplate($"{CommonTemplateSearchPath}/{name}", out template))
        {
            return template;
        }

        throw new Exception($"template:{name} not found");
    }

    public override void GenerateTables(GenerationContext ctx, List<DefTable> tables, CodeWriter writer)
    {
        var template = GetTemplate("tables");
        var tplCtx = CreateTemplateContext(template);
        var extraEnvs = new ScriptObject
        {
            { "__ctx", ctx },
            { "__name", ctx.Target.Manager },
            { "__namespace", ctx.Target.TopModule },
            { "__tables", tables },
            { "__code_style", CodeStyle },
        };
        tplCtx.PushGlobal(extraEnvs);
        writer.Write(template.Render(tplCtx));
    }

    public struct GroupByInfo
    {
        // 懒得管 scriban 中的命名规则转换了，这里全部用 scriban 中一致的命名
        public string dict_key_type;
        public string group_name;
        public string getter_parameters_to_group_key;
        public string getter_parameters;
        public DefField[] fields;
    }

    public virtual List<GroupByInfo> GatherGroupByInfo(DefTable table)
    {
        return new List<GroupByInfo>();
    }

    public override void GenerateTable(GenerationContext ctx, DefTable table, CodeWriter writer)
    {
        var template = GetTemplate("table");
        var tplCtx = CreateTemplateContext(template);
        var groupBy = GatherGroupByInfo(table);
        var extraEnvs = new ScriptObject
        {
            { "__ctx", ctx },
            { "__top_module", ctx.Target.TopModule },
            { "__manager_name", ctx.Target.Manager },
            { "__manager_name_with_top_module", TypeUtil.MakeFullName(ctx.TopModule, ctx.Target.Manager) },
            { "__name", table.Name },
            { "__namespace", table.Namespace },
            { "__namespace_with_top_module", table.NamespaceWithTopModule },
            { "__full_name_with_top_module", table.FullNameWithTopModule },
            { "__table", table },
            { "__this", table },
            { "__key_type", table.KeyTType },
            { "__value_type", table.ValueTType },
            { "__code_style", CodeStyle },
            { "__group_by", groupBy },
        };
        tplCtx.PushGlobal(extraEnvs);
        writer.Write(template.Render(tplCtx));
    }

    struct ExportArray
    {
        public TType CType;
        public DefField ArrayField;
        public DefField[] DefFields;
    }

    List<ExportArray> GetExportArrayGroups(GenerationContext ctx, DefBean bean)
    {
        var result = new List<ExportArray>();
        var exportFields = bean.GetExportFields();
        var exportArrayGroups = exportFields.Where(defField => defField.Tags.ContainsKey("array")).GroupBy(defField => defField.Tags["array"]);
        foreach (var exportArray in exportArrayGroups)
        {
            var groups = exportArray.GroupBy(defField =>
            {
                if (defField.CType is TBean tbean)
                {
                    // Console.WriteLine($"defField.CType.ToString()： {tbean.DefBean.FullName}");
                    return tbean.DefBean.FullName;
                }

                return defField.CType.ToString();
            });
            foreach (var group in groups)
            {
                var defFields = group.ToArray();
                var defFieldNames = defFields.Select(defField => defField.Name);
                // 取 defFieldNames 字符串的最长公共前缀
                string longestCommonPrefix = defFieldNames.Aggregate(
                    (prefix, next) => new string(prefix.Zip(next, (c1, c2) => c1 == c2 ? c1 : '\0').TakeWhile(c => c != '\0').ToArray())
                );
                longestCommonPrefix = $"{longestCommonPrefix.TrimEnd('_')}_array";
                var rawField = new RawField()
                {
                    Name = longestCommonPrefix,
                    Type = defFields[0].Type,
                    Comment = "",
                    Tags = new Dictionary<string, string>(),
                    NotNameValidation = false,
                    Groups = new List<string>()
                };
                // Console.WriteLine($"group.ElementAt(0).CType : {group.ElementAt(0).CType}");
                result.Add(new ExportArray() { CType = group.ElementAt(0).CType, DefFields = defFields, ArrayField = new DefField(defFields[0].HostType, rawField, 0), });
            }
        }

        return result;
    }

    public override void GenerateBean(GenerationContext ctx, DefBean bean, CodeWriter writer)
    {
        var template = GetTemplate("bean");
        var tplCtx = CreateTemplateContext(template);
        var exportArrayGroups = GetExportArrayGroups(ctx, bean);
        var extraEnvs = new ScriptObject
        {
            { "__ctx", ctx },
            { "__top_module", ctx.Target.TopModule },
            { "__manager_name", ctx.Target.Manager },
            { "__manager_name_with_top_module", TypeUtil.MakeFullName(ctx.TopModule, ctx.Target.Manager) },
            { "__name", bean.Name },
            { "__namespace", bean.Namespace },
            { "__changeable", bean.HasTag("IsChangeable") },
            { "__namespace_with_top_module", bean.NamespaceWithTopModule },
            { "__full_name_with_top_module", bean.FullNameWithTopModule },
            { "__bean", bean },
            { "__this", bean },
            { "__export_fields", bean.ExportFields },
            { "__hierarchy_export_fields", bean.HierarchyExportFields },
            { "__parent_def_type", bean.ParentDefType },
            { "__code_style", CodeStyle },
            { "export_array_groups", exportArrayGroups },
        };
        tplCtx.PushGlobal(extraEnvs);
        writer.Write(template.Render(tplCtx));
    }

    public struct EditableContent
    {
        public string begin;
        public string end;
        public string content;
    }

    public virtual EditableContent GetEditableContent(DefEnum defEnum)
    {
        return new EditableContent();
    }

    public override void GenerateEnum(GenerationContext ctx, DefEnum @enum, CodeWriter writer)
    {
        // // 检查 enum 定义中 non-empty alias 是否存在重复
        // // 这里假设 @enum.Items 是枚举项集合，每个项都有 Alias 属性
        // var aliasList = @enum.Items
        //     .Where(item => !string.IsNullOrWhiteSpace(item.Alias))
        //     .Select(item => item.Alias)
        //     .ToList();

        // var duplicateAliases = aliasList
        //     .GroupBy(alias => alias)
        //     .Where(g => g.Count() > 1)
        //     .Select(g => g.Key)
        //     .ToList();

        // if (duplicateAliases.Any())
        // {
        //     throw new Exception($"Enum '{@enum.Name}' has duplicate alias(es): {string.Join(", ", duplicateAliases)}");
        // }

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
        };
        tplCtx.PushGlobal(extraEnvs);
        writer.Write(template.Render(tplCtx));
    }
}
