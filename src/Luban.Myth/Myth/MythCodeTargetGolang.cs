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
            { "__golang_top_myth_package", Path.GetFileName(MythManager.Ins.MythConfig.OutputMythCodeDir) },
            { "__import_prefix", string.Join("\n", MythManager.Ins.MythConfig.ImportPrefixList.Select(prefix => $"\"{prefix}\"")) },
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
            { "__golang_top_myth_package", Path.GetFileName(MythManager.Ins.MythConfig.OutputMythCodeDir) },
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
    }

    private bool HandlePlaceHolderNode_Go(List<MythExprNode> nodes, GenerationContext ctx)
    {
        foreach (var node in nodes)
        {
            if (node is PlaceHolderNode placeHolderNode)
            {
                var splitContent = placeHolderNode.OriginalValue.Split('.');
                var defineTable = ctx.ExportTables.Find(table => table.ValueType == splitContent[0]);
                var defineField = defineTable.ValueTType.DefBean.ExportFields.Find(field => field.Name == splitContent[1]);
                // Assuming Go `Tables` struct has PascalCase fields for each table.
                placeHolderNode.OutputValue = $"inExcels.Tables().{TypeUtil.ToPascalCase(defineTable.Name)}.Get().{TypeUtil.ToPascalCase(defineField.Name)}";
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
            else if (node is AssignmentNode assignmentNode)
            {
                if (HandlePlaceHolderNode_Go(new List<MythExprNode> { assignmentNode.Target, assignmentNode.Value }, ctx))
                {
                    return true;
                }
            }
            else if (node is ReturnNode returnNode)
            {
                if (HandlePlaceHolderNode_Go(new List<MythExprNode> { returnNode.Value }, ctx))
                {
                    return true;
                }
            }
            else if (node is CastExpressionNode castExpressionNode)
            {
                if (HandlePlaceHolderNode_Go(new List<MythExprNode> { castExpressionNode.Expression }, ctx))
                {
                    return true;
                }
            }
            else if (node is ConditionalExpressionNode conditionalExpressionNode)
            {
                if (HandlePlaceHolderNode_Go(new List<MythExprNode> { conditionalExpressionNode.Condition, conditionalExpressionNode.ThenExpr, conditionalExpressionNode.ElseExpr }, ctx))
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
                ReturnType = MythTypeUtil.MythValueTypeToGoStringNoFloat(functionBody.Signature.ReturnType),
                Parameters = string.Join(", ", functionBody.Signature.Parameters.Select(p => p.VariableSignature + " " + MythTypeUtil.MythValueTypeToGoStringNoFloat(p.Type)).ToList()),
            };

            if (HandlePlaceHolderNode_Go(functionBody.ParsedBodyLines, ctx))
            {
                needImportTables = true;
                var tableParam = $"inExcels *excels.Excels";
                outputFunction.Parameters = string.IsNullOrEmpty(outputFunction.Parameters) ? tableParam : tableParam + ", " + outputFunction.Parameters;
            }

            outputFunction.BodyLines = functionBody.ParsedBodyLines.Select(node => mythCodeGenerator.GenerateExpressionCode(node)).ToList();
            // if (functionBody.ParsedBodyLines.Count == 1)
            // {
            //     outputFunction.BodyLines[0] = "return " + outputFunction.BodyLines[0];
            // }
            return outputFunction;
        }).ToList();

        var extraEnvs = new ScriptObject
        {
            { "__ctx", ctx },
            { "__functions", functions },
            { "__need_import_tables", needImportTables },
            { "__golang_top_myth_package", Path.GetFileName(MythManager.Ins.MythConfig.OutputMythExpressionDir) },
            { "__import_prefix", string.Join("\n", MythManager.Ins.MythConfig.ImportPrefixList.Select(prefix => $"\"{prefix}\"")) },
        };
        tplCtx.PushGlobal(extraEnvs);
        writer.Write(template.Render(tplCtx));
        return new OutputFile() { File = $"MythExpressions.Myth.{MythManager.Ins.MythConfig.GetOutputSuffixByCodeTarget()}", Content = writer.ToResult(FileHeader) };
    }
}
