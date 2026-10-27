param(
    [Parameter(Mandatory = $true)][string]$IsccPath,
    [string]$OutputDirectory,
    [switch]$UsePnpm
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $root ('artifacts\windows-' + [Guid]::NewGuid().ToString('N')) }
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Brug en ny outputmappe / Use a new output directory' }
if (-not (Test-Path -LiteralPath $IsccPath -PathType Leaf)) { throw 'ISCC.exe mangler / ISCC.exe missing' }
if ((Get-Content (Join-Path $root 'VERSION') -Raw).Trim() -ne '0.0.001') { throw 'Versionsgodkendelse kræves / Version approval required' }
$payload = Join-Path $output 'payload'
$files = Join-Path $output 'files'
$pluginOutput = Join-Path $payload 'Plugins\StreamDeck'
New-Item -ItemType Directory -Path $payload,$files,$pluginOutput -Force | Out-Null
Push-Location $root
try {
    & dotnet publish src/ControlHub.Host/ControlHub.Host.csproj -c Release -r win-x64 --self-contained true -o (Join-Path $payload 'host') -m:1 -nr:false
    if ($LASTEXITCODE -ne 0) { throw 'Host publish failed / Host-pakning mislykkedes' }
    & dotnet publish src/ControlHub.Desktop/ControlHub.Desktop.csproj -c Release -r win-x64 --self-contained true -o $payload -m:1 -nr:false
    if ($LASTEXITCODE -ne 0) { throw 'Desktop publish failed / Desktop-pakning mislykkedes' }
    # CLI may normalize manifest whitespace; package a copy to keep source unchanged.
    # CLI kan normalisere manifestet; pak en kopi, så kilden ikke ændres.
    $plugin = Join-Path $output 'source\Plugins\StreamDeck\dk.thebonden.controlhub.sdPlugin'
    New-Item -ItemType Directory -Path (Split-Path $plugin -Parent) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $root 'Plugins/StreamDeck/dk.thebonden.controlhub.sdPlugin') -Destination $plugin -Recurse
    if ($UsePnpm) {
        & pnpm --package=@elgato/cli@1.10.3 dlx streamdeck pack --no-update-check --no-file-list $plugin --output $pluginOutput
    } else {
        & npx --yes --package=@elgato/cli@1.10.3 streamdeck pack --no-update-check --no-file-list $plugin --output $pluginOutput
    }
    if ($LASTEXITCODE -ne 0) { throw 'Stream Deck-pakning mislykkedes / Stream Deck packaging failed' }
    Copy-Item -LiteralPath (Join-Path $root 'docs') -Destination (Join-Path $payload 'docs') -Recurse
    Copy-Item -LiteralPath (Join-Path $root 'README.md'),(Join-Path $root 'README.en.md'),(Join-Path $root 'VERSION') -Destination $payload
    Copy-Item -LiteralPath (Join-Path $pluginOutput 'dk.thebonden.controlhub.streamDeckPlugin') -Destination $files
    & $IsccPath ("/DPayloadDir=$payload") ("/DOutputDir=$files") (Join-Path $root 'packaging/windows/ControlHub.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Installer-build mislykkedes / Installer build failed' }
    Get-ChildItem -LiteralPath $files -File | Sort-Object Name | ForEach-Object {
        '{0}  {1}' -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name
    } | Set-Content -LiteralPath (Join-Path $files 'SHA256SUMS.txt') -Encoding ascii
    Write-Output "Installationsfiler / Installation files: $files"
} finally { Pop-Location }
