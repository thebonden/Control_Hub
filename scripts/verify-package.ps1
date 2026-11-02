param([Parameter(Mandatory = $true)][string]$PackageDirectory)
$ErrorActionPreference = 'Stop'
$package = [IO.Path]::GetFullPath($PackageDirectory)
$payload = Join-Path $package 'payload'
$resultDirectory = Join-Path $package ('verification-' + [Guid]::NewGuid().ToString('N'))
foreach ($relative in @('ControlHub.exe','host\ControlHub.Host.exe','docs\installation.da.html','docs\installation.en.html','Plugins\StreamDeck\dk.thebonden.controlhub.streamDeckPlugin')) {
    if (-not (Test-Path -LiteralPath (Join-Path $payload $relative) -PathType Leaf)) { throw "Pakkefil mangler / Missing package file: $relative" }
}
foreach ($relative in @('ControlHub.runtimeconfig.json','host\ControlHub.Host.runtimeconfig.json')) {
    $runtime = Get-Content -LiteralPath (Join-Path $payload $relative) -Raw | ConvertFrom-Json
    if ($runtime.runtimeOptions.framework -or $runtime.runtimeOptions.frameworks -or -not $runtime.runtimeOptions.includedFrameworks) {
        throw 'Pakken skal inkludere .NET / Package must include .NET'
    }
}
$test = Start-Process -FilePath (Join-Path $payload 'ControlHub.exe') -ArgumentList @('--self-test', ('"' + $resultDirectory + '"')) -WindowStyle Hidden -PassThru
if (-not $test.WaitForExit(90000)) { throw 'Pakketest overskred tidsgrænsen / Package test timed out' }
$test.Refresh()
if ($test.ExitCode -ne 0) { throw "Pakketest fejlede / Package test failed: $resultDirectory" }
$result = Get-Content -LiteralPath (Join-Path $resultDirectory 'result.json') -Raw | ConvertFrom-Json
if (-not $result.passed) { throw 'Pakketest fejlede / Package test failed' }
$result.checks
Write-Output 'Pakketest bestået / Package test passed'
