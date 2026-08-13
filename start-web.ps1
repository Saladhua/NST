# 启动前端 Vite 开发服务器，日志统一写入 logs/ 目录
# 用法：powershell -File start-web.ps1
$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$logsDir = Join-Path $root "logs"
$webDir = Join-Path $root "src\OrderPlatform.Web"

if (-not (Test-Path -LiteralPath $logsDir)) {
    New-Item -ItemType Directory -Path $logsDir -Force | Out-Null
}

$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$out = Join-Path $logsDir "web_stdout_$stamp.log"
$err = Join-Path $logsDir "web_stderr_$stamp.log"

Write-Host "启动前端 Vite，日志写入：$logsDir"
Write-Host "stdout: $out"
Write-Host "stderr: $err"

Push-Location $webDir
try {
    npm run dev 2>&1 | Tee-Object -FilePath $out
    if ($LASTEXITCODE -ne 0) {
        Write-Host "前端退出，代码：$LASTEXITCODE"
    }
}
finally {
    Pop-Location
}