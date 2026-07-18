param(
    [Parameter(Mandatory)]
    [ValidateSet('x64', 'ARM64')]
    [string] $Platform,
    [Parameter(Mandatory)]
    [string] $PackageRoot,
    [Parameter(Mandatory)]
    [string] $Destination,
    # Some localized MSBuild Exec hosts forward their command-file marker on stdin.
    [Parameter(ValueFromPipeline)]
    [object] $IgnoredInput
)

$ErrorActionPreference = 'Stop'

$PackageRoot = $PackageRoot.Trim().Trim([char]0xFEFF).Trim('"')
$Destination = $Destination.Trim().Trim([char]0xFEFF).Trim('"')
$packageRootPath = [IO.Path]::GetFullPath($PackageRoot)
$destinationPath = [IO.Path]::GetFullPath($Destination)
$appxPath = Join-Path $packageRootPath "tools\AppX\$Platform\Release\Microsoft.UI.Xaml.2.8.appx"
if (-not (Test-Path -LiteralPath $appxPath)) {
    throw "WinUI 2 runtime package was not found: $appxPath"
}

[IO.Directory]::CreateDirectory($destinationPath) | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem

$archive = [IO.Compression.ZipFile]::OpenRead($appxPath)
try {
    foreach ($entryName in @('Microsoft.UI.Xaml.dll', 'resources.pri')) {
        $entry = $archive.GetEntry($entryName)
        if ($null -eq $entry) {
            throw "Required WinUI 2 runtime entry is missing: $entryName"
        }

        $destinationName = if ($entryName -eq 'resources.pri') { 'Microsoft.UI.Xaml.pri' } else { $entryName }
        $destinationFile = Join-Path $destinationPath $destinationName
        $input = $entry.Open()
        $output = [IO.File]::Create($destinationFile)
        try {
            $input.CopyTo($output)
        }
        finally {
            $output.Dispose()
            $input.Dispose()
        }
    }
}
finally {
    $archive.Dispose()
}
