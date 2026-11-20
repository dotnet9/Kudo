# 校验发布目录：5 平台安装包 + 绿色版压缩包 + SHA-256 完整且格式正确。
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Version,
    [string] $PackageDirectory = ''
)
$ErrorActionPreference = 'Stop'
if (-not $PackageDirectory) { $PackageDirectory = Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts/release' }
$directory = (Resolve-Path -LiteralPath $PackageDirectory).Path
$files = @(Get-ChildItem -LiteralPath $directory -File)
$releaseVersion = $Version.Trim().TrimStart('v', 'V')
$expected = @{
    'win-x64' = "Kudo-v$releaseVersion-win-x64-setup.exe"
    'linux-x64' = "Kudo-v$releaseVersion-linux-x64.deb"
    'linux-arm64' = "Kudo-v$releaseVersion-linux-arm64.deb"
    'osx-x64' = "Kudo-v$releaseVersion-osx-x64.dmg"
    'osx-arm64' = "Kudo-v$releaseVersion-osx-arm64.dmg"
}
$archives = @{
    'win-x64' = "Kudo-v$releaseVersion-win-x64.zip"
    'linux-x64' = "Kudo-v$releaseVersion-linux-x64.tar.gz"
    'linux-arm64' = "Kudo-v$releaseVersion-linux-arm64.tar.gz"
    'osx-x64' = "Kudo-v$releaseVersion-osx-x64.zip"
    'osx-arm64' = "Kudo-v$releaseVersion-osx-arm64.zip"
}
$allowed = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($map in @($expected, $archives)) {
    foreach ($rid in $map.Keys) {
        $packages = @($files | Where-Object { $_.Name.Equals($map[$rid], [StringComparison]::Ordinal) })
        if ($packages.Count -ne 1) { throw "平台 $rid 必须有且只有一个对应版本的安装包，实际数量：$($packages.Count)" }
        $package = $packages[0]
        if ($package.Length -eq 0) { throw "安装包为空：$($package.Name)" }
        $checksumPath = $package.FullName + '.sha256'
        if (-not (Test-Path -LiteralPath $checksumPath)) { throw "缺少校验文件：$($package.Name).sha256" }
        $checksum = (Get-Content -LiteralPath $checksumPath -Raw).Trim()
        if ($checksum -notmatch ('^([a-fA-F0-9]{64})\s+\*?' + [regex]::Escape($package.Name) + '$')) {
            throw "校验文件格式或目标名称错误：$($package.Name)"
        }
        $expectedHash = $Matches[1]
        if ((Get-FileHash -LiteralPath $package.FullName -Algorithm SHA256).Hash -ne $expectedHash) {
            throw "安装包 SHA-256 不匹配：$($package.Name)"
        }
        [void] $allowed.Add($package.Name)
        [void] $allowed.Add($package.Name + '.sha256')
    }
}
$unexpected = @($files | Where-Object { -not $allowed.Contains($_.Name) })
if ($unexpected.Count -gt 0) { throw "发布目录只允许五平台安装包、绿色版压缩包与校验文件，发现额外文件：$($unexpected.Name -join ', ')" }
Write-Host '五平台安装包、绿色版压缩包、版本及 SHA-256 校验通过。'
