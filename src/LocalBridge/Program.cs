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
            version = "1.0.0",
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
        return Results.NotFound(new { ok = false, message = $"未找到文件 '{req.fileName}' 对应的配置表。" });
    }

    var fields = table.ValueTType.DefBean.HierarchyFields.Select(f => new
    {
        name = f.Name,
        isOptionType = TypeOptionsManager.IsOptionType(f.CType),
        typeFullName = TypeOptionsManager.GetTypeFullName(f.CType),
        isContainerType = TypeOptionsManager.IsContainerType(f.CType),
        comment = f.Comment
    }).ToList();
    var tableInfo = new
    {
        fullName = table.FullName,
        name = table.Name,
        @namespace = table.Namespace,
        mode = table.Mode.ToString(),
        inputFiles = table.InputFiles,
        fields = fields
    };
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

record HealthRequest(string projectRootDir);
