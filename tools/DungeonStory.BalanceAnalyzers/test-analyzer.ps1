param(
    [string]$UnityEditorData = 'C:\Program Files\Unity\Hub\Editor\6000.3.8f1\Editor\Data'
)

$ErrorActionPreference = 'Stop'
$python = (Get-Command python -ErrorAction Stop).Source
$script = Join-Path $PSScriptRoot 'verify_analyzer.py'
$unityDotnet = Join-Path $UnityEditorData 'NetCoreRuntime\dotnet.exe'
$dotnet = if (Test-Path -LiteralPath $unityDotnet) {
    $unityDotnet
} else {
    (Get-Command dotnet -ErrorAction Stop).Source
}

& $python $script --dotnet $dotnet
if ($LASTEXITCODE -ne 0) {
    throw "Pinned analyzer verification failed with exit code $LASTEXITCODE."
}
