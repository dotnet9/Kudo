# 升级 Directory.Build.props 的版本号（同步更新更日志小节由发布流程自动提取）。
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [ValidatePattern('^\d+(\.\d+){1,3}$')] [string] $Version
)
$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }
$root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
$propsPath = Join-Path $root 'Directory.Build.props'
$content = Get-Content -LiteralPath $propsPath -Raw -Encoding utf8
$updated = [regex]::Replace($content, '(<Version>)[^<]+(</Version>)', "`$1$Version`$2")
Set-Content -LiteralPath $propsPath -Value $updated -Encoding utf8
Write-Host "Directory.Build.props 版本已升级为 $Version"
