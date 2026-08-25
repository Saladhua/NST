# 一键启动前后端：复用 start-api.ps1 / start-web.ps1，各自开独立窗口，日志仍写入 logs/
# 用法：powershell -File start-all.ps1（或直接双击 一键启动.bat）
$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$apiPort = 5080
$webPort = 5173

function Test-PortListening {
    param([int]$Port)
    return [bool](Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue)
}

if (Test-PortListening $apiPort) {
    Write-Host "后端 API 已在运行（端口 $apiPort），跳过启动"
} else {
    Start-Process powershell -WindowStyle Minimized -ArgumentList '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $root 'start-api.ps1')
    Write-Host "后端 API 启动中（端口 $apiPort）..."
}

if (Test-PortListening $webPort) {
    Write-Host "前端 Web 已在运行（端口 $webPort），跳过启动"
} else {
    Start-Process powershell -WindowStyle Minimized -ArgumentList '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $root 'start-web.ps1')
    Write-Host "前端 Web 启动中（端口 $webPort）..."
}

# 轮询等待两个端口就绪（最长 90 秒）
$apiOk = Test-PortListening $apiPort
$webOk = Test-PortListening $webPort
$deadline = (Get-Date).AddSeconds(90)
while (-not ($apiOk -and $webOk) -and (Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 2
    $apiOk = Test-PortListening $apiPort
    $webOk = Test-PortListening $webPort
}

Write-Host ""
if ($apiOk) { Write-Host "[成功] Swagger:   http://localhost:$apiPort/swagger" } else { Write-Host "[失败] 后端未就绪，请查看 logs/ 下最新 api 日志" }
if ($webOk) { Write-Host "[成功] 前端页面: http://localhost:$webPort" } else { Write-Host "[失败] 前端未就绪，请查看 logs/ 下最新 web 日志" }
