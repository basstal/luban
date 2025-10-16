using Luban;
using Luban.CodeTarget;
using Luban.CSharp.CodeTarget;
using Luban.Defs;
using Luban.Golang.CodeTarget;
using Luban.Myth;
using Luban.Utils;
using Myth;
using Neo.IronLua;
using Scriban.Runtime;

[CodeTarget("myth_golang")]
public class MythCodeTemplateTargetGolang : GoCodeTargetBase, IMythCodeTemplateTarget
{
    public OutputFile GenerateMyth(GenerationContext ctx, Dictionary<string, (string, string)> result, DefBean bean, string interfaceName)
    {
        var writer = new CodeWriter();
        var template = GetTemplate("MythTemplate");
        var tplCtx = CreateTemplateContext(template);
        var typeNameToFileSaverPath = GetFileNameWithoutExtByTypeName(bean.FullName);
        var folderName = typeNameToFileSaverPath.Split(".").First().lower();
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
            { "__methods", result.Keys },
            { "__method_values", result.Values },
            { "__interface_name", interfaceName },
            { "__golang_myth_package", folderName },
            { "__golang_top_myth_package", MythGolangCodeGenerator.GolangTopModuleName },
            { "__import_prefix", MythManager.Ins.MythConfig.ImportPrefix },
            // { "__methods", result.methods },
            // { "__constDefinitions", result.constDefinitions },
            // { "__valueCallMappings", result.valueCallMappings },
            // { "__delegateTypesMapping", result.delegateTypesMapping },
            // { "__constValues", result.constValues },
            // { "__constValueGetters", result.constValueGetters },
            // { "__getterInfos", getterInfos }
        };
        tplCtx.PushGlobal(extraEnvs);
        writer.Write(template.Render(tplCtx));
        return new OutputFile() { File = $"{folderName}/{typeNameToFileSaverPath}.Myth.{MythManager.Ins.MythConfig.GetOutputSuffixByCodeTarget()}", Content = writer.ToResult(FileHeader) };
    }

    public OutputFile GenerateMythInterface(GenerationContext ctx, string interfaceName)
    {
        var writer = new CodeWriter();
        var template = GetTemplate("IMythConditionContextTemplate");
        var tplCtx = CreateTemplateContext(template);
        var extraEnvs = new ScriptObject
        {
            { "__ctx", ctx },
            { "__interface_name", interfaceName },
            { "__golang_top_myth_package", MythGolangCodeGenerator.GolangTopModuleName },
            // { "__top_module", ctx.Target.TopModule },
            // { "__manager_name", ctx.Target.Manager },
            // { "__manager_name_with_top_module", TypeUtil.MakeFullName(ctx.TopModule, ctx.Target.Manager) },
            // { "__code_style", CodeStyle },
            // { "__methods", ctx.Target.Methods },
            // { "__method_mapper", ctx.Target.MethodMapper },
            // { "__namespace", ctx.Target.Namespace },
            // { "__namespace_with_top_module", ctx.Target.NamespaceWithTopModule },
            // { "__full_name_with_top_module", ctx.Target.FullNameWithTopModule },
            // { "__bean", ctx.Target.Bean },
            // { "__this", ctx.Target.Bean },
            // { "__export_fields", ctx.Target.Bean.ExportFields },
            // { "__hierarchy_export_fields", ctx.Target.Bean.HierarchyExportFields },
            // { "__parent_def_type", ctx.Target.Bean.ParentDefType },
            // { "__constDefinitions", ctx.Target.ConstDefinitions },
            // { "__valueCallMappings", ctx.Target.ValueCallMappings },
            // { "__delegateTypesMapping", ctx.Target.DelegateTypesMapping },
            // { "__constValues", ctx.Target.ConstValues },
            // { "__constValueGetters", ctx.Target.ConstValueGetters },
            // { "__getterInfos", ctx.Target.GetterInfos }
        };
        tplCtx.PushGlobal(extraEnvs);
        writer.Write(template.Render(tplCtx));
        return new OutputFile() { File = $"{interfaceName}.Myth.{MythManager.Ins.MythConfig.GetOutputSuffixByCodeTarget()}", Content = writer.ToResult(FileHeader) };
    }

    public class GoOutputFunction
    {
        public string Name { get; set; }
        public string ReturnType { get; set; }
        public string Parameters { get; set; }
        public List<string> BodyLines { get; set; }

        public static string MythValueTypeToGoString(MythValueType inValueType)
        {
            switch (inValueType)
            {
                case MythValueType.Int:
                case MythValueType.IntTenThousandth:
                case MythValueType.Float:
                    return "int32";
                case MythValueType.Bool:
                    return "bool";
                case MythValueType.String:
                    return "string";
                case MythValueType.Enum:
                    return "int32"; // Assuming enums are represented as integers in Go
                default:
                    return "interface{}"; // Fallback for unknown types
            }
        }
    }

    private bool HandlePlaceHolderNode_Go(List<MythExprNode> nodes, GenerationContext ctx)
    {
        foreach (var node in nodes)
        {
            if (node is PlaceHolderNode placeHolderNode)
            {
                var splitContent = placeHolderNode.OriginalValue.Split('.');
                if (splitContent.Length != 2)
                {
                    // For now, we only support simple table.field access.
                    continue;
                }
                var tableName = splitContent[0];
                var fieldName = splitContent[1];

                var defineTable = ctx.ExportTables.Find(table => table.Name.Equals(tableName, StringComparison.OrdinalIgnoreCase));
                if (defineTable == null)
                {
                    continue;
                }

                var defineField = defineTable.ValueTType.DefBean.ExportFields.Find(field => field.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
                if (defineField == null)
                {
                    continue;
                }

                // Assuming Go `Tables` struct has PascalCase fields for each table.
                placeHolderNode.OutputValue = $"inTables.{TypeUtil.ToPascalCase(defineTable.Name)}.{TypeUtil.ToPascalCase(defineField.Name)}";
                return true;
            }
            else if (node is ArithmeticNode arithmeticNode)
            {
                if (HandlePlaceHolderNode_Go(new List<MythExprNode> { arithmeticNode.Left, arithmeticNode.Right }, ctx))
                {
                    return true;
                }
            }
            else if (node is ComparisonNode comparisonNode)
            {
                if (HandlePlaceHolderNode_Go(new List<MythExprNode> { comparisonNode.Left, comparisonNode.Right }, ctx))
                {
                    return true;
                }
            }
            else if (node is LogicalNode logicalNode)
            {
                if (HandlePlaceHolderNode_Go(new List<MythExprNode> { logicalNode.Left, logicalNode.Right }, ctx))
                {
                    return true;
                }
            }
            else if (node is FunctionCallNode functionCallNode)
            {
                if (HandlePlaceHolderNode_Go(functionCallNode.Arguments, ctx))
                {
                    return true;
                }
            }
            else if (node is ListNode listNode)
            {
                if (HandlePlaceHolderNode_Go(listNode.Elements, ctx))
                {
                    return true;
                }
            }
        }
        return false;
    }

    public OutputFile GenerateMythExpression(GenerationContext ctx, IMythCodeGenerator mythCodeGenerator)
    {
        var writer = new CodeWriter();
        var template = GetTemplate("MythExpression");
        var tplCtx = CreateTemplateContext(template);
        bool needImportTables = false;

        var functions = MythFunctionTable.FunctionBodies.Values.Select(functionBody =>
        {
            var outputFunction = new GoOutputFunction()
            {
                Name = functionBody.Signature.Name,
                ReturnType = GoOutputFunction.MythValueTypeToGoString(functionBody.Signature.ReturnType),
                Parameters = string.Join(", ", functionBody.Signature.Parameters.Select(p => p.VariableSignature + " " + GoOutputFunction.MythValueTypeToGoString(p.Type)).ToList()),
            };

            if (HandlePlaceHolderNode_Go(functionBody.ParsedBodyLines, ctx))
            {
                needImportTables = true;
                var tableParam = $"inTables *{MythGolangCodeGenerator.GolangTopModuleName}.Tables";
                outputFunction.Parameters = string.IsNullOrEmpty(outputFunction.Parameters) ? tableParam : tableParam + ", " + outputFunction.Parameters;
            }

            outputFunction.BodyLines = functionBody.ParsedBodyLines.Select(node => mythCodeGenerator.GenerateExpressionCode(node)).ToList();
            if (functionBody.ParsedBodyLines.Count == 1)
            {
                outputFunction.BodyLines[0] = "return " + outputFunction.BodyLines[0];
            }
            return outputFunction;
        }).ToList();

        var extraEnvs = new ScriptObject
        {
            { "__ctx", ctx },
            { "__functions", functions },
            { "__need_import_tables", needImportTables },
            { "__golang_top_myth_package", MythGolangCodeGenerator.GolangTopModuleName },
            { "__import_prefix", MythManager.Ins.MythConfig.ImportPrefix },
        };
        tplCtx.PushGlobal(extraEnvs);
        writer.Write(template.Render(tplCtx));
        return new OutputFile() { File = $"MythExpressions.Myth.{MythManager.Ins.MythConfig.GetOutputSuffixByCodeTarget()}", Content = writer.ToResult(FileHeader) };
    }
}
