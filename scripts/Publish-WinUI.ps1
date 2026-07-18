param(
    [ValidateSet('x64', 'ARM64')]
    [string[]] $Architecture = @('x64', 'ARM64'),
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release'
)

$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\src\MCServerLauncher.WinUI\MCServerLauncher.WinUI.csproj'))
$projectDirectory = Split-Path $project
$binDirectory = [IO.Path]::GetFullPath((Join-Path $projectDirectory 'bin'))
$vsWhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild = $null
if (Test-Path -LiteralPath $vsWhere) {
    $msbuild = & $vsWhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\Current\Bin\MSBuild.exe' | Select-Object -First 1
}
if ([string]::IsNullOrWhiteSpace($msbuild)) {
    $msbuild = (Get-Command msbuild.exe -ErrorAction SilentlyContinue).Source
}
if ([string]::IsNullOrWhiteSpace($msbuild) -or -not (Test-Path -LiteralPath $msbuild)) {
    throw 'Visual Studio MSBuild is required for CoreIsland XAML compilation.'
}

foreach ($architectureName in $Architecture) {
    $rid = "win-$($architectureName.ToLowerInvariant())"
    $publishDirectory = [IO.Path]::GetFullPath((Join-Path $projectDirectory "bin\$architectureName\$Configuration\net10.0-windows10.0.26100.0\$rid\publish"))
    $binDirectoryPrefix = $binDirectory.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $publishDirectory.StartsWith($binDirectoryPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Unsafe WinUI publish directory: $publishDirectory"
    }
    if (Test-Path -LiteralPath $publishDirectory) {
        Remove-Item -LiteralPath $publishDirectory -Recurse -Force
    }

    & $msbuild $project /t:Restore /p:Configuration=$Configuration /p:Platform=$architectureName /p:RuntimeIdentifier=$rid /m:1 /v:minimal
    if ($LASTEXITCODE -ne 0) { throw "WinUI restore failed for $rid" }
    & $msbuild $project /t:Publish /p:Configuration=$Configuration /p:Platform=$architectureName /p:RuntimeIdentifier=$rid /p:SelfContained=true /p:CoreIslandPackaging=false /p:WindowsPackageType=None /p:PublishSingleFile=false /p:PublishTrimmed=false /m:1 /v:minimal
    if ($LASTEXITCODE -ne 0) { throw "WinUI publish failed for $rid" }

    foreach ($required in @('CoreIsland.dll', 'resources.pri', 'Microsoft.UI.Xaml.dll', 'Microsoft.UI.Xaml.pri', 'WinUIEditor.dll', 'WinUIEditor.pri', 'MCServerLauncher.WinUI.exe')) {
        if (-not (Test-Path -LiteralPath (Join-Path $publishDirectory $required))) {
            throw "Required CoreIsland/WinUIEdit runtime file is missing: $required"
        }
    }
    if (Test-Path -LiteralPath (Join-Path $publishDirectory 'Package.appxmanifest')) {
        throw 'MSIX package manifest must not be published.'
    }
    Write-Host "Verified unpackaged self-contained publish: $publishDirectory"
}
