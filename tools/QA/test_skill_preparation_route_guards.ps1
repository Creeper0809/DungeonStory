# Read and exercise only the launcher's preparation guard. Never launch Unity.
$ErrorActionPreference = 'Stop'
$qaLauncher = Join-Path $PSScriptRoot 'run_isolated_unity_batch.ps1'
$qaTokens = $null
$qaErrors = $null
$qaAst = [System.Management.Automation.Language.Parser]::ParseFile(
    $qaLauncher, [ref]$qaTokens, [ref]$qaErrors)
if ($qaErrors.Count) { throw ($qaErrors | Out-String) }
$qaGuardAst = $qaAst.FindAll({ param($node)
    $node -is [System.Management.Automation.Language.IfStatementAst] -and
    $node.Clauses[0].Item1.Extent.Text -eq '$SkillEquipmentPreparationFixture'
}, $true) | Select-Object -First 1
if ($null -eq $qaGuardAst) { throw 'Preparation guard not found.' }
$qaGuard = [scriptblock]::Create($qaAst.ParamBlock.Extent.Text + "`n" + @'
$qaHasResumeWorld = -not [string]::IsNullOrEmpty($CampaignResumeWorld)
$qaSkillSupportContinuation = -not [string]::IsNullOrEmpty($SkillBattleActorId)
'@ + "`n" + $qaGuardAst.Extent.Text)
$qaCommon = @{
    ExecuteMethod = 'DungeonLiveNarrativeCampaignRunner.RequestSkillBattleDiagnostics'
    OutputDirectory = 'guard-only-no-output'; Mode = 'campaign'
    CampaignResumeWorld = 'guard-only-no-file'; CampaignResumeSha256 = 'guard-only-no-file'
    SkillEquipmentPreparationFixture = $true; SkillPreparationEnvironmentFixture = $true
    SkillPreparationInstantWorkFixture = $true; SkillNormalWeaponPreparation = $true
    SkillReplayHoldNaturalInvasion = $true; SkillNonTargetHungerFixture = $true
}
$qaSupport = $qaCommon.Clone()
$qaSupport.SkillBattleActorId = 'actor:guard'
$qaSupport.SkillBattleSkillId = 'skill:guard'
$qaReplay = $qaCommon.Clone()
$qaReplay.ExecuteMethod = 'DungeonFullWorldRoundTripPlayModeFacade.RequestIsolatedBatch'
$qaReplay.SourceV9SkillReplay = $true
$qaReplay.SourceV9SkillCandidateIndex = 2
$qaReplay.SourceV9ReplayPackage = 'guard-only-source'
$qaReplay.SourceV9ReplayResponse = 'guard-only-response'
$qaCases = [Collections.Generic.List[object]]::new()
$qaCases.Add(@{ Name = 'owned-support'; Args = $qaSupport; Expected = $true })
$qaCases.Add(@{ Name = 'owned-replay'; Args = $qaReplay; Expected = $true })
$qaBad = $qaReplay.Clone(); $qaBad.ExecuteMethod = 'Unowned.Entry'
$qaCases.Add(@{ Name = 'unowned-entry'; Args = $qaBad; Expected = $false })
$qaBad = $qaReplay.Clone(); $qaBad.SkillBattleActorId = 'actor:guard'; $qaBad.SkillBattleSkillId = 'skill:guard'
$qaCases.Add(@{ Name = 'mixed-routes'; Args = $qaBad; Expected = $false })
$qaBad = $qaReplay.Clone(); $qaBad.SourceV9FacilityReplay = $false
$qaCases.Add(@{ Name = 'explicit-false-unrelated-selector'; Args = $qaBad; Expected = $false })
$qaBad = $qaSupport.Clone(); $qaBad.SourceV9SkillReplay = $false
$qaCases.Add(@{ Name = 'support-false-replay-selector'; Args = $qaBad; Expected = $false })
$qaBad = $qaReplay.Clone(); $qaBad.Remove('SourceV9ReplayResponse')
$qaCases.Add(@{ Name = 'missing-response'; Args = $qaBad; Expected = $false })
$qaBad = $qaReplay.Clone(); $qaBad.SkillNonTargetHungerFixture = $false
$qaCases.Add(@{ Name = 'missing-required-opt-in'; Args = $qaBad; Expected = $false })
$qaBad = $qaSupport.Clone(); $qaBad.Remove('SkillBattleSkillId')
$qaCases.Add(@{ Name = 'missing-support-skill'; Args = $qaBad; Expected = $false })
$qaBad = $qaReplay.Clone(); $qaBad.Remove('CampaignResumeWorld')
$qaCases.Add(@{ Name = 'missing-world'; Args = $qaBad; Expected = $false })
foreach ($qaCase in $qaCases) {
    $qaArgs = $qaCase.Args
    $qaAccepted = $true
    $qaFailure = ''
    try { & $qaGuard @qaArgs }
    catch { $qaAccepted = $false; $qaFailure = $_.Exception.Message }
    if ($qaAccepted -ne $qaCase.Expected) {
        throw ('Guard case {0}: expected accepted={1}, actual={2}; {3}' -f $qaCase.Name, $qaCase.Expected, $qaAccepted, $qaFailure)
    }
    Write-Output ('PASS ' + $qaCase.Name)
}
Write-Output ('PASS {0} preparation-route cases; no Unity, desktop or process created.' -f $qaCases.Count)
