param(
    [Parameter(Mandatory=$true)][string]$ExecuteMethod,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [switch]$IsolatedPlayMode,
    [switch]$LiveNarrativeCampaign
)
$ErrorActionPreference = 'Stop'
# Hidden windows on the interactive desktop stole focus during real use.
# Do not turn this into an opt-out switch: a verified isolated launcher must
# replace this entry point before Unity execution is allowed again.
throw 'FOCUS_ISOLATION_REQUIRED: this interactive-desktop runner is disabled. Use Tools/QA/run_isolated_unity_batch.ps1 (noninteractive window station); no Unity process was started.'
# Caller owns the SOURCE_EDIT -> UNITY_VERIFY freeze. This runner never focuses
# a window, kills an existing Editor, deletes evidence, or starts another run.
$auditProject = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$auditRoot = [IO.Path]::GetFullPath((Join-Path $auditProject 'Artifacts/QA')) + [IO.Path]::DirectorySeparatorChar
$auditOutput = [IO.Path]::GetFullPath($OutputDirectory)
if (-not $auditOutput.StartsWith($auditRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Output must be a new directory under this project Artifacts/QA.'
}
if ($ExecuteMethod -notmatch '^[A-Za-z_][A-Za-z0-9_.]+$') { throw 'Invalid execute method.' }
if (Get-Process -Name Unity -ErrorAction SilentlyContinue) { throw 'An Editor is already running; do not overlap verification.' }
$auditLog = $auditOutput + '.log'
if ((Test-Path -LiteralPath $auditOutput) -or (Test-Path -LiteralPath $auditLog)) { throw 'Evidence target already exists.' }
if ($IsolatedPlayMode) {
    if ($ExecuteMethod -ne 'DungeonFullWorldRoundTripPlayModeFacade.RequestIsolatedBatch') { throw 'Only the owned full-world isolated runner may omit -quit.' }
    foreach ($auditPending in @('Temp/playmode-persistence-snapshots', 'Temp/full-world-round-trip-playmode.request', 'Temp/full-world-round-trip-playmode-console.buffer')) {
        if (Test-Path -LiteralPath (Join-Path $auditProject $auditPending)) { throw ('Existing verification state must be preserved: ' + $auditPending) }
    }
}
New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($auditOutput)) -Force | Out-Null
$auditStart = [Diagnostics.ProcessStartInfo]::new()
$auditStart.FileName = 'C:\Program Files\Unity\Hub\Editor\6000.3.8f1\Editor\Unity.exe'
$auditStart.Arguments = '-batchmode -nographics -projectPath "' + $auditProject + '" -executeMethod ' + $ExecuteMethod + ' -quit -logFile "' + $auditLog + '"'
if ($IsolatedPlayMode) { $auditStart.Arguments = $auditStart.Arguments.Replace(' -quit ', ' ') }
$auditStart.UseShellExecute = $false
$auditStart.CreateNoWindow = $true
$auditStart.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
$auditStart.EnvironmentVariables['DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT'] = $auditOutput
if ($LiveNarrativeCampaign -and -not $IsolatedPlayMode) { throw 'Live narrative capture requires isolated PlayMode.' }
if ($LiveNarrativeCampaign) { $auditStart.EnvironmentVariables['DUNGEONSTORY_LIVE_NARRATIVE_CAPTURE'] = '1' }
else { $auditStart.EnvironmentVariables.Remove('DUNGEONSTORY_LIVE_NARRATIVE_CAPTURE') }
if ($IsolatedPlayMode) {
    $auditStart.EnvironmentVariables['DUNGEONSTORY_QA_PERSISTENCE_ROOT'] = Join-Path $auditOutput 'persistent-data'
} else {
    $auditStart.EnvironmentVariables.Remove('DUNGEONSTORY_QA_PERSISTENCE_ROOT')
}
$auditProcess = [Diagnostics.Process]::Start($auditStart)
try { $auditProcess.PriorityClass = [Diagnostics.ProcessPriorityClass]::BelowNormal }
catch { if (-not $auditProcess.HasExited) { Write-Warning 'Could not lower priority; existing run remains tracked.' } }
Write-Output ('UNITY_VERIFY pid=' + $auditProcess.Id)
$auditProcess.WaitForExit()
Write-Output ('UNITY_EXIT=' + $auditProcess.ExitCode)
Get-Content -LiteralPath $auditLog -Tail 10
$auditReport = Join-Path $auditOutput 'report.json'
if (Test-Path -LiteralPath $auditReport) { Get-Content -LiteralPath $auditReport -TotalCount 65 }
if ($IsolatedPlayMode -and (Test-Path -LiteralPath (Join-Path $auditOutput 'full-world-report.txt'))) {
    Get-Content -LiteralPath (Join-Path $auditOutput 'full-world-report.txt') -TotalCount 20
}
exit $auditProcess.ExitCode
