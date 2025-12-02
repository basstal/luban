param(
    [string]$OutputDir = "C:\FD2\trunk\Externals\luban\publish",
    [string]$Configuration = "Release",
    [string[]]$Runtimes = @("win-x64", "linux-arm64", "osx-x64", "osx-arm64")
)

# 确保输出目录存在
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
    Write-Host "Created output directory: $OutputDir"
}

# 设置环境变量
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = 1
$env:DOTNET_CLI_TELEMETRY_OPTOUT = 1

# 清理之前的构建
Write-Host "Cleaning previous build..."
dotnet clean Luban.sln -c $Configuration

# 还原包
Write-Host "Restoring packages..."
dotnet restore Luban.sln

# 为每个运行时构建
foreach ($runtime in $Runtimes) {
    $runtimeOutputDir = Join-Path $OutputDir $runtime
    Write-Host "`nBuilding for runtime: $runtime" -ForegroundColor Cyan
    
    # 确保运行时特定输出目录存在
    if (-not (Test-Path $runtimeOutputDir)) {
        New-Item -ItemType Directory -Path $runtimeOutputDir | Out-Null
    }
    
    # 发布到特定运行时
    dotnet publish Luban.sln -c $Configuration -r $runtime --self-contained true -o $runtimeOutputDir
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Successfully built for $runtime" -ForegroundColor Green
    }
    else {
        Write-Host "Failed to build for $runtime" -ForegroundColor Red
        exit 1
    }
}

Write-Host "`nAll platform builds completed!" -ForegroundColor Green
Write-Host "Output directory: $OutputDir" 