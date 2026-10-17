param(
    [Parameter(Mandatory=$true)][ValidateSet('SkillTrait','Equipment','Facility')][string]$Target,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [Parameter(Mandatory=$true)][string]$PackageId,
    [ValidateSet('Owner','Staff')][string]$SkillActor = 'Owner',
    [ValidateRange(1,2147483647)][int]$InitialSeed,
    [string]$ResumeSourceDirectory,
    [string]$FrozenInputs
)
$ErrorActionPreference = 'Stop'
$qaProject = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$qaOutput = [IO.Path]::GetFullPath($OutputDirectory)
$qaAllowed = (Join-Path $qaProject 'Artifacts\QA') + '\'
if (-not $qaOutput.StartsWith($qaAllowed, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Pilot output must be below this project Artifacts/QA.'
}
if ($PackageId -cnotmatch '^[a-z0-9][a-z0-9-]+$') { throw 'PackageId must be a stable lower-case identifier.' }
foreach ($qaPath in @($qaOutput, ($qaOutput + '.host'), ($qaOutput + '.launcher'))) {
    if (Test-Path -LiteralPath $qaPath) { throw 'Create-only output already exists.' }
}
if (@(Get-Process Unity -ErrorAction SilentlyContinue).Count) { throw 'An existing Unity must not be overlapped.' }
$qaIndexPath = Join-Path $qaProject 'Artifacts/QA/AuditRemediation-20260926-r1/u0-matrix-r598/local-source-delivery-index-r1.json'
$qaIndex = Get-Content -LiteralPath $qaIndexPath -Raw -Encoding UTF8 | ConvertFrom-Json
$qaPinCache = @{}
function Confirm-Pin([string]$Base, $Descriptor) {
    $qaPinPath = [IO.Path]::GetFullPath((Join-Path $Base $Descriptor.path))
    if (-not $qaPinPath.StartsWith(($qaProject + '\'), [StringComparison]::OrdinalIgnoreCase)) { throw 'Input escaped the project.' }
    if (-not $qaPinCache.ContainsKey($qaPinPath)) {
        $qaPinCache[$qaPinPath] = 'sha256:' + (Get-FileHash -LiteralPath $qaPinPath -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    if ($qaPinCache[$qaPinPath] -cne $Descriptor.sha256 -or (Get-Item -LiteralPath $qaPinPath).Length -ne $Descriptor.bytes) {
        throw ('Frozen input differs: ' + $qaPinPath)
    }
    return $qaPinPath
}
if ($FrozenInputs) {
    $qaFrozenPath = [IO.Path]::GetFullPath($FrozenInputs)
    if (-not $qaFrozenPath.StartsWith($qaAllowed,[StringComparison]::OrdinalIgnoreCase)) { throw 'Frozen inputs outside QA.' }
    $qaFrozen = Get-Content -LiteralPath $qaFrozenPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($qaFrozen.schema -cne 'dungeonstory-pilot-capture-freeze@1') { throw 'Unknown pilot freeze schema.' }
    $qaInventory = Confirm-Pin $qaProject $qaFrozen.currentInventory
    $qaB1Manifest = Confirm-Pin $qaProject $qaFrozen.b1EvidenceManifest
    foreach ($qaCode in @($qaFrozen.codeIdentity) + @($qaFrozen.auxiliaryInputs)) { $null = Confirm-Pin $qaProject $qaCode }
} else {
    $qaInventory = Confirm-Pin $qaProject $qaIndex.currentInventory
    $qaB1Manifest = Confirm-Pin $qaProject $qaIndex.currentB1ReviewManifest
    foreach ($qaPackage in $qaIndex.packages) {
        $qaManifestPath = Confirm-Pin $qaProject $qaPackage.manifest
        $qaPackageRoot = Split-Path -Parent $qaManifestPath
        $qaManifest = Get-Content -LiteralPath $qaManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $qaMechanicsPath = Confirm-Pin $qaPackageRoot $qaManifest.mechanics
        $qaMechanics = Get-Content -LiteralPath $qaMechanicsPath -Raw -Encoding UTF8 | ConvertFrom-Json
        foreach ($qaCode in $qaMechanics.codeIdentity) { $null = Confirm-Pin $qaProject $qaCode }
    }
}
$qaSelector = @{SkillTrait='-SourceV9SkillIntake';Equipment='-SourceV9EquipmentIntake';Facility='-SourceV9FacilityIntake'}[$Target]
$qaArguments = @('-NoProfile','-ExecutionPolicy','Bypass','-File',
    (Join-Path $qaProject 'Tools/QA/run_isolated_unity_batch.ps1'), '-Mode','campaign',
    '-OutputDirectory',$qaOutput,'-ExecuteMethod','DungeonFullWorldRoundTripPlayModeFacade.RequestIsolatedBatch',
    $qaSelector,'-SourceV9Inventory',$qaInventory,'-SourceV9MechanicsManifest',$qaB1Manifest,'-SourceV9PackageId',$PackageId)
if ($Target -eq 'SkillTrait') {
    if ($ResumeSourceDirectory) { throw 'New pilot character capture must not clone a prior checkpoint.' }
    if ($SkillActor -eq 'Staff') { $qaArguments += '-SourceV9SkillExpeditionActor' }
    if ($PSBoundParameters.ContainsKey('InitialSeed')) { $qaArguments += @('-SourceV9InitialSeed', $InitialSeed.ToString([Globalization.CultureInfo]::InvariantCulture)) }
} else {
    if ($PSBoundParameters.ContainsKey('InitialSeed')) { throw 'Resumed campaign branches must preserve their original seed.' }
    if (-not $ResumeSourceDirectory) { throw 'Equipment/facility requires an explicit original campaign source.' }
    $qaResumeRoot = [IO.Path]::GetFullPath($ResumeSourceDirectory)
    if (-not $qaResumeRoot.StartsWith($qaAllowed, [StringComparison]::OrdinalIgnoreCase)) { throw 'Resume source outside QA.' }
    $qaResumeManifest = Get-Content -LiteralPath (Join-Path $qaResumeRoot 'manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $qaCampaignPath = Confirm-Pin $qaResumeRoot $qaResumeManifest.executedCampaign
    $qaCampaign = Get-Content -LiteralPath $qaCampaignPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $qaInitial = @($qaCampaign.files | Where-Object { $_.path -ceq 'campaign/initial-world.json' })
    if ($qaInitial.Count -ne 1) { throw 'Original campaign initial-world pin must be unique.' }
    $qaResume = Confirm-Pin $qaResumeRoot $qaInitial[0]
    $qaArguments += @('-CampaignResumeWorld',$qaResume,'-CampaignResumeSha256',$qaInitial[0].sha256.Substring(7))
}
$qaStart = [Diagnostics.ProcessStartInfo]::new()
$qaStart.FileName = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
$qaStart.Arguments = ($qaArguments | ForEach-Object { '"' + $_ + '"' }) -join ' '
$qaStart.WorkingDirectory = $qaProject
$qaStart.UseShellExecute = $false
$qaStart.CreateNoWindow = $true
$qaStart.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
$qaStart.RedirectStandardOutput = $true
$qaStart.RedirectStandardError = $true
$qaStart.EnvironmentVariables['DOTNET_GCHeapCount'] = '2'
$qaStart.EnvironmentVariables['COMPlus_GCHeapCount'] = '2'
$qaStart.EnvironmentVariables['PSModulePath'] = Join-Path ([IO.Path]::GetDirectoryName($qaStart.FileName)) 'Modules'
$qaHostOutput = $qaOutput + '.host'
New-Item -ItemType Directory -Path $qaHostOutput | Out-Null
function Write-NewEvidence([string]$Name,[string]$Value) {
    $qaStream = [IO.File]::Open((Join-Path $qaHostOutput $Name),[IO.FileMode]::CreateNew,[IO.FileAccess]::Write)
    $qaWriter = [IO.StreamWriter]::new($qaStream,[Text.UTF8Encoding]::new($false))
    try { $qaWriter.Write($Value) } finally { $qaWriter.Dispose() }
}
Write-NewEvidence 'inputs.json' ([ordered]@{target=$Target;skillActor=$SkillActor;packageId=$PackageId;resume=$qaResume;
    initialSeed=$InitialSeed;initialSeedExplicit=$PSBoundParameters.ContainsKey('InitialSeed');
    sourceIndex=$qaIndexPath;frozenInputs=$qaFrozenPath;launcher='Tools/QA/run_isolated_unity_batch.ps1';captureOnly=$true;trainingEligible=$false} | ConvertTo-Json)
$qaProcess = [Diagnostics.Process]::Start($qaStart)
try {
    Write-Output ('QA_HOST_PID=' + $qaProcess.Id)
    $qaStdout = $qaProcess.StandardOutput.ReadToEndAsync()
    $qaStderr = $qaProcess.StandardError.ReadToEndAsync()
    $qaProcess.WaitForExit()
    Write-NewEvidence 'stdout.txt' $qaStdout.Result
    Write-NewEvidence 'stderr.txt' $qaStderr.Result
    Write-NewEvidence 'exit.json' ([ordered]@{exitCode=$qaProcess.ExitCode;pid=$qaProcess.Id} | ConvertTo-Json)
    if ($qaProcess.ExitCode -ne 0) { throw 'Isolated capture failed. Preserve outputs; do not retry or run later captures automatically.' }
    $qaReport = Get-Content -LiteralPath (Join-Path $qaOutput 'unity-evidence/report.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if (-not $qaReport.passed -or -not $qaReport.sourceV9PackageWritten -or $qaReport.responseApplied -or $qaReport.networkSent) {
        throw 'Native response-free request capture did not pass.'
    }
    $qaLog = Get-Content -LiteralPath (Join-Path $qaOutput 'unity.log') -Raw -Encoding UTF8
    if ($qaLog -match 'error CS[0-9]+') { throw 'Compilation failed.' }
    foreach ($qaPinPath in $qaPinCache.Keys) {
        if (('sha256:' + (Get-FileHash -LiteralPath $qaPinPath -Algorithm SHA256).Hash.ToLowerInvariant()) -cne $qaPinCache[$qaPinPath]) {
            throw ('Frozen input changed during capture: ' + $qaPinPath)
        }
    }
    Write-Output ('CAPTURE_PASS=' + $Target + '; source=' + (Join-Path $qaOutput 'unity-evidence/source-v9'))
} finally { $qaProcess.Dispose() }
