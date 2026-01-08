using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Luban;
using Luban.Defs;
using System.IO;
using LocalBridge;

var builder = WebApplication.CreateBuilder(args);

// 离线加载项（file://）常见 Origin: null；这里为“先跑通”放开 CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("wps", p =>
        p.SetIsOriginAllowed(_ => true)
         .AllowAnyHeader()
         .AllowAnyMethod()
    );
});

var app = builder.Build();
app.UseCors("wps");

// 探活：用于 WPS 端显示绿/灰
app.MapPost("/health", (HealthRequest req) =>
{
    try
    {
        var projectRootDir = req.projectRootDir;
        if (!Directory.Exists(projectRootDir))
        {
            return Results.Problem($"未找到项目根目录：{projectRootDir}。");
        }
        var args = new string[] {
            "-t",
            "client",
            "-c",
            "cs-simple-json",
            "-d",
            "json",
            "--config",
            $"{projectRootDir}\\Externals\\luban\\luban.conf",
            "-x",
            $"outputDataDir={projectRootDir}\\Client\\Assets\\FD2\\AssetBundle\\Data\\Json",
            $"outputCodeDir={projectRootDir}\\Client\\Assets\\FD2\\Scripts\\Generated\\LubanEditor\\Code",
            "l10n.provider=default",
            $"l10n.textFile.path={projectRootDir}\\Externals\\xlsx\\TextInfo.xlsx",
            "l10n.textFile.keyFieldName=key",
            "dataExporter=myth",
            $"mythConfig={projectRootDir}\\Externals\\luban\\temp\\ClientMythConfig.json",
            $"pathValidator.rootDir={projectRootDir}\\Client\\Assets\\FD2\\AssetBundle"
        };
        RuntimeEnvironment.Initialize(args);
        return Results.Ok(new
        {
            ok = true,
            name = "WpsLocalBridge",
            version = "2.0.0",
            serverTime = DateTimeOffset.Now
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

// 示例 1：简单 echo（调试用）
app.MapPost("/api/echo", (EchoRequest req) =>
{
    return Results.Ok(new EchoResponse
    {
        ok = true,
        message = req.message,
        receivedAt = DateTimeOffset.Now
    });
});

// 示例 2：执行某个动作（你替换成真实业务逻辑）
app.MapPost("/api/doSomething", (DoSomethingRequest req) =>
{
    // TODO: 在这里执行业务逻辑，比如写文件、调用你项目内部模块、触发任务等
    // 注意：不要做长时间阻塞；长任务建议入队/后台执行

    var result = $"Processed: {req.action}, payloadLen={req.payload?.Length ?? 0}";
    return Results.Ok(new { ok = true, result });
});

// 示例 3：返回服务信息/配置
app.MapGet("/api/info", () =>
{
    return Results.Ok(new
    {
        ok = true,
        machine = Environment.MachineName,
        os = Environment.OSVersion.ToString(),
        dotnet = Environment.Version.ToString()
    });
});

app.MapPost("/api/schema/find", (FindSchemaRequest req) =>
{
    if (GenerationContext.Current == null)
    {
        return Results.Problem("生成上下文未初始化。请先调用 /health 接口。");
    }

    var fileName = Path.GetFileName(req.fileName);
    var table = GenerationContext.Current.Tables.FirstOrDefault(t =>
        t.InputFiles.Any(f => Path.GetFileName(f).Equals(fileName, StringComparison.OrdinalIgnoreCase)));

    if (table == null)
    {
        return Results.Problem($"未找到文件 '{req.fileName}' 对应的配置表。");
    }

    var tableInfo = SchemaInfoManager.GetTableInfo(table);
    return Results.Ok(new
    {
        ok = true,
        table = tableInfo
    });
});

app.MapPost("/api/type/options", (TypeOptionsRequest req) =>
{
    if (GenerationContext.Current == null)
    {
        return Results.Problem("生成上下文未初始化。请先调用 /health 接口。");
    }

    var options = TypeOptionsManager.GetOptions(req.typeFullName);
    return Results.Ok(new
    {
        ok = true,
        options = options
    });
});

app.MapPost("/api/diff/run", async (DiffRequest req) =>
{
    try
    {
        await DiffManager.RunDiff(req.fileName);
        return Results.Ok(new { ok = true });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

app.MapPost("/api/diff/run2", async (DiffRequest2 req) =>
{
    try
    {
        await DiffManager.RunExcelDiff(req.xlsxPath);
        return Results.Ok(new { ok = true });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

app.MapPost("/api/rule/parse", (ParseRuleRequest req) =>
{
    try
    {
        if (string.IsNullOrWhiteSpace(req.dsl))
        {
            return Results.BadRequest(new { ok = false, message = "DSL 不能为空。" });
        }

        Myth.MythLexer lexer = new Myth.MythLexer(req.dsl);
        var tokens = lexer.Tokenize();
        var parser = new Myth.MythParser(tokens);
        Myth.MythExprNode ast = parser.ParseExpression();

        // 语义分析可能需要已初始化的 MythFunctionTable
        try
        {
            ast = Myth.MythSemanticAnalyzer.AnalyzeAST(ast);
        }
        catch (Exception ex)
        {
            // 如果语义分析失败（例如函数未定义），我们仍然尝试返回基础 AST，
            // 或者在这里处理错误。为了保证解析能跑通，如果语义分析报错，
            // 我们可以选择继续使用原始 AST，但某些类型信息可能会缺失。
            Console.WriteLine($"语义分析警告: {ex.Message}");
        }

        var result = RuleAstConverter.Convert(ast);
        return Results.Ok(new
        {
            ok = true,
            ast = result
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

app.MapPost("/api/rule/serialize", (SerializeRuleRequest req) =>
{
    try
    {
        if (req.ast.ValueKind == System.Text.Json.JsonValueKind.Undefined || req.ast.ValueKind == System.Text.Json.JsonValueKind.Null)
        {
            return Results.BadRequest(new { ok = false, message = "AST 不能为空。" });
        }

        string dsl = RuleAstConverter.Serialize(req.ast);
        return Results.Ok(new
        {
            ok = true,
            dsl = dsl
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

app.MapPost("/api/table/records", (TableRecordsRequest req) =>
{
    try
    {
        if (GenerationContext.Current == null)
        {
            return Results.Problem("生成上下文未初始化。请先调用 /health 接口。");
        }

        var table = GenerationContext.Current.Tables.FirstOrDefault(t => t.FullName.Equals(req.tableName, StringComparison.OrdinalIgnoreCase));
        if (table == null)
        {
            // 尝试不带模块名的匹配，有些地方可能只传了 TbXXX
            table = GenerationContext.Current.Tables.FirstOrDefault(t => t.Name.Equals(req.tableName, StringComparison.OrdinalIgnoreCase));
        }

        if (table == null)
        {
            return Results.Problem($"未找到表 '{req.tableName}'。");
        }

        if (!GenerationContext.Current.RecordsByTables.TryGetValue(table.FullName, out var tableDataInfo))
        {
            return Results.Problem($"表 '{req.tableName}' 数据未加载。");
        }
        var records = tableDataInfo.FinalRecords;

        var keys = new List<object>();

        if (table.IndexList.Count == 1)
        {
            var indexInfo = table.IndexList[0];
            foreach (var record in records)
            {
                var dType = record.Data.Fields[indexInfo.IndexFieldIdIndex];
                object value = dType switch
                {
                    Luban.Datas.DInt di => di.Value,
                    Luban.Datas.DLong dl => dl.Value,
                    Luban.Datas.DString ds => ds.Value,
                    Luban.Datas.DEnum de => de.Value,
                    Luban.Datas.DBool db => db.Value,
                    _ => dType.ToString()
                };
                keys.Add(value);
            }
        }
        else if (table.IndexList.Count > 1)
        {
            // TODO: 预留多主键处理，目前返回空列表
        }

        var firstInputFilePath = Path.Combine(GenerationContext.GetInputDataPath(), table.InputFiles.FirstOrDefault());
        return Results.Ok(new
        {
            ok = true,
            keys = keys,
            filePath = firstInputFilePath
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

app.MapGet("/api/rule/schema", () =>
{
    try
    {
        var functions = Myth.MythFunctionTable.Signatures.Values.Select(s => new
        {
            funcKey = s.Name,
            name = s.Name,
            @params = s.Parameters.Select(p => new
            {
                type = RuleAstConverter.MapValueType(p.Type),
                lubanType = p.LubanTypeReference,
                name = p.VariableSignature
            }).ToList(),
            returnType = RuleAstConverter.MapValueType(s.ReturnType),
            isParams = s.IsParams
        }).ToList();

        return Results.Ok(new
        {
            ok = true,
            functions = functions
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

// 只绑定 127.0.0.1，避免被局域网访问
app.Urls.Clear();
app.Urls.Add("http://127.0.0.1:18123");

app.Run();

record EchoRequest(string message);
record EchoResponse
{
    public bool ok { get; init; }
    public string message { get; init; } = "";
    public DateTimeOffset receivedAt { get; init; }
}

record DoSomethingRequest(string action, string? payload);

record FindSchemaRequest(string fileName);

record TypeOptionsRequest(string typeFullName);

record DiffRequest(string fileName);

record DiffRequest2(string xlsxPath);

record ParseRuleRequest(string dsl);

record SerializeRuleRequest(System.Text.Json.JsonElement ast);

record TableRecordsRequest(string tableName);

record HealthRequest(string projectRootDir);
