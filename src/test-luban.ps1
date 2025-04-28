param(
    # [Parameter(Mandatory = $true)]
    [string]$WorkingDir = "D:/svn_repo/trunk/Client/Externals/luban", # 工作目录，包含 luban.conf 和 ClientMythConfig.json
    
    # [Parameter(Mandatory = $true)]
    [string]$Client = "D:/svn_repo/trunk/Client", # 客户端目录，用于输出数据
    
    # [Parameter(Mandatory = $true)]
    [string]$XlsxDir = "D:/svn_repo/trunk/Client/Externals/xlsx", # Excel 文件目录
    
    [string]$LubanExePath = "D:/Documents/GitHub/luban/publish/win-x64/luban.exe", # Luban 程序路径
    [string]$Configuration = "Release"
)

# 1. 首先编译 Luban
Write-Host "Step 1: Building Luban..." -ForegroundColor Cyan
.\build-multi-platform.ps1 -Configuration $Configuration

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

# 2. 检查 Luban 程序是否存在
if (-not (Test-Path $LubanExePath)) {
    Write-Host "Luban executable not found at: $LubanExePath" -ForegroundColor Red
    exit 1
}

# 3. 构建参数
$processArgs = @(
    "-t", "client",
    "-c", "cs-bin",
    "-d", "bin",
    "--config", "$WorkingDir\luban.conf",
    "-x", "outputDataDir=$Client\Assets\FD2\AssetBundle\Data",
    "outputCodeDir=$Client\Assets\FD2\Scripts\Generated\Luban\Code",
    "l10n.provider=default",
    "l10n.textFile.path=$XlsxDir\TextInfo.xlsx",
    "l10n.textFile.keyFieldName=key",
    "dataExporter=myth",
    "mythConfig=$WorkingDir\ClientMythConfig.json"
)

# 4. 运行 Luban
Write-Host "`nStep 2: Running Luban..." -ForegroundColor Cyan
Write-Host "Arguments: $($processArgs -join ' ')" -ForegroundColor Gray

try {
    $process = Start-Process -FilePath $LubanExePath -ArgumentList $processArgs -NoNewWindow -Wait -PassThru
    
    if ($process.ExitCode -eq 0) {
        Write-Host "`nLuban execution completed successfully!" -ForegroundColor Green
    }
    else {
        Write-Host "`nLuban execution failed with exit code: $($process.ExitCode)" -ForegroundColor Red
        exit 1
    }
}
catch {
    Write-Host "Error running Luban: $_" -ForegroundColor Red
    exit 1
}

Write-Host "`nTest completed!" -ForegroundColor Green 