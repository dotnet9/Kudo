param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("win-x64", "linux-x64", "linux-arm64")]
    [string] $RuntimeIdentifier,

    [ValidateSet("auto", "net8.0", "net10.0", "net11.0")]
    [string] $TargetFramework = "auto"
)

$ErrorActionPreference = "Stop"

$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
$projectPath = Join-Path $repositoryRoot "samples\Kudo.Demo.Desktop\Kudo.Demo.Desktop.csproj"
$publishRoot = Join-Path $repositoryRoot "artifacts\publish"
$targetFrameworkBase = $TargetFramework
if ($targetFrameworkBase -eq "auto") {
    $sdkVersion = (& dotnet --version 2>$null | Select-Object -First 1)
    $targetFrameworkBase = if ($sdkVersion -match "^11\.") {
        "net11.0"
    }
    elseif ($sdkVersion -match "^10\.") {
        "net10.0"
    }
    else {
        "net8.0"
    }
}
$resolvedTargetFramework = if ($RuntimeIdentifier -eq "win-x64") { "$targetFrameworkBase-windows" } else { $targetFrameworkBase }
$outputPath = Join-Path $publishRoot "$RuntimeIdentifier\$resolvedTargetFramework\Kudo.Demo.Desktop"
$resolvedPublishRoot = [IO.Path]::GetFullPath($publishRoot)
$resolvedOutputPath = [IO.Path]::GetFullPath($outputPath)

if (-not $resolvedOutputPath.StartsWith($resolvedPublishRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to replace unexpected publish path: $resolvedOutputPath"
}

$restoreArguments = @(
    "restore",
    $projectPath,
    "--force",
    "-p:Configuration=Release",
    "-p:RuntimeIdentifier=$RuntimeIdentifier",
    "-p:SelfContained=true",
    "-p:PublishAot=$($RuntimeIdentifier -eq 'win-x64')",
    "-p:PublishTrimmed=$($RuntimeIdentifier -eq 'win-x64')",
    "-p:KudoValidationTargetFramework=$targetFrameworkBase",
    "-p:KudoValidationWindowsTargetFramework=$($targetFrameworkBase)-windows"
)
Write-Host "Restoring $resolvedTargetFramework for $RuntimeIdentifier..."
& dotnet @restoreArguments
if ($LASTEXITCODE -ne 0) {
    throw "Kudo.Demo.Desktop restore failed for $RuntimeIdentifier with exit code $LASTEXITCODE."
}

$cleanArguments = @(
    "clean",
    $projectPath,
    "-c", "Release",
    "-f", $resolvedTargetFramework,
    "-r", $RuntimeIdentifier,
    "-p:KudoValidationTargetFramework=$targetFrameworkBase",
    "-p:KudoValidationWindowsTargetFramework=$($targetFrameworkBase)-windows"
)
Write-Host "Cleaning restored build outputs for $resolvedTargetFramework and $RuntimeIdentifier..."
& dotnet @cleanArguments
if ($LASTEXITCODE -ne 0) {
    throw "Kudo.Demo.Desktop clean failed for $RuntimeIdentifier with exit code $LASTEXITCODE."
}

if (Test-Path -LiteralPath $resolvedOutputPath) {
    Remove-Item -LiteralPath $resolvedOutputPath -Recurse -Force
}

$publishArguments = @(
    "publish",
    $projectPath,
    "-c", "Release",
    "-f", $resolvedTargetFramework,
    "-r", $RuntimeIdentifier,
    "-p:KudoValidationTargetFramework=$targetFrameworkBase",
    "-p:KudoValidationWindowsTargetFramework=$($targetFrameworkBase)-windows",
    "--self-contained", "true",
    "-p:GeneratePackageOnBuild=false",
    "-p:GenerateDocumentationFile=false",
    "-p:DebugType=None",
    "-p:DebugSymbols=false",
    "-o", $resolvedOutputPath,
    "--no-restore"
)

if ($RuntimeIdentifier -eq "win-x64") {
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
    $publishArguments += @(
        "-p:PublishAot=false",
        "-p:PublishSingleFile=true",
        "-p:PublishTrimmed=false"
    )
}

Write-Host "Publishing Kudo.Demo.Desktop for $RuntimeIdentifier..."
& dotnet @publishArguments
if ($LASTEXITCODE -ne 0) {
    throw "Kudo.Demo.Desktop publish failed for $RuntimeIdentifier with exit code $LASTEXITCODE."
}

if ($RuntimeIdentifier -eq "win-x64") {
    $executablePath = Join-Path $resolvedOutputPath "Kudo.Demo.Desktop.exe"
    if (-not (Test-Path -LiteralPath $executablePath -PathType Leaf)) {
        throw "Windows Native AOT executable was not produced: $executablePath"
    }
}

Get-ChildItem -LiteralPath $resolvedOutputPath -Recurse -File -Filter "*.pdb" -ErrorAction SilentlyContinue |
    Remove-Item -Force

Write-Host "Published Kudo.Demo.Desktop to $resolvedOutputPath"
