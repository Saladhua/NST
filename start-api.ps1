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
    # 用 .NET 直接启动 dotnet 并重定向输出：文件按 UTF-8 写入，避免 PowerShell 管道按 GBK 转码导致乱码
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = "dotnet"
    $psi.Arguments = "run --project ."
    $psi.WorkingDirectory = $apiDir
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.StandardOutputEncoding = [System.Text.Encoding]::UTF8
    $psi.StandardErrorEncoding = [System.Text.Encoding]::UTF8

    $utf8 = New-Object System.Text.UTF8Encoding($true)
    $proc = [System.Diagnostics.Process]::Start($psi)
    $outTask = $proc.StandardOutput.ReadToEndAsync()
    $errTask = $proc.StandardError.ReadToEndAsync()
    $proc.WaitForExit()
    $outText = $outTask.GetAwaiter().GetResult()
    $errText = $errTask.GetAwaiter().GetResult()

    [System.IO.File]::AppendAllText($out, $outText, $utf8)
    if ($errText) {
        [System.IO.File]::AppendAllText($err, $errText, $utf8)
        $host.UI.WriteErrorLine($errText)
    }
    Write-Host "后端退出，代码：$($proc.ExitCode)"
}
finally {
    Pop-Location
}