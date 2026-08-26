#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Independent coverage inventory. This gate never substitutes bookkeeping or
/// static type existence for live gameplay evidence: each row states whether
/// its evidence is PlayMode, deterministic contract, or still uncovered.
/// </summary>
public static class CharacterAiCoverageManifestDebugScenarios
{
    public const string ReportPath =
        "docs/implementation-reports/character-ai-coverage-manifest-latest.txt";

    private static Dictionary<string, EvidenceSourceFreshness>
        evidenceFreshnessByType;

    private static readonly string[] OffenseFreshnessVerifierTypes =
    {
        "OffenseStrategicPlayModeVerifier",
        "OffenseJourneyPlayModeFacade",
        "OffenseTacticalJourneyPlayModeVerifier"
    };

    private static readonly string[] OffenseFreshnessDependencyPaths =
    {
        "Assets/Scripts/Services/Offense/Strategic/OffenseCommandBattleDirector.cs",
        "Assets/Scripts/Services/Offense/Strategic/OffenseCommandResolutionAdapter.cs",
        "Assets/Scripts/Services/Offense/OffenseBattleRuntime.cs",
        "Assets/Scripts/Services/Offense/OffenseBattleModel.cs"
    };

    private static readonly string[] OffenseJourneyFreshnessDependencyPaths =
    {
        "Assets/Scripts/Services/Offense/Strategic/OffenseCommandBattleDirector.cs",
        "Assets/Scripts/Services/Offense/Strategic/OffenseCommandResolutionAdapter.cs",
        "Assets/Scripts/Services/Offense/OffenseBattleRuntime.cs",
        "Assets/Scripts/Services/Offense/OffenseBattleModel.cs",
        "Assets/Scripts/Services/Offense/OffensePreparationService.cs",
        "Assets/Scripts/Services/Economy/ProductionItemGateway.cs",
        "Assets/Scripts/Services/Items/FacilityBufferDestinationClaimRegistry.cs",
        "Assets/Scripts/Services/Items/WorldItemHaulDestinationAuthority.cs",
        "Assets/Scripts/Services/Items/WorldItemHaulPlanningService.cs",
        "Assets/Scripts/Services/Items/ItemTransferService.cs",
        "Assets/Scripts/Services/Items/AbilityHaul.cs"
    };

    private static readonly ActionCoverage[] Actions =
    {
        Covered("Drink", new[]
        {
            E("CharacterAiSelfCarePlayModeVerifier", "RequestRun", "success/terminal"),
            E("CharacterConsumableActionFaultPlayModeVerifier", "RequestRun", "drink source-loss and lease-invalid terminal/cleanup")
        }),
        Covered("Eat", new[]
        {
            E("CharacterAiFaultRecoveryPlayModeVerifier", "RequestActionFacilityGroup", "facility/path/reservation/terminal"),
            E("CharacterConsumableActionFaultPlayModeVerifier", "RequestRun", "eat source-loss/spoil/lease-invalid terminal/cleanup"),
            E("DailyRoutineWuPlayModeVerifier", "RequestRun", "five-day three-seed compound progress", typeof(int), "three calls:157181/157182/157183")
        }),
        Covered("ExitDungeon", "CharacterAiFaultRecoveryPlayModeVerifier", "RequestDestinationlessGroup", "deferred/path-starvation/terminal", "DungeonAiActionSaveLoadPlayModeVerifier", "RequestRun", "movement save/restore"),
        Covered("Haul", "CharacterAiCrossActionFaultPlayModeVerifier", "RequestRun", "source shrink/despawn/destination loss/lifecycle", "PhysicalItemLogisticsPlayModeVerifier", "RequestRunFromMenu", "quantity conservation"),
        Covered("Hunt", "WildlifeAiHuntPlayModeVerifier", "RequestRun", "live progress/terminal", "CharacterAiCrossActionFaultPlayModeVerifier", "RequestRun", "target invalidation/lifecycle"),
        Covered("Hygiene", new[] { E("CharacterAiFaultRecoveryPlayModeVerifier", "RequestActionFacilityGroup", "facility/path/reservation/terminal"), E("DailyRoutineWuPlayModeVerifier", "RequestRun", "five-day three-seed compound progress", typeof(int), "three calls:157181/157182/157183") }),
        Covered("LookAround", "CharacterAiFaultRecoveryPlayModeVerifier", "RequestDestinationlessGroup", "deferred/path-starvation/terminal"),
        Covered("PrimitiveBucketWash", new[]
        {
            E("PrimitiveStartSurvivalPlayModeVerifier", "RunFocusedFromMenu", "primitive bucket wash success/progress"),
            E("CharacterAiFaultRecoveryPlayModeVerifier", "RequestPrimitiveSurvivalGroup", "live path/target invalidation")
        }),
        Covered("PrimitiveFieldMeal", new[]
        {
            E("PrimitiveStartSurvivalPlayModeVerifier", "RunFocusedFromMenu", "primitive field meal success/progress"),
            E("CharacterAiFaultRecoveryPlayModeVerifier", "RequestPrimitiveSurvivalGroup", "live source loss before commit")
        }),
        Covered("PrimitiveFloorRest", new[]
        {
            E("PrimitiveStartSurvivalPlayModeVerifier", "RunFocusedFromMenu", "primitive floor rest success/progress"),
            E("CharacterAiFaultRecoveryPlayModeVerifier", "RequestPrimitiveSurvivalGroup", "live interruption/terminal")
        }),
        Covered("PrimitiveLatrine", new[]
        {
            E("PrimitiveStartSurvivalPlayModeVerifier", "RunFocusedFromMenu", "primitive latrine success/progress"),
            E("CharacterAiFaultRecoveryPlayModeVerifier", "RequestPrimitiveSurvivalGroup", "live path/target invalidation")
        }),
        Covered("Recreation", "CharacterAiFaultRecoveryPlayModeVerifier", "RequestActionFacilityGroup", "facility/path/reservation/terminal"),
        Covered("Rescue", "CharacterAiAutonomousMedicalPlayModeVerifier", "RequestRun", "live selection/progress/terminal", "CharacterAiCrossActionFaultPlayModeVerifier", "RequestRun", "patient/bed/medicine/save lifecycle"),
        Covered("Rest", "CharacterAiFaultRecoveryPlayModeVerifier", "RequestSharedFacilityGroup", "facility/path/reservation/terminal"),
        Covered("Shopping", "CharacterAiFaultRecoveryPlayModeVerifier", "RequestActionFacilityGroup", "facility/path/reservation/terminal"),
        Covered("SubstanceUse", new[]
        {
            E("CharacterAiSelfCarePlayModeVerifier", "RequestRun", "success/terminal"),
            E("CharacterConsumableActionFaultPlayModeVerifier", "RequestRun", "substance source-loss and lease-invalid terminal/cleanup")
        }),
        Covered("Toilet", "CharacterAiFaultRecoveryPlayModeVerifier", "RequestActionFacilityGroup", "facility/path/reservation/terminal"),
        Covered("Wait", "CharacterAiFaultRecoveryPlayModeVerifier", "RequestDestinationlessGroup", "deferred/path-starvation/terminal"),
        Covered("Work", BuildWorkAggregateEvidence())
    };

    private static readonly DomainCoverage[] Domains =
    {
        Live("combat:defense-autonomy", "DefenseEngagementPlayModeVerifier", "StartFromMenu", "engagement/target/terminal"),
        Live("combat:commands-rescue", "CombatV14PlayModeVerifier", "StartRuntimeProbe", "orders/reservations/rescue/treatment"),
        Live("medical:autonomous-rescue-treatment", "CharacterAiAutonomousMedicalPlayModeVerifier", "RequestRun", "selection/progress/terminal"),
        Live("medical:surgery", "SurgeryPlayModeVerifier", "RequestRunFromMenu", "doctor/patient/medicine/progress/save"),
        Live("wildlife:hunt", "WildlifeAiHuntPlayModeVerifier", "RequestRun", "selection/chase/attack/terminal"),
        Live("wildlife:capture-transport", "CaptivityWildlifeLifecyclePlayModeVerifier", "RequestRun", "capture transport ownership/progress/success and injected-fault terminals"),
        Live("wildlife:animal-care", "CaptivityWildlifeLifecyclePlayModeVerifier", "RequestRun", "animal-care Brain/AIWork progress and lifecycle cleanup"),
        Live("captivity:warden-interactions-labor", "CaptivityAiPlayModeVerifier", "RequestRun", "live production runtime interaction progress/terminal"),
        Live("captivity:escort", "CaptivityAiPlayModeVerifier", "RequestRun", "escort ownership/progress/terminal"),
        Live("captivity:escape", "CaptivityWildlifeLifecyclePlayModeVerifier", "RequestRun", "AbilityCaptiveEscape movement/success and injected-fault terminals"),
        Live("captivity:recapture", "CaptivityAiPlayModeVerifier", "RequestRun", "recapture reservation/cancellation terminal"),
        Live("visitor:customer-lifecycle", "CharacterVisitorControlJourneyPlayModeVerifier", "RequestRun", "official spawn/entry/service terminal/macro/exit lifecycle"),
        Live("invasion:defense-engagement", "DefenseEngagementPlayModeVerifier", "StartFromMenu", "invasion engagement/target/terminal"),
        Live("offense:strategic-ui", "OffenseStrategicPlayModeVerifier", "RunFromMenu", "live UI surface only"),
        Live("offense:journey-battle-reward", "OffenseJourneyPlayModeFacade", "RequestRun", "production strategic world-travel/decision/battle/return/reward terminal"),
        Live("offense:enemy-tactics", "OffenseTacticalJourneyPlayModeVerifier", "RequestRun", "Attack/Move/Protect/UseAbility/Retreat intent-to-command terminals")
    };

    private static readonly EvidenceArtifact[] EvidenceArtifacts =
    {
        A("CharacterAiSelfCarePlayModeVerifier", "Artifacts/QA/character-ai-self-care-playmode.txt", "RESULT=PASS"),
        A("CharacterAiFaultRecoveryPlayModeVerifier", "Artifacts/QA/character-ai-fault-recovery-playmode.txt", "RESULT=PASS"),
        A("CharacterAiCrossActionFaultPlayModeVerifier", "Artifacts/QA/character-ai-cross-action-fault-playmode.txt", "result=PASS"),
        A("CharacterConsumableActionFaultPlayModeVerifier", "Artifacts/QA/character-consumable-action-fault-playmode.txt", "RESULT=PASS"),
        A("CharacterAiLifecycleFaultPlayModeVerifier", "Artifacts/QA/character-ai-lifecycle-fault-playmode.txt", "result=PASS"),
        A("DungeonAiActionSaveLoadPlayModeVerifier", "Artifacts/QA/ai-mid-action-save-load-playmode.txt", "result=PASS"),
        A("PhysicalItemLogisticsPlayModeVerifier", "Artifacts/QA/physical-item-logistics-playmode-report.txt", "RESULT=PASS"),
        A("WildlifeAiHuntPlayModeVerifier", "Artifacts/QA/wildlife-ai-hunt-playmode.txt", "RESULT=PASS"),
        A("CharacterAiAutonomousMedicalPlayModeVerifier", "Artifacts/QA/character-ai-autonomous-medical-playmode.txt", "RESULT=PASS"),
        A("SurgeryPlayModeVerifier", "Artifacts/QA/surgery-playmode-report.txt", "RESULT=PASS"),
        A("CaptivityAiPlayModeVerifier", "Artifacts/QA/captivity-ai-playmode.txt", "RESULT=PASS"),
        A("CaptivityWildlifeLifecyclePlayModeVerifier", "Artifacts/QA/captivity-wildlife-lifecycle-playmode.txt", "RESULT=PASS; failures=0"),
        A("CombatV14PlayModeVerifier", "Artifacts/QA/combat-v14-playmode-report.txt", "RESULT=PASS"),
        A("DefenseEngagementPlayModeVerifier", "Artifacts/QA/defense-engagement-playmode.txt", "result=PASS"),
        A("CharacterAlarmResponsePlayModeVerifier", "Artifacts/QA/character-alarm-response-playmode.txt", "RESULT=PASS"),
        A("PrimitiveStartSurvivalPlayModeVerifier", "Artifacts/QA/primitive-start-survival-5day-report.txt", "FIRST_LINE=PASS"),
        A("CharacterAiWorkTypeLiveMatrixPlayModeVerifier", "Artifacts/QA/character-ai-worktype-live-matrix.txt", "RESULT=PASS; rows=20"),
        A("CharacterVisitorControlJourneyPlayModeVerifier", "Artifacts/QA/character-visitor-control-journey-playmode.txt", "result=PASS"),
        A("FirstRunObjectivePlayModeVerifier", "Temp/first-run-objective-report.txt", "FIRST_RUN_OBJECTIVE PASS"),
        A("OffenseStrategicPlayModeVerifier", "Temp/OffenseStrategicValidation/offense-strategic-visual-report.txt", "PASS Offense Strategic visual verification"),
        A("OffenseJourneyPlayModeFacade", "Artifacts/QA/offense-journey-playmode.txt", "result=PASS"),
        A("OffenseTacticalJourneyPlayModeVerifier", "Artifacts/QA/offense-tactical-journey-playmode.txt", "result=PASS")
    };

    [MenuItem("Tools/Dungeon Story/Validation/AI/Capture Coverage Manifest")]
    public static void RunAll()
    {
        Evaluation evaluation = Evaluate();
        Write(evaluation.Report);
        Debug.Log(evaluation.Summary);
        if (!evaluation.Passed)
            throw new InvalidOperationException(evaluation.Summary);
    }

    public static string RunFromUnityMcp()
    {
        RunAll();
        return File.ReadAllText(ReportPath);
    }

    public static string CaptureReportWithoutThrowing()
    {
        try
        {
            Evaluation evaluation = Evaluate();
            try
            {
                Write(evaluation.Report);
                return evaluation.Report;
            }
            catch (Exception writeException)
            {
                return BuildCaptureFailureReport("report-write", writeException);
            }
        }
        catch (Exception evaluationException)
        {
            string report = BuildCaptureFailureReport("evaluation", evaluationException);
            try
            {
                Write(report);
            }
            catch
            {
                // The no-throw API must still return the diagnostic when the
                // filesystem or AssetDatabase is unavailable.
            }
            return report;
        }
    }

    private static Evaluation Evaluate()
    {
        evidenceFreshnessByType =
            new Dictionary<string, EvidenceSourceFreshness>(StringComparer.Ordinal);
        List<string> gaps = new List<string>();
        StringBuilder report = new StringBuilder(16384);
        report.AppendLine("CHARACTER_AI_COVERAGE_MANIFEST_V2");
        report.AppendLine("rule=LiveExecuted requires a durable PASS artifact newer than its verifier and the current coverage-critical production source set, plus an exact selector/row marker for the claimed scope. Entrypoint/type/file existence alone is ContractOnly.");
        report.AppendLine("rule=DailyRoutine evidence is one compound claim: seeds 157181/157182/157183 must each be fresh and contain the five-day, seed, runtime-diagnostics, and PASS markers.");
        report.AppendLine("rule=The authored Work action is covered only when lifecycle evidence and all 31 WorkType production-live evidence rows are LiveExecuted; a static executor contract is insufficient.");
        AppendSourceInventory(report, gaps);
        report.AppendLine();
        report.AppendLine("## Canonical fault-recovery union");
        EvidenceResolution canonicalFaultRecovery = ResolveEvidence(E(
            "CharacterAiFaultRecoveryPlayModeVerifier",
            "RequestRun",
            "all seven selector groups in one PlayMode session"));
        report.AppendLine(canonicalFaultRecovery.Status + " | "
            + Describe(canonicalFaultRecovery));
        if (canonicalFaultRecovery.Status != EvidenceStatus.LiveExecuted)
        {
            gaps.Add("fault-recovery:canonical-union: "
                + canonicalFaultRecovery.Detail);
        }
        report.AppendLine();
        report.AppendLine("## Authored action assets");
        report.AppendLine("status | action | asset | evidenceState | evidence | uncovered");
        SourceInventorySnapshot inventory = CharacterAiCoverageSourceInventory.Capture();
        string[] authored = inventory.AuthoredActionAssetPaths
            .Select(Path.GetFileNameWithoutExtension)
            .ToArray();
        foreach (ActionCoverage row in Actions)
        {
            bool assetExists = authored.Contains(row.AssetName, StringComparer.Ordinal);
            EvidenceResolution[] resolutions = row.Evidence.Select(ResolveEvidence).ToArray();
            bool evidenceExists = resolutions.Length > 0
                && resolutions.All(value => value.Status == EvidenceStatus.LiveExecuted);
            bool covered = assetExists && evidenceExists && string.IsNullOrEmpty(row.Gap);
            if (!covered)
                gaps.Add($"action:{row.AssetName}: " + (!assetExists ? "asset missing" : !evidenceExists ? "production-live evidence not executed" : row.Gap));
            report.Append(covered ? "COVERED" : "UNCOVERED").Append(" | ").Append(row.AssetName)
                .Append(" | ").Append(assetExists ? "present" : "missing").Append(" | ")
                .Append(string.Join(",", resolutions.Select(value => value.Status.ToString()))).Append(" | ")
                .Append(string.Join("; ", resolutions.Select(Describe))).Append(" | ")
                .AppendLine(string.IsNullOrEmpty(row.Gap) ? "-" : row.Gap);
        }

        report.AppendLine();
        report.AppendLine("## Deprivation logical actions (5)");
        report.AppendLine("status | type | evidenceState | uncovered");
        EvidenceResolution deprivationEvidence = ResolveEvidence(E(
            "CharacterAiFaultRecoveryPlayModeVerifier",
            "RequestDeprivationGroup",
            "five production CharacterBreakdownActionRunner external-intent rows"));
        foreach (string typeName in CharacterAiCoverageSourceInventory.ExpectedDeprivationActionTypes)
        {
            bool typeExists = inventory.ConcreteActionTypes.Contains(typeName, StringComparer.Ordinal);
            bool covered = typeExists
                && deprivationEvidence.Status == EvidenceStatus.LiveExecuted;
            report.Append(covered ? "COVERED" : "UNCOVERED")
                .Append(" | ").Append(typeName).Append(" | ")
                .Append(deprivationEvidence.Status).Append(" | ")
                .AppendLine(typeExists
                    ? deprivationEvidence.Detail
                    : "Concrete type missing.");
            if (!covered)
                gaps.Add("deprivation:" + typeName + ": "
                    + (typeExists ? deprivationEvidence.Detail : "concrete type missing"));
        }

        report.AppendLine();
        report.AppendLine("## Work types (31)");
        report.AppendLine("status | workType | executor | evidence");
        IReadOnlyList<WorkExecutionFailureProfile> profiles = BuiltInWorkExecutionFailureProfiles.All;
        foreach (WorkTypeId id in BuiltInWorkTypeIds.All)
        {
            WorkExecutionFailureProfile profile = profiles.FirstOrDefault(value => value.WorkTypeId == id);
            EvidenceResolution evidence = ResolveEvidence(GetWorkEvidence(id));
            bool covered = profile != null && evidence.Status == EvidenceStatus.LiveExecuted;
            if (!covered) gaps.Add("work:" + id.Value + ": " + (profile == null ? "profile missing" : evidence.Detail));
            report.Append(covered ? "COVERED" : "UNCOVERED").Append(" | ").Append(id.Value)
                .Append(" | ").Append(profile?.ExecutorType?.Name ?? "missing")
                .Append(" | ").Append(evidence.Status).Append(" | ").AppendLine(evidence.Detail);
        }
        if (BuiltInWorkTypeIds.All.Count != 31 || profiles.Count != 31)
            gaps.Add($"work catalog/profile count mismatch: {BuiltInWorkTypeIds.All.Count}/{profiles.Count}");

        report.AppendLine();
        report.AppendLine("## Combat / medical / wildlife / captivity / offense");
        report.AppendLine("status | branch | evidence | uncovered");
        foreach (DomainCoverage row in Domains)
        {
            EvidenceResolution evidence = ResolveEvidence(row.Evidence);
            bool covered = evidence.Status == EvidenceStatus.LiveExecuted
                && string.IsNullOrEmpty(row.Gap);
            if (!covered) gaps.Add("domain:" + row.Id + ": " + (string.IsNullOrEmpty(row.Gap) ? evidence.Detail : row.Gap));
            report.Append(covered ? "COVERED" : "UNCOVERED").Append(" | ").Append(row.Id)
                .Append(" | ").Append(evidence.Status).Append(": ").Append(Describe(evidence))
                .Append(" | ").AppendLine(covered ? "-" : row.Gap);
        }

        report.AppendLine();
        report.AppendLine("## Remaining uncovered rows only");
        foreach (string gap in gaps) report.AppendLine("- " + gap);
        report.AppendLine($"result={(gaps.Count == 0 ? "PASS" : "FAIL")}; authored={Actions.Length}; runtimeActions={inventory.ConcreteActionTypes.Length}; deprivationLogical={CharacterAiCoverageSourceInventory.ExpectedDeprivationActionTypes.Length}; workTypes={BuiltInWorkTypeIds.All.Count}; domains={Domains.Length}; uncovered={gaps.Count}");
        string summary = $"CHARACTER_AI_COVERAGE_MANIFEST={(gaps.Count == 0 ? "PASS" : "FAIL")}; uncovered={gaps.Count}; report={ReportPath}";
        return new Evaluation(gaps.Count == 0, summary, report.ToString());
    }

    private static void AppendSourceInventory(StringBuilder report, ICollection<string> gaps)
    {
        SourceInventorySnapshot inventory = CharacterAiCoverageSourceInventory.Capture();
        string[] assetNames = inventory.AuthoredActionAssetPaths
            .Select(Path.GetFileNameWithoutExtension)
            .ToArray();
        AddInventoryDiff(gaps, CharacterAiCoverageSourceInventory.CompareExact(
            CharacterAiCoverageSourceInventory.ExpectedAuthoredActionAssets,
            assetNames,
            "authored-action"));
        string[] expectedPaths = CharacterAiCoverageSourceInventory.ExpectedAuthoredActionAssets
            .Select(name => "Assets/Resources/SO/AI/Action/" + name + ".asset")
            .ToArray();
        AddInventoryDiff(gaps, CharacterAiCoverageSourceInventory.CompareExact(
            expectedPaths, inventory.AuthoredActionAssetPaths, "authored-action-path"));
        AddInventoryDiff(gaps, CharacterAiCoverageSourceInventory.CompareExact(
            CharacterAiCoverageSourceInventory.ExpectedConcreteActionTypes,
            inventory.ConcreteActionTypes,
            "runtime-action-type"));
        AddInventoryDiff(gaps, CharacterAiCoverageSourceInventory.CompareExact(
            CharacterAiCoverageSourceInventory.ExpectedBranches,
            inventory.Branches,
            "branch"));
        AddInventoryDiff(gaps, CharacterAiCoverageSourceInventory.CompareExact(
            CharacterAiCoverageSourceInventory.ExpectedBehaviorOperations,
            inventory.BehaviorOperations,
            "behavior-operation"));
        AddInventoryDiff(gaps, CharacterAiCoverageSourceInventory.CompareExact(
            CharacterAiCoverageSourceInventory.ExpectedBehaviorTaskTypes,
            inventory.BehaviorTaskTypes,
            "behavior-task"));
        BehaviorTaskAttachmentInventory attachments = inventory.BehaviorTaskAttachments;
        if (!attachments.Resolved)
        {
            gaps.Add("inventory:behavior-task-attachment:unresolved:"
                + attachments.FailureReason);
        }
        else
        {
            string[] liveAttached = attachments.Rows
                .Where(row => row.Status == BehaviorTaskAttachmentStatus.LiveAttached)
                .Select(row => row.TypeName)
                .ToArray();
            AddInventoryDiff(gaps, CharacterAiCoverageSourceInventory.CompareExact(
                CharacterAiCoverageSourceInventory.ExpectedLiveAttachedBehaviorTaskTypes,
                liveAttached,
                "behavior-task-live-attachment"));
            foreach (BehaviorTaskAttachment missing in attachments.Rows.Where(
                         row => row.Status == BehaviorTaskAttachmentStatus.Missing))
            {
                gaps.Add("inventory:behavior-task-attachment:missing:"
                    + missing.TypeName + ":" + missing.Reason);
            }
            foreach (string unexpected in attachments.UnexpectedAttachedTypes)
            {
                gaps.Add("inventory:behavior-task-attachment:unexpected-live:"
                    + unexpected);
            }
        }
        AddInventoryDiff(gaps, CharacterAiCoverageSourceInventory.CompareExact(
            CharacterAiCoverageSourceInventory.ExpectedJobGiverTypes,
            inventory.JobGiverTypes,
            "job-giver"));
        AddInventoryDiff(gaps, CharacterAiCoverageSourceInventory.CompareExternalIntentCallsites(
            inventory.ExternalIntentCallsites));
        AddInventoryDiff(gaps, CharacterAiCoverageSourceInventory.CompareExact(
            inventory.Domains.Select(domain => domain.Id),
            Domains.Select(domain => domain.Id),
            "domain-registry"));
        foreach (DomainSurface domain in inventory.Domains)
        {
            if (!HasTypeNamed(domain.AuthorityType))
                gaps.Add("inventory:domain-authority-type:missing:" + domain.Id + ":" + domain.AuthorityType);
        }
        foreach (string verifierType in OffenseFreshnessVerifierTypes)
        {
            string[] transitivePaths = CharacterAiCoverageSourceInventory
                .CaptureEvidenceTransitiveSourcePaths(verifierType);
            string[] expectedTransitivePaths = string.Equals(
                    verifierType,
                    "OffenseJourneyPlayModeFacade",
                    StringComparison.Ordinal)
                ? OffenseJourneyFreshnessDependencyPaths
                : OffenseFreshnessDependencyPaths;
            AddInventoryDiff(gaps, CharacterAiCoverageSourceInventory.CompareExact(
                expectedTransitivePaths,
                transitivePaths,
                "offense-evidence-transitive-source:" + verifierType));
            foreach (string dependencyPath in transitivePaths)
            {
                if (!File.Exists(dependencyPath))
                {
                    gaps.Add("inventory:offense-evidence-transitive-path:missing:"
                        + verifierType + ":" + dependencyPath);
                }
            }
        }

        report.AppendLine();
        report.AppendLine("## Source-derived exact inventory");
        report.AppendLine("authoredActionAssets=" + inventory.AuthoredActionAssetPaths.Length
            + "; concreteRuntimeActionTypes=" + inventory.ConcreteActionTypes.Length
            + "; deprivationLogical=" + CharacterAiCoverageSourceInventory.ExpectedDeprivationActionTypes.Length
            + "; branches=" + inventory.Branches.Length
            + "; behaviorOperations=" + inventory.BehaviorOperations.Length
            + "; behaviorTasks=" + inventory.BehaviorTaskTypes.Length
            + "; behaviorTasksLiveAttached=" + attachments.Rows.Count(
                row => row.Status == BehaviorTaskAttachmentStatus.LiveAttached)
            + "; behaviorTasksDormant=" + attachments.Rows.Count(
                row => row.Status == BehaviorTaskAttachmentStatus.DormantLegacy)
            + "; behaviorTasksMissing=" + attachments.Rows.Count(
                row => row.Status == BehaviorTaskAttachmentStatus.Missing)
            + "; jobGivers=" + inventory.JobGiverTypes.Length
            + "; externalIntentCallsites=" + inventory.ExternalIntentCallsites.Length
            + "; domains=" + inventory.Domains.Length);
        report.AppendLine("authored=" + string.Join(",", inventory.AuthoredActionAssetPaths));
        report.AppendLine("runtimeTypes=" + string.Join(",", inventory.ConcreteActionTypes));
        report.AppendLine("branches=" + string.Join(",", inventory.Branches));
        report.AppendLine("behaviorOperations=" + string.Join(",", inventory.BehaviorOperations));
        report.AppendLine("behaviorTasks=" + string.Join(",", inventory.BehaviorTaskTypes));
        report.AppendLine("externalBehavior=" + attachments.ExternalBehaviorPath
            + "; resolved=" + attachments.Resolved
            + (string.IsNullOrWhiteSpace(attachments.FailureReason)
                ? string.Empty
                : "; failure=" + attachments.FailureReason));
        report.AppendLine("behaviorTaskAttachmentStatus | taskType | evidenceAuthority");
        foreach (BehaviorTaskAttachment attachment in attachments.Rows)
        {
            report.Append(attachment.Status).Append(" | ")
                .Append(attachment.TypeName).Append(" | ")
                .AppendLine(attachment.Reason);
        }
        report.AppendLine("jobGivers=" + string.Join(",", inventory.JobGiverTypes));
        foreach (ExternalIntentCallsite callsite in inventory.ExternalIntentCallsites)
            report.AppendLine("externalIntent=" + callsite.OwnerId + " | " + callsite.Path + ":" + callsite.Line);
        foreach (DomainSurface domain in inventory.Domains)
            report.AppendLine("domain=" + domain.Id + " | authority=" + domain.AuthorityType);
        foreach (string verifierType in OffenseFreshnessVerifierTypes)
        {
            report.AppendLine("evidenceTransitiveFreshness=" + verifierType
                + " | sources=" + string.Join(",", CharacterAiCoverageSourceInventory
                    .CaptureEvidenceTransitiveSourcePaths(verifierType)));
        }
    }

    private static void AddInventoryDiff(ICollection<string> gaps, IEnumerable<string> differences)
    {
        foreach (string difference in differences)
            gaps.Add("inventory:" + difference);
    }

    private static bool HasTypeNamed(string simpleName)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(GetLoadableTypes)
            .Any(type => string.Equals(type.Name, simpleName, StringComparison.Ordinal));
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(type => type != null);
        }
    }

    private static EvidenceResolution ResolveEvidence(Evidence evidence)
    {
        if (evidence.DeclaredStatus == EvidenceStatus.Missing)
            return new EvidenceResolution(EvidenceStatus.Missing, evidence, evidence.Axes);

        if (evidence.DeclaredStatus == EvidenceStatus.ContractOnly)
        {
            return new EvidenceResolution(
                EvidenceStatus.ContractOnly,
                evidence,
                evidence.Axes + (HasEntrypoint(evidence) ? " (contract entrypoint present)" : " (contract entrypoint missing)"));
        }

        if (string.Equals(
                evidence.TypeName,
                "DailyRoutineWuPlayModeVerifier",
                StringComparison.Ordinal))
        {
            return ResolveDailyThreeSeedEvidence(evidence);
        }

        EvidenceArtifact artifact = ResolveEvidenceArtifact(evidence);
        // ArtifactPassMarker narrows a claim to one row. It must never replace
        // the verifier's aggregate PASS marker, otherwise one green WorkType
        // row could promote evidence from an otherwise failing matrix run.
        string requiredMarker = artifact.PassMarker;
        string[] requiredScopeMarkers = ResolveRequiredScopeMarkers(evidence);
        EvidenceSourceFreshness source = ResolveSourceFreshness(evidence.TypeName);
        bool artifactExists = !string.IsNullOrEmpty(artifact.Path)
            && File.Exists(artifact.Path);
        DateTime artifactWriteUtc = artifactExists
            ? File.GetLastWriteTimeUtc(artifact.Path)
            : DateTime.MinValue;
        bool artifactFresh = source.Resolved
            && artifactExists
            && artifactWriteUtc >= source.LatestWriteUtc;
        bool entrypointExists = HasEntrypoint(evidence);
        if (!string.IsNullOrEmpty(artifact.Path)
            && artifactFresh
            && entrypointExists
            && ArtifactPasses(artifact.Path, requiredMarker)
            && requiredScopeMarkers.Length > 0
            && ArtifactContainsAll(artifact.Path, requiredScopeMarkers))
        {
            return new EvidenceResolution(
                EvidenceStatus.LiveExecuted,
                evidence,
                artifact.Path + " contains " + requiredMarker
                + "; scope=" + string.Join("+", requiredScopeMarkers)
                + "; artifactUtc=" + artifactWriteUtc.ToString("O")
                + "; sourceUtc=" + source.LatestWriteUtc.ToString("O")
                + "; latest=" + source.LatestSourcePath
                + "; verifier=" + source.VerifierSourcePath
                + "; sourceCount=" + source.SourceCount);
        }

        if (entrypointExists)
        {
            string freshness = !source.Resolved
                ? " (source freshness unresolved: " + source.FailureReason + ")"
                : !artifactExists
                    ? " (entrypoint only; durable artifact missing)"
                    : !artifactFresh
                        ? " (artifact stale: artifactUtc="
                          + artifactWriteUtc.ToString("O")
                          + "; sourceUtc=" + source.LatestWriteUtc.ToString("O")
                          + "; latest=" + source.LatestSourcePath + ")"
                        : !ArtifactPasses(artifact.Path, requiredMarker)
                            ? " (artifact lacks PASS marker '" + requiredMarker + "')"
                            : requiredScopeMarkers.Length == 0
                                ? " (artifact has no exact selector/row marker contract for this claim)"
                                : " (artifact lacks scope marker(s): "
                                  + string.Join(",", requiredScopeMarkers) + ")";
            return new EvidenceResolution(EvidenceStatus.ContractOnly, evidence, evidence.Axes + freshness);
        }

        return new EvidenceResolution(EvidenceStatus.Missing, evidence, evidence.Axes + " (entrypoint/artifact missing)");
    }

    private static EvidenceResolution ResolveDailyThreeSeedEvidence(
        Evidence evidence)
    {
        int[] seeds = { 157181, 157182, 157183 };
        EvidenceSourceFreshness source = ResolveSourceFreshness(evidence.TypeName);
        bool entrypointExists = HasEntrypoint(evidence);
        List<string> failures = new List<string>();
        List<string> successes = new List<string>();

        if (!entrypointExists)
            failures.Add("entrypoint missing");
        if (!source.Resolved)
            failures.Add("source freshness unresolved: " + source.FailureReason);

        foreach (int seed in seeds)
        {
            string path = "Artifacts/QA/phase157-daily-routine-wu-seed-"
                + seed + ".txt";
            if (!File.Exists(path))
            {
                failures.Add("seed " + seed + " artifact missing");
                continue;
            }

            DateTime artifactWriteUtc = File.GetLastWriteTimeUtc(path);
            if (!source.Resolved || artifactWriteUtc < source.LatestWriteUtc)
            {
                failures.Add("seed " + seed + " stale: artifactUtc="
                    + artifactWriteUtc.ToString("O") + "; sourceUtc="
                    + source.LatestWriteUtc.ToString("O"));
                continue;
            }

            string[] markers =
            {
                "observedDays=5",
                "runSeed=" + seed,
                "runtimeDiagnosticsGate=ai-runtime-gate-v3",
                "RESULT=PASS;"
            };
            if (!ArtifactContainsAll(path, markers))
            {
                failures.Add("seed " + seed + " lacks compound marker(s): "
                    + string.Join(",", markers));
                continue;
            }

            successes.Add(seed + "@" + artifactWriteUtc.ToString("O"));
        }

        if (failures.Count == 0 && successes.Count == seeds.Length)
        {
            return new EvidenceResolution(
                EvidenceStatus.LiveExecuted,
                evidence,
                "three-seed compound PASS; seeds=" + string.Join(",", successes)
                + "; sourceUtc=" + source.LatestWriteUtc.ToString("O")
                + "; latest=" + source.LatestSourcePath
                + "; verifier=" + source.VerifierSourcePath
                + "; sourceCount=" + source.SourceCount);
        }

        return new EvidenceResolution(
            entrypointExists ? EvidenceStatus.ContractOnly : EvidenceStatus.Missing,
            evidence,
            evidence.Axes + " (three-seed compound incomplete: "
            + string.Join("; ", failures) + ")");
    }

    private static EvidenceSourceFreshness ResolveSourceFreshness(
        string verifierTypeName)
    {
        evidenceFreshnessByType ??=
            new Dictionary<string, EvidenceSourceFreshness>(StringComparer.Ordinal);
        string key = verifierTypeName ?? string.Empty;
        if (!evidenceFreshnessByType.TryGetValue(
                key,
                out EvidenceSourceFreshness freshness))
        {
            freshness = CharacterAiCoverageSourceInventory
                .CaptureEvidenceSourceFreshness(key);
            evidenceFreshnessByType.Add(key, freshness);
        }
        return freshness;
    }

    private static EvidenceArtifact ResolveEvidenceArtifact(Evidence evidence)
    {
        if (string.Equals(
                evidence.TypeName,
                "CharacterAiFaultRecoveryPlayModeVerifier",
                StringComparison.Ordinal))
        {
            string selector = ResolveFaultRecoverySelector(evidence);
            return A(
                evidence.TypeName,
                CharacterAiFaultRecoveryPlayModeVerifier
                    .GetReportPathForSelector(selector),
                "RESULT=PASS");
        }

        if (string.Equals(
                evidence.TypeName,
                "PrimitiveStartSurvivalPlayModeVerifier",
                StringComparison.Ordinal)
            && string.Equals(
                evidence.MethodName,
                "RunFocusedFromMenu",
                StringComparison.Ordinal))
        {
            return A(
                evidence.TypeName,
                PrimitiveStartSurvivalPlayModeVerifier.FocusedReportPath,
                "FIRST_LINE=PASS");
        }

        return EvidenceArtifacts.FirstOrDefault(value =>
            string.Equals(
                value.TypeName,
                evidence.TypeName,
                StringComparison.Ordinal));
    }

    private static bool ArtifactPasses(string artifactPath, string passMarker)
    {
        if (string.IsNullOrEmpty(artifactPath) || string.IsNullOrEmpty(passMarker))
            return false;

        if (string.Equals(passMarker, "FIRST_LINE=PASS", StringComparison.Ordinal))
        {
            return string.Equals(
                File.ReadLines(artifactPath).FirstOrDefault()?.Trim(),
                "PASS",
                StringComparison.Ordinal);
        }

        return File.ReadAllText(artifactPath)
            .IndexOf(passMarker, StringComparison.Ordinal) >= 0;
    }

    private static bool ArtifactContainsAll(
        string artifactPath,
        IReadOnlyCollection<string> markers)
    {
        if (string.IsNullOrEmpty(artifactPath)
            || markers == null
            || markers.Count == 0)
        {
            return false;
        }

        string text = File.ReadAllText(artifactPath);
        return markers.All(marker => !string.IsNullOrWhiteSpace(marker)
            && text.IndexOf(marker, StringComparison.Ordinal) >= 0);
    }

    private static string[] ResolveRequiredScopeMarkers(Evidence evidence)
    {
        string type = evidence.TypeName ?? string.Empty;
        string axes = evidence.Axes ?? string.Empty;
        if (type == "CharacterAiFaultRecoveryPlayModeVerifier")
        {
            string selector = ResolveFaultRecoverySelector(evidence);
            IReadOnlyList<string> expectedRows =
                CharacterAiFaultRecoveryPlayModeVerifier
                    .GetExpectedRowsForSelector(selector);
            if (expectedRows.Count == 0)
                return Array.Empty<string>();

            string rows = string.Join(",", expectedRows);
            return new[]
            {
                "selector=" + selector,
                "verifierRevision="
                    + CharacterAiFaultRecoveryPlayModeVerifier.VerifierRevision,
                "expectedRows=" + rows,
                "startedRowIds=" + rows,
                "completedRowIds=" + rows,
                "exactRows=True",
                "result=PASS"
            };
        }

        if (type == "CharacterAiCrossActionFaultPlayModeVerifier")
        {
            if (axes.IndexOf("haul", StringComparison.OrdinalIgnoreCase) >= 0
                || axes.IndexOf("source", StringComparison.OrdinalIgnoreCase) >= 0)
                return new[] { "rows=haul-source-despawn,haul-source-shrink,haul-destination-destroy" };
            if (axes.IndexOf("patient", StringComparison.OrdinalIgnoreCase) >= 0
                || axes.IndexOf("medicine", StringComparison.OrdinalIgnoreCase) >= 0)
                return new[] { "rescue-patient-despawn", "rescue-medicine-loss" };
            if (axes.IndexOf("hunt", StringComparison.OrdinalIgnoreCase) >= 0
                || axes.IndexOf("target", StringComparison.OrdinalIgnoreCase) >= 0)
                return new[] { "hunt-hunter-downed", "hunt-prey-despawn" };
        }

        if (type == "CharacterConsumableActionFaultPlayModeVerifier")
        {
            if (axes.IndexOf("drink", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new[]
                {
                    "authority=Brain/BT -> AIDrink/AIEat/AISubstanceUse -> production ability/runtime",
                    "PASS\tDRINK_SOURCE_LOSS_RUNNING_LEASE\t",
                    "PASS\tDRINK_SOURCE_LOSS_TYPED_FAILED\t",
                    "PASS\tDRINK_SOURCE_LOSS_IMMEDIATE_REPLAN\t",
                    "PASS\tDRINK_SOURCE_LOSS_TARGET_LEASE_CLEAN\t",
                    "PASS\tDRINK_SOURCE_LOSS_FOREIGN_LEASE_PRESERVED\t",
                    "PASS\tDRINK_LEASE_INVALID_RUNNING_LEASE\t",
                    "PASS\tDRINK_LEASE_INVALID_TYPED_FAILED\t",
                    "PASS\tDRINK_LEASE_INVALID_IMMEDIATE_REPLAN\t",
                    "PASS\tDRINK_LEASE_INVALID_TARGET_LEASE_CLEAN\t",
                    "PASS\tDRINK_LEASE_INVALID_FOREIGN_LEASE_PRESERVED\t"
                };
            }

            if (axes.IndexOf("eat", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new[]
                {
                    "authority=Brain/BT -> AIDrink/AIEat/AISubstanceUse -> production ability/runtime",
                    "PASS\tEAT_SOURCE_LOSS_RUNNING_MEAL_PLAN\t",
                    "PASS\tEAT_SOURCE_LOSS_TYPED_FAILED\t",
                    "PASS\tEAT_SOURCE_LOSS_IMMEDIATE_REPLAN\t",
                    "PASS\tEAT_SOURCE_LOSS_FOREIGN_LEASE_PRESERVED\t",
                    "PASS\tEAT_SOURCE_LOSS_MEAL_SEAT_PLAN_CLEAN\t",
                    "PASS\tEAT_SPOIL_RUNNING_MEAL_PLAN\t",
                    "PASS\tEAT_SPOIL_TYPED_FAILED\t",
                    "PASS\tEAT_SPOIL_IMMEDIATE_REPLAN\t",
                    "PASS\tEAT_SPOIL_FOREIGN_LEASE_PRESERVED\t",
                    "PASS\tEAT_SPOIL_MEAL_SEAT_PLAN_CLEAN\t",
                    "PASS\tEAT_LEASE_INVALID_RUNNING_MEAL_PLAN\t",
                    "PASS\tEAT_LEASE_INVALID_TYPED_FAILED\t",
                    "PASS\tEAT_LEASE_INVALID_IMMEDIATE_REPLAN\t",
                    "PASS\tEAT_LEASE_INVALID_FOREIGN_LEASE_PRESERVED\t",
                    "PASS\tEAT_LEASE_INVALID_MEAL_SEAT_PLAN_CLEAN\t"
                };
            }

            if (axes.IndexOf("substance", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new[]
                {
                    "authority=Brain/BT -> AIDrink/AIEat/AISubstanceUse -> production ability/runtime",
                    "PASS\tSUBSTANCE_SOURCE_LOSS_RUNNING_LEASE\t",
                    "PASS\tSUBSTANCE_SOURCE_LOSS_TYPED_FAILED\t",
                    "PASS\tSUBSTANCE_SOURCE_LOSS_IMMEDIATE_REPLAN\t",
                    "PASS\tSUBSTANCE_SOURCE_LOSS_TARGET_LEASE_CLEAN\t",
                    "PASS\tSUBSTANCE_SOURCE_LOSS_FOREIGN_LEASE_PRESERVED\t",
                    "PASS\tSUBSTANCE_LEASE_INVALID_RUNNING_LEASE\t",
                    "PASS\tSUBSTANCE_LEASE_INVALID_TYPED_FAILED\t",
                    "PASS\tSUBSTANCE_LEASE_INVALID_IMMEDIATE_REPLAN\t",
                    "PASS\tSUBSTANCE_LEASE_INVALID_TARGET_LEASE_CLEAN\t",
                    "PASS\tSUBSTANCE_LEASE_INVALID_FOREIGN_LEASE_PRESERVED\t"
                };
            }
        }

        if (type == "CharacterAiSelfCarePlayModeVerifier")
        {
            if (axes.IndexOf("substance", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new[]
                {
                    "SUBSTANCE_BT_SELECTED\tPASS\t",
                    "SUBSTANCE_ABILITY_RAN\tPASS\t",
                    "SUBSTANCE_PHYSICAL_EXACTLY_ONCE\tPASS\t",
                    "SUBSTANCE_TYPED_COMPLETION\tPASS\t",
                    "SUBSTANCE_NO_OWNERSHIP_LEAK\tPASS\t",
                    "SUBSTANCE_NO_INVARIANT_ANOMALY\tPASS\t",
                    "CONSOLE_WARNING_ERROR_ZERO\tPASS\t"
                };
            }

            return new[]
            {
                "DRINK_SELECTED_ACTION_EPOCH_OWNED\tPASS\t",
                "DRINK_NO_EXTERNAL_OWNER_OVERLAP\tPASS\t",
                "DRINK_RESTORED_THIRST\tPASS\t",
                "DRINK_PHYSICAL_EXACTLY_ONCE\tPASS\t",
                "DRINK_LIFECYCLE_CONSERVED\tPASS\t",
                "DRINK_NO_INVARIANT_ANOMALY\tPASS\t",
                "CONSOLE_WARNING_ERROR_ZERO\tPASS\t"
            };
        }

        if (type == "DungeonAiActionSaveLoadPlayModeVerifier")
        {
            return new[]
            {
                "PASS movement is still live at the save boundary",
                "PASS mid-action slot restored",
                "PASS old actor instance retired",
                "PASS transient movement coroutine was not serialized or rebound",
                "PASS transient external AI intent was not serialized or rebound",
                "PASS replacement actor acquired one fresh execution owner",
                "PASS replacement actor produced typed gameplay progress",
                "PASS post-restore AI invariants remain clean",
                "PASS HAUL_SAVE_FIXTURE_AUTHORITIES_READY",
                "PASS HAUL_SAVE_AUTHORED_FIXTURE_DEFINITIONS_READY",
                "PASS HAUL_SAVE_REACHABLE_FIXTURE_POSITIONS_READY",
                "PASS HAUL_SAVE_WAREHOUSE_REGISTERED",
                "PASS HAUL_SAVE_CONSTRUCTION_SITE_REGISTERED",
                "PASS HAUL_SAVE_CONSTRUCTION_ORDER_CREATED;",
                "PASS HAUL_SAVE_CONSTRUCTION_AUTHORITY_PUBLISHED;",
                "PASS HAUL_SAVE_CONSTRUCTION_MATERIAL_ROUTED_EXACT;",
                "PASS HAUL_SAVE_EXACT_CONSTRUCTION_CANDIDATE_ISOLATED;",
                "PASS HAUL_SAVE_PRODUCTION_AI_ACTION_READY",
                "PASS HAUL_SAVE_MID_PICKUP_COMMITTED;",
                "PASS HAUL_SAVE_MID_PICKUP_DESTINATION_EXACT;",
                "PASS HAUL_SAVE_V18_CHARACTER_PAYLOAD_EXACT;",
                "PASS HAUL_SAVE_V18_SLOT_CAPTURED_AT_COMMITTED_PICKUP",
                "PASS HAUL_SAVE_NEGATIVE_DESTINATION_ID_FIXTURE_READY;",
                "PASS HAUL_SAVE_NEGATIVE_DESTINATION_ID_WHOLE_RESTORE_REJECTED;",
                "PASS HAUL_SAVE_NEGATIVE_DESTINATION_ID_ATOMIC_ROLLBACK_UNCHANGED;",
                "PASS HAUL_SAVE_NEGATIVE_DESTINATION_KIND_FIXTURE_READY;",
                "PASS HAUL_SAVE_NEGATIVE_DESTINATION_KIND_WHOLE_RESTORE_REJECTED;",
                "PASS HAUL_SAVE_NEGATIVE_DESTINATION_KIND_ATOMIC_ROLLBACK_UNCHANGED;",
                "PASS HAUL_SAVE_NEGATIVE_DELIVERY_POSITION_FIXTURE_READY;",
                "PASS HAUL_SAVE_NEGATIVE_DELIVERY_POSITION_WHOLE_RESTORE_REJECTED;",
                "PASS HAUL_SAVE_NEGATIVE_DELIVERY_POSITION_ATOMIC_ROLLBACK_UNCHANGED;",
                "PASS HAUL_SAVE_NEGATIVE_DROP_POSITION_FIXTURE_READY;",
                "PASS HAUL_SAVE_NEGATIVE_DROP_POSITION_WHOLE_RESTORE_REJECTED;",
                "PASS HAUL_SAVE_NEGATIVE_DROP_POSITION_ATOMIC_ROLLBACK_UNCHANGED;",
                "PASS HAUL_SAVE_NEGATIVE_MISSING_FACILITY_BUFFER_AUTHORITY_FIXTURE_READY;",
                "PASS HAUL_SAVE_NEGATIVE_MISSING_FACILITY_BUFFER_AUTHORITY_WHOLE_RESTORE_REJECTED;",
                "PASS HAUL_SAVE_NEGATIVE_MISSING_FACILITY_BUFFER_AUTHORITY_ATOMIC_ROLLBACK_UNCHANGED;",
                "PASS HAUL_SAVE_RESTORE_1_SUCCEEDED;",
                "PASS HAUL_SAVE_RESTORE_1_INTENT_BOUND_BEFORE_AI_WAKE;",
                "PASS HAUL_SAVE_RESTORE_1_DESTINATION_QUANTITY_EXACT;",
                "PASS HAUL_SAVE_RESTORE_1_INERT_BEFORE_AI_WAKE;",
                "PASS HAUL_SAVE_RESTORE_1_PHYSICAL_QUANTITIES_UNCHANGED_BEFORE_AI_WAKE;",
                "PASS HAUL_SAVE_RESTORE_1_CONSTRUCTION_SITE_REBOUND",
                "PASS HAUL_SAVE_RESTORE_1_DUPLICATE_REQUEST_ZERO;",
                "PASS HAUL_SAVE_RESTORE_1_AI_HAUL_ACTION_READY",
                "PASS HAUL_SAVE_RESTORE_1_NO_ACTION_BEFORE_AI_WAKE",
                "PASS HAUL_SAVE_RESTORE_1_DELIVERY_COMPLETED;",
                "PASS HAUL_SAVE_RESTORE_1_BRAIN_AIHAUL_EXACT_ONCE;",
                "PASS HAUL_SAVE_RESTORE_1_CONSERVATION_EXACT;",
                "PASS HAUL_SAVE_RESTORE_2_SUCCEEDED;",
                "PASS HAUL_SAVE_RESTORE_2_INTENT_BOUND_BEFORE_AI_WAKE;",
                "PASS HAUL_SAVE_RESTORE_2_DESTINATION_QUANTITY_EXACT;",
                "PASS HAUL_SAVE_RESTORE_2_INERT_BEFORE_AI_WAKE;",
                "PASS HAUL_SAVE_RESTORE_2_PHYSICAL_QUANTITIES_UNCHANGED_BEFORE_AI_WAKE;",
                "PASS HAUL_SAVE_RESTORE_2_CONSTRUCTION_SITE_REBOUND",
                "PASS HAUL_SAVE_RESTORE_2_DUPLICATE_REQUEST_ZERO;",
                "PASS HAUL_SAVE_RESTORE_2_AI_HAUL_ACTION_READY",
                "PASS HAUL_SAVE_RESTORE_2_NO_ACTION_BEFORE_AI_WAKE",
                "PASS HAUL_SAVE_RESTORE_2_DELIVERY_COMPLETED;",
                "PASS HAUL_SAVE_RESTORE_2_BRAIN_AIHAUL_EXACT_ONCE;",
                "PASS HAUL_SAVE_RESTORE_2_CONSERVATION_EXACT;",
                "PASS HAUL_SAVE_REPEATED_RESTORE_CONSERVATION_EXACT;",
                "PASS no unexpected Error/Exception/Assert logs"
            };
        }

        if (type == "PhysicalItemLogisticsPlayModeVerifier")
        {
            return new[]
            {
                "[PASS] WAREHOUSE_MASS_ADMISSION_PRODUCTION_INGRESS_COMMITTED ",
                "[PASS] WAREHOUSE_MASS_UI_PRODUCTION_EXACT_KG ",
                "[PASS] WAREHOUSE_NONEMPTY_DEMOLITION_REJECTED ",
                "[PASS] WAREHOUSE_NONEMPTY_RELOCATION_REJECTED ",
                "[PASS] WAREHOUSE_EMPTY_LIFECYCLE_GATE_OPEN ",
                "[PASS] WAREHOUSE_RESTORE_INVALID_DESTINATION_ATOMIC ",
                "[PASS] WAREHOUSE_RESTORE_POSITION_MISMATCH_ATOMIC ",
                "[PASS] WAREHOUSE_RESTORE_OVER_CAPACITY_PRESERVED ",
                "[PASS] WAREHOUSE_RESTORE_OVER_CAPACITY_ADMISSION_BLOCKED ",
                "[PASS] WAREHOUSE_RESTORE_OFFICIAL_FULL_ROUNDTRIP ",
                "[PASS] WAREHOUSE_RESTORE_OFFICIAL_OVER_CAPACITY_PRESERVED ",
                "[PASS] WAREHOUSE_RESTORE_EVACUATION_PUBLISHED_AFTER_ROOT_SWAP ",
                "[PASS] WAREHOUSE_RESTORE_EVACUATION_CLEANUP_EXACT ",
                "[PASS] WAREHOUSE_EVACUATION_TARGET_READY ",
                "[PASS] WAREHOUSE_EVACUATION_LIVE_FIXTURE_READY ",
                "[PASS] WAREHOUSE_EVACUATION_AI_HAUL_COMPLETED ",
                "[PASS] WAREHOUSE_EVACUATION_GRAM_TOKEN_CONSERVATION_EXACT ",
                "[PASS] AI_HAUL_CAN_START_WAREHOUSE ",
                "[PASS] AI_HAUL_DEPOSITED_TO_WAREHOUSE ",
                "[PASS] COMBAT_EQUIPMENT_STATEFUL_WAREHOUSE_MASS_EXACT ",
                "[PASS] FACILITY_REQUEST_RESERVED_IN_STORAGE ",
                "[PASS] AI_HAUL_CAN_START_FACILITY ",
                "[PASS] AI_HAUL_DEPOSITED_TO_FACILITY_BUFFER ",
                "[PASS] FACILITY_STOCK_WITHDRAWN_ON_PICKUP ",
                "[PASS] CONSTRUCTION_READY_AFTER_PHYSICAL_DELIVERY ",
                "[PASS] MATERIAL_REPAIR_DESTINATION_CLAIM_EXACT ",
                "[PASS] MATERIAL_REPAIR_HAUL_PLAN_PREFLIGHT ",
                "[PASS] MATERIAL_REPAIR_INPUTS_DELIVERED ",
                "[PASS] MATERIAL_REPAIR_NO_DUPLICATE_REQUEST ",
                "[PASS] MATERIAL_REPAIR_PRESERVES_INSTANCE_AND_MATERIAL ",
                "[PASS] MATERIAL_REPAIR_DESTINATION_CLAIM_REVOKED_AFTER_COMPLETE ",
                "[PASS] MATERIAL_SALVAGE_RETURNS_ORIGINAL_MATERIAL ",
                "[PASS] EXPEDITION_SUPPLY_DELIVERY_COMMITTED ",
                "[PASS] EXPEDITION_RESERVED_TARGET_CLAIM_EXACT ",
                "[PASS] EXPEDITION_SUPPLIES_PACKED ",
                "[PASS] EXPEDITION_PACKED_STACK_VISIBLE ",
                "[PASS] EXPEDITION_REPEATED_READY_POLL_NO_DUPLICATE ",
                "[PASS] EXPEDITION_PACKED_STACK_CONSUME_COMMITTED ",
                "[PASS] EXPEDITION_PACKED_STACK_CONSUMED ",
                "[PASS] EXPEDITION_RESERVED_TARGET_CLAIM_REVOKED_AFTER_CONSUME ",
                "[PASS] EXPEDITION_SUPPLY_CONSUME_QUANTITY_CONSERVED ",
                "[PASS] EXPEDITION_CANCEL_RELEASE_CONSERVED ",
                "[PASS] EXPEDITION_UNKNOWN_OR_DUPLICATE_RETURN_NO_MINT ",
                "RESULT=PASS; failures=0"
            };
        }

        if (type == "WildlifeAiHuntPlayModeVerifier")
        {
            return new[]
            {
                "HUNT_ACTION_STARTED\tPASS\t",
                "HUNT_APPLIED_DAMAGE\tPASS\t",
                "HUNT_TARGET_KILLED\tPASS\t",
                "HUNT_CARCASS_EXACTLY_ONCE\tPASS\t",
                "HUNT_TERMINAL_ONCE\tPASS\t",
                "HUNT_COMPLETED_TERMINAL\tPASS\t",
                "HUNT_NO_RUNTIME_OWNERSHIP_LEAK\tPASS\t",
                "HUNT_NO_INVARIANT_ANOMALY\tPASS\t"
            };
        }

        if (type == "CharacterAiAutonomousMedicalPlayModeVerifier")
        {
            return new[]
            {
                "RESCUE_BT_SELECTED\tPASS\t",
                "RESCUE_ABILITY_RAN\tPASS\t",
                "AUTONOMOUS_STABILIZATION\tPASS\t",
                "AUTONOMOUS_PHYSICAL_CARRY\tPASS\t",
                "AUTONOMOUS_BED_TREATMENT\tPASS\t",
                "AUTONOMOUS_RECOVERY\tPASS\t",
                "RESCUE_TYPED_COMPLETION\tPASS\t",
                "RESCUE_NO_RUNTIME_OWNERSHIP_LEAK\tPASS\t",
                "RESCUE_NO_INVARIANT_ANOMALY\tPASS\t"
            };
        }

        if (type == "SurgeryPlayModeVerifier")
        {
            return new[]
            {
                "[PASS] SURGERY_SCHEDULED_BY_POINTER ",
                "[PASS] SURGERY_MATERIAL_DESTINATION_CLAIM_EXACT ",
                "[PASS] SURGERY_COMPLETED_BY_WORK_AI ",
                "[PASS] SURGERY_MATERIAL_HAUL_PLAN_PREFLIGHT ",
                "[PASS] SURGERY_MATERIALS_DELIVERED_BY_AI_HAUL ",
                "[PASS] SURGERY_REPEATED_MATERIAL_POLL_NO_DUPLICATE ",
                "[PASS] SURGERY_MATERIAL_FLOW ",
                "[PASS] SURGERY_PHYSICAL_MEDICINE_CONSUMED ",
                "[PASS] SURGERY_PHYSICAL_PROCESS_WATER_CONSUMED ",
                "[PASS] SURGERY_WORK_ACCUMULATED ",
                "[PASS] SURGERY_STAGES_OBSERVED ",
                "[PASS] SURGERY_PATIENT_RECOVERED ",
                "[PASS] SURGERY_MATERIAL_DESTINATION_CLAIM_REVOKED_AFTER_COMPLETE ",
                "[PASS] SURGERY_CANCEL_ORDER_CREATED ",
                "[PASS] SURGERY_CANCEL_RELEASE_CONSERVED ",
                "[PASS] SURGERY_CANCEL_CLAIM_REVOKED ",
                "RESULT=PASS; failures=0"
            };
        }

        if (type == "CaptivityAiPlayModeVerifier")
        {
            if (axes.IndexOf("recapture", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new[]
                {
                    "PASS\tRECAPTURE_RESTRAINT\t",
                    "PASS\tRECAPTURE_RESERVATION\t",
                    "PASS\tRECAPTURE_LIVE\t",
                    "PASS\tRECAPTURE_CANCEL_TERMINAL\t",
                    "PASS\tFIXTURE_RESTRAINT_RESTORE\t",
                    "PASS\tFIXTURE_MOVEMENT_RESTORE\t"
                };
            }

            if (axes.IndexOf("escort", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new[]
                {
                    "PASS\tCAPTURE_RESTRAINT\t",
                    "PASS\tCAPTURE_COMMAND\t",
                    "PASS\tCAPTURE_TERMINAL\t",
                    "PASS\tFIXTURE_MOVEMENT_RESTORE\t"
                };
            }

            return new[]
            {
                "PASS\tWARDEN_AI_ASSIGNMENT\t",
                "PASS\tWARDEN_AI_START\t",
                "PASS\tWARDEN_PROGRESS\t",
                "PASS\tWARDEN_TERMINAL\t",
                "PASS\tFIXTURE_RESTRAINT_RESTORE\t",
                "PASS\tFIXTURE_MOVEMENT_RESTORE\t"
            };
        }

        if (type == "CaptivityWildlifeLifecyclePlayModeVerifier")
        {
            if (axes.IndexOf("capture transport", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new[]
                {
                    "PASS\tTRANSPORT_SUCCESS_STARTED\t",
                    "PASS\tTRANSPORT_SUCCESS_TERMINAL\t",
                    "PASS\tTRANSPORT_SOURCE_LOSS_TERMINAL\t",
                    "PASS\tTRANSPORT_NOPATH_TERMINAL\t",
                    "PASS\tTRANSPORT_DOWNED_TERMINAL\t",
                    "PASS\tTRANSPORT_PEN_DESTROY_TERMINAL\t",
                    "PASS\tTRANSPORT_ALERT_EXTERNAL_LEASE_RED_BOUND\t",
                    "PASS\tTRANSPORT_ALERT_NO_GUARD_OVERLAP\t",
                    "PASS\tTRANSPORT_ALERT_CARRIER_DOWNED_EXACT_TERMINAL\t",
                    "PASS\tTRANSPORT_ALERT_ROLLBACK_PHYSICAL_CONVERGED\t",
                    "PASS\tTRANSPORT_ALERT_RESPONSE_GREEN_CONVERGED\t",
                    "PASS\tTRANSPORT_CONTENDER_INJECTED_AFTER_DESTINATION_RESOLVED\t",
                    "PASS\tTRANSPORT_CONTENDER_TYPED_ROLLBACK\t",
                    "PASS\tTRANSPORT_CONTENDER_PHYSICAL_CONSERVATION\t",
                    "PASS\tTRANSPORT_CONTENDER_NO_PENNED_DIVERGENCE\t",
                    "PASS\tFIXTURE_WILDLIFE_V18_RESTORE\t",
                    "PASS\tCONSOLE_WARNING_ERROR_ZERO\t"
                };
            }

            if (axes.IndexOf("animal-care", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new[]
                {
                    "PASS\tANIMAL_CARE_FEED_SOURCE_PHYSICAL\t",
                    "PASS\tANIMAL_CARE_FEED_SINK_EXACT\t",
                    "PASS\tANIMAL_CARE_FEED_OUTBOX_CLEAN\t",
                    "PASS\tANIMAL_CARE_FEED_SAVE_EXACT\t",
                    "PASS\tANIMAL_CARE_CANDIDATE_INDEX_READY\t",
                    "PASS\tANIMAL_CARE_BRAIN_AIWORK_STARTED\t",
                    "PASS\tANIMAL_CARE_PROGRESS\t",
                    "PASS\tANIMAL_CARE_DOWNED_EXACT_CLEANUP\t",
                    "PASS\tANIMAL_CARE_PEN_DESTROY_EXACT_CLEANUP\t",
                    "PASS\tFIXTURE_WILDLIFE_V18_RESTORE\t",
                    "PASS\tCONSOLE_WARNING_ERROR_ZERO\t"
                };
            }

            if (axes.IndexOf("escape", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new[]
                {
                    "PASS\tESCAPE_NOPATH_TERMINAL\t",
                    "PASS\tESCAPE_RUNNING_DOWNED_TERMINAL\t",
                    "PASS\tESCAPE_DISABLE_TERMINAL\t",
                    "PASS\tESCAPE_SUCCESS_STARTED\t",
                    "PASS\tESCAPE_SUCCESS_TERMINAL\t",
                    "PASS\tESCAPE_SUCCESS_EXIT_HANDOFF_TERMINAL\t",
                    "PASS\tCONSOLE_WARNING_ERROR_ZERO\t"
                };
            }
        }

        if (type == "CombatV14PlayModeVerifier")
        {
            return new[]
            {
                "COMBAT_COMMAND_LATE_PARTICIPANT=PASS",
                "POINTER_MULTI_SELECT=PASS",
                "POINTER_COMBAT_STANCE=PASS",
                "POINTER_RELOAD=PASS",
                "POINTER_RESCUE_COMMAND=PASS",
                "RESCUE_COMMAND_RELEASED=PASS",
                "FIELD_STABILIZATION=PASS",
                "PHYSICAL_RESCUE=PASS",
                "BED_TREATMENT=PASS",
                "RECOVERY_HYSTERESIS=PASS",
                "CONSOLE_ERRORS=PASS",
                "CONSOLE_WARNINGS=PASS"
            };
        }

        if (type == "CharacterAlarmResponsePlayModeVerifier")
        {
            return new[]
            {
                "INLINE_WORK_ACTION_SELECTED\tPASS\t",
                "INLINE_WORK_PARTIAL_PROGRESS\tPASS\t",
                "INLINE_PROGRESS_SAVE_ROUNDTRIP\tPASS\t",
                "INLINE_PROGRESS_RESUMED_NOT_RESTARTED\tPASS\t",
                "INLINE_WORK_COMPLETED_ONCE_AFTER_RESUME\tPASS\t",
                "WORK_SUSPENDED_AT_CHECKPOINT\tPASS\t",
                "PERSISTENT_PROGRESS_PRESERVED\tPASS\t",
                "RED_TO_AMBER_AFTER_TWO_HOURS\tPASS\t",
                "AMBER_TO_GREEN_AFTER_TWO_HOURS\tPASS\t",
                "ORIGINAL_WORK_RETURNED\tPASS\t",
                "DESTROYED_WORK_JOURNAL_ABANDONED\tPASS\t",
                "DESTROY_CASE_AI_INVARIANTS\tPASS\t"
            };
        }

        if (type == "PrimitiveStartSurvivalPlayModeVerifier"
            && string.Equals(
                evidence.MethodName,
                "RunFocusedFromMenu",
                StringComparison.Ordinal))
        {
            if (axes.IndexOf("bucket wash", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new[]
                {
                    "PASS FOCUSED_BUCKET_WASH_REGISTERED:",
                    "PASS FOCUSED_BUCKET_WASH_AI_ELIGIBLE:",
                    "PASS FOCUSED_BUCKET_WASH_COMPLETED:"
                };
            }

            if (axes.IndexOf("field meal", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new[]
                {
                    "PASS FOCUSED_FIELD_MEAL_REGISTERED:",
                    "PASS FOCUSED_FIELD_MEAL_AI_ELIGIBLE:",
                    "PASS FOCUSED_FIELD_MEAL_COMPLETED:",
                    "PASS FOCUSED_FIELD_MEAL_AUTHORITY:"
                };
            }

            if (axes.IndexOf("floor rest", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new[]
                {
                    "PASS FOCUSED_FLOOR_REST_REGISTERED:",
                    "PASS FOCUSED_FLOOR_REST_AI_ELIGIBLE:",
                    "PASS FOCUSED_FLOOR_REST_COMPLETED:"
                };
            }

            if (axes.IndexOf("latrine", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new[]
                {
                    "PASS FOCUSED_PRIMITIVE_LATRINE_REGISTERED:",
                    "PASS FOCUSED_PRIMITIVE_LATRINE_AI_ELIGIBLE:",
                    "PASS FOCUSED_PRIMITIVE_LATRINE_COMPLETED:"
                };
            }

            return Array.Empty<string>();
        }

        if (type == "FirstRunObjectivePlayModeVerifier")
        {
            return new[]
            {
                "[PASS] RESEARCH_ARCHIVE_READY ",
                "[PASS] BLUEPRINT_ARCHIVE_DESTINATION_CLAIM_EXACT ",
                "[PASS] BLUEPRINT_ARCHIVE_DELIVERY_ASSIGNED ",
                "[PASS] BLUEPRINT_HAUL_PLAN_READY ",
                "[PASS] BLUEPRINT_AI_HAUL_OWNERSHIP_OBSERVED ",
                "[PASS] BLUEPRINT_AI_HAUL_TO_ARCHIVE ",
                "[PASS] BLUEPRINT_AI_HAUL_OWNERSHIP_CLEAN ",
                "[PASS] BLUEPRINT_PROJECT_MAPPING ",
                "[PASS] RESEARCH_PRIORITY_COMMAND ",
                "[PASS] RESEARCH_AI_ACTION_AVAILABLE ",
                "[PASS] PROJECT_COMPLETED_BY_WORK_ROUTINE ",
                "[PASS] PHYSICAL_RESEARCH_FLOW_CAPTURE "
            };
        }

        if (type == "OffenseStrategicPlayModeVerifier")
        {
            return new[]
            {
                "PASS Strategic_PANEL_ACTIVE ",
                "PASS RESOLUTION_1600x900 ",
                "PASS PANEL_BOUNDS_1600x900 ",
                "PASS TEXT_OVERFLOW_1600x900 ",
                "PASS CAPTURE_CONTENT_1600x900 ",
                "PASS RESOLUTION_900x1600 ",
                "PASS PANEL_BOUNDS_900x1600 ",
                "PASS TEXT_OVERFLOW_900x1600 ",
                "PASS CAPTURE_CONTENT_900x1600 "
            };
        }

        if (type == "DefenseEngagementPlayModeVerifier")
        {
            return new[]
            {
                "authority=InvasionDirectorRuntime.TrySpawnIntruder->IDefenseEngagementRuntime->production combat exchanges->autonomous medical terminal",
                "PASS\tGAMEPLAY_SCENE_CLEAN_BOOT\t",
                "PASS\tCOMMAND_INTRUDER_SPAWN\t",
                "PASS\tINVASION_ENTRY_PHASES\t",
                "PASS\tENGAGEMENT_STARTED\t",
                "PASS\tCOMBAT_3_EXCHANGES_OBSERVED\t",
                "PASS\tPRODUCTION_DEFENSE_VICTORY_TERMINAL\t",
                "PASS\tMEDICAL_DOWNED\t",
                "PASS\tMEDICAL_STABILIZATION\t",
                "PASS\tMEDICAL_PHYSICAL_CARRY\t",
                "PASS\tMEDICAL_TREATMENT\t",
                "PASS\tMEDICAL_RECOVERY_TERMINAL\t",
                "PASS\tAI_OWNERSHIP_RESUMED\t",
                "terminal=PASS: "
            };
        }

        if (type == "OffenseJourneyPlayModeFacade")
        {
            return new[]
            {
                "authority=production UI pointer->OffenseExpeditionRuntime->IOffenseBattleRuntime->reward terminal",
                "PASS\tSTART_PARTY_OWNER_POINTER\t",
                "PASS\tSTART_PARTY_UI_COMPLETED\t",
                "PASS\tCOMMAND_OWNER_SELECTION\t",
                "PASS\tSTRATEGIC_RESEARCH_PREREQUISITE\t",
                "PASS\tSTRATEGIC_SITE_POINTER\t",
                "PASS\tSTRATEGIC_DEPARTURE_POINTER\t",
                "PASS\tSTRATEGIC_PHYSICAL_SUPPLY_POINTER\t",
                "PASS\tSTRATEGIC_REAL_PARTY_WORLD_TRAVEL\t",
                "PASS\tSTRATEGIC_DEPARTURE_TERMINAL\t",
                "PASS\tSTRATEGIC_DECISION_POINTER\t",
                "PASS\tSTRATEGIC_BATTLE_COMMAND_POINTER\t",
                "PASS\tSTRATEGIC_BATTLE_TERMINAL\t",
                "PASS\tSTRATEGIC_RETURN_POINTER\t",
                "PASS\tSTRATEGIC_JOURNEY_TERMINAL\t",
                "PASS\tSTRATEGIC_REWARD_HISTORY\t",
                "PASS\tSTRATEGIC_OWNERSHIP_CLEAN\t",
                "PASS\tCONSOLE_CLEAN\t",
                "terminal=PASS: strategicSite=",
                "battleCommands=",
                "rewards=",
                "grown="
            };
        }

        if (type == "CharacterAiLifecycleFaultPlayModeVerifier")
            return new[] { "rows=Downed,Dead,Despawned,Disabled,Destroyed" };
        if (type == "CharacterAiWorkTypeLiveMatrixPlayModeVerifier"
            && !string.IsNullOrWhiteSpace(evidence.ArtifactPassMarker))
            return new[] { evidence.ArtifactPassMarker };
        if (type == "CharacterVisitorControlJourneyPlayModeVerifier")
            return new[] { "PASS\tvisitor-entry=", "PASS\tvisitor-exit=" };
        if (type == "OffenseTacticalJourneyPlayModeVerifier")
            return new[]
            {
                "condition-row:Attack", "condition-row:Move",
                "condition-row:Protect", "condition-row:UseAbility",
                "condition-row:Retreat"
            };

        // A generic RESULT=PASS is intentionally insufficient. Verifiers not
        // listed above must add a stable selector/row marker to their artifact
        // contract before the manifest can promote them to LiveExecuted.
        return Array.Empty<string>();
    }

    private static string ResolveFaultRecoverySelector(Evidence evidence)
    {
        switch (evidence.MethodName)
        {
            case "RequestCoreMovementGroup":
                return "core";
            case "RequestSharedFacilityGroup":
                return "facility-shared";
            case "RequestActionFacilityGroup":
                return "facility-action";
            case "RequestDestinationlessGroup":
                return "destinationless";
            case "RequestDeprivationGroup":
                return "deprivation";
            case "RequestPrimitiveSurvivalGroup":
                return "primitive";
            case "RequestSubscriberGroup":
                return "subscriber";
            case "RequestRun":
                return "all";
            default:
                return string.Empty;
        }
    }

    private static Evidence GetWorkEvidence(WorkTypeId id)
    {
        string value = id.Value;
        if (value == BuiltInWorkTypeIds.Construct.Value || value == BuiltInWorkTypeIds.Clean.Value)
            return E("CharacterAlarmResponsePlayModeVerifier", "RequestRun", "typed work selection/progress/terminal");
        if (value == BuiltInWorkTypeIds.Haul.Value)
            return E("CharacterAiCrossActionFaultPlayModeVerifier", "RequestRun", "haul lifecycle and conservation");
        if (value == BuiltInWorkTypeIds.Hunt.Value)
            return E("WildlifeAiHuntPlayModeVerifier", "RequestRun", "hunt lifecycle");
        if (value == BuiltInWorkTypeIds.Rescue.Value || value == BuiltInWorkTypeIds.Treat.Value)
            return E("CharacterAiAutonomousMedicalPlayModeVerifier", "RequestRun", "medical lifecycle");
        if (value == BuiltInWorkTypeIds.Surgery.Value)
            return E("SurgeryPlayModeVerifier", "RequestRunFromMenu", "surgery lifecycle");
        if (value == BuiltInWorkTypeIds.Warden.Value)
            return E("CaptivityAiPlayModeVerifier", "RequestRun", "warden lifecycle");
        if (value == BuiltInWorkTypeIds.Rest.Value)
            return E("DailyRoutineWuPlayModeVerifier", "RequestRun", "rest three-seed compound progress", typeof(int), "three calls:157181/157182/157183");
        if (value == BuiltInWorkTypeIds.Research.Value)
            return E("FirstRunObjectivePlayModeVerifier", "RequestRunFromMenu", "production Brain research completion");
        if (value == BuiltInWorkTypeIds.Guard.Value)
            return E("DefenseEngagementPlayModeVerifier", "StartFromMenu", "guard engagement");
        return E(
            "CharacterAiWorkTypeLiveMatrixPlayModeVerifier",
            "RequestRun",
            "parameterized Brain -> AIWork -> AbilityWork -> WorkTaskExecutor row for " + value,
            artifactPassMarker: "PASS\t" + value + "\t");
    }

    private static Evidence[] BuildWorkAggregateEvidence()
    {
        List<Evidence> evidence = new List<Evidence>
        {
            E(
                "CharacterAiLifecycleFaultPlayModeVerifier",
                "RequestRun",
                "common live lifecycle conservation")
        };
        foreach (WorkTypeId id in BuiltInWorkTypeIds.All)
            evidence.Add(GetWorkEvidence(id));
        return evidence.ToArray();
    }

    private static bool HasEntrypoint(Evidence evidence)
    {
        try
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(evidence.TypeName, false))
                .FirstOrDefault(value => value != null);
            if (type == null)
                return false;

            return type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(method => string.Equals(
                    method.Name,
                    evidence.MethodName,
                    StringComparison.Ordinal))
                .OrderBy(method => method.MetadataToken)
                .Any(method => ParametersMatch(method, evidence.ParameterTypes));
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string BuildCaptureFailureReport(string phase, Exception exception)
    {
        StringBuilder report = new StringBuilder(1024);
        report.AppendLine("CHARACTER_AI_COVERAGE_MANIFEST_V2");
        report.AppendLine("result=FAIL; authored=unknown; workTypes=unknown; domains=unknown; uncovered=1");
        report.Append("- manifest:").Append(phase).Append(": ")
            .Append(exception.GetType().FullName).Append(": ")
            .AppendLine(exception.Message ?? string.Empty);
        return report.ToString();
    }

    private static string Describe(EvidenceResolution value) =>
        Describe(value.Evidence) + " => " + value.Detail;

    private static string Describe(Evidence value) =>
        string.IsNullOrEmpty(value.TypeName)
            ? value.Axes
            : value.TypeName + "." + value.MethodName + "(" + value.InvocationArguments
              + ") [" + value.Axes + "]";

    private static bool ParametersMatch(MethodInfo method, Type[] expected)
    {
        ParameterInfo[] actual = method.GetParameters();
        if (actual.Length != expected.Length)
            return false;
        for (int index = 0; index < actual.Length; index++)
        {
            if (actual[index].ParameterType != expected[index])
                return false;
        }
        return true;
    }

    private static void Write(string report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath) ?? "docs/implementation-reports");
        File.WriteAllText(ReportPath, report, new UTF8Encoding(false));
        AssetDatabase.Refresh();
    }

    private static ActionCoverage Covered(string asset, params string[] evidence) =>
        new ActionCoverage(asset, Parse(evidence), string.Empty);
    private static ActionCoverage Covered(string asset, Evidence[] evidence) =>
        new ActionCoverage(asset, evidence, string.Empty);
    private static ActionCoverage Gap(string asset, string type, string method, string axes, string gap) =>
        new ActionCoverage(asset, new[] { E(type, method, axes) }, gap);
    private static Evidence[] Parse(string[] values)
    {
        Evidence[] result = new Evidence[values.Length / 3];
        for (int index = 0; index < result.Length; index++)
            result[index] = E(values[index * 3], values[index * 3 + 1], values[index * 3 + 2]);
        return result;
    }
    private static Evidence E(
        string type,
        string method,
        string axes,
        Type parameterType = null,
        string invocationArguments = "",
        string artifactPassMarker = "") =>
        new Evidence(
            type,
            method,
            axes,
            parameterType == null ? Array.Empty<Type>() : new[] { parameterType },
            invocationArguments,
            EvidenceStatus.LiveExecuted,
            artifactPassMarker);
    private static Evidence C(string type, string method, string axes) =>
        new Evidence(
            type,
            method,
            axes,
            Array.Empty<Type>(),
            string.Empty,
            EvidenceStatus.ContractOnly,
            string.Empty);
    private static Evidence MissingEvidence(string gap) =>
        new Evidence(
            string.Empty,
            string.Empty,
            gap,
            Array.Empty<Type>(),
            string.Empty,
            EvidenceStatus.Missing,
            string.Empty);
    private static EvidenceArtifact A(string typeName, string path, string passMarker) =>
        new EvidenceArtifact(typeName, path, passMarker);
    private static DomainCoverage Live(string id, string type, string method, string axes) =>
        new DomainCoverage(id, true, E(type, method, axes), string.Empty);
    private static DomainCoverage Missing(string id, string gap) =>
        new DomainCoverage(id, false, MissingEvidence(gap), gap);

    private enum EvidenceStatus
    {
        Missing,
        ContractOnly,
        LiveExecuted
    }

    private readonly struct Evidence
    {
        public Evidence(
            string typeName,
            string methodName,
            string axes,
            Type[] parameterTypes,
            string invocationArguments,
            EvidenceStatus declaredStatus,
            string artifactPassMarker)
        {
            TypeName = typeName;
            MethodName = methodName;
            Axes = axes;
            ParameterTypes = parameterTypes ?? Array.Empty<Type>();
            InvocationArguments = invocationArguments ?? string.Empty;
            DeclaredStatus = declaredStatus;
            ArtifactPassMarker = artifactPassMarker ?? string.Empty;
        }

        public string TypeName { get; }
        public string MethodName { get; }
        public string Axes { get; }
        public Type[] ParameterTypes { get; }
        public string InvocationArguments { get; }
        public EvidenceStatus DeclaredStatus { get; }
        public string ArtifactPassMarker { get; }
    }

    private readonly struct EvidenceResolution
    {
        public EvidenceResolution(EvidenceStatus status, Evidence evidence, string detail)
        {
            Status = status;
            Evidence = evidence;
            Detail = detail ?? string.Empty;
        }

        public EvidenceStatus Status { get; }
        public Evidence Evidence { get; }
        public string Detail { get; }
    }

    private readonly struct EvidenceArtifact
    {
        public EvidenceArtifact(string typeName, string path, string passMarker)
        {
            TypeName = typeName ?? string.Empty;
            Path = path ?? string.Empty;
            PassMarker = passMarker ?? string.Empty;
        }

        public string TypeName { get; }
        public string Path { get; }
        public string PassMarker { get; }
    }

    private readonly struct ActionCoverage
    {
        public ActionCoverage(string assetName, Evidence[] evidence, string gap)
        {
            AssetName = assetName;
            Evidence = evidence;
            Gap = gap;
        }

        public string AssetName { get; }
        public Evidence[] Evidence { get; }
        public string Gap { get; }
    }

    private readonly struct DomainCoverage
    {
        public DomainCoverage(string id, bool live, Evidence evidence, string gap)
        {
            Id = id;
            Live = live;
            Evidence = evidence;
            Gap = gap;
        }

        public string Id { get; }
        public bool Live { get; }
        public Evidence Evidence { get; }
        public string Gap { get; }
    }

    private readonly struct Evaluation
    {
        public Evaluation(bool passed, string summary, string report)
        {
            Passed = passed;
            Summary = summary;
            Report = report;
        }

        public bool Passed { get; }
        public string Summary { get; }
        public string Report { get; }
    }
}
#endif
