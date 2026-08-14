[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$OutputRoot = 'mcsl-future-win-x64-aot',

    [string[]]$Languages = @('en-US', 'zh-CN'),

    [switch]$AllLanguages
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$knownCultures = @('en-US', 'ja-JP', 'ru-RU', 'zh-CN', 'zh-HK', 'zh-TW')

if ($AllLanguages) {
    $Languages = $knownCultures
}

$invalidLanguages = @($Languages | Where-Object { $_ -notin $knownCultures })
if ($invalidLanguages.Count -gt 0) {
    throw "Unknown language culture(s): $($invalidLanguages -join ', '). Valid values: $($knownCultures -join ', ')."
}

if ($Languages.Count -eq 0) {
    throw 'At least one language culture must be selected.'
}

if ([IO.Path]::IsPathRooted($OutputRoot)) {
    $packageRoot = [IO.Path]::GetFullPath($OutputRoot)
}
else {
    $packageRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot $OutputRoot))
}

if ($packageRoot -eq $repoRoot) {
    throw 'OutputRoot must be a package directory below the repository root.'
}

$tempRoot = Join-Path $env:TEMP ("mcsl-future-win-x64-aot-{0}" -f [Guid]::NewGuid().ToString('N'))
$daemonStage = Join-Path $tempRoot 'daemon'
$daemonClientStage = Join-Path $tempRoot 'daemon-client'
$winUiStage = Join-Path $tempRoot 'winui'

function Invoke-NativeAotPublish {
    param(
        [Parameter(Mandatory)]
        [string]$Project,

        [Parameter(Mandatory)]
        [string]$PublishDirectory,

        [switch]$WinUi
    )

    $arguments = @(
        'publish',
        $Project,
        '-c', $Configuration,
        '-r', 'win-x64',
        '--self-contained', 'true',
        '-p:PublishAot=true',
        '-p:PublishSingleFile=true',
        '-p:PublishTrimmed=true',
        '-p:PublishReadyToRun=false',
        '-p:StripSymbols=true',
        '-p:DebugType=None',
        '-p:JsonSerializerIsReflectionEnabledByDefault=false',
        '-o', $PublishDirectory,
        '/m:1'
    )

    if ($WinUi) {
        $arguments += '-p:Platform=x64'
    }

    Write-Host "Publishing $Project (Native AOT, win-x64)..."
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for $Project with exit code $LASTEXITCODE."
    }
}

function Copy-RequiredFile {
    param(
        [Parameter(Mandatory)]
        [string]$SourceDirectory,

        [Parameter(Mandatory)]
        [string]$DestinationDirectory,

        [Parameter(Mandatory)]
        [string]$Name
    )

    $source = Join-Path $SourceDirectory $Name
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
        throw "Required publish file is missing: $source"
    }

    Copy-Item -LiteralPath $source -Destination $DestinationDirectory -Force
}

try {
    New-Item -ItemType Directory -Force -Path $daemonStage, $daemonClientStage, $winUiStage | Out-Null

    Invoke-NativeAotPublish `
        -Project (Join-Path $repoRoot 'src\MCServerLauncher.Daemon\MCServerLauncher.Daemon.csproj') `
        -PublishDirectory $daemonStage
    Invoke-NativeAotPublish `
        -Project (Join-Path $repoRoot 'src\MCServerLauncher.DaemonClient\MCServerLauncher.DaemonClient.csproj') `
        -PublishDirectory $daemonClientStage
    Invoke-NativeAotPublish `
        -Project (Join-Path $repoRoot 'src\MCServerLauncher.WinUI\MCServerLauncher.WinUI.csproj') `
        -PublishDirectory $winUiStage `
        -WinUi

    if (Test-Path -LiteralPath $packageRoot) {
        Remove-Item -LiteralPath $packageRoot -Recurse -Force
    }

    $daemonRoot = Join-Path $packageRoot 'daemon'
    $winUiRoot = Join-Path $packageRoot 'winui'
    New-Item -ItemType Directory -Force -Path $daemonRoot, $winUiRoot | Out-Null

    # The daemon may need native helper DLLs, so keep its complete AOT output.
    Copy-Item -Path (Join-Path $daemonStage '*') -Destination $daemonRoot -Recurse -Force

    # Keep only files required by the unpackaged WinUIIslands application.
    $winUiFiles = @(
        'MCServerLauncher.WinUI.exe',
        'Microsoft.UI.Xaml.dll',
        'Microsoft.Web.WebView2.Core.dll',
        'WebView2Loader.dll',
        'WinUIEditor.dll',
        'WinUIEditor.pri',
        'resources.pri'
    )
    foreach ($file in $winUiFiles) {
        Copy-RequiredFile -SourceDirectory $winUiStage -DestinationDirectory $winUiRoot -Name $file
    }

    Copy-RequiredFile -SourceDirectory $daemonClientStage -DestinationDirectory $winUiRoot -Name 'MCServerLauncher.DaemonClient.exe'
    Copy-Item -LiteralPath (Join-Path $winUiStage 'Resources') -Destination $winUiRoot -Recurse -Force

    foreach ($language in $Languages) {
        $languageDirectory = Join-Path $winUiStage $language
        if (-not (Test-Path -LiteralPath $languageDirectory -PathType Container)) {
            throw "Selected language resource directory is missing: $languageDirectory"
        }

        Copy-Item -LiteralPath $languageDirectory -Destination $winUiRoot -Recurse -Force
    }

    Write-Host "Native AOT package created: $packageRoot"
    Write-Host "Languages: $($Languages -join ', ')"
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
