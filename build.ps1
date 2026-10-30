param(
    [string]$GamePath = 'E:\Games\Vintagestory',
    [string]$DotnetPath = 'dotnet'
)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.build\dotnet-home'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:MSBuildEnableWorkloadResolver = 'false'
foreach ($relativeFile in @('VintagestoryAPI.dll', 'Mods\VSSurvivalMod.dll', 'Lib\OpenTK.Graphics.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $GamePath $relativeFile))) {
        throw "Missing game assembly: $relativeFile. Set -GamePath to your Vintage Story 1.22.7 installation."
    }
}
$project = Join-Path $projectRoot 'src\ChiselGlassVisibility\ChiselGlassVisibility.csproj'
& $DotnetPath build $project -c Release "-p:VintageStoryPath=$GamePath" "-p:RestoreConfigFile=$(Join-Path $projectRoot 'NuGet.Config')" --verbosity minimal
if ($LASTEXITCODE -ne 0) { throw 'Mod build failed. A .NET 10 SDK is required.' }
& $DotnetPath run --project (Join-Path $projectRoot 'tests\GeometryTests\GeometryTests.csproj') -c Release --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw 'Automated checks failed.' }
$dist = Join-Path $projectRoot 'dist'
$stage = Join-Path $dist ('package-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $stage | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'src\ChiselGlassVisibility\bin\Release\net10.0\ChiselGlassVisibility.dll') -Destination $stage
Copy-Item -LiteralPath (Join-Path $projectRoot 'src\ChiselGlassVisibility\modinfo.json') -Destination $stage
Copy-Item -LiteralPath (Join-Path $projectRoot 'src\ChiselGlassVisibility\assets') -Destination $stage -Recurse
$zip = Join-Path $dist 'chiselglassvisibility-1.0.0.zip'
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -Force
Write-Host "Created $zip"
