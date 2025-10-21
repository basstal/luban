using Luban;
using Luban.CodeTarget;
using Luban.CSharp.CodeTarget;
using Luban.Defs;
using Luban.Myth;
using Luban.Utils;
using Myth;
using Neo.IronLua;
using Scriban.Runtime;
using Scriban.Syntax;

[CodeTarget("myth_csharp")]
public class MythCodeTemplateTargetCSharp : CsharpCodeTargetBase, IMythCodeTemplateTarget
{
    public OutputFile GenerateMyth(GenerationContext ctx, Dictionary<string, (string, string)> result, DefBean bean, string interfaceName)
    {
        var writer = new CodeWriter();
        var template = GetTemplate($"MythTemplate");
        var tplCtx = CreateTemplateContext(template);
        var typeNameToFileSaverPath = GetFileNameWithoutExtByTypeName(bean.FullName);
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
        return new OutputFile()
        {
            File = $"{typeNameToFileSaverPath}.Myth.{MythManager.Ins.MythConfig.GetOutputSuffixByCodeTarget()}",
            Content = writer.ToResult(FileHeader)
        };
    }

    public OutputFile GenerateMythInterface(GenerationContext ctx, string interfaceName)
    {
        var writer = new CodeWriter();
        var template = GetTemplate($"IMythConditionContextTemplate");
        var tplCtx = CreateTemplateContext(template);
        var extraEnvs = new ScriptObject
        {
            { "__ctx", ctx },
            { "__interface_name", interfaceName },
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

    public class OutputFunction
    {
        public string Name { get; set; }
        public string ReturnType { get; set; }
        public string Parameters { get; set; }
        public List<string> BodyLines { get; set; }
    }

    private bool HandlePlaceHolderNode(List<MythExprNode> nodes, GenerationContext ctx)
    {
        foreach (var node in nodes)
        {
            if (node is PlaceHolderNode placeHolderNode)
            {
                var splitContent = placeHolderNode.OriginalValue.Split(".");
                var defineTable = ctx.ExportTables.Find(table => table.ValueType == splitContent[0]);
                var defineField = defineTable.ValueTType.DefBean.ExportFields.Find(field => field.Name == splitContent[1]);
                placeHolderNode.OutputValue = $"inTables.{defineTable.Name}.{TypeUtil.ToCsStyleName(defineField.Name)}";
                return true;
            }
            else if (node is ArithmeticNode arithmeticNode)
            {
                if (HandlePlaceHolderNode(new List<MythExprNode> { arithmeticNode.Left, arithmeticNode.Right }, ctx))
                {
                    return true;
                }
            }
            else if (node is ComparisonNode comparisonNode)
            {
                if (HandlePlaceHolderNode(new List<MythExprNode> { comparisonNode.Left, comparisonNode.Right }, ctx))
                {
                    return true;
                }
            }
            else if (node is LogicalNode logicalNode)
            {
                if (HandlePlaceHolderNode(new List<MythExprNode> { logicalNode.Left, logicalNode.Right }, ctx))
                {
                    return true;
                }
            }
            else if (node is FunctionCallNode functionCallNode)
            {
                if (HandlePlaceHolderNode(functionCallNode.Arguments, ctx))
                {
                    return true;
                }
            }
            else if (node is ListNode listNode)
            {
                if (HandlePlaceHolderNode(listNode.Elements, ctx))
                {
                    return true;
                }
            }
            else if (node is AssignmentNode assignmentNode)
            {
                if (HandlePlaceHolderNode(new List<MythExprNode> { assignmentNode.Target, assignmentNode.Value }, ctx))
                {
                    return true;
                }
            }
            else if (node is ReturnNode returnNode)
            {
                if (HandlePlaceHolderNode(new List<MythExprNode> { returnNode.Value }, ctx))
                {
                    return true;
                }
            }
            else if (node is CastExpressionNode castExpressionNode)
            {
                if (HandlePlaceHolderNode(new List<MythExprNode> { castExpressionNode.Expression }, ctx))
                {
                    return true;
                }
            }
            else if (node is ConditionalExpressionNode conditionalExpressionNode)
            {
                if (HandlePlaceHolderNode(new List<MythExprNode> { conditionalExpressionNode.Condition, conditionalExpressionNode.ThenExpr, conditionalExpressionNode.ElseExpr }, ctx))
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
        var template = GetTemplate($"MythExpression");
        var tplCtx = CreateTemplateContext(template);
        var functions = MythFunctionTable.FunctionBodies.Values.Select(functionBody =>
        {
            var outputFunction = new OutputFunction()
            {
                Name = functionBody.Signature.Name,
                ReturnType = MythTypeUtil.MythValueTypeToCSharpNoFloat(functionBody.Signature.ReturnType),
                Parameters = string.Join(", ", functionBody.Signature.Parameters.Select(p => MythTypeUtil.MythValueTypeToCSharpNoFloat(p.Type) + " " + p.VariableSignature).ToList()),
            };
            if (HandlePlaceHolderNode(functionBody.ParsedBodyLines, ctx))
            {
                outputFunction.Parameters = "cfg.Tables inTables, " + outputFunction.Parameters;
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
            {"__functions", functions},
        };
        tplCtx.PushGlobal(extraEnvs);
        writer.Write(template.Render(tplCtx));
        return new OutputFile() { File = $"MythFunctions.Myth.{MythManager.Ins.MythConfig.GetOutputSuffixByCodeTarget()}", Content = writer.ToResult(FileHeader) };
    }
}
