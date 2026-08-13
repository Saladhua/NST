# 启动后端 API，日志统一写入 logs/ 目录
# 用法：powershell -File start-api.ps1
$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$logsDir = Join-Path $root "logs"
$apiDir = Join-Path $root "src\OrderPlatform.Api"

if (-not (Test-Path -LiteralPath $logsDir)) {
    New-Item -ItemType Directory -Path $logsDir -Force | Out-Null
}

$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$out = Join-Path $logsDir "api_stdout_$stamp.log"
$err = Join-Path $logsDir "api_stderr_$stamp.log"

Write-Host "启动后端 API，日志写入：$logsDir"
Write-Host "stdout: $out"
Write-Host "stderr: $err"

Push-Location $apiDir
try {
    dotnet run --project . 2>&1 | Tee-Object -FilePath $out
    if ($LASTEXITCODE -ne 0) {
        Write-Host "后端退出，代码：$LASTEXITCODE"
    }
}
finally {
    Pop-Location
}