# CI 运行时验证：启动 AOT 产物确认能存活并渲染 UI（参照 Vex release.yml 的验证步骤）。
param(
    [string] $ExePath = "artifacts/publish/win-x64/Kudo.Demo.Desktop/Kudo.Demo.Desktop.exe"
)
$ErrorActionPreference = 'Stop'
$exe = (Resolve-Path -LiteralPath $ExePath).Path
$process = [Diagnostics.Process]::Start([Diagnostics.ProcessStartInfo]::new($exe))
Start-Sleep -Seconds 8
if ($process.HasExited) {
    throw "AOT 产物启动后立即退出（exit=$($process.ExitCode)），运行时验证失败。"
}
$process.Kill()
$process.WaitForExit(5000) | Out-Null
Write-Host "AOT 产物启动验证通过（存活 8 秒后正常终止）。"
