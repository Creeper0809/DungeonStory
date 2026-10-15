#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>Actual combat owner -> saved progression -> live inference boundary.</summary>
public static partial class GameplayModuleOwnerEligibilityDebugScenarios
{
    public static void RunSkillEvidenceAgingBatch()
    {
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output) || Directory.Exists(output))
            throw new InvalidOperationException("A new skill evidence aging output directory is required.");
        Directory.CreateDirectory(output);
        var observations = new JArray();
        try
        {
            var settings = new ResourceCharacterSkillSystemSettingsProvider(
                new ResourceGameContentCatalog(new UnityGameContentRootLoader()));
            VerifyDelayedPassiveEvidence(settings, ageEvidence: false, observations);
            VerifyDelayedPassiveEvidence(settings, ageEvidence: true, observations);
            VerifyReadyActiveEvidenceRestore(settings, observations);
            Write(output, new JObject
            {
                ["testId"] = "audit-skill-evidence-aging", ["passed"] = true,
                ["observations"] = observations, ["controlledDomainExecution"] = true,
                ["naturalPlayTelemetry"] = false, ["fullAuditClosure"] = false
            });
        }
        catch (Exception error)
        {
            Write(output, new JObject
            {
                ["testId"] = "audit-skill-evidence-aging", ["passed"] = false,
                ["observations"] = observations, ["failure"] = error.ToString()
            });
            throw;
        }
    }

    private static void VerifyDelayedPassiveEvidence(
        ICharacterSkillSystemSettingsProvider settings, bool ageEvidence, JArray observations)
    {
        PrimitiveSkillModuleSelectionCapture capture = CharacterCombatModuleSelectionSourceCapture.CaptureSkill(
            "owner:audit-skill-aging:" + (ageEvidence ? "delayed" : "control"),
            settings.Settings, CharacterSkillKind.Passive, 0, 4);
        using var owner = new CharacterCombatModuleSelectionSourceCapture.SelectionActor(
            capture.CharacterId, capture.DisplayName, capture.Checkpoint);
        CharacterProgression progression = owner.Progression;
        var query = new GameplayOutcomeNarrativeEvidenceQuery(owner.Ledger);
        var runtime = new DeferredRuntime();
        var service = new CharacterSkillGenerationService(settings, runtime, new FixedUiClock(), query);
        SetProgressionDependency(progression, "generationService", service);
        SetProgressionDependency(progression, "settingsProvider", settings);
        SetProgressionDependency(progression, "evidenceUseTransaction",
            new GameplayOutcomeEvidenceUseTransaction(owner.Ledger, query));
        CharacterSkillDraft draft = service.CreateDraft(progression, CharacterSkillKind.Passive, 1,
            progression.GrowthState.skillGenerationRevision);
        progression.GrowthState.drafts.RemoveAll(value => value.kind == draft.kind && value.unlockLevel == 1);
        progression.GrowthState.drafts.Add(draft);
        Require(progression.PassiveSkills.Count == 0, "Aging fixture already has a passive skill.");
        CharacterSkillModuleOfferState offer = draft.moduleSelectionOffers.Single();
        GameplayOutcomeEvidenceBindingSnapshot selected = draft.outcomeEvidenceBindings.First(value =>
            offer.evidenceFactIds.Contains(value.publicFactId) && value.sourceKind == "exact");
        Require(query.TryResolveExactBinding(selected, out var originalSource),
            "Fresh evidence does not resolve before dispatch.");
        var response = new CharacterSkillModuleSelectionResponseDto
        {
            selectionId = offer.selectionId,
            positiveModuleIds = new List<string> { offer.positiveModuleIds[0] },
            drawbackModuleIds = new List<string>(),
            evidenceFactIds = new List<string> { selected.publicFactId },
            displayName = capture.DisplayName + " 감각",
            narrativeFlavor = capture.DisplayName + "의 반복된 경험에서 익힌 감각이다."
        };
        var batch = new CharacterSkillModuleSelectionBatchResponseDto();
        batch.candidates.Add(response);
        string responseJson = JsonUtility.ToJson(batch);
        Require(service.TryValidateResponse(draft, JsonUtility.ToJson(response), out _, out string validation),
            "Aging fixture response is not legal: " + validation);
        service.RequestDraft(progression, draft);
        service.Tick();
        Require(runtime.Callback != null && service.PendingRequestCount == 1,
            "Aging fixture did not reach the real deferred transport: " + service.LastDiagnostic);

        if (ageEvidence)
        {
            GameplayOutcomeLedgerSaveData before = owner.Ledger.CaptureGameplayOutcomes();
            var registry = (IGameplayOutcomeRegistry)(typeof(GameplayOutcomeLedger)
                .GetField("registry", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingFieldException("GameplayOutcomeLedger.registry")).GetValue(owner.Ledger);
            int day = Math.Max(before.lastScheduledEvaluationDay + 1,
                before.exactOutcomes.Max(value => value.absoluteDay) + 1);
            Require(owner.Ledger.TrySchedule(new GameplayOutcomeConsolidationRequest(
                    day, before.nextSequence - 1, registry.ConsolidationPolicyVersion, owner.Ledger.CurrentWorldEpoch),
                    out string scheduleFailure), "Actual evidence aging could not be scheduled: " + scheduleFailure);
            int slices = 0;
            while (owner.Ledger.PendingJobCount > 0 && slices++ < 1000)
                owner.Ledger.ProcessSlice(64, 0L);
            Require(owner.Ledger.PendingJobCount == 0, "Actual evidence aging did not finish.");
        }

        bool resolvesBeforeCallback = query.TryResolveExactBinding(selected, out _);
        GameplayOutcomeNarrativeEvidenceSource currentSource = query.GetForCharacter(
            capture.CharacterId, 24, 0f, includeCompacted: true).Single(value => value.SourceId == selected.publicFactId);
        var observation = new JObject
        {
            ["case"] = ageEvidence ? "delayed" : "control", ["requestKey"] = draft.requestKey,
            ["selectedBinding"] = JObject.Parse(JsonConvert.SerializeObject(selected)),
            ["currentSalience"] = currentSource.SubjectSalience,
            ["currentAnchorRevision"] = currentSource.AnchorRevision,
            ["currentInfluenceRevision"] = currentSource.InfluenceRevision,
            ["bindingResolvesBeforeCallback"] = resolvesBeforeCallback
        };
        observations.Add(observation);
        Require(ageEvidence ? !resolvesBeforeCallback
                && currentSource.SubjectSalience < originalSource.SubjectSalience : resolvesBeforeCallback,
            "The aging/control fixture did not exercise the expected evidence freshness boundary.");
        string ledgerBefore = JsonConvert.SerializeObject(owner.Ledger.CaptureGameplayOutcomes());
        long acquisitionBefore = progression.GrowthState.skillAcquisitionRevision;
        Action<LocalLlmResult> originalCallback = runtime.Callback;
        Exception callbackError = null;
        try { runtime.Callback(new LocalLlmResult(LocalLlmRequestStatus.Succeeded, responseJson, "", "")); }
        catch (Exception error) { callbackError = error; }
        observation["callbackError"] = callbackError?.ToString() ?? string.Empty;
        observation["ready"] = draft.isReady;
        observation["chosen"] = draft.permanentlyChosen;
        observation["candidateCount"] = draft.candidates.Count;
        observation["passiveCount"] = progression.PassiveSkills.Count;
        observation["pendingCount"] = service.PendingRequestCount;
        observation["presentationState"] = draft.presentationState.ToString();
        observation["diagnostic"] = service.LastDiagnostic;
        observation["ledgerUnchanged"] = ledgerBefore == JsonConvert.SerializeObject(owner.Ledger.CaptureGameplayOutcomes());
        observation["acquisitionRevision"] = progression.GrowthState.skillAcquisitionRevision;
        if (!ageEvidence)
        {
            Require(callbackError == null && draft.permanentlyChosen && progression.PassiveSkills.Count == 1
                && progression.GrowthState.skillAcquisitionRevision == acquisitionBefore + 1,
                "Current-evidence passive did not commit: " + callbackError);
            string completedGrowth = JsonUtility.ToJson(progression.GrowthState);
            string completedLedger = JsonConvert.SerializeObject(owner.Ledger.CaptureGameplayOutcomes());
            originalCallback(new LocalLlmResult(LocalLlmRequestStatus.Succeeded, responseJson, "", ""));
            observation["normalDuplicateUnchanged"] = completedGrowth == JsonUtility.ToJson(progression.GrowthState)
                && completedLedger == JsonConvert.SerializeObject(owner.Ledger.CaptureGameplayOutcomes());
            Require((bool)observation["normalDuplicateUnchanged"], "Completed callback changed a chosen draft or its ledger.");
            service.CancelRequests(progression);
            return;
        }
        Require((bool)observation["ledgerUnchanged"] && progression.PassiveSkills.Count == 0
            && progression.GrowthState.skillAcquisitionRevision == acquisitionBefore,
            "Rejected stale evidence changed the ledger or acquired a passive.");
        Require(callbackError == null && !draft.isReady && !draft.permanentlyChosen
            && service.LastDiagnostic.StartsWith("STALE_EVIDENCE", StringComparison.Ordinal),
            "A delayed stale-evidence callback must be explicitly rejected without an exception or a stranded Ready draft.");
        string invalidatedGrowth = JsonUtility.ToJson(progression.GrowthState);
        originalCallback(new LocalLlmResult(LocalLlmRequestStatus.Succeeded, responseJson, "", ""));
        Require(invalidatedGrowth == JsonUtility.ToJson(progression.GrowthState)
            && ledgerBefore == JsonConvert.SerializeObject(owner.Ledger.CaptureGameplayOutcomes()),
            "Repeated stale callback changed the rejected request or the ledger.");
        CharacterSkillDraft savedInvalidation = JsonUtility.FromJson<CharacterSkillDraft>(JsonUtility.ToJson(draft));
        Require(savedInvalidation.IsInvalidated() && !savedInvalidation.isReady
            && savedInvalidation.presentationCorrection == draft.presentationCorrection
            && JsonConvert.SerializeObject(savedInvalidation.outcomeEvidenceBindings)
                == JsonConvert.SerializeObject(draft.outcomeEvidenceBindings),
            "Invalidated request lost its exact reason or evidence through draft JSON restore.");
        progression.GrowthState.drafts[progression.GrowthState.drafts.IndexOf(draft)] = savedInvalidation;
        service.RequestDraft(progression, savedInvalidation);
        Require(service.PendingRequestCount == 0, "Restored invalidated request was redispatched.");
        service.Tick();
        CharacterSkillDraft replacement = progression.GrowthState.drafts.Single(value =>
            value.kind == CharacterSkillKind.Passive && value.unlockLevel == 1 && !value.IsInvalidated());
        Require(replacement.requestKey != draft.requestKey
            && replacement.ownerBinding.Matches(progression, out _)
            && replacement.rules.Select(value => value.rarity).SequenceEqual(draft.rules.Select(value => value.rarity))
            && replacement.outcomeEvidenceBindings.All(value => query.TryResolveExactBinding(value, out _)),
            "Replacement reused the old identity, rerolled rarity, or captured stale evidence.");
        int replacementDraftCount = progression.GrowthState.drafts.Count;
        service.Tick();
        Require(replacementDraftCount == progression.GrowthState.drafts.Count,
            "An unchanged replacement repeatedly created new drafts.");
        string beforeLateCallback = JsonUtility.ToJson(progression.GrowthState);
        originalCallback(new LocalLlmResult(LocalLlmRequestStatus.Succeeded, responseJson, "", ""));
        Require(beforeLateCallback == JsonUtility.ToJson(progression.GrowthState),
            "The old callback changed its replacement request.");
        CharacterSkillModuleOfferState replacementOffer = replacement.moduleSelectionOffers.Single();
        var replacementResponse = new CharacterSkillModuleSelectionResponseDto
        {
            selectionId = replacementOffer.selectionId,
            positiveModuleIds = new List<string> { replacementOffer.positiveModuleIds[0] },
            drawbackModuleIds = new List<string>(),
            evidenceFactIds = new List<string> { replacement.outcomeEvidenceBindings.First(value =>
                replacementOffer.evidenceFactIds.Contains(value.publicFactId)).publicFactId },
            displayName = capture.DisplayName + " 감각",
            narrativeFlavor = capture.DisplayName + "의 반복된 경험에서 익힌 감각이다."
        };
        var replacementBatch = new CharacterSkillModuleSelectionBatchResponseDto();
        replacementBatch.candidates.Add(replacementResponse);
        string replacementJson = JsonUtility.ToJson(replacementBatch);
        observation["invalidationDraftJsonRoundTrip"] = true;
        observation["staleCallbackNoOp"] = true;
        observation["replacementRequestKey"] = replacement.requestKey;
        observation["rarityRollPreserved"] = true;
        Action<LocalLlmResult> replacementCallback = runtime.Callbacks.Single(value =>
            service.TryCapturePendingModuleDispatch(value.Key, out _, out CharacterSkillDraft dispatched,
                out _, out _) && dispatched.requestKey == replacement.requestKey).Value;
        replacementCallback(new LocalLlmResult(LocalLlmRequestStatus.Succeeded, replacementJson, "", ""));
        Require(replacement.permanentlyChosen && progression.PassiveSkills.Count == 1
            && progression.GrowthState.skillAcquisitionRevision == acquisitionBefore + 1,
            "The replacement current-evidence response did not acquire the passive exactly once.");
        string finalGrowth = JsonUtility.ToJson(progression.GrowthState);
        string finalLedger = JsonConvert.SerializeObject(owner.Ledger.CaptureGameplayOutcomes());
        replacementCallback(new LocalLlmResult(LocalLlmRequestStatus.Succeeded, replacementJson, "", ""));
        originalCallback(new LocalLlmResult(LocalLlmRequestStatus.Succeeded, responseJson, "", ""));
        Require(finalGrowth == JsonUtility.ToJson(progression.GrowthState)
            && finalLedger == JsonConvert.SerializeObject(owner.Ledger.CaptureGameplayOutcomes()),
            "Late or duplicate response changed the committed replacement.");
        observation["invalidationDraftJsonRoundTrip"] = true;
        observation["staleCallbackNoOp"] = true;
        observation["replacementRequestKey"] = replacement.requestKey;
        observation["rarityRollPreserved"] = true;
        observation["replacementAcquiredExactlyOnce"] = true;
        observation["replacementDuplicateNoOp"] = true;
        service.CancelRequests(progression);
    }

    private static void VerifyReadyActiveEvidenceRestore(
        ICharacterSkillSystemSettingsProvider settings,
        JArray observations)
    {
        PrimitiveSkillModuleSelectionCapture capture =
            CharacterCombatModuleSelectionSourceCapture.CaptureSkill(
                "owner:audit-ready-active-aging",
                settings.Settings,
                CharacterSkillKind.Active,
                0,
                4);
        CharacterProgressionSnapshot pendingSnapshot;
        JObject pendingOwnerCheckpoint;
        string originalRequestKey;
        string replacementRequestKey;
        string invalidationReason;
        int eligibleLevel;
        int baselineActiveCount;
        string[] baselineActiveIds;
        long acquisitionBefore;

        using (var source = new CharacterCombatModuleSelectionSourceCapture.SelectionActor(
                   capture.CharacterId,
                   capture.DisplayName,
                   capture.Checkpoint))
        {
            CharacterProgression progression = source.Progression;
            var query = new GameplayOutcomeNarrativeEvidenceQuery(source.Ledger);
            var runtime = new DeferredRuntime();
            var service = new CharacterSkillGenerationService(
                settings,
                runtime,
                new FixedUiClock(),
                query);
            ConfigureProgression(progression, settings, service, source.Ledger, query);
            eligibleLevel = settings.Settings.activeUnlockLevels
                .Where(value => value <= progression.Level)
                .Min();
            baselineActiveCount = progression.ActiveSkills.Count;
            baselineActiveIds = progression.ActiveSkills
                .Where(value => value != null)
                .Select(value => value.id)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            Require(baselineActiveCount < CharacterProgression.NormalActiveSlots,
                "The active restore fixture has no free active slot.");
            foreach (CharacterSkillDraft existing in progression.GrowthState.drafts
                         .Where(value => value != null)
                         .ToArray())
            {
                progression.MarkGenerationRequestCompleted(existing.requestKey);
            }
            progression.GrowthState.drafts.Clear();
            progression.GrowthState.pendingRequestKeys.Clear();
            CharacterSkillDraft draft = service.CreateDraft(
                progression,
                CharacterSkillKind.Active,
                eligibleLevel,
                progression.GrowthState.skillGenerationRevision);
            progression.GrowthState.drafts.Add(draft);
            string responseJson = BuildValidModuleSelectionBatchJson(
                service,
                draft,
                capture.DisplayName);
            service.RequestDraft(progression, draft);
            service.Tick();
            Action<LocalLlmResult> originalCallback = RequireDispatchCallback(
                runtime,
                service,
                draft.requestKey);
            originalCallback(new LocalLlmResult(
                LocalLlmRequestStatus.Succeeded,
                responseJson,
                string.Empty,
                string.Empty));
            Require(draft.isReady
                    && !draft.permanentlyChosen
                    && draft.candidates.Count == draft.moduleSelectionOffers.Count,
                "The active fixture did not reach an unchosen Ready state.");
            Require(draft.candidates.SelectMany(value => value.evidenceBindings)
                    .All(value => query.TryResolveExactBinding(value, out _)),
                "The Ready active fixture started with stale evidence.");

            AgeGameplayEvidence(source);
            Require(draft.candidates[0].evidenceBindings
                    .Any(value => !query.TryResolveExactBinding(value, out _)),
                "The Ready active fixture did not age the evidence of the selected candidate.");
            string ledgerBeforeChoice = JsonConvert.SerializeObject(
                source.Ledger.CaptureGameplayOutcomes());
            acquisitionBefore = progression.GrowthState.skillAcquisitionRevision;
            originalRequestKey = draft.requestKey;
            Require(!progression.TryChooseActiveSkill(
                    eligibleLevel,
                    0,
                    confirmed: true,
                    out string staleMessage)
                && string.Equals(
                    staleMessage,
                    "evidence-binding-invalid",
                    StringComparison.Ordinal),
                "A stale Ready active skill was accepted or lost its exact rejection reason.");
            Require(draft.IsInvalidated()
                    && !draft.isReady
                    && !draft.permanentlyChosen
                    && progression.ActiveSkills.Count == baselineActiveCount
                    && baselineActiveIds.SequenceEqual(progression.ActiveSkills
                        .Where(value => value != null)
                        .Select(value => value.id)
                        .OrderBy(value => value, StringComparer.Ordinal),
                        StringComparer.Ordinal)
                    && progression.GrowthState.skillAcquisitionRevision == acquisitionBefore
                    && ledgerBeforeChoice == JsonConvert.SerializeObject(
                        source.Ledger.CaptureGameplayOutcomes()),
                "Rejecting a stale Ready active changed skills, acquisition revision, or the ledger.");
            CharacterSkillDraft replacement = progression.GrowthState.drafts.Single(value =>
                value != null
                && value.kind == CharacterSkillKind.Active
                && value.unlockLevel == eligibleLevel
                && !value.IsInvalidated());
            Require(replacement.requestKey != draft.requestKey
                    && replacement.rules.Select(value => value.rarity)
                        .SequenceEqual(draft.rules.Select(value => value.rarity))
                    && replacement.outcomeEvidenceBindings.All(value =>
                        query.TryResolveExactBinding(value, out _))
                    && replacement.requestSubmitted,
                "Active replacement reused identity, rerolled rarity, or missed current evidence/pending state.");
            replacementRequestKey = replacement.requestKey;
            invalidationReason = draft.presentationCorrection;
            string beforeLateOriginal = JsonUtility.ToJson(progression.GrowthState);
            originalCallback(new LocalLlmResult(
                LocalLlmRequestStatus.Succeeded,
                responseJson,
                string.Empty,
                string.Empty));
            Require(beforeLateOriginal == JsonUtility.ToJson(progression.GrowthState)
                    && ledgerBeforeChoice == JsonConvert.SerializeObject(
                        source.Ledger.CaptureGameplayOutcomes()),
                "A late Ready-active response changed the invalidation or its replacement.");
            pendingSnapshot = progression.CapturePersistentState();
            pendingOwnerCheckpoint = source.CaptureOwnerCheckpoint();
            service.CancelRequests(progression);
        }

        CharacterProgressionSnapshot committedSnapshot;
        JObject committedOwnerCheckpoint;
        string committedSkillId;
        using (var restored = new CharacterCombatModuleSelectionSourceCapture.SelectionActor(
                   capture.CharacterId,
                   capture.DisplayName,
                   null,
                   pendingOwnerCheckpoint))
        {
            CharacterProgression progression = restored.Progression;
            var query = new GameplayOutcomeNarrativeEvidenceQuery(restored.Ledger);
            var runtime = new DeferredRuntime();
            var service = new CharacterSkillGenerationService(
                settings,
                runtime,
                new FixedUiClock(),
                query);
            ConfigureProgression(progression, settings, service, restored.Ledger, query);
            progression.RestorePersistentState(pendingSnapshot);
            CharacterSkillDraft invalidated = progression.GrowthState.drafts.Single(value =>
                string.Equals(value.requestKey, originalRequestKey, StringComparison.Ordinal));
            CharacterSkillDraft replacement = progression.GrowthState.drafts.Single(value =>
                string.Equals(value.requestKey, replacementRequestKey, StringComparison.Ordinal));
            Require(invalidated.IsInvalidated()
                    && !invalidated.isReady
                    && string.Equals(
                        invalidated.presentationCorrection,
                        invalidationReason,
                        StringComparison.Ordinal)
                    && replacement.requestSubmitted
                    && progression.GrowthState.pendingRequestKeys.Contains(
                        replacementRequestKey,
                        StringComparer.Ordinal)
                    && replacement.outcomeEvidenceBindings.All(value =>
                        query.TryResolveExactBinding(value, out _))
                    && progression.ActiveSkills.Count == baselineActiveCount
                    && baselineActiveIds.SequenceEqual(progression.ActiveSkills
                        .Where(value => value != null)
                        .Select(value => value.id)
                        .OrderBy(value => value, StringComparer.Ordinal),
                        StringComparer.Ordinal),
                "Fresh owner restore lost invalidation reason, replacement pending state, or current evidence.");
            service.Tick();
            Action<LocalLlmResult> replacementCallback = RequireDispatchCallback(
                runtime,
                service,
                replacementRequestKey);
            string replacementJson = BuildValidModuleSelectionBatchJson(
                service,
                replacement,
                capture.DisplayName);
            replacementCallback(new LocalLlmResult(
                LocalLlmRequestStatus.Succeeded,
                replacementJson,
                string.Empty,
                string.Empty));
            Require(replacement.isReady
                    && !replacement.permanentlyChosen
                    && replacement.candidates.Count == replacement.moduleSelectionOffers.Count,
                "Fresh owner replacement did not reach Ready exactly once.");
            long restoredAcquisitionBefore = progression.GrowthState.skillAcquisitionRevision;
            Require(progression.TryChooseActiveSkill(
                    eligibleLevel,
                    0,
                    confirmed: true,
                    out _)
                && replacement.permanentlyChosen
                && progression.ActiveSkills.Count == baselineActiveCount + 1
                && progression.GrowthState.skillAcquisitionRevision
                    == restoredAcquisitionBefore + 1,
                "Fresh owner did not commit the replacement active exactly once.");
            committedSkillId = replacement.ChosenSkill?.id
                ?? throw new InvalidOperationException(
                    "The committed replacement has no chosen skill.");
            Require(progression.ActiveSkills.Any(value => value != null
                    && string.Equals(value.id, committedSkillId, StringComparison.Ordinal))
                && baselineActiveIds.All(id => progression.ActiveSkills.Any(value =>
                    value != null && string.Equals(value.id, id, StringComparison.Ordinal))),
                "Committing the replacement lost a pre-existing active skill.");
            string finalGrowth = JsonUtility.ToJson(progression.GrowthState);
            string finalLedger = JsonConvert.SerializeObject(
                restored.Ledger.CaptureGameplayOutcomes());
            replacementCallback(new LocalLlmResult(
                LocalLlmRequestStatus.Succeeded,
                replacementJson,
                string.Empty,
                string.Empty));
            Require(progression.TryChooseActiveSkill(
                    eligibleLevel,
                    0,
                    confirmed: true,
                    out _)
                && finalGrowth == JsonUtility.ToJson(progression.GrowthState)
                && finalLedger == JsonConvert.SerializeObject(
                    restored.Ledger.CaptureGameplayOutcomes()),
                "Duplicate replacement response or choice changed the committed active.");
            committedSnapshot = progression.CapturePersistentState();
            committedOwnerCheckpoint = restored.CaptureOwnerCheckpoint();
            service.CancelRequests(progression);
        }

        using (var committed = new CharacterCombatModuleSelectionSourceCapture.SelectionActor(
                   capture.CharacterId,
                   capture.DisplayName,
                   null,
                   committedOwnerCheckpoint))
        {
            CharacterProgression progression = committed.Progression;
            var query = new GameplayOutcomeNarrativeEvidenceQuery(committed.Ledger);
            var runtime = new DeferredRuntime();
            var service = new CharacterSkillGenerationService(
                settings,
                runtime,
                new FixedUiClock(),
                query);
            ConfigureProgression(progression, settings, service, committed.Ledger, query);
            progression.RestorePersistentState(committedSnapshot);
            CharacterSkillDraft chosen = progression.GrowthState.drafts.Single(value =>
                string.Equals(value.requestKey, replacementRequestKey, StringComparison.Ordinal));
            Require(chosen.permanentlyChosen
                    && chosen.isReady
                    && !chosen.requestSubmitted
                    && progression.ActiveSkills.Count == baselineActiveCount + 1
                    && progression.ActiveSkills.Any(value => value != null
                        && string.Equals(
                            value.id,
                            committedSkillId,
                            StringComparison.Ordinal))
                    && baselineActiveIds.All(id => progression.ActiveSkills.Any(value =>
                        value != null && string.Equals(value.id, id, StringComparison.Ordinal)))
                    && !progression.GrowthState.pendingRequestKeys.Contains(
                        replacementRequestKey,
                        StringComparer.Ordinal),
                "Committed fresh owner restore lost its chosen active or resumed the terminal request.");
            string beforeReplay = JsonUtility.ToJson(progression.CapturePersistentState().GrowthState);
            string ledgerBeforeReplay = JsonConvert.SerializeObject(
                committed.Ledger.CaptureGameplayOutcomes());
            service.RequestDraft(progression, chosen);
            service.Tick();
            Require(progression.TryChooseActiveSkill(
                    eligibleLevel,
                    0,
                    confirmed: true,
                    out _)
                && beforeReplay == JsonUtility.ToJson(
                    progression.CapturePersistentState().GrowthState)
                && ledgerBeforeReplay == JsonConvert.SerializeObject(
                    committed.Ledger.CaptureGameplayOutcomes())
                && !progression.GrowthState.pendingRequestKeys.Contains(
                    replacementRequestKey,
                    StringComparer.Ordinal),
                "Committed fresh owner replay redispatched or reapplied the terminal request.");
            service.CancelRequests(progression);
        }

        observations.Add(new JObject
        {
            ["case"] = "ready-active-fresh-owner",
            ["originalRequestKey"] = originalRequestKey,
            ["replacementRequestKey"] = replacementRequestKey,
            ["eligibleLevel"] = eligibleLevel,
            ["baselineActiveCount"] = baselineActiveCount,
            ["invalidationReason"] = invalidationReason,
            ["acquisitionRevisionBefore"] = acquisitionBefore,
            ["pendingFreshOwnerRestored"] = true,
            ["replacementCommittedExactlyOnce"] = true,
            ["committedFreshOwnerRestored"] = true,
            ["terminalReplayNoOp"] = true
        });
    }

    public static void RunBatch()
    {
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output) || Directory.Exists(output))
            throw new InvalidOperationException("A new eligibility output directory is required.");
        Directory.CreateDirectory(output);
        var checks = new JArray();
        try
        {
            var settings = new ResourceCharacterSkillSystemSettingsProvider(
                new ResourceGameContentCatalog(new UnityGameContentRootLoader()));
            PrimitiveSkillModuleSelectionCapture downed = CharacterCombatModuleSelectionSourceCapture.CaptureSkill(
                "owner:audit-downed", settings.Settings, CharacterSkillKind.Active, 0, 3);
            Require((int)downed.Checkpoint["evidenceOwnerCheckpoint"]["domainState"]["stage"] == 1,
                "Downed source lost its actual body-health owner.");
            VerifyDownedDispatchProjection(settings, downed, output);
            checks.Add("living-downed-request-preserved");
            ExpectDeadSourceRejected(settings.Settings);
            checks.Add("dead-source-rejected");
            foreach (string mutation in new[] { "death", "generation-revision", "lifecycle-revision" })
            {
                VerifyPendingOwnerChange(settings, mutation, output);
                checks.Add("pending-" + mutation + "-rejected-before-response-parse");
            }
            VerifyPersistedSkillBinding(settings);
            checks.Add("persisted-skill-binding-preserved-and-stale-or-missing-binding-not-rebound");
            VerifyPersistedTraitBinding();
            checks.Add("persisted-trait-stale-rejected-invalidated-once-and-new-request-id-without-milestone-consumption");
            Require(CharacterProgressionDebugScenarios.RunAcquiredTraitAutomaticOwnerInvalidationScenario(),
                "Live trait producer invalidation regression failed.");
            checks.Add("live-trait-producer-cancels-and-reissues-on-life-or-generation-change-and-rejects-late-callback");
            Write(output, new JObject
            {
                ["testId"] = "audit-u1-owner-eligibility", ["passed"] = true, ["checks"] = checks,
                ["controlledDomainExecution"] = true, ["naturalPlayTelemetry"] = false,
                ["fullU1Closure"] = false, ["humanApprovalClaimed"] = false, ["trainingEligible"] = false
            });
        }
        catch (Exception error)
        {
            Write(output, new JObject { ["passed"] = false, ["checks"] = checks, ["failure"] = error.ToString() });
            throw;
        }
    }

    private static void ExpectDeadSourceRejected(CharacterSkillSystemSettingsSO settings)
    {
        try
        {
            CharacterCombatModuleSelectionSourceCapture.CaptureSkill(
                "owner:audit-dead", settings, CharacterSkillKind.Active, 0, 5);
        }
        catch (InvalidOperationException error) when (error.Message.Contains("INELIGIBLE_DEAD_OWNER"))
        {
            return;
        }
        throw new InvalidOperationException("Dead source produced a new skill request.");
    }

    private static void VerifyDownedDispatchProjection(ICharacterSkillSystemSettingsProvider settings,
        PrimitiveSkillModuleSelectionCapture capture, string output)
    {
        using var owner = new CharacterCombatModuleSelectionSourceCapture.SelectionActor(
            capture.CharacterId, capture.DisplayName, capture.Checkpoint);
        var runtime = new DeferredRuntime();
        var service = new CharacterSkillGenerationService(settings, runtime, new FixedUiClock());
        CharacterSkillDraft draft = owner.Actor.Progression.GrowthState.drafts.Single(
            value => value.requestKey == capture.Draft.requestKey);
        service.RequestDraft(owner.Actor.Progression, draft);
        service.Tick();
        string key = runtime.Callbacks.Keys.Single();
        string before = owner.CaptureOwnerCheckpoint().ToString();
        Require(GameplayModuleDispatchEligibilityCapture.TryCaptureSkill(service, key,
                out _, out _, out _, out _, out var eligibility)
            && eligibility.decision == "allowed"
            && eligibility.requestId == draft.requestKey
            && eligibility.ownerId == owner.Actor.Identity.PersistentId
            && before == owner.CaptureOwnerCheckpoint().ToString(),
            "Living downed dispatch was rejected, rebound or mutated by eligibility projection.");
        var witness = new JObject
        {
            ["profileId"] = eligibility.profileId, ["correlationId"] = key,
            ["dispatchEligibility"] = JObject.Parse(JsonUtility.ToJson(eligibility))
        };
        GameplayModuleDispatchEligibilityCapture.RequireForExport(witness,
            eligibility.ownerId, eligibility.targetId, eligibility.requestId);
        foreach (string field in new[] { "schema", "scope", "decision", "reason", "authority",
                     "profileId", "correlationId", "ownerId", "targetId", "requestId" })
        {
            var changed = (JObject)witness.DeepClone();
            changed["dispatchEligibility"][field] = "changed";
            bool rejected = false;
            try { GameplayModuleDispatchEligibilityCapture.RequireForExport(changed,
                eligibility.ownerId, eligibility.targetId, eligibility.requestId); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "Export accepted altered dispatch eligibility " + field);
        }
        Require(!GameplayModuleDispatchEligibilityCapture.TryCaptureSkill(service, key + ":missing",
                out _, out _, out _, out _, out var missing) && missing == null,
            "A nonexistent dispatch produced an allowed eligibility witness.");
        File.WriteAllText(Path.Combine(output, "downed-dispatch-eligibility.json"), witness.ToString());
    }

    private static void VerifyPendingOwnerChange(
        ICharacterSkillSystemSettingsProvider settings, string mutation, string output)
    {
        PrimitiveSkillModuleSelectionCapture capture = CharacterCombatModuleSelectionSourceCapture.CaptureSkill(
            "owner:audit-pending:" + mutation, settings.Settings, CharacterSkillKind.Active, 0, 4);
        using var owner = new CharacterCombatModuleSelectionSourceCapture.SelectionActor(
            capture.CharacterId, capture.DisplayName, capture.Checkpoint);
        {
            CharacterActor actor = owner.Actor;
            Require(!actor.IsDead && actor.Stats.CurrentHealth > 20f, "Restored living owner is not the recovered combatant.");
            var runtime = new DeferredRuntime();
            var service = new CharacterSkillGenerationService(settings, runtime, new FixedUiClock());
            CharacterSkillDraft draft = actor.Progression.GrowthState.drafts.Single(value => value.requestKey == capture.Draft.requestKey);
            service.RequestDraft(actor.Progression, draft);
            service.Tick();
            Require(runtime.Callback != null && service.PendingRequestCount == 1,
                "Actual skill dispatch did not reach the deferred transport: " + service.LastDiagnostic);
            string correlationId = runtime.Callbacks.Keys.Single();
            Require(GameplayModuleDispatchEligibilityCapture.TryCaptureSkill(service, correlationId,
                    out _, out _, out _, out _, out var eligible) && eligible != null,
                "Current owner did not produce an authoritative dispatch eligibility witness.");
            if (mutation == "death")
            {
                Require(owner.Execute(MigratedProducerCombatDefenseEntryE2EDebugScenarios.FatalCombatInjuryActionId),
                    "Actual fatal combat failed.");
                Require(actor.IsDead, "Fatal combat did not kill the same request owner.");
            }
            else if (mutation == "generation-revision")
                actor.Progression.GrowthState.skillGenerationRevision++;
            else
                actor.Lifecycle.SetLifecycleState(actor.CurrentLifecycleState);

            Require(!GameplayModuleDispatchEligibilityCapture.TryCaptureSkill(service, correlationId,
                    out _, out _, out _, out _, out var staleEligibility) && staleEligibility == null,
                "Changed owner retained an allowed dispatch eligibility witness: " + mutation);

            JObject beforeCallback = owner.CaptureOwnerCheckpoint();
            int skillsBefore = actor.Progression.ActiveSkills.Count + actor.Progression.PassiveSkills.Count;
            // Intentionally invalid JSON proves eligibility is checked BEFORE parsing the response.
            runtime.Callback(new LocalLlmResult(LocalLlmRequestStatus.Succeeded, "not-json", "", ""));
            Require(service.LastDiagnostic.StartsWith("STALE_OWNER", StringComparison.Ordinal)
                && service.PendingRequestCount == 0 && draft.candidates.Count == 0 && !draft.requestSubmitted,
                "Stale callback was parsed/applied or kept pending: " + service.LastDiagnostic);
            Require(skillsBefore == actor.Progression.ActiveSkills.Count + actor.Progression.PassiveSkills.Count
                && JToken.DeepEquals(beforeCallback, owner.CaptureOwnerCheckpoint()),
                "Rejected callback changed mechanics or the authoritative ledger.");
            runtime.Callback(new LocalLlmResult(LocalLlmRequestStatus.Succeeded, "not-json", "", ""));
            Require(JToken.DeepEquals(beforeCallback, owner.CaptureOwnerCheckpoint()), "Repeated stale callback mutated owner.");
            if (mutation == "death") VerifyDeadCommands(owner, settings, runtime);
            File.WriteAllText(Path.Combine(output, mutation + "-checkpoint.json"),
                owner.CaptureOwnerCheckpoint().ToString(Formatting.None));
        }
    }

    private static void VerifyDeadCommands(CharacterCombatModuleSelectionSourceCapture.SelectionActor owner,
        ICharacterSkillSystemSettingsProvider settings, DeferredRuntime runtime)
    {
        CharacterActor actor = owner.Actor;
        var service = new CharacterSkillGenerationService(settings, runtime, new FixedUiClock());
        foreach (CharacterSkillKind kind in new[] { CharacterSkillKind.Active, CharacterSkillKind.Passive, CharacterSkillKind.Ultimate })
        {
            bool rejected = false;
            try { service.CreateDraft(actor.Progression, kind, 50); }
            catch (InvalidOperationException error) when (error.Message.Contains("INELIGIBLE_DEAD_OWNER")) { rejected = true; }
            Require(rejected, "Dead owner created " + kind + " draft.");
        }
        string before = JsonUtility.ToJson(actor.Progression.CaptureAcquiredTraitState());
        var traits = new CharacterAcquiredTraitInferenceService(null, Array.Empty<CharacterAcquiredTraitModuleSO>());
        var timestamp = NarrativeInferenceTimestamp.FromGameTick(4000);
        var submission = traits.SubmitMilestone(actor.Progression,
            new CharacterAcquiredTraitSubmissionCommand(actor.Identity.PersistentId, "request:audit", "key:audit",
                3, Array.Empty<string>(), actor.Progression.AcquiredTraitRevision, timestamp));
        var completion = traits.CompleteMilestone(actor.Progression,
            new CharacterAcquiredTraitCompletionCommand(actor.Identity.PersistentId, "request:audit", "key:audit", "",
                0, actor.Progression.AcquiredTraitRevision, "not-json", timestamp),
            new CharacterAcquiredTraitRequestPacketDto(), owner.TraitInferenceFacts, owner.TraitInferences);
        Require(!submission.Succeeded && submission.Audit.ValidationError.Contains("INELIGIBLE_DEAD_OWNER")
            && !completion.Succeeded && completion.Audit.ValidationError.Contains("INELIGIBLE_DEAD_OWNER")
            && before == JsonUtility.ToJson(actor.Progression.CaptureAcquiredTraitState()),
            "Dead trait submission/completion changed state or missed the eligibility guard.");
        Require(!actor.Progression.TryChooseActiveSkill(10, 0, true, out _), "Dead owner accepted active skill choice.");
    }

    private static void ConfigureProgression(
        CharacterProgression progression,
        ICharacterSkillSystemSettingsProvider settings,
        CharacterSkillGenerationService service,
        GameplayOutcomeLedger ledger,
        IGameplayOutcomeNarrativeEvidenceQuery query)
    {
        SetProgressionDependency(progression, "generationService", service);
        SetProgressionDependency(progression, "settingsProvider", settings);
        SetProgressionDependency(progression, "evidenceUseTransaction",
            new GameplayOutcomeEvidenceUseTransaction(ledger, query));
    }

    private static void SetProgressionDependency(
        CharacterProgression progression,
        string name,
        object value) =>
        (typeof(CharacterProgression).GetField(
             name,
             BindingFlags.Instance | BindingFlags.NonPublic)
         ?? throw new MissingFieldException("CharacterProgression." + name))
        .SetValue(progression, value);

    private static void AgeGameplayEvidence(
        CharacterCombatModuleSelectionSourceCapture.SelectionActor owner)
    {
        GameplayOutcomeLedgerSaveData before = owner.Ledger.CaptureGameplayOutcomes();
        var registry = (IGameplayOutcomeRegistry)(typeof(GameplayOutcomeLedger)
            .GetField("registry", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException("GameplayOutcomeLedger.registry"))
            .GetValue(owner.Ledger);
        int day = Math.Max(
            before.lastScheduledEvaluationDay + 1,
            before.exactOutcomes.Max(value => value.absoluteDay) + 1);
        Require(owner.Ledger.TrySchedule(
                new GameplayOutcomeConsolidationRequest(
                    day,
                    before.nextSequence - 1,
                    registry.ConsolidationPolicyVersion,
                    owner.Ledger.CurrentWorldEpoch),
                out string scheduleFailure),
            "Actual active evidence aging could not be scheduled: " + scheduleFailure);
        int slices = 0;
        while (owner.Ledger.PendingJobCount > 0 && slices++ < 1000)
            owner.Ledger.ProcessSlice(64, 0L);
        Require(owner.Ledger.PendingJobCount == 0,
            "Actual active evidence aging did not finish.");
    }

    private static string BuildValidModuleSelectionBatchJson(
        CharacterSkillGenerationService service,
        CharacterSkillDraft draft,
        string characterName)
    {
        CharacterSkillDraft scratch = draft.Clone();
        var batch = new CharacterSkillModuleSelectionBatchResponseDto();
        string[] labels = { "선봉", "응수", "결의" };
        while (scratch.nextPresentationIndex < scratch.moduleSelectionOffers.Count)
        {
            CharacterSkillModuleOfferState offer =
                scratch.moduleSelectionOffers[scratch.nextPresentationIndex];
            HashSet<string> usedModules = scratch.candidates
                .Where(value => value != null)
                .Select(value => value.combinationId)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToHashSet(StringComparer.Ordinal);
            string moduleId = offer.positiveModuleIds.FirstOrDefault(value =>
                !usedModules.Contains(value));
            GameplayOutcomeEvidenceBindingSnapshot evidence =
                scratch.outcomeEvidenceBindings.FirstOrDefault(value =>
                    value != null
                    && string.Equals(
                        GameplayOutcomeEvidenceBindingAuthority.EffectiveSourceKind(value),
                        "exact",
                        StringComparison.Ordinal)
                    && offer.evidenceFactIds.Contains(value.publicFactId));
            Require(!string.IsNullOrWhiteSpace(moduleId) && evidence != null,
                "The active response fixture has no unused module or exact evidence.");
            string label = labels[Math.Min(
                scratch.nextPresentationIndex,
                labels.Length - 1)];
            var response = new CharacterSkillModuleSelectionResponseDto
            {
                selectionId = offer.selectionId,
                positiveModuleIds = new List<string> { moduleId },
                drawbackModuleIds = new List<string>(),
                evidenceFactIds = new List<string> { evidence.publicFactId },
                displayName = characterName + " " + label,
                narrativeFlavor = characterName + "의 실전 경험에서 다듬은 " + label + " 전술이다."
            };
            Require(service.TryValidateResponse(
                    scratch,
                    JsonUtility.ToJson(response),
                    out List<CharacterSkillInstance> resolved,
                    out string validation)
                && resolved.Count == 1,
                "The active response fixture is not legal: " + validation);
            batch.candidates.Add(response);
            scratch.candidates.Add(resolved[0].Clone());
            scratch.frozenMechanics.Add(resolved[0].Clone());
            scratch.nextPresentationIndex++;
        }
        return JsonUtility.ToJson(batch);
    }

    private static Action<LocalLlmResult> RequireDispatchCallback(
        DeferredRuntime runtime,
        CharacterSkillGenerationService service,
        string requestKey) => runtime.Callbacks.Single(value =>
        service.TryCapturePendingModuleDispatch(
            value.Key,
            out _,
            out CharacterSkillDraft dispatched,
            out _,
            out _)
        && string.Equals(
            dispatched.requestKey,
            requestKey,
            StringComparison.Ordinal)).Value;

    private static void Require(bool condition, string error) { if (!condition) throw new InvalidOperationException(error); }
    private static void Write(string output, JObject report)
    {
        using var file = new FileStream(Path.Combine(output, "report.json"), FileMode.CreateNew);
        using var writer = new StreamWriter(file);
        writer.Write(report.ToString(Formatting.Indented));
    }
    private sealed class FixedUiClock : IUiClock { public float Time => 0; public float DeltaTime => 0; }
    private sealed class DeferredRuntime : ILocalLlmRuntimeProvider, ILocalLlmRuntime, ICharacterSkillModuleSelectionBatchLlmRuntime
    {
        internal Action<LocalLlmResult> Callback;
        internal readonly Dictionary<string, Action<LocalLlmResult>> Callbacks = new(StringComparer.Ordinal);
        public bool TryGetRuntime(out ILocalLlmRuntime runtime) { runtime = this; return true; }
        public ILocalLlmRuntime GetRequiredRuntime() => this;
        public bool GenerateCharacterSkillModuleSelectionBatchAsync(string key, string prompt, Action<LocalLlmResult> callback)
        { Callback = callback; Callbacks[key] = callback; return true; }
        public bool GenerateCharacterSkillAsync(string p, Action<LocalLlmResult> c) => false;
        public bool GeneratePersonaAsync(string p, Action<LocalLlmResult> c) => false;
        public bool GenerateMacroGoalAsync(string p, Action<LocalLlmResult> c) => false;
        public bool GenerateMoodImpulseAsync(string p, Action<LocalLlmResult> c) => false;
        public bool GenerateSocialRumorAsync(string p, Action<LocalLlmResult> c) => false;
        public bool GenerateFacilityEvolutionAsync(string p, Action<LocalLlmResult> c) => false;
        public bool GenerateCharacterRecordAsync(string p, string o, Action<LocalLlmResult> c) => false;
        public bool GenerateBubbleLineAsync(string p, string o, Action<LocalLlmResult> c) => false;
    }
}
#endif
