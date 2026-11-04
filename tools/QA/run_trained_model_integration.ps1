param(
    [Parameter(Mandatory=$true)][string]$BundleRoot,
    [Parameter(Mandatory=$true)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
# Budgets for this all-GPU 8B test configuration, not universal model requirements.
function Assert-ModelMemoryHeadroom {
$memory = Get-CimInstance Win32_OperatingSystem
if ($memory.FreePhysicalMemory -lt 8MB) {
    throw 'Test deferred: this configuration reserves at least 8 GiB free physical RAM. No app is closed automatically.'
}
$gpuTool = Get-Command nvidia-smi -ErrorAction Stop
$gpuFreeText = @(& $gpuTool.Source --query-gpu=memory.free --format=csv,noheader,nounits)
if ($LASTEXITCODE -ne 0 -or $gpuFreeText.Count -ne 1 -or $gpuFreeText[0].Trim() -notmatch '^\d+$' -or
    [long]$gpuFreeText[0].Trim() -lt 7168) {
    throw 'Test deferred: this all-GPU configuration reserves at least 7 GiB free VRAM on one NVIDIA GPU.'
}
}
Assert-ModelMemoryHeadroom
if (-not [IO.Path]::IsPathRooted($BundleRoot) -or -not [IO.Path]::IsPathRooted($OutputDirectory)) {
    throw 'Absolute bundle and create-only QA output paths are required.'
}
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$previousRoot = $env:DUNGEONSTORY_LLM_STREAMING_ROOT
$previousTraceRoot = $env:DUNGEONSTORY_R80_TRACE_ROOT
Push-Location -LiteralPath $project
try {
    # Compile in its own contained Editor first. Closing that job releases the
    # several-GiB Roslyn server before the 8B model is loaded in the Play job.
    # Source remains frozen across both invocations; no memory gate is relaxed.
    & (Join-Path $PSScriptRoot 'run_isolated_unity_batch.ps1') `
        -ExecuteMethod TrainedNarrativeModelPlayModeVerifier.CompileForBatch `
        -OutputDirectory ($OutputDirectory + '.compile') -Mode edit -SerializedStartup
    Assert-ModelMemoryHeadroom
    $env:DUNGEONSTORY_LLM_STREAMING_ROOT = [IO.Path]::GetFullPath($BundleRoot)
    $env:DUNGEONSTORY_R80_TRACE_ROOT = Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) 'runtime-traces'
    & (Join-Path $PSScriptRoot 'run_isolated_unity_batch.ps1') `
        -ExecuteMethod TrainedNarrativeModelPlayModeVerifier.RequestForBatch `
        -OutputDirectory $OutputDirectory -Mode play -SerializedStartup
}
finally {
    $env:DUNGEONSTORY_LLM_STREAMING_ROOT = $previousRoot
    $env:DUNGEONSTORY_R80_TRACE_ROOT = $previousTraceRoot
    Pop-Location
}
