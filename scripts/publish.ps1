# 统一发布入口（CI 与本地共用）：scripts/publish.ps1
# 用法：pwsh scripts/publish.ps1 -RuntimeIdentifier win-x64 [-Version 0.0.0.1]
# 输出：artifacts/publish/<rid>/Kudo.Demo.Desktop/
# 平台策略：win-x64 走 NativeAOT（已验证），Linux/macOS 走 self-contained 单文件。
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("win-x64", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64")]
    [string] $RuntimeIdentifier,

    [string] $Version = ""
)

$ErrorActionPreference = "Stop"
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path

if ([string]::IsNullOrWhiteSpace($Version)) {
    $props = Join-Path $repositoryRoot "Directory.Build.props"
    $match = Select-String -LiteralPath $props -Pattern '<Version>([^<]+)</Version>' | Select-Object -First 1
    if ($match) { $Version = $match.Matches[0].Groups[1].Value }
}
if ([string]::IsNullOrWhiteSpace($Version)) { throw "无法从 Directory.Build.props 解析版本号，且未传入 -Version。" }
Write-Host "发布 Kudo.Demo.Desktop $RuntimeIdentifier (Version=$Version)"

$projectPath = Join-Path $repositoryRoot "samples\Kudo.Demo.Desktop\Kudo.Demo.Desktop.csproj"
$publishRoot = Join-Path $repositoryRoot "artifacts\publish"
$targetFramework = if ($RuntimeIdentifier -eq "win-x64") { "net11.0-windows" } else { "net11.0" }
$outputPath = Join-Path $publishRoot "$RuntimeIdentifier\Kudo.Demo.Desktop"

$restoreArguments = @(
    "restore", $projectPath,
    "-p:Configuration=Release",
    "-p:RuntimeIdentifier=$RuntimeIdentifier",
    "-p:SelfContained=true",
    "-p:PublishAot=true",
    "-p:PublishTrimmed=true",
    "-p:Version=$Version"
)
Write-Host "还原 $RuntimeIdentifier..."
& dotnet @restoreArguments
if ($LASTEXITCODE -ne 0) { throw "restore failed for $RuntimeIdentifier" }

$publishArguments = @(
    "publish", $projectPath,
    "-c", "Release",
    "-f", $targetFramework,
    "-r", $RuntimeIdentifier,
    "--self-contained", "true",
    "-p:Version=$Version",
    "-p:GeneratePackageOnBuild=false",
    "-p:GenerateDocumentationFile=false",
    "-o", $outputPath
)

if ($RuntimeIdentifier -eq "win-x64") {
    # Windows：NativeAOT（本地与 CI 已验证）。
    $publishArguments += @(
        "-p:PublishAot=true",
        "-p:PublishTrimmed=true",
        "-p:StripSymbols=true",
        "-p:IlcSingleThreaded=true",
        "-p:TreatWarningsAsErrors=false",
        "-p:ILLinkTreatWarningsAsErrors=false"
    )
}
else {
    # Linux/macOS：self-contained 单文件（跨平台单文件发布，AOT 留待各平台原生工具链验证）。
    $publishArguments += @(
        "-p:PublishAot=false",
        "-p:PublishSingleFile=true",
        "-p:PublishTrimmed=false",
        "-p:IncludeNativeLibrariesForSelfExtract=true"
    )
}

Write-Host "发布 $RuntimeIdentifier..."
& dotnet @publishArguments
if ($LASTEXITCODE -ne 0) { throw "publish failed for $RuntimeIdentifier" }

# 产物检查
$expectedExecutable = if ($RuntimeIdentifier -eq "win-x64") { "Kudo.Demo.Desktop.exe" } else { "Kudo.Demo.Desktop" }
$executablePath = Join-Path $outputPath $expectedExecutable
if (-not (Test-Path -LiteralPath $executablePath -PathType Leaf)) {
    throw "可执行文件未生成：$executablePath"
}
if ($RuntimeIdentifier -ne "win-x64") {
    & chmod 755 $executablePath
}
Write-Host "发布完成：$executablePath"
