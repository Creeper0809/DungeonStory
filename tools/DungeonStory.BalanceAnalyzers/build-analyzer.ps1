param(
    [string]$UnityEditorData = 'C:\Program Files\Unity\Hub\Editor\6000.3.8f1\Editor\Data',
    [string]$OutputPath = ''
)

$ErrorActionPreference = 'Stop'
$python = (Get-Command python -ErrorAction Stop).Source
$script = Join-Path $PSScriptRoot 'verify_analyzer.py'
$output = if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    Join-Path $PSScriptRoot '..\..\Assets\Analyzers\DungeonStory.BalanceAnalyzers.dll'
} else {
    $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputPath)
}
$unityDotnet = Join-Path $UnityEditorData 'NetCoreRuntime\dotnet.exe'
$dotnet = if (Test-Path -LiteralPath $unityDotnet) {
    $unityDotnet
} else {
    (Get-Command dotnet -ErrorAction Stop).Source
}

& $python $script --dotnet $dotnet --output $output
if ($LASTEXITCODE -ne 0) {
    throw "Pinned analyzer compilation failed with exit code $LASTEXITCODE."
}
