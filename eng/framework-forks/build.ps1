param(
    [Parameter(Mandatory = $true)]
    [string] $ForkRoot
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$forkRootPath = (Resolve-Path $ForkRoot).Path
$avaloniaRoot = Join-Path $forkRootPath 'Avalonia'
$sukiRoot = Join-Path $forkRootPath 'SukiUI'
$packageOutput = Join-Path $repositoryRoot '.nuget\local-packages'
$nugetConfig = Join-Path $repositoryRoot 'NuGet.Config'

if (-not (Test-Path -LiteralPath (Join-Path $avaloniaRoot '.git'))) {
    throw "Avalonia fork was not found: $avaloniaRoot"
}

if (-not (Test-Path -LiteralPath (Join-Path $sukiRoot '.git'))) {
    throw "SukiUI fork was not found: $sukiRoot"
}

New-Item -ItemType Directory -Force -Path $packageOutput | Out-Null

git -C $avaloniaRoot submodule update --init external/XamlX
if ($LASTEXITCODE -ne 0) {
    throw 'Failed to initialize the XamlX submodule.'
}

Push-Location $repositoryRoot
try {
    dotnet pack (Join-Path $avaloniaRoot 'src\Skia\Avalonia.Skia\Avalonia.Skia.csproj') `
        -c Release `
        -p:AtomicArtPackageVersion=12.0.6-atomicart.2 `
        -p:LangVersion=preview `
        -p:AvsCurrentTargetFramework=net8.0 `
        -p:AvsSkipBuildingLegacyTargetFrameworks=True `
        -o $packageOutput

    if ($LASTEXITCODE -ne 0) {
        throw 'Failed to build the Avalonia.Skia package.'
    }

    dotnet build (Join-Path $sukiRoot 'SukiUI\SukiUI.csproj') `
        -c Release `
        -p:AtomicArtPackageVersion=7.0.2.1-atomicart.3 `
        -p:PackageOutputPath=$packageOutput `
        --configfile $nugetConfig

    if ($LASTEXITCODE -ne 0) {
        throw 'Failed to build the SukiUI package.'
    }
}
finally {
    Pop-Location
}
