#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;
using UnityEditor;
using UnityEngine;

public static class CaptivityTransitionOutcomeDebugScenarios
{
    private const int FixtureAbsoluteDay = 21;
    private const string HousingAssetPath =
        "Assets/Resources/SO/Building/Captivity/CP01_감방구속대.asset";

    internal static string RunLaborForArchitectureBatch()
    {
        var errors = new List<string>();
        void Observe(string condition, string stack, LogType type)
        {
            if (type is LogType.Error or LogType.Exception or LogType.Assert) errors.Add(condition);
        }
        Application.logMessageReceived += Observe;
        try
        {
            Require(RunFocused(), "D09 transition contracts failed.");
            VerifyLaborAssignmentWearAndRetry();
            CaptivityCareLaborInputOwnerDebugScenarios.RunAll();
            Require(errors.Count == 0, "Captivity labor emitted engine errors: " + string.Join(" | ", errors));
            return "labor=threshold,request-owner-failure,delivery-failure,no-premature-work,physical-assignment,permission,employment,wear,broken-return-failure; restore=assignment-close-pending,worn-tool; retry=one-unique,no-duplicate; D09=6; care-input-owner=5; consoleErrors=0";
        }
        finally { Application.logMessageReceived -= Observe; }
    }

    private static void VerifyLaborAssignmentWearAndRetry()
    {
        using Fixture fixture = new("labor-production", careFailures: 1, captiveStatus: CaptivityStatus.Confined);
        CharacterActor actor = fixture.Captive;
        actor.gameObject.SetActive(false);
        AbilityWork work = actor.gameObject.AddComponent<AbilityWork>();
        CharacterAiEditorTestDependencies.Inject(actor.gameObject);
        actor.RefreshAbilityCache();
        actor.gameObject.SetActive(true);
        CaptiveState state = fixture.State;
        state.compliance = 49f;
        string before = JsonUtility.ToJson(state);
        Require(!fixture.Runtime.TrySetLaborPermissions(state.captiveId, CaptiveLaborPermission.Clean, out _)
            && JsonUtility.ToJson(state) == before && fixture.ItemFaults.DeliveryRequests == 0,
            "Labor below compliance threshold changed state or requested a tool.");
        state.compliance = 50f;
        before = JsonUtility.ToJson(state);
        Require(!fixture.Runtime.TrySetLaborPermissions(state.captiveId, CaptiveLaborPermission.Clean, out _)
            && JsonUtility.ToJson(state) == before && fixture.ItemFaults.DeliveryRequests == 0,
            "Failed labor destination ownership left a partial command.");
        fixture.ItemFaults.FailNextDeliveryRequest = true;
        Require(!fixture.Runtime.TrySetLaborPermissions(state.captiveId, CaptiveLaborPermission.Clean, out _)
            && JsonUtility.ToJson(state) == before && fixture.ItemFaults.DeliveryRequests == 1,
            "Failed physical delivery request left a partial labor command.");
        Require(fixture.Runtime.TrySetLaborPermissions(state.captiveId, CaptiveLaborPermission.Clean, out string failure)
            && state.pendingLaborPermissions == CaptiveLaborPermission.Clean
            && state.status == CaptivityStatus.Confined && state.laborPermissions == CaptiveLaborPermission.None,
            "Labor tool request failed or enabled work before assignment: " + failure);
        fixture.Labor.Tick(state, actor);
        Require(!fixture.Runtime.IsWorkAllowed(actor, BuiltInWorkTypeIds.Clean, out _)
            && state.assignedLaborToolItemId.Length == 0,
            "A captive without the delivered tool could work.");
        const string instanceId = "item-instance:d09-labor-production";
        fixture.PhysicalItems.AddEditorTestStack(CaptivityItemDefinitions.PrisonerWorkKitItemId, 1,
            WorldItemStackState.FacilityBuffer, destinationId: state.laborToolDestinationId,
            components: new[] { DurableToolItemRules.CreateDurability(CaptivityItemDefinitions.PrisonerWorkKitItemId, 80f) },
            itemInstanceId: instanceId);
        fixture.Care.FailNext();
        fixture.Labor.Tick(state, actor);
        Require(state.laborToolAssignmentCompleted && state.pendingLaborPermissions == CaptiveLaborPermission.Clean
            && state.status == CaptivityStatus.Confined && fixture.PhysicalItems.Records.Count == 0
            && fixture.PhysicalItems.GetEditorPendingBatchDispositionCount() == 0,
            "Assignment close failure lost the committed tool or enabled labor early.");
        CaptiveState retired = state;
        state = RoundTrip(state);
        fixture.ReplaceState(state);
        fixture.Labor.Tick(state, actor);
        Require(state.status == CaptivityStatus.Labor && state.pendingLaborPermissions == CaptiveLaborPermission.None
            && state.laborPermissions == CaptiveLaborPermission.Clean && state.assignedLaborToolInstanceId == instanceId
            && Mathf.Approximately(state.assignedLaborToolDurability, 80f)
            && actor.characterType == CharacterType.NPC && actor.CurrentLifecycleState == CharacterLifecycleState.Active
            && fixture.Runtime.IsWorkAllowed(actor, BuiltInWorkTypeIds.Clean, out _)
            && !fixture.Runtime.IsWorkAllowed(actor, BuiltInWorkTypeIds.Haul, out _)
            && retired.status == CaptivityStatus.Confined,
            "JSON-restored labor assignment did not complete on the current state/actor.");
        fixture.Clock.Elapsed = 60;
        fixture.Labor.Tick(state, actor);
        Require(Mathf.Approximately(state.assignedLaborToolDurability, 80f), "An idle labor tool wore down.");
        work.isWorking = true;
        fixture.Labor.Tick(state, actor);
        fixture.Labor.Tick(state, actor);
        Require(Mathf.Approximately(state.assignedLaborToolDurability, 79f), "A working tool did not wear once per interval.");
        retired = state;
        state = RoundTrip(state);
        fixture.ReplaceState(state);
        Require(Mathf.Approximately(state.assignedLaborToolDurability, 79f), "Worn-tool JSON lost durability.");
        state.assignedLaborToolDurability = 1f;
        fixture.Clock.Elapsed = 120;
        fixture.ItemFaults.FailNextUniqueSpawn = true;
        fixture.Labor.Tick(state, actor);
        Require(state.status == CaptivityStatus.Confined && state.laborPermissions == CaptiveLaborPermission.None
            && state.assignedLaborToolInstanceId == instanceId && state.assignedLaborToolDurability == 0
            && actor.characterType == CharacterType.Intruder && actor.CurrentLifecycleState == CharacterLifecycleState.Downed
            && fixture.PhysicalItems.Records.Count == 0,
            "Failed broken-tool return lost custody or kept labor enabled.");
        fixture.Labor.Tick(state, actor);
        fixture.Labor.Tick(state, actor);
        Require(fixture.Runtime.TrySetLaborPermissions(state.captiveId, CaptiveLaborPermission.None, out _)
            && fixture.PhysicalItems.Records.Count == 1 && state.assignedLaborToolItemId.Length == 0
            && fixture.PhysicalItems.Records.Single().itemInstanceId == instanceId
            && DurableToolItemRules.ReadCurrentDurability(fixture.PhysicalItems.Records.Single().itemId,
                fixture.PhysicalItems.Records.Single().components) == 0
            && retired.assignedLaborToolInstanceId == instanceId && retired.assignedLaborToolDurability == 79f,
            "Broken tool retry duplicated the unique item, lost durability or changed the retired state.");
    }

    public static bool RunFocused()
    {
        VerifyDescriptorMetricWhitelists();
        VerifyRehabilitationPartialAndCompletion();
        VerifyRehabilitationSavedForwardRetry();
        VerifyReleasePhysicalFailureAndSuccess();
        VerifyDurableToolReturnFailureAndRetry();
        VerifyReleaseSavedForwardRetry();
        Debug.Log("[D09 Captivity Transition Outcome] PASS");
        return true;
    }

    public static void RunD09FromBatch()
    {
        string reportPath = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_U0_D09_CAPTIVITY_REPORT_PATH");
        if (string.IsNullOrWhiteSpace(reportPath)
            || !Path.IsPathRooted(reportPath))
        {
            throw new InvalidOperationException(
                "D09 batch verification requires an absolute report path.");
        }
        if (File.Exists(reportPath))
            throw new IOException("D09 batch report already exists: " + reportPath);

        Require(RunFocused(), "D09 focused scenario returned false.");
        string directory = Path.GetDirectoryName(reportPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException(
                "D09 batch report path has no parent directory.");
        }
        Directory.CreateDirectory(directory);
        string report = string.Join(Environment.NewLine, new[]
        {
            "U0_D09_CAPTIVITY=PASS",
            "rehabilitation=partial,completed-day,food-one,relationship-deltas",
            "release=physical-failure-no-outcome,success",
            "participants=captive,warden,decider,facility,food",
            "restore=rehabilitation-pending,release-pending",
            "delivery=terminal-retry-exactly-once",
            "fullAuditClosure=false",
            "humanApprovalClaimed=false",
            "trainingEligible=false"
        }) + Environment.NewLine;
        using (FileStream stream = new(
                   reportPath,
                   FileMode.CreateNew,
                   FileAccess.Write,
                   FileShare.None))
        using (StreamWriter writer = new(stream))
            writer.Write(report);
        Debug.Log("U0_D09_CAPTIVITY=PASS; report=" + reportPath);
    }

    private static void VerifyDescriptorMetricWhitelists()
    {
        CaptivityRehabilitationOutcomeDescriptor rehabilitation = new();
        CaptivityReleaseOutcomeDescriptor release = new();
        Require(rehabilitation.IsKnownMetric(
                CaptivityTransitionOutcomeIds.ApprovedWork,
                CaptivityTransitionOutcomeIds.WorkUnit)
            && rehabilitation.IsKnownMetric(
                CaptivityTransitionOutcomeIds.TrustAfter,
                CaptivityTransitionOutcomeIds.PointUnit)
            && !rehabilitation.IsKnownMetric(
                CaptivityTransitionOutcomeIds.ApprovedWork,
                CaptivityTransitionOutcomeIds.CountUnit)
            && !rehabilitation.IsKnownMetric(
                new GameplayMetricId("captivity.rehabilitation.unknown"),
                CaptivityTransitionOutcomeIds.WorkUnit),
            "Rehabilitation descriptor accepted an unknown metric or unit.");
        Require(release.IsKnownMetric(
                CaptivityTransitionOutcomeIds.PhysicalInputsReturned,
                CaptivityTransitionOutcomeIds.BooleanUnit)
            && release.IsKnownMetric(
                CaptivityTransitionOutcomeIds.ReleaseRevision,
                CaptivityTransitionOutcomeIds.RevisionUnit)
            && !release.IsKnownMetric(
                CaptivityTransitionOutcomeIds.ReleaseRevision,
                CaptivityTransitionOutcomeIds.BooleanUnit)
            && !release.IsKnownMetric(
                new GameplayMetricId("captivity.release.unknown"),
                CaptivityTransitionOutcomeIds.BooleanUnit),
            "Release descriptor accepted an unknown metric or unit.");
    }

    private static void VerifyRehabilitationPartialAndCompletion()
    {
        using Fixture partial = new("rehab-partial", rehabilitationWork: 5f);
        Require(partial.Runtime.AdvanceRehabilitation(
                partial.State.captiveId,
                partial.Warden,
                3f,
                out string partialStatus),
            "Partial rehabilitation failed: " + partialStatus);
        Require(Mathf.Approximately(
                partial.State.completedRehabilitationWork,
                8f)
            && partial.State.rehabilitationDays == 2
            && partial.Food.Consumed == 0
            && partial.State.rehabilitationOutcomeRevision == 1L,
            "Partial rehabilitation state or food delta drifted.");
        GameplayOutcomeSnapshot partialOutcome = partial.SingleOutcome();
        Require(Metric(partialOutcome,
                    CaptivityTransitionOutcomeIds.CompletedDay.Value) == 0d
            && Metric(partialOutcome,
                    CaptivityTransitionOutcomeIds.WorkBefore.Value) == 5d
            && Metric(partialOutcome,
                    CaptivityTransitionOutcomeIds.WorkAfter.Value) == 8d
            && Metric(partialOutcome,
                    CaptivityTransitionOutcomeIds.FoodConsumed.Value) == 0d,
            "Partial rehabilitation canonical metrics drifted.");
        RequireRehabilitationParticipants(partialOutcome, partial);

        using Fixture completed = new("rehab-complete", rehabilitationWork: 17.5f);
        float trustBefore = completed.State.trust;
        float grudgeBefore = completed.State.grudge;
        float corruptionBefore = completed.State.corruption;
        Require(completed.Runtime.AdvanceRehabilitation(
                completed.State.captiveId,
                completed.Warden,
                1f,
                out string completionStatus),
            "Daily rehabilitation completion failed: " + completionStatus);
        Require(!completed.State.rehabilitationInProgress
            && completed.State.rehabilitationDays == 3
            && completed.State.completedRehabilitationWork == 0f
            && completed.Food.Consumed == 1
            && Mathf.Approximately(
                completed.State.trust,
                trustBefore + MinionIntegrationRules.RehabilitationTrustDelta)
            && Mathf.Approximately(
                completed.State.grudge,
                grudgeBefore + MinionIntegrationRules.RehabilitationGrudgeDelta)
            && Mathf.Approximately(
                completed.State.corruption,
                corruptionBefore
                    + MinionIntegrationRules.RehabilitationCorruptionDelta),
            "Daily rehabilitation did not apply the exact food/day/stat deltas.");
        GameplayOutcomeSnapshot completedOutcome = completed.SingleOutcome();
        Require(Metric(completedOutcome,
                    CaptivityTransitionOutcomeIds.CompletedDay.Value) == 1d
            && Metric(completedOutcome,
                    CaptivityTransitionOutcomeIds.FoodConsumed.Value) == 1d
            && Metric(completedOutcome,
                    CaptivityTransitionOutcomeIds.DaysBefore.Value) == 2d
            && Metric(completedOutcome,
                    CaptivityTransitionOutcomeIds.DaysAfter.Value) == 3d,
            "Completed rehabilitation canonical metrics drifted.");
        RequireRehabilitationParticipants(completedOutcome, completed);
    }

    private static void VerifyRehabilitationSavedForwardRetry()
    {
        using Fixture fixture = new(
            "rehab-retry",
            rehabilitationWork: 17.5f,
            factCommitFailures: 1);
        Require(fixture.Runtime.AdvanceRehabilitation(
                fixture.State.captiveId,
                fixture.Warden,
                1f,
                out _)
            && fixture.State.rehabilitationTerminal.outcomePending
            && fixture.State.rehabilitationOutcomeRevision == 0L
            && fixture.State.rehabilitationTerminal.publicationKey.Length > 0
            && fixture.Food.Consumed == 1
            && fixture.Publisher.RehabilitationCommitCount == 1
            && fixture.OutcomeCount == 0,
            "Rejected rehabilitation result did not remain saved-forward pending.");
        string publicationKey = fixture.State.rehabilitationTerminal.publicationKey;
        CaptiveState restored = RoundTrip(fixture.State);
        fixture.ReplaceState(restored);
        Require(fixture.Runtime.AdvanceRehabilitation(
                restored.captiveId,
                fixture.Warden,
                4f,
                out string retryStatus)
            && !restored.rehabilitationTerminal.outcomePending
            && restored.rehabilitationOutcomeRevision == 1L
            && restored.rehabilitationTerminal.publicationKey == publicationKey
            && fixture.Food.Consumed == 1
            && fixture.Publisher.RehabilitationCommitCount == 1
            && fixture.OutcomeCount == 1,
            "Saved rehabilitation result did not retry exactly once: "
            + retryStatus);
    }

    private static void VerifyReleasePhysicalFailureAndSuccess()
    {
        using Fixture rejected = new(
            "release-physical-failure",
            careFailures: 1,
            captiveStatus: CaptivityStatus.Confined);
        Require(!rejected.Runtime.TryRelease(
                rejected.State.captiveId,
                out string rejectedReason)
            && rejectedReason.Length > 0
            && rejected.State.status == CaptivityStatus.Confined
            && !rejected.State.releaseTerminal.HasOutcome
            && rejected.OutcomeCount == 0
            && rejected.ReleaseDecisionCount == 0,
            "Physical release failure leaked an observer or canonical result.");

        using Fixture success = new(
            "release-success",
            captiveStatus: CaptivityStatus.Confined);
        Require(success.Runtime.TryRelease(
                success.State.captiveId,
                out string releaseFailure)
            && string.IsNullOrEmpty(releaseFailure)
            && success.State.status == CaptivityStatus.Released
            && success.State.releaseOutcomeRevision == 1L
            && !success.State.releaseTerminal.outcomePending
            && !success.State.releaseTerminal.observersPending
            && success.State.releaseTerminal.effectsApplied
            && success.State.releaseTerminal.decisionPublished
            && success.ReleaseDecisionCount == 1
            && !success.Doors.IsCaptive(success.State.captiveId),
            "Successful release did not publish exactly one terminal result: "
            + releaseFailure);
        GameplayOutcomeSnapshot outcome = success.SingleOutcome();
        Require(outcome.outcomeTypeId
                == CaptivityTransitionOutcomeIds.Release.Value
            && outcome.absoluteDay == FixtureAbsoluteDay
            && Metric(outcome,
                    CaptivityTransitionOutcomeIds.CustodyBefore.Value) == 1d
            && Metric(outcome,
                    CaptivityTransitionOutcomeIds.CustodyAfter.Value) == 0d
            && Metric(outcome,
                    CaptivityTransitionOutcomeIds.PhysicalInputsReturned.Value)
                == 1d,
            "Release canonical before/after metrics drifted.");
        RequireReleaseParticipants(outcome, success);
    }

    private static void VerifyDurableToolReturnFailureAndRetry()
    {
        using Fixture fixture = new("tool-return-retry", captiveStatus: CaptivityStatus.Confined);
        CaptiveState original = fixture.State;
        original.assignedRestraintItemId = CaptivityItemDefinitions.ReinforcedRestraintItemId;
        original.assignedRestraintInstanceId = "item-instance:d09-return-restraint";
        original.assignedRestraintMaximumDurability = 140f;
        original.assignedRestraintDurability = 81.75f;
        string before = JsonUtility.ToJson(original);
        fixture.ItemFaults.FailNextUniqueSpawn = true;
        Require(!fixture.Runtime.TryRelease(original.captiveId, out string failure)
            && !string.IsNullOrWhiteSpace(failure)
            && JsonUtility.ToJson(original) == before
            && fixture.PhysicalItems.Records.Count == 0
            && fixture.OutcomeCount == 0 && fixture.ReleaseDecisionCount == 0,
            "Failed durable-tool return changed custody, ownership, or release outcome.");
        CaptiveState restored = RoundTrip(original);
        fixture.ReplaceState(restored);
        bool captureRejectedDuringReturn = false;
        fixture.ToolMarkers.OnRefresh = () =>
        {
            try { fixture.Runtime.Capture(); }
            catch (InvalidOperationException exception)
                when (exception.Message.Contains("durable-tool return", StringComparison.Ordinal))
            { captureRejectedDuringReturn = true; }
        };
        Require(fixture.Runtime.TryRelease(restored.captiveId, out failure),
            "JSON-restored tool return retry failed: " + failure);
        Require(captureRejectedDuringReturn,
            "Reentrant save observed both captive and physical ownership during tool return.");
        fixture.DurableTools.ValidateBeforeCapture();
        WorldItemStackRecord returned = fixture.PhysicalItems.Records.Single();
        Require(returned.itemInstanceId == "item-instance:d09-return-restraint"
            && returned.quantity == 1
            && Mathf.Approximately(DurableToolItemRules.ReadCurrentDurability(returned.itemId,
                returned.components), 81.75f)
            && restored.assignedRestraintItemId.Length == 0
            && original.assignedRestraintItemId.Length > 0
            && fixture.OutcomeCount == 1 && fixture.ReleaseDecisionCount == 1,
            "Tool return lost durability, cleared a retired state, or duplicated release.");
        fixture.Runtime.TryRelease(restored.captiveId, out _);
        Require(fixture.PhysicalItems.Records.Count == 1 && fixture.OutcomeCount == 1,
            "Repeated release duplicated the returned unique tool or outcome.");

        using Fixture labor = new("labor-return-retry", captiveStatus: CaptivityStatus.Confined);
        CaptiveState state = labor.State;
        state.assignedLaborToolItemId = CaptivityItemDefinitions.PrisonerWorkKitItemId;
        state.assignedLaborToolInstanceId = "item-instance:d09-return-work-kit";
        state.assignedLaborToolMaximumDurability = 100f;
        state.assignedLaborToolDurability = 22.5f;
        state.laborToolAssignmentCompleted = true;
        state.laborToolAssignmentOperationId = CaptivityLaborToolAssignmentOutbox.FormatOperationId(
            state.captiveId, state.assignedLaborToolInstanceId);
        state.laborToolAssignmentSourceStackId = "stack:d09-tool-source";
        state.laborToolAssignmentCommitId = "commit:d09-tool-source";
        before = JsonUtility.ToJson(state);
        labor.ItemFaults.FailNextUniqueSpawn = true;
        Require(!labor.Runtime.TrySetLaborPermissions(state.captiveId,
                CaptiveLaborPermission.None, out failure)
            && JsonUtility.ToJson(state) == before && labor.PhysicalItems.Records.Count == 0,
            "Failed labor disable lost its assigned tool or changed the command state.");
        Require(labor.Runtime.TrySetLaborPermissions(state.captiveId,
                CaptiveLaborPermission.None, out failure)
            && state.assignedLaborToolItemId.Length == 0
            && labor.PhysicalItems.Records.Count == 1
            && Mathf.Approximately(DurableToolItemRules.ReadCurrentDurability(
                labor.PhysicalItems.Records.Single().itemId,
                labor.PhysicalItems.Records.Single().components), 22.5f),
            "Labor disable retry lost the exact durable tool: " + failure);
    }

    private static void VerifyReleaseSavedForwardRetry()
    {
        using Fixture fixture = new(
            "release-retry",
            captiveStatus: CaptivityStatus.Confined,
            factCommitFailures: 1);
        Require(fixture.Runtime.TryRelease(
                fixture.State.captiveId,
                out _)
            && fixture.State.status == CaptivityStatus.Released
            && fixture.State.releaseTerminal.outcomePending
            && fixture.State.releaseTerminal.effectsApplied
            && !fixture.State.releaseTerminal.decisionPublished
            && fixture.State.releaseTerminal.publicationKey.Length > 0
            && fixture.ReleaseDecisionCount == 0
            && fixture.Publisher.ReleaseCommitCount == 1
            && fixture.OutcomeCount == 0,
            "Rejected release result did not suppress the decision observer.");
        string publicationKey = fixture.State.releaseTerminal.publicationKey;
        CaptiveState restored = RoundTrip(fixture.State);
        fixture.ReplaceState(restored);
        Require(fixture.Runtime.TryRelease(
                restored.captiveId,
                out string retryFailure)
            && string.IsNullOrEmpty(retryFailure)
            && restored.releaseOutcomeRevision == 1L
            && !restored.releaseTerminal.outcomePending
            && !restored.releaseTerminal.observersPending
            && restored.releaseTerminal.effectsApplied
            && restored.releaseTerminal.decisionPublished
            && restored.releaseTerminal.publicationKey == publicationKey
            && fixture.ReleaseDecisionCount == 1
            && fixture.Publisher.ReleaseCommitCount == 1
            && fixture.OutcomeCount == 1,
            "Saved release did not recover exactly once: " + retryFailure);
    }

    private static double Metric(GameplayOutcomeSnapshot outcome, string id) =>
        outcome.metrics.Single(value => value.metricId == id).value;

    private static void RequireRehabilitationParticipants(
        GameplayOutcomeSnapshot outcome,
        Fixture fixture)
    {
        RequireParticipant(
            outcome,
            CaptivityTransitionOutcomeIds.CharacterKind,
            fixture.Captive.Identity.PersistentId,
            CaptivityTransitionOutcomeIds.CaptiveRole,
            "rehabilitation captive");
        RequireParticipant(
            outcome,
            CaptivityTransitionOutcomeIds.CharacterKind,
            fixture.Warden.Identity.PersistentId,
            CaptivityTransitionOutcomeIds.WardenRole,
            "rehabilitation warden");
        RequireParticipant(
            outcome,
            CaptivityTransitionOutcomeIds.BuildingKind,
            fixture.Housing.RequirePersistentInstanceId().Value,
            CaptivityTransitionOutcomeIds.FacilityRole,
            "rehabilitation facility");
        RequireParticipant(
            outcome,
            CaptivityTransitionOutcomeIds.StockCategoryKind,
            "food",
            CaptivityTransitionOutcomeIds.FoodRole,
            "rehabilitation food");
        Require(outcome.participants.Count == 4,
            "Rehabilitation outcome contains an unexpected participant.");
    }

    private static void RequireReleaseParticipants(
        GameplayOutcomeSnapshot outcome,
        Fixture fixture)
    {
        RequireParticipant(
            outcome,
            CaptivityTransitionOutcomeIds.CharacterKind,
            fixture.Captive.Identity.PersistentId,
            CaptivityTransitionOutcomeIds.CaptiveRole,
            "release captive");
        RequireParticipant(
            outcome,
            CaptivityTransitionOutcomeIds.CharacterKind,
            fixture.Owner.Identity.PersistentId,
            CaptivityTransitionOutcomeIds.DeciderRole,
            "release decider");
        RequireParticipant(
            outcome,
            CaptivityTransitionOutcomeIds.BuildingKind,
            fixture.Housing.RequirePersistentInstanceId().Value,
            CaptivityTransitionOutcomeIds.FacilityRole,
            "release facility");
        Require(outcome.participants.Count == 3,
            "Release outcome contains an unexpected participant.");
    }

    private static void RequireParticipant(
        GameplayOutcomeSnapshot outcome,
        GameplayEntityKindId entityKind,
        string entityId,
        GameplayRoleId role,
        string context)
    {
        int count = outcome.participants.Count(value =>
            value.entityKindId == entityKind.Value
            && value.entityId == entityId
            && value.roleId == role.Value
            && value.participationKind == GameplayParticipationKind.Direct);
        Require(count == 1,
            "Missing or duplicate " + context + " participant identity.");
    }

    private static CaptiveState RoundTrip(CaptiveState source)
    {
        CaptivitySaveData payload = new()
        {
            captives = new List<CaptiveState> { source },
            policies = new List<CaptivePolicyData>
            {
                new()
                {
                    policyId = CaptivityPolicyIds.Standard,
                    displayName = "표준 수용"
                }
            }
        };
        CaptivitySaveData restored = JsonUtility.FromJson<CaptivitySaveData>(
            JsonUtility.ToJson(payload));
        DungeonGameRestoreReport report = new();
        CaptivitySaveValidation.Validate(restored, report);
        Require(report.Success,
            "D09 save round trip was rejected: "
            + string.Join(" | ", report.Errors));
        return CaptivitySaveValidation.CreateRestoredCaptive(
            restored.captives.Single());
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly List<UnityEngine.Object> cleanup = new();
        private readonly CaptivityActorAccess actors;
        private readonly IDisposable decisionSubscription;
        private readonly DomainFactDispatcher transitionFacts;
        private readonly IDisposable rehabilitationObservation;
        private readonly IDisposable releaseObservation;

        internal Fixture(
            string suffix,
            float rehabilitationWork = 0f,
            int careFailures = 0,
            CaptivityStatus captiveStatus = CaptivityStatus.Minion,
            int factCommitFailures = 0)
        {
            Captive = CreateActor(
                "D09 Captive " + suffix,
                "character:d09-captive-" + suffix,
                "가람",
                CharacterRole.Regular,
                CharacterType.NPC);
            Warden = CreateActor(
                "D09 Warden " + suffix,
                "character:d09-warden-" + suffix,
                "도윤",
                CharacterRole.Regular,
                CharacterType.NPC);
            Owner = CreateActor(
                "D09 Owner " + suffix,
                "character:d09-owner-" + suffix,
                "연화",
                CharacterRole.Owner,
                CharacterType.NPC);
            Housing = CreateHousing(suffix);
            State = new CaptiveState
            {
                captiveId = Captive.Identity.PersistentId,
                displayName = "가람",
                speciesTag = "human",
                status = captiveStatus,
                policyId = CaptivityPolicyIds.Standard,
                restrained = captiveStatus == CaptivityStatus.Confined,
                housingBuildingId = captiveStatus == CaptivityStatus.Confined
                    ? Housing.RequirePersistentInstanceId().Value
                    : string.Empty,
                trust = 50f,
                grudge = 30f,
                corruption = 50f,
                rehabilitationDays = 2,
                completedRehabilitationWork = rehabilitationWork,
                rehabilitationInProgress = captiveStatus == CaptivityStatus.Minion,
                reservedWardenId = captiveStatus == CaptivityStatus.Minion
                    ? Warden.Identity.PersistentId
                    : string.Empty,
                rehabilitationFacilityBuildingId =
                    captiveStatus == CaptivityStatus.Minion
                        ? Housing.RequirePersistentInstanceId().Value
                        : string.Empty,
                rehabilitationPosition = Housing.centerPos
            };
            actors = new CaptivityActorAccess(
                new DungeonRuntimeAggregateRootStore(),
                _ => { });
            actors.AddState(State);
            World = new FakeWorldRegistry(
                new[] { Captive, Warden, Owner },
                new[] { Housing });
            Food = new FakeFood(4);
            Care = new FakeCareOwner(careFailures);
            Doors = new FakeDoorSubjects();
            Doors.SetCaptive(State.captiveId, State.IsInCustody);
            Events = new GameEventBus();
            decisionSubscription = Events.Subscribe<PrisonerDecisionEvent>(
                value =>
                {
                    if (value.Prisoner.Equals(new CharacterId(State.captiveId))
                        && value.DecisionId == "release")
                    {
                        ReleaseDecisionCount++;
                    }
                });
            Clock = new FakeClock();
            Calendar = new EditorFixedGameCalendar(
                FixtureAbsoluteDay,
                hour: 0);

            Registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[]
                {
                    new CaptivityRehabilitationOutcomeDescriptor(),
                    new CaptivityReleaseOutcomeDescriptor()
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new CaptivityRehabilitationOutcomeAdapter(),
                    new CaptivityReleaseOutcomeAdapter()
                });
            Ledger = new GameplayOutcomeLedger(
                Registry,
                new GameplayOutcomeBufferLimits(16, 0, 128),
                new GameplayOutcomeRunId("run:d09-" + suffix),
                1L);
            GameplayOutcomeRecorder recorder = new(Ledger, Registry, Events);
            GameplayDomainFactCommitGroup commits = new(recorder, Ledger);
            transitionFacts = new DomainFactDispatcher();
            rehabilitationObservation = transitionFacts.Subscribe(
                "captivity-rehabilitation:d09-fixture",
                new GameplayDomainFactObserver<
                    CaptivityRehabilitationProgressedFact,
                    CaptivityRehabilitationOutcomeReceipt>(
                    CaptivityTransitionFactMapping.Capture,
                    Registry,
                    Ledger,
                    recorder,
                    commits));
            releaseObservation = transitionFacts.Subscribe(
                "captivity-release:d09-fixture",
                new GameplayDomainFactObserver<
                    CaptivityReleasedFact,
                    CaptivityReleaseOutcomeReceipt>(
                    CaptivityTransitionFactMapping.Capture,
                    Registry,
                    Ledger,
                    recorder,
                    commits));
            Publisher = new ConfigurablePublisher(
                transitionFacts,
                factCommitFailures);
            Runtime = CreateRuntime(Publisher, transitionFacts);
        }

        internal CharacterActor Captive { get; }
        internal CharacterActor Warden { get; }
        internal CharacterActor Owner { get; }
        internal BuildableObject Housing { get; }
        internal CaptiveState State { get; private set; }
        internal FakeWorldRegistry World { get; }
        internal FakeFood Food { get; }
        internal FakeCareOwner Care { get; }
        internal FakeDoorSubjects Doors { get; }
        internal GameEventBus Events { get; }
        internal FakeClock Clock { get; }
        internal IGameCalendar Calendar { get; }
        internal GameplayOutcomeRegistry Registry { get; }
        internal GameplayOutcomeLedger Ledger { get; }
        internal ConfigurablePublisher Publisher { get; }
        internal CaptivityRuntime Runtime { get; }
        internal WorldItemRepository PhysicalItems { get; private set; }
        internal DurableToolItems ItemFaults { get; private set; }
        internal CaptivityDurableToolRuntime DurableTools { get; private set; }
        internal ToolReturnMarkers ToolMarkers { get; private set; }
        internal CaptivityLaborRuntime Labor { get; private set; }
        internal int ReleaseDecisionCount { get; private set; }
        internal int OutcomeCount => Ledger.GetGlobal(
            OutcomeCursor.FirstPage(32), OutcomeFilter.All).Items.Count;

        internal GameplayOutcomeSnapshot SingleOutcome() => Ledger.GetGlobal(
            OutcomeCursor.FirstPage(32), OutcomeFilter.All).Items.Single().Exact;

        internal void ReplaceState(CaptiveState restored)
        {
            actors.ClearStates();
            actors.AddState(restored);
            State = restored;
        }

        public void Dispose()
        {
            decisionSubscription?.Dispose();
            releaseObservation?.Dispose();
            rehabilitationObservation?.Dispose();
            transitionFacts?.Dispose();
            foreach (UnityEngine.Object value in cleanup.Where(value => value != null))
                UnityEngine.Object.DestroyImmediate(value);
        }

        private CaptivityRuntime CreateRuntime(
            IDomainFactStagingPublisher facts,
            IDomainFactRecoveryPublisher recovery)
        {
#pragma warning disable SYSLIB0050
            CaptivityRuntime runtime = (CaptivityRuntime)
                FormatterServices.GetUninitializedObject(typeof(CaptivityRuntime));
#pragma warning restore SYSLIB0050
            IDungeonItemCatalogProvider catalog = new ResourceDungeonItemCatalogProvider(
                new ResourceItemDefinitionCatalog(
                    new ResourceGameContentCatalog(new UnityGameContentRootLoader())));
            PhysicalItems = new WorldItemRepository(new GuidPersistentIdGenerator(),
                new DungeonRuntimeAggregateRootStore());
            ToolMarkers = new ToolReturnMarkers();
            IItemMarkerPresenter markers = ToolMarkers;
            // Exercise the actual physical spawn method without assembling unrelated logistics.
#pragma warning disable SYSLIB0050
            WorldItemStackRuntime physical = (WorldItemStackRuntime)
                FormatterServices.GetUninitializedObject(typeof(WorldItemStackRuntime));
#pragma warning restore SYSLIB0050
            Set(physical, "itemRepository", PhysicalItems);
            Set(physical, "itemSpawner", new WorldItemSpawner(catalog, PhysicalItems, markers));
            Set(physical, "itemQueryService", new WorldItemQueryService(catalog,
                new PhysicalItemMassQuery(catalog), PhysicalItems, markers));
            IWorldItemStackRuntime items = DispatchProxy.Create<IWorldItemStackRuntime, DurableToolItems>();
            ItemFaults = (DurableToolItems)(object)items;
            ItemFaults.Target = physical;
            IPhysicalItemBatchDispositionService dispositions = new PhysicalItemBatchDispositionService(
                PhysicalItems, new PhysicalItemMassQuery(catalog), markers, clock: new DungeonStory.Foundation.UnityUiClock());
            DurableTools = new CaptivityDurableToolRuntime();
            Set(runtime, "durableTools", DurableTools);
            Labor = new CaptivityLaborRuntime(actors,
                new CaptivityActorRuntimeLookup(id => World.AllCharacters.FirstOrDefault(
                    actor => actor.Identity.PersistentId == id)), World,
                CharacterAiEditorTestDependencies.BodyHealthRuntime, items, dispositions, Care, Clock, DurableTools);
            Set(runtime, "laborRuntime", Labor);
            Set(runtime, "actorAccess", actors);
            Set(runtime, "worldRegistry", World);
            Set(runtime, "survivalFood", Food);
            Set(runtime, "transitionFacts", facts);
            Set(runtime, "transitionRecovery", recovery);
            Set(runtime, "gameClock", Clock);
            Set(runtime, "gameCalendar", Calendar);
            Set(runtime, "gameEventBus", Events);
            Set(runtime, "careLaborInputOwner", Care);
            Set(runtime, "doorSubjectRegistry", Doors);
            return runtime;
        }

        private CharacterActor CreateActor(
            string objectName,
            string id,
            string display,
            CharacterRole role,
            CharacterType type)
        {
            CharacterSO data = CharacterAiEditorTestDependencies
                .CreateCharacterFixtureData(
                    type,
                    display,
                    "human",
                    CharacterRole.Regular);
            data.role = role;
            cleanup.Add(data);
            GameObject actorObject = new(objectName);
            cleanup.Add(actorObject);
            actorObject.AddComponent<SpriteRenderer>();
            CharacterActor actor = actorObject.AddComponent<CharacterActor>();
            actorObject.AddComponent<AbilityMove>();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            actor.RefreshAbilityCache();
            actor.Initialization(data);
            actor.EnsureRuntimeState();
            actor.Identity.SetPersistentId(id);
            actor.Identity.SetCharacterType(type);
            actor.characterType = type;
            actor.SetLifecycleState(
                role == CharacterRole.Owner
                    ? CharacterLifecycleState.Active
                    : CharacterLifecycleState.Downed);
            actor.SetAiPaused(role != CharacterRole.Owner);
            return actor;
        }

        private BuildableObject CreateHousing(string suffix)
        {
            BuildingSO data = AssetDatabase.LoadAssetAtPath<BuildingSO>(
                HousingAssetPath);
            Require(data?.GetCaptiveHousingAbility()?.IsValid == true,
                "Authored captive housing asset is unavailable.");
            GameObject buildingObject = new("D09 Housing " + suffix);
            cleanup.Add(buildingObject);
            BuildableObject building =
                buildingObject.AddComponent<BuildableObject>();
            CharacterAiEditorTestDependencies.Inject(building);
            building.RestorePersistentIdentity(new BuildingInstanceId(
                "building:d09-housing-" + suffix));
            building.Initialization(data, new Vector2Int(7, 9));
            return building;
        }

        private static void Set(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingFieldException(
                    target.GetType().FullName,
                    fieldName);
            field.SetValue(target, value);
        }
    }

    internal sealed class ToolReturnMarkers : IItemMarkerPresenter
    {
        internal Action OnRefresh;
        public void Initialize(IWorldItemMarkerDataSource dataSource) { }
        public void RefreshAll(IEnumerable<Vector2Int> positions) { }
        public void RefreshAt(Vector2Int position) => OnRefresh?.Invoke();
        public bool TryGetMarkerAt(Vector2Int position, out UnityEngine.Object marker)
        { marker = null; return false; }
        public void Clear() { }
    }

    public class DurableToolItems : DispatchProxy
    {
        internal WorldItemStackRuntime Target;
        internal bool FailNextUniqueSpawn;
        internal bool FailNextDeliveryRequest;
        internal int DeliveryRequests;
        protected override object Invoke(MethodInfo method, object[] args)
        {
            if (method.Name == nameof(IWorldItemStackRuntime.GetAllStacks)) return Target.GetAllStacks();
            if (method.Name == nameof(IWorldItemStackRuntime.TryRequestItemDelivery))
            {
                DeliveryRequests++;
                bool reject = FailNextDeliveryRequest;
                FailNextDeliveryRequest = false;
                args[4] = reject ? 0 : 1;
                args[5] = reject ? "d09-injected-delivery-request-failure" : string.Empty;
                return !reject;
            }
            if (method.Name != nameof(IWorldItemStackRuntime.SpawnExistingUniqueItemAt)
                || args.Length != 7)
                throw new InvalidOperationException("Unexpected durable-tool physical call: " + method.Name);
            args[6] = string.Empty;
            if (FailNextUniqueSpawn)
            {
                FailNextUniqueSpawn = false;
                return false;
            }
            bool result = Target.SpawnExistingUniqueItemAt((string)args[0], (ItemInstanceId)args[1],
                (Vector2Int)args[2], (WorldItemStackState)args[3], (string)args[4],
                (IReadOnlyList<ItemInstanceComponentSaveData>)args[5], out string stackId);
            args[6] = stackId;
            return result;
        }
    }

    private sealed class ConfigurablePublisher : IDomainFactStagingPublisher
    {
        private readonly IDomainFactStagingPublisher inner;
        private int failures;

        internal ConfigurablePublisher(
            IDomainFactStagingPublisher inner,
            int failures)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.failures = Math.Max(0, failures);
        }

        internal int RehabilitationCommitCount { get; private set; }
        internal int ReleaseCommitCount { get; private set; }

        public bool TryStage<TFact>(
            in TFact fact,
            out IDomainFactStage stage,
            out string failureReason)
        {
            TransitionFactKind kind = fact switch
            {
                CaptivityRehabilitationProgressedFact _ =>
                    TransitionFactKind.Rehabilitation,
                CaptivityReleasedFact _ => TransitionFactKind.Release,
                _ => TransitionFactKind.None
            };
            if (kind == TransitionFactKind.None)
            {
                stage = null;
                failureReason = "d09-unexpected-transition-fact";
                return false;
            }
            if (!inner.TryStage(fact, out stage, out failureReason))
                return false;
            stage = new ConfigurableStage(this, stage, kind);
            return true;
        }

        private sealed class ConfigurableStage : IDomainFactStage
        {
            private readonly ConfigurablePublisher owner;
            private readonly IDomainFactStage inner;
            private readonly TransitionFactKind kind;

            internal ConfigurableStage(
                ConfigurablePublisher owner,
                IDomainFactStage inner,
                TransitionFactKind kind)
            {
                this.owner = owner;
                this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
                this.kind = kind;
            }

            public string OperationKey => inner.OperationKey;
            public long OwnerRevision => inner.OwnerRevision;
            public bool IsReplay => inner.IsReplay;
            public bool IsActive => inner.IsActive;

            public DomainFactDelivery Commit()
            {
                if (kind == TransitionFactKind.Rehabilitation)
                    owner.RehabilitationCommitCount++;
                else
                    owner.ReleaseCommitCount++;
                if (owner.failures > 0)
                {
                    owner.failures--;
                    inner.Cancel();
                    return DomainFactDelivery.Reject(
                        "d09-injected-commit-rejection");
                }
                return inner.Commit();
            }

            public void Cancel() => inner.Cancel();
        }

        private enum TransitionFactKind
        {
            None,
            Rehabilitation,
            Release
        }
    }

    private sealed class FakeFood : ISurvivalFoodCommand
    {
        private int available;
        internal FakeFood(int available) => this.available = available;
        internal int Consumed { get; private set; }
        public bool TryApplySurvivalWork(IBuildingVisitorPort actor,
            BuildableObject building, WorkTypeId workTypeId, out int amount,
            out DomainFailure failure)
        {
            amount = 0;
            failure = DomainFailure.None;
            return false;
        }
        public int TryConsumeStoredStock(StockCategory category, int amount)
        {
            int consumed = Math.Min(Math.Max(0, amount), available);
            available -= consumed;
            Consumed += consumed;
            return consumed;
        }
    }

    private sealed class FakeCareOwner : ICaptivityCareLaborInputOwnerRuntime
    {
        private int failures;
        internal FakeCareOwner(int failures) => this.failures = failures;
        internal void FailNext() => failures = 1;
        public bool TryReconcileLive(IReadOnlyList<CaptiveState> states,
            out string failureReason)
        {
            if (failures-- > 0)
            {
                failureReason = "d09-injected-physical-cleanup-failure";
                return false;
            }
            failureReason = string.Empty;
            return true;
        }
        public bool TryReconcileRestore(IReadOnlyList<CaptiveState> states,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class FakeDoorSubjects : IDoorAccessSubjectRegistry
    {
        private readonly HashSet<string> captives = new(StringComparer.Ordinal);
        internal bool IsCaptive(string id) => captives.Contains(id);
        public void SetCaptive(string persistentId, bool captive)
        {
            if (captive) captives.Add(persistentId);
            else captives.Remove(persistentId);
        }
        public void SetCapturedWildlife(string wildlifeId, bool captured) { }
        public void ReplaceCaptiveSubjects(IEnumerable<string> persistentIds)
        {
            captives.Clear();
            foreach (string id in persistentIds ?? Array.Empty<string>())
                captives.Add(id);
        }
        public void ReplaceCapturedWildlifeSubjects(
            IEnumerable<string> wildlifeIds) { }
    }

    private sealed class FakeClock : IGameClock
    {
        internal float Elapsed;
        public float DeltaTime => 0.02f;
        public float Time => GameCalendarRules.SecondsPerDay * 21f + Elapsed;
        public int FrameCount => 120;
        public bool IsPaused => false;
    }

    private sealed class FakeWorldRegistry : ICharacterAiWorldRegistry
    {
        private readonly List<CharacterActor> characters;
        private readonly List<BuildableObject> buildings;
        internal FakeWorldRegistry(IEnumerable<CharacterActor> characters,
            IEnumerable<BuildableObject> buildings)
        {
            this.characters = characters.ToList();
            this.buildings = buildings.ToList();
        }
        public int CharacterVersion => 1;
        public IReadOnlyList<CharacterActor> Characters => characters;
        public int LifetimeCharacterVersion => 1;
        public IReadOnlyList<CharacterActor> AllCharacters => characters;
        public int WildlifeVersion => 0;
        public IReadOnlyList<WildlifeActor> Wildlife => Array.Empty<WildlifeActor>();
        public int BuildingVersion => 1;
        public IReadOnlyList<BuildableObject> Buildings => buildings;
        public int WarehouseVersion => 0;
        public IReadOnlyList<IWarehouseFacility> Warehouses =>
            Array.Empty<IWarehouseFacility>();
        public int RetailVersion => 0;
        public IReadOnlyList<IRetailFacility> RetailFacilities =>
            Array.Empty<IRetailFacility>();
        public int Version => 1;
        public void RegisterCharacter(CharacterActor actor) => characters.Add(actor);
        public void UnregisterCharacter(CharacterActor actor) => characters.Remove(actor);
        public void RegisterCharacterLifetime(CharacterActor actor) { }
        public void UnregisterCharacterLifetime(CharacterActor actor) { }
        public void RegisterWildlife(WildlifeActor actor) { }
        public void UnregisterWildlife(WildlifeActor actor) { }
        public void RegisterBuilding(BuildableObject building) => buildings.Add(building);
        public void UnregisterBuilding(BuildableObject building) => buildings.Remove(building);
        public int ReleaseTransientBuildingOwnership(
            IBuildingVisitorPort visitor, string reason) => 0;
        public int GetTransientBuildingOwnershipCount(CharacterId characterId) => 0;
        public void RegisterWarehouse(IWarehouseFacility warehouse) { }
        public void UnregisterWarehouse(IWarehouseFacility warehouse) { }
        public void SetGrid(Grid grid) { }
        public bool TryGetGrid(out Grid grid) { grid = null; return false; }
        public bool TryGetSessionState(out GameSessionState data)
        { data = null; return false; }
        public void Clear() { characters.Clear(); buildings.Clear(); }
    }
}
#endif
