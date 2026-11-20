# 从根目录 更新日志.md 提取当前版本的发布说明。
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [ValidatePattern('^v?\d+(\.\d+)+$')] [string] $Version,
    [Parameter(Mandatory)] [string] $OutputPath,
    [string] $RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [switch] $FetchTag
)
$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }
$releaseVersion = $Version.TrimStart('v')
$tagName = "v$releaseVersion"
$root = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$outputFile = [IO.Path]::GetFullPath($OutputPath)

$body = ''
$tagRef = "refs/tags/$tagName"
if ($FetchTag) {
    $tagRef = "refs/kudo-release-tags/$tagName"
    git -C $root fetch --quiet origin "refs/tags/${tagName}:$tagRef"
    if ($LASTEXITCODE -ne 0) { throw "无法读取远端标签：$tagName" }
}
if (git -C $root show-ref --verify --quiet $tagRef) {
    $tagType = git -C $root cat-file -t $tagRef
    if ($tagType -eq 'tag') {
        $body = (git -C $root for-each-ref $tagRef '--format=%(contents)') -join "`n"
    }
}
if ([string]::IsNullOrWhiteSpace($body)) {
    $updateLog = Join-Path $root '更新日志.md'
    if (-not (Test-Path -LiteralPath $updateLog -PathType Leaf)) { throw "未找到 更新日志.md" }
    $lines = [Collections.Generic.List[string]]::new()
    $collecting = $false
    foreach ($line in Get-Content -LiteralPath $updateLog -Encoding utf8) {
        if ($line -match '^## ') {
            if ($collecting) { break }
            if ($line -match "^## $releaseVersion(\s|$)") { $collecting = $true; continue }
        }
        elseif ($collecting) { $lines.Add($line) }
    }
    if (-not $collecting) { throw "更新日志.md 中未找到 $releaseVersion 小节" }
    $body = ($lines -join "`n").Trim()
}
if ([string]::IsNullOrWhiteSpace($body)) { $body = "Kudo v$releaseVersion" }
Set-Content -LiteralPath $outputFile -Value $body -Encoding utf8
Write-Host "发布说明已写入：$outputFile"
