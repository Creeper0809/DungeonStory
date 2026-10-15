#if UNITY_EDITOR
using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public static partial class GameplayModuleOwnerEligibilityDebugScenarios
{
    private static void VerifyPersistedSkillBinding(ICharacterSkillSystemSettingsProvider settings)
    {
        PrimitiveSkillModuleSelectionCapture capture = CharacterCombatModuleSelectionSourceCapture.CaptureSkill(
            "owner:audit-persisted-skill", settings.Settings, CharacterSkillKind.Active, 0, 4);
        using var owner = new CharacterCombatModuleSelectionSourceCapture.SelectionActor(
            capture.CharacterId, capture.DisplayName, capture.Checkpoint);
        CharacterProgression progression = owner.Actor.Progression;
        CharacterSkillDraft draft = progression.GrowthState.drafts.Single(value => value.requestKey == capture.Draft.requestKey);
        Require(draft.ownerBinding != null && draft.ownerBinding.Matches(progression, out _)
            && JsonUtility.ToJson(draft.ownerBinding) == JsonUtility.ToJson(capture.Draft.ownerBinding),
            "Actual restored skill owner binding changed.");
        // The legacy source fixture inserts a level-10 request into a level-1 owner.
        // Reissue must follow live unlock rules, not bless that fixture's synthetic gate.
        int eligibleLevel = settings.Settings.activeUnlockLevels.Where(value => value <= progression.Level).Min();
        Require(!progression.GrowthState.drafts.Any(value => value.kind == CharacterSkillKind.Active
            && value.unlockLevel == eligibleLevel && value.permanentlyChosen), "Fixture already chose the live unlock slot.");
        var creator = new CharacterSkillGenerationService(settings, new DeferredRuntime(), new FixedUiClock());
        draft = creator.CreateDraft(progression, CharacterSkillKind.Active, eligibleLevel,
            progression.GrowthState.skillGenerationRevision);
        progression.GrowthState.drafts.RemoveAll(value => value.kind == draft.kind && value.unlockLevel == eligibleLevel);
        progression.GrowthState.drafts.Add(draft);
        owner.QueueSkillRequest(draft);
        string originalBinding = JsonUtility.ToJson(draft.ownerBinding);
        string saved = JsonUtility.ToJson(draft);
        owner.Actor.Lifecycle.SetLifecycleState(owner.Actor.CurrentLifecycleState);
        owner.TickSkillRequests();
        Require(draft.presentationState == CharacterSkillPresentationState.InvalidatedOwner
            && progression.GrowthState.drafts.Any(value => value.kind == draft.kind
                && value.unlockLevel == draft.unlockLevel && value.requestKey != draft.requestKey
                && value.ownerBinding != null && value.ownerBinding.Matches(progression, out _)),
            "Actual skill service tick did not invalidate and replace the stale request.");
        int countAfterReissue = progression.GrowthState.drafts.Count;
        owner.TickSkillRequests();
        Require(progression.GrowthState.drafts.Count == countAfterReissue,
            "Skill service repeatedly generated replacement requests for unchanged owner state.");
        foreach (bool missing in new[] { false, true })
        {
            CharacterSkillDraft restored = JsonUtility.FromJson<CharacterSkillDraft>(saved);
            if (missing) restored.ownerBinding = null;
            progression.GrowthState.drafts.RemoveAll(value => value.requestKey == restored.requestKey);
            progression.GrowthState.drafts.Add(restored);
            var transport = new DeferredRuntime();
            var service = new CharacterSkillGenerationService(settings, transport, new FixedUiClock());
            JObject before = owner.CaptureOwnerCheckpoint();
            service.RequestDraft(progression, restored);
            Require(restored.presentationState == CharacterSkillPresentationState.InvalidatedOwner
                && !restored.requestSubmitted && service.PendingRequestCount == 0 && transport.Callback == null
                && service.LastDiagnostic.StartsWith("STALE_OWNER", StringComparison.Ordinal)
                && (missing ? restored.ownerBinding == null : JsonUtility.ToJson(restored.ownerBinding) == originalBinding)
                && JToken.DeepEquals(before, owner.CaptureOwnerCheckpoint()),
                "Restored stale/legacy skill request rebound, dispatched or changed authoritative state.");
            CharacterSkillDraft replacement = service.CreateDraft(progression, restored.kind,
                restored.unlockLevel, progression.GrowthState.skillGenerationRevision);
            Require(replacement.requestKey != restored.requestKey
                && replacement.ownerBinding.Matches(progression, out _),
                "Replacement skill reused the stale request identity.");
        }
    }

    private static void VerifyPersistedTraitBinding()
    {
        var settings = AssetDatabase.LoadAssetAtPath<CharacterAcquiredTraitSettingsSO>(
            V25AcquiredTraitContentAssetBuilder.SettingsPath);
        var modules = AssetDatabase.FindAssets("t:CharacterAcquiredTraitModuleSO",
                new[] { V25AcquiredTraitContentAssetBuilder.ModuleRoot })
            .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<CharacterAcquiredTraitModuleSO>)
            .Where(value => value != null).OrderBy(value => value.ModuleId, StringComparer.Ordinal).ToArray();
        PrimitiveTraitModuleSelectionCapture capture = CharacterCombatModuleSelectionSourceCapture.CaptureTrait(
            "owner:audit-persisted-trait", settings, modules, 4);
        using var owner = new CharacterCombatModuleSelectionSourceCapture.SelectionActor(
            capture.CharacterId, capture.DisplayName, capture.Checkpoint);
        CharacterProgression progression = owner.Actor.Progression;
        CharacterAcquiredTraitAggregateState before = progression.CaptureAcquiredTraitState();
        CharacterAcquiredTraitPendingRequestState pending = before.pendingRequests.Single();
        Require(pending.ownerBinding != null && pending.ownerBinding.Matches(progression, out _)
            && JsonUtility.ToJson(pending.ownerBinding) == JsonUtility.ToJson(capture.Pending.ownerBinding),
            "Actual restored trait binding changed.");
        var service = new CharacterAcquiredTraitInferenceService(settings, modules);
        Require(!service.TryInvalidateStalePending(progression, pending.requestId, before.revision, out _),
            "Current-owner trait request was discarded.");
        owner.Actor.Lifecycle.SetLifecycleState(owner.Actor.CurrentLifecycleState);
        JObject ownerBefore = owner.CaptureOwnerCheckpoint();
        var timestamp = NarrativeInferenceTimestamp.FromGameTick(owner.Calendar.AbsoluteHour);
        var completion = service.CompleteMilestone(progression,
            new CharacterAcquiredTraitCompletionCommand(capture.CharacterId, pending.requestId, pending.requestKey,
                pending.candidatePacketHash, pending.registeredRevision, before.revision, "not-json", timestamp),
            capture.Packet, owner.TraitInferenceFacts, owner.TraitInferences);
        Require(!completion.Succeeded && completion.Audit.ValidationError.StartsWith("STALE_OWNER", StringComparison.Ordinal)
            && JsonUtility.ToJson(before) == JsonUtility.ToJson(progression.CaptureAcquiredTraitState())
            && JToken.DeepEquals(ownerBefore, owner.CaptureOwnerCheckpoint()),
            "Stale trait response parsed, consumed a milestone or changed effects.");
        string ledgerBefore = JsonUtility.ToJson(progression.NarrativeLedger);
        Require(service.TryInvalidateStalePending(progression, pending.requestId, before.revision, out _),
            "Stale persisted trait request was not invalidated.");
        CharacterAcquiredTraitAggregateState invalidated = progression.CaptureAcquiredTraitState();
        Require(invalidated.revision == before.revision + 1 && invalidated.pendingRequests.Count == 0
            && invalidated.processedMilestones.SequenceEqual(before.processedMilestones)
            && invalidated.instances.Count == before.instances.Count
            && invalidated.lastRequestInvalidation?.requestId == pending.requestId
            && ledgerBefore == JsonUtility.ToJson(progression.NarrativeLedger)
            && JToken.DeepEquals(ownerBefore, owner.CaptureOwnerCheckpoint()),
            "Trait invalidation consumed gameplay state or lost its receipt.");
        Require(!service.TryInvalidateStalePending(progression, pending.requestId, invalidated.revision, out _)
            && JsonUtility.ToJson(invalidated) == JsonUtility.ToJson(progression.CaptureAcquiredTraitState()),
            "Repeated invalidation changed the aggregate.");
        const string semanticHash = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        CharacterAcquiredTraitManifestationRuntime.BuildProductionRequestIdentity(settings, capture.CharacterId,
            pending.manifestationMilestone, pending.evidenceFactIds, semanticHash, pending.ownerBinding,
            pending.registeredRevision - 1, out string oldId, out string oldKey);
        CharacterAcquiredTraitManifestationRuntime.BuildProductionRequestIdentity(settings, capture.CharacterId,
            pending.manifestationMilestone, pending.evidenceFactIds, semanticHash,
            CharacterNarrativeRequestOwnerBinding.Capture(progression), invalidated.revision, out string newId, out string newKey);
        Require(oldId != newId && oldKey != newKey, "Live identity builder reused the invalidated epoch.");
        var submission = service.SubmitMilestone(progression,
            new CharacterAcquiredTraitSubmissionCommand(capture.CharacterId, newId, newKey,
                pending.manifestationMilestone, pending.evidenceFactIds, pending.evidenceBindings,
                invalidated.revision, timestamp));
        Require(submission.Succeeded, "Eligible replacement trait submission failed: " + submission.Audit.ValidationError);
        var restored = JsonUtility.FromJson<CharacterAcquiredTraitAggregateState>(
            JsonUtility.ToJson(progression.CaptureAcquiredTraitState()));
        Require(restored.pendingRequests.Single().requestId == newId
            && restored.pendingRequests.Single().ownerBinding.Matches(progression, out _)
            && restored.lastRequestInvalidation.requestId == pending.requestId
            && restored.processedMilestones.SequenceEqual(before.processedMilestones),
            "Replacement trait lost binding/receipt through JSON or consumed a milestone.");
    }
}
#endif
