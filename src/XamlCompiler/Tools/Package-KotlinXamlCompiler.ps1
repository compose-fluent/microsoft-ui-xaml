param(
    [Parameter(Mandatory)] [string] $CompilerDirectory,
    [Parameter(Mandatory)] [ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+(?:-[a-zA-Z0-9.-]+)?$')] [string] $Version,
    [Parameter(Mandatory)] [string] $OutputDirectory
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$compiler = (Resolve-Path -LiteralPath $CompilerDirectory).Path
$output = [IO.Path]::GetFullPath($OutputDirectory)
$packageName = "kotlin-xamlc-$Version-win-x64"
$stage = Join-Path $output $packageName
if (Test-Path -LiteralPath $stage) { throw "Package staging directory already exists: $stage" }
if (!(Test-Path -LiteralPath (Join-Path $compiler 'XamlCompiler.exe'))) { throw 'XamlCompiler.exe is missing.' }
New-Item -ItemType Directory -Path $stage -Force | Out-Null
Get-ChildItem -LiteralPath $compiler -File | Where-Object {
    $_.Extension -in '.dll', '.exe', '.config'
} | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $stage }
Copy-Item -LiteralPath (Join-Path $repo 'LICENSE') -Destination $stage
$revision = (& git -C $repo rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot determine compiler source revision.' }
$files = [ordered]@{}
Get-ChildItem -LiteralPath $stage -File | Sort-Object Name | ForEach-Object {
    $files[$_.Name] = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
}
$manifest = [ordered]@{
    schemaVersion = 1
    protocolVersion = 2
    features = @('named-elements', 'events', 'compiled-bindings', 'templates', 'phased-bindings', 'deferred-elements')
    version = $Version
    host = 'win-x64'
    sourceRevision = $revision
    executable = 'XamlCompiler.exe'
    genXbf = 'Provided by the selected Microsoft.WindowsAppSDK.WinUI NuGet package'
    files = $files
}
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $stage 'kotlin-xamlc.json') -Encoding utf8NoBOM
$archive = Join-Path $output "$packageName.zip"
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $archive
$hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $packageName.zip" | Set-Content -LiteralPath "$archive.sha256" -Encoding ascii
Write-Output $archive
