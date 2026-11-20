# 打包 Kudo Linux deb 安装包 + sha256。
# 用法：pwsh scripts/package_linux_deb.ps1 -RuntimeIdentifier linux-x64 -Version 0.0.0.1
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("linux-x64", "linux-arm64")]
    [string] $RuntimeIdentifier,

    [Parameter(Mandatory = $true)]
    [string] $Version,

    [string] $SourceDirectory = "",
    [string] $OutputDirectory = "",
    [switch] $Force
)

$ErrorActionPreference = "Stop"
$scriptRoot = (Resolve-Path -LiteralPath $PSScriptRoot).Path
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $scriptRoot "..")).Path

if ([string]::IsNullOrWhiteSpace($SourceDirectory)) {
    $SourceDirectory = Join-Path $repositoryRoot "artifacts/publish/$RuntimeIdentifier/Kudo.Demo.Desktop"
}
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repositoryRoot "artifacts/release"
}

$sourcePath = (Resolve-Path -LiteralPath $SourceDirectory).Path
$outputPath = [System.IO.Path]::GetFullPath($OutputDirectory)
$cleanVersion = $Version.Trim().TrimStart('v', 'V').Split('+')[0]
if ($cleanVersion -notmatch '^\d+(\.\d+){1,3}$') {
    throw "版本号必须为数字格式（例如 0.0.0.1）：$Version"
}

$architecture = if ($RuntimeIdentifier -eq "linux-arm64") { "arm64" } else { "amd64" }
$packageName = "Kudo-v$cleanVersion-$RuntimeIdentifier.deb"
$packagePath = Join-Path $outputPath $packageName
$stageRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("Kudo-deb-" + [Guid]::NewGuid().ToString("N"))
$appPath = Join-Path $stageRoot "usr/lib/kudo"
$binPath = Join-Path $stageRoot "usr/bin"
$desktopPath = Join-Path $stageRoot "usr/share/applications"
$iconPath = Join-Path $stageRoot "usr/share/icons/hicolor/256x256/apps"
$controlPath = Join-Path $stageRoot "DEBIAN"

if ((Test-Path -LiteralPath $packagePath) -and -not $Force) {
    throw "安装包已存在：$packagePath（使用 -Force 覆盖）"
}
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
New-Item -ItemType Directory -Path $appPath, $binPath, $desktopPath, $iconPath, $controlPath -Force | Out-Null

try {
    Copy-Item -LiteralPath (Join-Path $repositoryRoot "logo.png") -Destination (Join-Path $iconPath "kudo.png")

    Get-ChildItem -LiteralPath $sourcePath -Recurse -File -Force | ForEach-Object {
        $relative = [IO.Path]::GetRelativePath($sourcePath, $_.FullName)
        if ($_.Extension -in '.pdb', '.dbg') { return }
        $destination = Join-Path $appPath $relative
        New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
        Copy-Item -LiteralPath $_.FullName -Destination $destination -Force
    }

    $executable = Join-Path $appPath "Kudo.Demo.Desktop"
    if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
        throw "发布可执行文件未生成：$executable"
    }
    & chmod 755 $executable
    if ($LASTEXITCODE -ne 0) { throw '无法设置程序启动权限。' }

    [IO.File]::WriteAllText((Join-Path $binPath 'kudo'), "#!/bin/sh`n" + 'exec /usr/lib/kudo/Kudo.Demo.Desktop "$@"' + "`n")
    & chmod 755 (Join-Path $binPath 'kudo')
    if ($LASTEXITCODE -ne 0) { throw '无法设置启动入口权限。' }

    @(
        "[Desktop Entry]",
        "Type=Application",
        "Name=Kudo",
        "Comment=Kudo node modeling workbench",
        "Exec=kudo",
        "Icon=kudo",
        "Terminal=false",
        "Categories=Development;IDE;"
    ) | Set-Content -LiteralPath (Join-Path $desktopPath "kudo.desktop") -Encoding ascii

    $installedSize = [Math]::Ceiling(((Get-ChildItem -LiteralPath (Join-Path $stageRoot 'usr') -Recurse -File | Measure-Object Length -Sum).Sum) / 1KB)
    @(
        "Package: kudo",
        "Version: $cleanVersion",
        "Section: devel",
        "Priority: optional",
        "Architecture: $architecture",
        "Installed-Size: $installedSize",
        "Maintainer: Dotnet9 <dotnet9.com@outlook.com>",
        "Depends: libc6, libx11-6, libxrandr2, libxrender1, libxi6, libfontconfig1, libfreetype6, libglib2.0-0",
        "Description: Kudo node modeling workbench",
        " A cross-platform node modeling and debugging desktop application built with Avalonia."
    ) | Set-Content -LiteralPath (Join-Path $controlPath "control") -Encoding ascii

    if ($IsLinux) {
        & chmod 644 (Join-Path $controlPath 'control')
        if ($LASTEXITCODE -ne 0) { throw '无法设置 DEB 元数据权限。' }
    }
    if (Test-Path -LiteralPath $packagePath) {
        Remove-Item -LiteralPath $packagePath -Force
    }
    & dpkg-deb --build --root-owner-group $stageRoot $packagePath
    if ($LASTEXITCODE -ne 0) {
        throw "dpkg-deb failed with exit code $LASTEXITCODE."
    }

    if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf)) { throw "安装包未生成：$packagePath" }
    $sha = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
    "$sha  $packageName" | Set-Content -LiteralPath "$packagePath.sha256" -Encoding ascii
    Write-Host "Package: $packagePath"
}
finally {
    if (Test-Path -LiteralPath $stageRoot) {
        Remove-Item -LiteralPath $stageRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
