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
        bool hasPlaceHolder = false;
        foreach (var node in nodes)
        {
            if (HandlePlaceHolderNodeRecursive(node, ctx))
            {
                hasPlaceHolder = true;
            }
        }
        return hasPlaceHolder;
    }

    private bool HandlePlaceHolderNodeRecursive(MythExprNode? node, GenerationContext ctx)
    {
        if (node == null)
        {
            return false;
        }
        bool found = false;
        switch (node)
        {
            case PlaceHolderNode placeHolderNode:
                var splitContent = placeHolderNode.OriginalValue.Split('.');
                if (splitContent.Length < 2)
                {
                    return false;
                }
                var defineTable = ctx.ExportTables.Find(table => table.ValueType == splitContent[0]);
                if (defineTable == null)
                {
                    return false;
                }
                var defineField = defineTable.ValueTType.DefBean.ExportFields.Find(field => field.Name == splitContent[1]);
                if (defineField == null)
                {
                    return false;
                }
                placeHolderNode.OutputValue = $"inTables.{defineTable.Name}.{TypeUtil.ToCsStyleName(defineField.Name)}";
                return true;
            case ArithmeticNode arithmeticNode:
                found |= HandlePlaceHolderNodeRecursive(arithmeticNode.Left, ctx);
                found |= HandlePlaceHolderNodeRecursive(arithmeticNode.Right, ctx);
                break;
            case ComparisonNode comparisonNode:
                found |= HandlePlaceHolderNodeRecursive(comparisonNode.Left, ctx);
                found |= HandlePlaceHolderNodeRecursive(comparisonNode.Right, ctx);
                break;
            case LogicalNode logicalNode:
                found |= HandlePlaceHolderNodeRecursive(logicalNode.Left, ctx);
                found |= HandlePlaceHolderNodeRecursive(logicalNode.Right, ctx);
                break;
            case FunctionCallNode functionCallNode:
                foreach (var arg in functionCallNode.Arguments)
                {
                    found |= HandlePlaceHolderNodeRecursive(arg, ctx);
                }
                break;
            case ListNode listNode:
                foreach (var element in listNode.Elements)
                {
                    found |= HandlePlaceHolderNodeRecursive(element, ctx);
                }
                break;
            case AssignmentNode assignmentNode:
                found |= HandlePlaceHolderNodeRecursive(assignmentNode.Target, ctx);
                found |= HandlePlaceHolderNodeRecursive(assignmentNode.Value, ctx);
                break;
            case ReturnNode returnNode:
                found |= HandlePlaceHolderNodeRecursive(returnNode.Value, ctx);
                break;
            case CastExpressionNode castExpressionNode:
                found |= HandlePlaceHolderNodeRecursive(castExpressionNode.Expression, ctx);
                break;
            case ConditionalExpressionNode conditionalExpressionNode:
                found |= HandlePlaceHolderNodeRecursive(conditionalExpressionNode.Condition, ctx);
                found |= HandlePlaceHolderNodeRecursive(conditionalExpressionNode.ThenExpr, ctx);
                found |= HandlePlaceHolderNodeRecursive(conditionalExpressionNode.ElseExpr, ctx);
                break;
        }
        return found;
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
