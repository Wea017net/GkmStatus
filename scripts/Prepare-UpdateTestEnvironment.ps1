param(
    [switch]$Launch
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$projectPath = Join-Path $repoRoot 'GkmStatus\GkmStatus.csproj'
$testRoot = Join-Path $env:TEMP 'GkmStatusUpdaterTest'
$runId = Get-Date -Format 'yyyyMMdd-HHmmss'
$runRoot = Join-Path $testRoot $runId
$beforeBuild = Join-Path $runRoot 'build-before'
$afterBuild = Join-Path $runRoot 'build-after'
$installDirectory = Join-Path $runRoot 'install'
$dataDirectory = Join-Path $runRoot 'profile'
$latestVersion = '99.0.0'
$packagePath = Join-Path $runRoot "GkmStatus_win-x64_v$latestVersion.zip"

New-Item -ItemType Directory -Path $runRoot, $beforeBuild, $afterBuild, $installDirectory, $dataDirectory -Force | Out-Null

function Publish-TestVersion([string]$version, [string]$outputPath) {
    & dotnet publish $projectPath -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:GkmStatusUpdaterTestMode=true "-p:Version=$version" `
        "-p:BaseOutputPath=$(Join-Path $runRoot 'build-output')\" -o $outputPath
    if ($LASTEXITCODE -ne 0) {
        throw "Publishing test version $version failed with exit code $LASTEXITCODE."
    }
}

Publish-TestVersion '0.1.0' $beforeBuild
Publish-TestVersion $latestVersion $afterBuild

$beforeFiles = @(Get-ChildItem -LiteralPath $beforeBuild -File)
$afterFiles = @(Get-ChildItem -LiteralPath $afterBuild -File)
if (-not ($beforeFiles.Name -contains 'GkmStatus.exe') -or -not ($afterFiles.Name -contains 'GkmStatus.exe')) {
    throw 'The test publish output does not contain GkmStatus.exe.'
}

Compress-Archive -Path (Join-Path $afterBuild '*') -DestinationPath $packagePath -CompressionLevel Optimal
Copy-Item -Path (Join-Path $beforeBuild '*') -Destination $installDirectory -Recurse -Force

$appPath = Join-Path $installDirectory 'GkmStatus.exe'
Write-Output "Test app: $appPath"
Write-Output "Isolated profile: $dataDirectory"
Write-Output "Local update package: $packagePath"
Write-Output 'Expected flow: version 0.1.0 -> UAC approval -> local download -> replace -> restart as 99.0.0.'

if ($Launch) {
    $env:GKMSTATUS_TEST_DATA_DIR = $dataDirectory
    $env:GKMSTATUS_TEST_ROOT = $runRoot
    $env:GKMSTATUS_TEST_PACKAGE = $packagePath
    $env:GKMSTATUS_TEST_LATEST_VERSION = $latestVersion
    Start-Process -FilePath $appPath -WorkingDirectory $installDirectory
}
