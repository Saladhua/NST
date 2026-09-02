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
    # 用 .NET 直接启动 npm 并重定向输出：文件按 UTF-8 写入，避免 PowerShell 管道按 GBK 转码导致乱码
    # npm 在 Windows 上是 npm.cmd，必须经 cmd.exe 启动
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = (Join-Path $env:SystemRoot 'System32\cmd.exe')
    $psi.Arguments = "/c npm run dev"
    $psi.WorkingDirectory = $webDir
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
    Write-Host "前端退出，代码：$($proc.ExitCode)"
}
finally {
    Pop-Location
}