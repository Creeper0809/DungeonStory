#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// Builds module-selection requests from the real combat life-cycle producer.
/// The selection state and the authoritative combat owner checkpoint travel
/// together so replay can resolve and consume the exact evidence again.
/// </summary>
internal static class CharacterCombatModuleSelectionSourceCapture
{
    private const string CheckpointSchema =
        "dungeonstory-module-source-character-combat-owner@2";
    private const string OwnerKind =
        "owner-kind:character-combat-module-selection";
    private const string DisplayName = "연화";

    internal static PrimitiveSkillModuleSelectionCapture CaptureSkill(
        string ownerId,
        CharacterSkillSystemSettingsSO settings,
        CharacterSkillKind kind,
        int revision,
        int scenarioIndex)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));
        string canonicalOwner = CausalGameplaySyntax.RequireId(
            ownerId,
            nameof(ownerId));
        string characterId = CharacterId(canonicalOwner);
        CombatEvidence evidence = CaptureCombatEvidence(
            canonicalOwner,
            characterId,
            scenarioIndex).SelectWindow(2, scenarioIndex);
        GameplayOutcomeEvidenceBindingSnapshot[] exactEvidence = evidence.Bindings
            .Where(IsExactEvidence)
            .ToArray();
        using var actor = new SelectionActor(characterId, DisplayName, null, evidence.OwnerCheckpoint);
        RecordProjectedFacts(actor.Progression, evidence.Bindings);
        CharacterSkillGenerationService service = new(
            NarrativeRequestContextTestFixture.CreateEmpty(), new SkillSettingsProvider(settings),
            MissingLlmRuntimeProvider.Instance,
            ZeroUiClock.Instance);
        CharacterSkillDraft draft = service.CreateDraft(
            actor.Progression,
            kind,
            unlockLevel: kind == CharacterSkillKind.Ultimate ? 50 : 10,
            revision: Math.Max(0, revision));
        CharacterSkillFormulaGeneration.InitializeModuleSelection(
            actor.Progression,
            draft,
            settings,
            exactEvidence);
        CharacterAiEditorTestDependencies.GetOwnedGrowthForFixture(actor.Progression).drafts.Add(draft);
        actor.Progression.MarkGenerationRequestPending(draft.requestKey);
        draft.requestSubmitted = true;
        JObject checkpoint = CaptureCheckpoint(
            canonicalOwner,
            characterId,
            actor,
            evidence,
            scenarioIndex);
        actor.Dispose();
        using var restored = new SelectionActor(
            characterId,
            DisplayName,
            checkpoint);
        CharacterSkillDraft restoredDraft = restored.Progression.GrowthState.drafts
            .Single(value => value != null
                && string.Equals(value.requestKey, draft.requestKey,
                    StringComparison.Ordinal));
        RequireEvidencePreserved(
            restoredDraft.outcomeEvidenceBindings,
            exactEvidence,
            "skill");
        NarrativePublicContextMaterial publicMaterial =
            CharacterSkillPromptBuilder.BuildModuleSelectionPublicMaterial(
                NarrativeRequestContextTestFixture.CreateEmpty(), restored.Progression, restoredDraft);
        string prompt = CharacterSkillPromptBuilder.BuildEnvelope(
            restored.Progression,
            restoredDraft,
            settings,
            publicMaterial,
            batchModuleSelection: true).Prompt;
        NarrativeFormulaModuleSelectionRequest[] requests = Enumerable.Range(
                restoredDraft.nextPresentationIndex,
                restoredDraft.moduleSelectionOffers.Count
                    - restoredDraft.nextPresentationIndex)
            .Select(index =>
            {
                CharacterSkillDraft indexed = restoredDraft.Clone();
                indexed.nextPresentationIndex = index;
                return CharacterSkillFormulaGeneration
                    .BuildModuleSelectionRequest(indexed, settings);
            })
            .ToArray();
        return new PrimitiveSkillModuleSelectionCapture(
            canonicalOwner,
            characterId,
            DisplayName,
            checkpoint,
            restoredDraft,
            requests,
            evidence.Bindings,
            prompt);
    }

    internal static PrimitiveTraitModuleSelectionCapture CaptureTrait(
        string ownerId,
        CharacterAcquiredTraitSettingsSO settings,
        IReadOnlyList<CharacterAcquiredTraitModuleSO> modules,
        int scenarioIndex)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));
        CharacterAcquiredTraitModuleSO[] definitions = (modules
                ?? throw new ArgumentNullException(nameof(modules)))
            .Where(value => value != null)
            .ToArray();
        string canonicalOwner = CausalGameplaySyntax.RequireId(
            ownerId,
            nameof(ownerId));
        string characterId = CharacterId(canonicalOwner);
        CombatEvidence evidence = CaptureCombatEvidence(
            canonicalOwner,
            characterId,
            scenarioIndex).SelectWindow(scenarioIndex < 2 ? 5 : 3, scenarioIndex);
        GameplayOutcomeEvidenceBindingSnapshot[] exactEvidence = evidence.Bindings
            .Where(IsExactEvidence)
            .ToArray();
        using var actor = new SelectionActor(characterId, DisplayName, null, evidence.OwnerCheckpoint);
        RecordProjectedFacts(actor.Progression, evidence.Bindings);
        CharacterProgression progression = actor.Progression;
        string[] meaningful = CharacterAcquiredTraitMeaningfulLedgerValidator
            .RequireMeaningfulFactIds(progression.NarrativeLedger)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (meaningful.Length < 3)
        {
            throw new InvalidOperationException(
                "Combat trait source requires at least three meaningful exact-origin facts.");
        }
        HashSet<string> meaningfulSet = meaningful.ToHashSet(
            StringComparer.Ordinal);
        string[] local = GameplayOutcomeEvidenceFormulaProjection
            .GetRemainingLegacyMilestoneCounts(progression.NarrativeLedger.Facts, evidence.Bindings).Keys
            .Where(value => value != null
                && meaningfulSet.Contains(value.factId))
            .Select(value => value.factId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        NarrativePublicContextMaterial publicMaterial =
            CharacterAcquiredTraitPromptBuilder
                .BuildPublicMaterialForLedgerEvidence(NarrativeRequestContextTestFixture.CreateEmpty(), progression, local);
        publicMaterial = GameplayOutcomeEvidencePublicMaterialComposer
            .AddExactEvidence(publicMaterial, evidence.Bindings);
        IReadOnlyList<string> projectedLocal = local.Length == 0
            ? Array.Empty<string>()
            : CharacterAcquiredTraitPromptBuilder
                .BuildProjectedEvidenceFactIds(publicMaterial, local);
        string[] evidenceIds = projectedLocal
            .Concat(exactEvidence.Select(value => value.publicFactId))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        CharacterAcquiredTraitManifestationRuntime.BuildProductionRequestIdentity(
            settings,
            characterId,
            milestone: 3,
            evidenceIds,
            publicMaterial.SemanticHash,
            CharacterNarrativeRequestOwnerBinding.Capture(progression),
            progression.AcquiredTraitRevision,
            out string requestId,
            out string requestKey);
        CharacterAcquiredTraitAggregateState current = progression
            .CaptureAcquiredTraitState();
        CharacterAcquiredTraitInferenceCommandResult submitted =
            new CharacterAcquiredTraitInferenceService(settings, definitions)
                .SubmitMilestone(
                    progression,
                    new CharacterAcquiredTraitSubmissionCommand(
                        characterId,
                        requestId,
                        requestKey,
                        3,
                        evidenceIds,
                        exactEvidence,
                        current.revision,
                        NarrativeInferenceTimestamp.FromGameTick(
                            actor.Calendar.AbsoluteHour)));
        if (!submitted.Succeeded || submitted.Packet == null)
        {
            throw new InvalidOperationException(
                "Combat acquired-trait pending registration failed: "
                + submitted.Audit.ValidationError);
        }
        JObject checkpoint = CaptureCheckpoint(
            canonicalOwner,
            characterId,
            actor,
            evidence,
            scenarioIndex);
        actor.Dispose();
        using var restored = new SelectionActor(
            characterId,
            DisplayName,
            checkpoint);
        CharacterAcquiredTraitAggregateState restoredState = restored.Progression
            .CaptureAcquiredTraitState();
        if (!restoredState.TryGetPendingRequest(
                requestId,
                out CharacterAcquiredTraitPendingRequestState pending)
            || pending.presentationState
                != CharacterAcquiredTraitPresentationState.ModuleSelectionPending)
        {
            throw new InvalidOperationException(
                "Combat acquired-trait pending request did not survive restore.");
        }
        RequireEvidencePreserved(
            pending.evidenceBindings,
            exactEvidence,
            "acquired trait");
        HashSet<string> exactIds = pending.evidenceBindings
            .Select(value => value.publicFactId)
            .ToHashSet(StringComparer.Ordinal);
        string[] localEvidence = pending.evidenceFactIds
            .Where(value => !exactIds.Contains(value))
            .ToArray();
        NarrativePublicContextMaterial restoredMaterial =
            CharacterAcquiredTraitPromptBuilder.BuildPublicMaterial(
                NarrativeRequestContextTestFixture.CreateEmpty(), restored.Progression,
                localEvidence);
        restoredMaterial = GameplayOutcomeEvidencePublicMaterialComposer
            .AddExactEvidence(restoredMaterial, pending.evidenceBindings);
        CharacterAcquiredTraitRequestPacketDto packet = new()
        {
            settingsId = settings.SettingsId,
            targetPersistentId = pending.targetPersistentId,
            requestId = pending.requestId,
            requestKey = pending.requestKey,
            manifestationMilestone = pending.manifestationMilestone,
            evidenceFactIds = pending.evidenceFactIds.ToList(),
            candidatePacketHash = pending.candidatePacketHash
        };
        NarrativeFormulaModuleSelectionRequest request =
            CharacterAcquiredTraitFormulaGeneration.PrepareModuleSelection(
                restored.Progression,
                pending.requestId,
                pending.requestKey,
                pending.manifestationMilestone,
                settings,
                definitions,
                pending.evidenceFactIds,
                pending.evidenceBindings);
        string prompt = CharacterAcquiredTraitPromptBuilder.BuildEnvelope(
            restored.Progression,
            packet,
            restoredMaterial,
            settings,
            definitions).Prompt;
        return new PrimitiveTraitModuleSelectionCapture(
            canonicalOwner,
            characterId,
            DisplayName,
            checkpoint,
            pending,
            packet,
            request,
            evidence.Bindings,
            prompt);
    }

    internal static bool IsCombatCheckpoint(JObject checkpoint) =>
        string.Equals(
            (string)checkpoint?["schema"],
            CheckpointSchema,
            StringComparison.Ordinal);

    internal static int RequireRevision(JObject checkpoint)
    {
        RequireCheckpointHeader(checkpoint);
        int revision = (int?)checkpoint?["revision"] ?? -1;
        if (revision < 1)
            throw new InvalidOperationException(
                "Character combat module-selection revision is invalid.");
        return revision;
    }

    internal static GameplayModuleSelectionRuntimeReplayResult ReplaySkill(
        string ownerId,
        JObject checkpoint,
        CharacterSkillSystemSettingsSO settings,
        string responseJson)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));
        string canonicalOwner = RequireOwner(ownerId, checkpoint);
        string characterId = RequireCharacterId(checkpoint);
        string canonicalResponse = responseJson?.Trim() ?? string.Empty;
        if (!NarrativeExactKeyContract.TryValidateProfileResponse(
                LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id,
                canonicalResponse,
                out _,
                out string responseError)
            || !LlmJsonResponseParser.TryParse(
                canonicalResponse,
                out CharacterSkillModuleSelectionBatchResponseDto response,
                out responseError))
        {
            throw new InvalidOperationException(
                "Combat skill replay response is invalid: " + responseError);
        }

        JObject before;
        JObject applied;
        string requestKey;
        string[] resultIds;
        using (var actor = new SelectionActor(
                   characterId,
                   DisplayName,
                   checkpoint))
        {
            BindSkillDependencies(actor.Progression, settings, actor.Ledger);
            CharacterSkillDraft draft = CharacterAiEditorTestDependencies.GetOwnedGrowthForFixture(actor.Progression).drafts
                .Single(value => value != null
                    && value.formulaVersion
                        >= CharacterSkillFormulaGeneration
                            .ModuleSelectionFormulaVersion
                    && value.requestSubmitted
                    && value.presentationState
                        == CharacterSkillPresentationState.PresentationPending
                    && (value.candidates == null || value.candidates.Count == 0));
            draft.isReady = false;
            before = CaptureCheckpointFromExisting(
                checkpoint,
                actor);
            requestKey = draft.requestKey;
            actor.Progression.SetPublicSkillNotificationsSuppressed(true);
            var runtime = new ReplaySkillRuntime(canonicalResponse);
            CharacterSkillGenerationService service = new(
                NarrativeRequestContextTestFixture.CreateEmpty(), new SkillSettingsProvider(settings),
                new ReplaySkillRuntimeProvider(runtime),
                ZeroUiClock.Instance);
            service.RequestDraft(actor.Progression, draft);
            service.Tick();
            if (runtime.BatchRequestCount != 1
                || !draft.isReady
                || draft.presentationState
                    != CharacterSkillPresentationState.Ready
                || draft.requestSubmitted
                || service.PendingRequestCount != 0
                || draft.candidates.Count != response.candidates.Count)
            {
                throw new InvalidOperationException(
                    "The combat-backed CharacterSkill service did not finish: "
                    + service.LastDiagnostic);
            }
            resultIds = draft.candidates
                .Select(value => value?.presentationId)
                .ToArray();
            applied = CaptureCheckpointFromExisting(
                checkpoint,
                actor);
        }

        JObject restoredCheckpoint;
        using (var restored = new SelectionActor(
                   characterId,
                   DisplayName,
                   applied))
        {
            CharacterSkillDraft restoredDraft = restored.Progression
                .GrowthState.drafts.Single(value => value != null
                    && string.Equals(value.requestKey, requestKey,
                        StringComparison.Ordinal));
            if (!restoredDraft.isReady
                || restoredDraft.candidates.Count != resultIds.Length
                || !restoredDraft.candidates
                    .Select(value => value.presentationId)
                    .SequenceEqual(resultIds, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    "Combat-backed skill result did not survive restore.");
            }
            restoredCheckpoint = CaptureCheckpointFromExisting(
                applied,
                restored);
        }
        return new GameplayModuleSelectionRuntimeReplayResult(
            before,
            applied,
            restoredCheckpoint,
            resultIds,
            response.candidates.SelectMany(value => value.evidenceFactIds)
                .Distinct(StringComparer.Ordinal));
    }

    internal static GameplayModuleSelectionRuntimeReplayResult ReplayTrait(
        string ownerId,
        JObject checkpoint,
        CharacterAcquiredTraitSettingsSO settings,
        IReadOnlyList<CharacterAcquiredTraitModuleSO> modules,
        string responseJson)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));
        CharacterAcquiredTraitModuleSO[] definitions = (modules
                ?? throw new ArgumentNullException(nameof(modules)))
            .Where(value => value != null)
            .ToArray();
        string canonicalOwner = RequireOwner(ownerId, checkpoint);
        string characterId = RequireCharacterId(checkpoint);
        string canonicalResponse = responseJson?.Trim() ?? string.Empty;
        JObject before;
        JObject applied;
        string requestId;
        string resultId;
        string[] evidenceIds;
        using (var actor = new SelectionActor(
                   characterId,
                   DisplayName,
                   checkpoint))
        {
            BindEvidenceUse(actor.Progression, actor.Ledger);
            before = CaptureCheckpointFromExisting(
                checkpoint,
                actor);
            CharacterAcquiredTraitAggregateState current = actor.Progression
                .CaptureAcquiredTraitState();
            CharacterAcquiredTraitPendingRequestState pending = current
                .pendingRequests.Single(value => value != null
                    && value.presentationState
                        == CharacterAcquiredTraitPresentationState
                            .ModuleSelectionPending);
            requestId = pending.requestId;
            CharacterAcquiredTraitRequestPacketDto packet = new()
            {
                settingsId = settings.SettingsId,
                targetPersistentId = pending.targetPersistentId,
                requestId = pending.requestId,
                requestKey = pending.requestKey,
                manifestationMilestone = pending.manifestationMilestone,
                evidenceFactIds = pending.evidenceFactIds.ToList(),
                candidatePacketHash = pending.candidatePacketHash
            };
            CharacterAcquiredTraitInferenceCommandResult completion =
                new CharacterAcquiredTraitInferenceService(settings, definitions)
                    .CompleteMilestone(
                        actor.Progression,
                        new CharacterAcquiredTraitCompletionCommand(
                            pending.targetPersistentId,
                            pending.requestId,
                            pending.requestKey,
                            pending.candidatePacketHash,
                            pending.registeredRevision,
                            current.revision,
                            canonicalResponse,
                            NarrativeInferenceTimestamp.FromGameTick(
                                actor.Calendar.AbsoluteHour)),
                        packet,
                        actor.TraitInferenceFacts,
                        actor.TraitInferences);
            if (!completion.Succeeded)
            {
                throw new InvalidOperationException(
                    "Combat-backed acquired trait was rejected: "
                    + completion.Audit.ValidationError);
            }
            CharacterAcquiredTraitInstanceState instance = actor.Progression
                .CaptureAcquiredTraitState().instances.Single(value =>
                    value != null
                    && string.Equals(value.originatingRequestId, requestId,
                        StringComparison.Ordinal));
            resultId = instance.instanceId;
            evidenceIds = instance.evidenceFactIds
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            applied = CaptureCheckpointFromExisting(
                checkpoint,
                actor);
        }
        JObject restoredCheckpoint;
        using (var restored = new SelectionActor(
                   characterId,
                   DisplayName,
                   applied))
        {
            CharacterAcquiredTraitAggregateState state = restored.Progression
                .CaptureAcquiredTraitState();
            if (state.pendingRequests.Any(value => value != null
                    && string.Equals(value.requestId, requestId,
                        StringComparison.Ordinal))
                || state.instances.Count(value => value != null
                    && string.Equals(value.instanceId, resultId,
                        StringComparison.Ordinal)) != 1)
            {
                throw new InvalidOperationException(
                    "Combat-backed acquired trait did not survive restore.");
            }
            restoredCheckpoint = CaptureCheckpointFromExisting(
                applied,
                restored);
        }
        return new GameplayModuleSelectionRuntimeReplayResult(
            before,
            applied,
            restoredCheckpoint,
            new[] { resultId },
            evidenceIds);
    }

    private static CombatEvidence CaptureCombatEvidence(
        string selectionOwnerId,
        string characterId,
        int scenarioIndex)
    {
        string evidenceOwnerId = selectionOwnerId + ":combat";
        IReadOnlyList<IProductionCausalActionFixture> fixtures =
            MigratedProducerCombatDefenseEntryE2EDebugScenarios
                .CreateCausalCombatLifeCycleFixtures(
                    "run:module-source:character-combat:"
                    + scenarioIndex.ToString("D5"),
                    characterId,
                    evidenceOwnerId);
        try
        {
            IProductionCausalActionFixture injury = RequireFixture(
                fixtures,
                MigratedProducerCombatDefenseEntryE2EDebugScenarios
                    .NonlethalCombatInjuryActionId);
            IProductionCausalActionFixture recovery = RequireFixture(
                fixtures,
                MigratedProducerCombatDefenseEntryE2EDebugScenarios
                    .RecoverBodyHealthActionId);
            IProductionCausalActionFixture fatal = RequireFixture(
                fixtures,
                MigratedProducerCombatDefenseEntryE2EDebugScenarios
                    .FatalCombatInjuryActionId);
            int stage = scenarioIndex % 3 + 1;
            if (!injury.Execute()
                || stage >= 2 && !recovery.Execute()
                || stage >= 3 && !fatal.Execute())
            {
                throw new InvalidOperationException(
                    "Actual combat life-cycle source did not reach stage "
                    + stage + ".");
            }
            IProductionCausalOwnerCheckpointFixture owner =
                injury as IProductionCausalOwnerCheckpointFixture
                ?? throw new InvalidOperationException(
                    "Combat source has no owner checkpoint.");
            if (scenarioIndex < 2)
            {
                IProductionCausalMemoryHistoryFixture memory =
                    owner.OwnerRuntimeIdentity as IProductionCausalMemoryHistoryFixture
                    ?? throw new InvalidOperationException(
                        "Combat source has no compactable memory-history runtime.");
                if (!memory.EnsureCompactedMemoryHistory())
                    throw new InvalidOperationException(
                        "Combat source did not produce compacted memory context.");
            }
            IProductionCausalNarrativeEvidenceFixture narrative =
                injury as IProductionCausalNarrativeEvidenceFixture
                ?? throw new InvalidOperationException(
                    "Combat source does not expose narrative evidence.");
            IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot> bindings =
                narrative.CaptureNarrativeEvidence(
                    new GameplayEntityId(
                        MigratedProducerOutcomeIds.CharacterKind,
                        characterId),
                    16);
            if (bindings.Count < 3
                || bindings.Any(value => !string.Equals(
                    value.subjectId,
                    characterId,
                    StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "Combat source evidence is not bound to the target character.");
            }
            return new CombatEvidence(
                owner.CaptureOwnerCheckpoint(),
                bindings,
                stage);
        }
        finally
        {
            foreach (IProductionCausalActionFixture fixture in fixtures)
                fixture.Dispose();
        }
    }

    private static IProductionCausalActionFixture RequireFixture(
        IEnumerable<IProductionCausalActionFixture> fixtures,
        string actionId) => fixtures.Single(value => string.Equals(
            value.ActionId,
            actionId,
            StringComparison.Ordinal));

    private static JObject CaptureCheckpoint(
        string ownerId,
        string characterId,
        SelectionActor actor,
        CombatEvidence evidence,
        int scenarioIndex)
    {
        CharacterProgressionSnapshot state = actor.Progression.CapturePersistentState();
        return new JObject
        {
            ["schemaVersion"] = 2,
            ["schema"] = CheckpointSchema,
            ["ownerId"] = ownerId,
            ["ownerKind"] = OwnerKind,
            ["revision"] = evidence.Stage,
            ["scenarioIndex"] = scenarioIndex,
            ["characterId"] = characterId,
            ["displayName"] = DisplayName,
            ["level"] = state.Level,
            ["currentExperience"] = state.CurrentExperience,
            ["growthState"] = JObject.Parse(JsonUtility.ToJson(
                state.GrowthState)),
            ["narrativeLedger"] = JObject.Parse(JsonUtility.ToJson(
                state.NarrativeLedger)),
            ["acquiredTraitState"] = JObject.Parse(JsonUtility.ToJson(
                state.AcquiredTraitState)),
            ["traitInferencePublications"] = JObject.Parse(JsonUtility.ToJson(
                actor.TraitInferences.Capture())),
            ["evidenceOwnerCheckpoint"] = actor.CaptureOwnerCheckpoint()
        };
    }

    private static JObject CaptureCheckpointFromExisting(
        JObject template,
        SelectionActor actor)
    {
        RequireCheckpointHeader(template);
        JObject copy = (JObject)template.DeepClone();
        CharacterProgressionSnapshot state = actor.Progression.CapturePersistentState();
        copy["level"] = state.Level;
        copy["currentExperience"] = state.CurrentExperience;
        copy["growthState"] = JObject.Parse(JsonUtility.ToJson(
            state.GrowthState));
        copy["narrativeLedger"] = JObject.Parse(JsonUtility.ToJson(
            state.NarrativeLedger));
        copy["acquiredTraitState"] = JObject.Parse(JsonUtility.ToJson(
            state.AcquiredTraitState));
        copy["traitInferencePublications"] = JObject.Parse(JsonUtility.ToJson(
            actor.TraitInferences.Capture()));
        copy["evidenceOwnerCheckpoint"] = actor.CaptureOwnerCheckpoint();
        return copy;
    }

    private static void RecordProjectedFacts(
        CharacterProgression progression,
        IEnumerable<GameplayOutcomeEvidenceBindingSnapshot> bindings)
    {
        foreach (GameplayOutcomeEvidenceBindingSnapshot binding in bindings
                     .Where(IsExactEvidence)
                     .OrderBy(value => value.outcomeSequence))
        {
            CharacterNarrativeDomain domain = ProjectDomain(binding);
            progression.RecordNarrative(
                domain,
                binding.publicFactId,
                binding.subjectId,
                GameplayOutcomeEvidenceBindingAuthority.RequireStatus(binding)
                    .ToString(),
                Math.Max(1, binding.occurrenceCount),
                Math.Max(0, binding.lastDay),
                new CharacterNarrativeEvidenceMetadata(
                    "gameplay-outcome:" + domain,
                    binding.operationId,
                    string.Empty,
                    Math.Max(1f, binding.subjectSalience)),
                triggerPassives: false,
                new GameplayNarrativeEventContext
                {
                    eventInstanceId = binding.publicFactId,
                    actorId = binding.subjectId,
                    actorDisplayName = DisplayName,
                    counterpartyId = binding.subjectId,
                    counterpartyDisplayName = DisplayName,
                    occurredDay = Math.Max(0, binding.lastDay),
                    sequence = Math.Max(1L, binding.outcomeSequence),
                    chainId = string.IsNullOrWhiteSpace(binding.rootOperationId)
                        ? binding.operationId
                        : binding.rootOperationId
                });
        }
    }

    private static bool IsExactEvidence(
        GameplayOutcomeEvidenceBindingSnapshot binding) =>
        binding != null
        && string.Equals(
            GameplayOutcomeEvidenceBindingAuthority.EffectiveSourceKind(binding),
            "exact",
            StringComparison.Ordinal);

    private static CharacterNarrativeDomain ProjectDomain(
        GameplayOutcomeEvidenceBindingSnapshot binding)
    {
        string type = binding.outcomeTypeId ?? string.Empty;
        if (type.Contains("recover", StringComparison.OrdinalIgnoreCase)
            || type.Contains("health", StringComparison.OrdinalIgnoreCase)
            || type.Contains("downed", StringComparison.OrdinalIgnoreCase)
            || type.Contains("death", StringComparison.OrdinalIgnoreCase)
            || type.Contains("killed", StringComparison.OrdinalIgnoreCase))
            return CharacterNarrativeDomain.Injury;
        return CharacterNarrativeDomain.Combat;
    }

    private static void BindSkillDependencies(
        CharacterProgression progression,
        CharacterSkillSystemSettingsSO settings,
        GameplayOutcomeLedger ledger)
    {
        SetProgressionField(
            progression,
            "settingsProvider",
            new SkillSettingsProvider(settings));
        BindEvidenceUse(progression, ledger);
    }

    private static void BindEvidenceUse(
        CharacterProgression progression,
        GameplayOutcomeLedger ledger) => SetProgressionField(
        progression,
        "evidenceUseTransaction",
        new GameplayOutcomeEvidenceUseTransaction(
            ledger,
            new GameplayOutcomeNarrativeEvidenceQuery(ledger)));

    private static void SetProgressionField(
        CharacterProgression progression,
        string name,
        object value)
    {
        FieldInfo field = typeof(CharacterProgression).GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(
                typeof(CharacterProgression).FullName,
                name);
        field.SetValue(progression, value);
    }

    private static void RequireEvidencePreserved(
        IEnumerable<GameplayOutcomeEvidenceBindingSnapshot> restored,
        IEnumerable<GameplayOutcomeEvidenceBindingSnapshot> expected,
        string label)
    {
        string[] restoredIds = restored.Where(value => value != null)
            .Select(value => value.publicFactId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] expectedIds = expected.Where(value => value != null)
            .Select(value => value.publicFactId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (!restoredIds.SequenceEqual(expectedIds, StringComparer.Ordinal))
            throw new InvalidOperationException(
                "Combat " + label + " evidence changed across restore.");
    }

    private static string RequireOwner(string ownerId, JObject checkpoint)
    {
        RequireCheckpointHeader(checkpoint);
        string canonical = CausalGameplaySyntax.RequireId(
            ownerId,
            nameof(ownerId));
        if (!string.Equals(
                (string)checkpoint["ownerId"],
                canonical,
                StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Combat module-selection owner ID changed.");
        return canonical;
    }

    private static string RequireCharacterId(JObject checkpoint)
    {
        RequireCheckpointHeader(checkpoint);
        return CausalGameplaySyntax.RequireId(
            (string)checkpoint["characterId"],
            "combat module-selection character ID");
    }

    private static void RequireCheckpointHeader(JObject checkpoint)
    {
        if (checkpoint == null
            || (int?)checkpoint["schemaVersion"] != 2
            || !string.Equals(
                (string)checkpoint["schema"],
                CheckpointSchema,
                StringComparison.Ordinal)
            || !string.Equals(
                (string)checkpoint["ownerKind"],
                OwnerKind,
                StringComparison.Ordinal)
            || checkpoint["growthState"] is not JObject
            || checkpoint["narrativeLedger"] is not JObject
            || checkpoint["acquiredTraitState"] is not JObject
            || checkpoint["evidenceOwnerCheckpoint"] is not JObject)
        {
            throw new InvalidOperationException(
                "Character combat module-selection checkpoint is invalid.");
        }
    }

    private static string CharacterId(string ownerId) =>
        "character:module-source:combat:"
        + ProductionCausalOutcomeEvidence.Sha256Text(ownerId)
            .Substring("sha256:".Length, 24);

    private sealed class CombatEvidence
    {
        internal CombatEvidence(
            JObject ownerCheckpoint,
            IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot> bindings,
            int stage)
        {
            OwnerCheckpoint = (JObject)ownerCheckpoint.DeepClone();
            Bindings = bindings.Select(value => value.Clone()).ToArray();
            Stage = stage;
        }

        internal JObject OwnerCheckpoint { get; }
        internal IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot> Bindings
            { get; }
        internal int Stage { get; }

        internal CombatEvidence SelectWindow(int count, int offset)
        {
            if (count <= 0 || Bindings.Count < count)
                throw new ArgumentOutOfRangeException(nameof(count));
            GameplayOutcomeEvidenceBindingSnapshot[] compacted = Bindings
                .Where(value => string.Equals(
                    value.sourceKind,
                    "compacted",
                    StringComparison.Ordinal))
                .ToArray();
            if (compacted.Length > 0)
            {
                GameplayOutcomeEvidenceBindingSnapshot[] exact = Bindings
                    .Where(value => !string.Equals(
                        value.sourceKind,
                        "compacted",
                        StringComparison.Ordinal))
                    .ToArray();
                if (exact.Length < count - 1)
                    throw new InvalidOperationException(
                        "Compacted combat context has too few exact selectable facts.");
                int exactStart = Math.Abs(offset) % exact.Length;
                GameplayOutcomeEvidenceBindingSnapshot[] compactedSelection = new[]
                    {
                        compacted[Math.Abs(offset) % compacted.Length]
                    }
                    .Concat(SelectCausallyClosedDistinctCircular(
                        exact,
                        count - 1,
                        exactStart))
                    .OrderBy(value => value.outcomeSequence)
                    .ToArray();
                return new CombatEvidence(
                    OwnerCheckpoint,
                    compactedSelection,
                    Stage);
            }
            int start = Math.Abs(offset) % Bindings.Count;
            GameplayOutcomeEvidenceBindingSnapshot[] selected =
                SelectCausallyClosedDistinctCircular(Bindings, count, start)
                .OrderBy(value => value.outcomeSequence)
                .ToArray();
            return new CombatEvidence(OwnerCheckpoint, selected, Stage);
        }

        private static IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot>
            SelectCausallyClosedDistinctCircular(
                IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot> source,
                int count,
                int start)
        {
            var selected = new List<GameplayOutcomeEvidenceBindingSnapshot>(count);
            var selectedIds = new HashSet<string>(StringComparer.Ordinal);
            var texts = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<string, GameplayOutcomeEvidenceBindingSnapshot> byId = source
                .Where(IsExactEvidence)
                .ToDictionary(ExactOutcomeId, StringComparer.Ordinal);
            for (int offset = 0;
                 offset < source.Count && selected.Count < count;
                 offset++)
            {
                GameplayOutcomeEvidenceBindingSnapshot candidate =
                    source[(start + offset) % source.Count];
                IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot> chain =
                    ResolveCausalChain(candidate, byId);
                if (chain == null)
                    continue;
                GameplayOutcomeEvidenceBindingSnapshot[] missing = chain
                    .Where(value => !selectedIds.Contains(ExactOutcomeId(value)))
                    .ToArray();
                if (missing.Length == 0
                    || selected.Count + missing.Length > count)
                    continue;
                string[] keys = missing.Select(value =>
                        GameplayOutcomeEvidenceBindingAuthority
                            .EffectiveSourceKind(value)
                        + "\u001f"
                        + value.canonicalFactText)
                    .ToArray();
                if (keys.Distinct(StringComparer.Ordinal).Count() != keys.Length
                    || keys.Any(texts.Contains))
                    continue;
                foreach (GameplayOutcomeEvidenceBindingSnapshot value in missing)
                {
                    selected.Add(value);
                    selectedIds.Add(ExactOutcomeId(value));
                }
                foreach (string key in keys)
                    texts.Add(key);
            }
            if (selected.Count != count)
            {
                throw new InvalidOperationException(
                    "Combat evidence window has too few distinct, causally closed "
                    + "public facts.");
            }
            return selected;
        }

        private static IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot>
            ResolveCausalChain(
                GameplayOutcomeEvidenceBindingSnapshot candidate,
                IReadOnlyDictionary<string,
                    GameplayOutcomeEvidenceBindingSnapshot> byId)
        {
            if (!IsExactEvidence(candidate))
                return null;
            var reverse = new List<GameplayOutcomeEvidenceBindingSnapshot>();
            var visited = new HashSet<string>(StringComparer.Ordinal);
            GameplayOutcomeEvidenceBindingSnapshot current = candidate;
            while (current != null)
            {
                string currentId = ExactOutcomeId(current);
                if (!visited.Add(currentId))
                    return null;
                reverse.Add(current);
                string parentId = current.parentOutcomeId ?? string.Empty;
                if (parentId.Length == 0)
                    break;
                if (!byId.TryGetValue(parentId, out current))
                    return null;
            }
            reverse.Reverse();
            return reverse;
        }

        private static string ExactOutcomeId(
            GameplayOutcomeEvidenceBindingSnapshot value) =>
            value.outcomeRunId + "/" + value.outcomeSequence
                .ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    internal sealed class SelectionActor : IDisposable
    {
        private readonly CharacterActor actor;
        private readonly IReadOnlyList<IProductionCausalActionFixture> fixtures;
        private readonly CharacterSkillGenerationService generationService;
        private readonly DomainFactDispatcher traitInferenceFacts;
        private readonly IDisposable traitInferenceBinding;
        private readonly CharacterTraitInferencePublicationRuntime traitInferences;
        private bool disposed;

        internal SelectionActor(
            string characterId,
            string displayName,
            JObject checkpoint,
            JObject initialOwnerCheckpoint = null)
        {
            if (checkpoint != null)
                RequireCheckpointHeader(checkpoint);
            JObject owner = checkpoint?["evidenceOwnerCheckpoint"] as JObject
                ?? initialOwnerCheckpoint
                ?? throw new InvalidOperationException(
                    "INCOMPLETE_OWNER_CHECKPOINT: selection requires the actual combat owner.");
            fixtures = MigratedProducerCombatDefenseEntryE2EDebugScenarios
                .RestoreCausalCombatLifeCycleFixtures(
                    "run:selection-restore:" + characterId,
                    (string)owner["ownerId"], owner);
            try
            {
                actor = MigratedProducerCombatDefenseEntryE2EDebugScenarios
                    .RequireLifeCycleActor(fixtures[0]);
                traitInferenceFacts = new DomainFactDispatcher();
                traitInferenceBinding = CharacterTraitInferredFactMapping
                    .CreateBinding(
                        MigratedProducerCombatDefenseEntryE2EDebugScenarios
                            .RequireLifeCycleRegistry(fixtures[0]),
                        Ledger,
                        Recorder,
                        new GameplayDomainFactCommitGroup(Recorder, Ledger))
                    .Connect(traitInferenceFacts);
                traitInferences = new CharacterTraitInferencePublicationRuntime(
                    traitInferenceFacts,
                    traitInferenceFacts);
                if (!string.Equals(actor.Identity.PersistentId, characterId, StringComparison.Ordinal)
                    || !string.Equals(actor.Identity.DisplayName, displayName, StringComparison.Ordinal))
                    throw new InvalidOperationException("Restored selection owner identity changed.");
                // The generic Editor composition marks every requested draft ready.
                // Replace it BEFORE restoring pending requests: only the real service
                // may update selection state, and capture never dispatches an LLM.
                generationService = new CharacterSkillGenerationService(
                    NarrativeRequestContextTestFixture.CreateEmpty(), new SkillSettingsProvider(actor.Progression.SkillSettings),
                    MissingLlmRuntimeProvider.Instance, ZeroUiClock.Instance);
                SetProgressionField(actor.Progression, "generationService", generationService);
                if (checkpoint != null)
                    Restore(checkpoint);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal CharacterProgression Progression => actor.Progression;
        internal void TickSkillRequests() => generationService.Tick();
        internal void QueueSkillRequest(CharacterSkillDraft draft) => generationService.RequestDraft(actor.Progression, draft);
        internal CharacterActor Actor => actor;
        internal bool Execute(string actionId) => fixtures.Single(value => value.ActionId == actionId).Execute();
        internal GameplayOutcomeLedger Ledger => MigratedProducerCombatDefenseEntryE2EDebugScenarios
            .RequireLifeCycleLedger(fixtures[0]);
        internal GameplayOutcomeRecorder Recorder => MigratedProducerCombatDefenseEntryE2EDebugScenarios
            .RequireLifeCycleRecorder(fixtures[0]);
        internal IDomainFactPublisher TraitInferenceFacts => traitInferenceFacts;
        internal CharacterTraitInferencePublicationRuntime TraitInferences => traitInferences;
        internal IGameCalendar Calendar => MigratedProducerCombatDefenseEntryE2EDebugScenarios
            .RequireLifeCycleCalendar(fixtures[0]);
        internal JObject CaptureOwnerCheckpoint() =>
            ((IProductionCausalOwnerCheckpointFixture)fixtures[0]).CaptureOwnerCheckpoint();

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            generationService?.CancelRequests(actor?.Progression);
            traitInferenceBinding?.Dispose();
            traitInferenceFacts?.Dispose();
            foreach (IProductionCausalActionFixture fixture in fixtures)
                fixture.Dispose();
        }

        private void Restore(JObject checkpoint)
        {
            RequireCheckpointHeader(checkpoint);
            string characterId = RequireCharacterId(checkpoint);
            if (!string.Equals(
                    actor.Identity.PersistentId,
                    characterId,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Combat selection actor identity changed before restore.");
            if (checkpoint["level"]?.Type != JTokenType.Integer
                || checkpoint["currentExperience"]?.Type != JTokenType.Integer)
                throw new InvalidOperationException("INCOMPLETE_OWNER_CHECKPOINT: progression values are missing.");
            actor.Progression.RestorePersistentState(
                new CharacterProgressionSnapshot(
                    (int)checkpoint["level"],
                    (int)checkpoint["currentExperience"],
                    JsonUtility.FromJson<CharacterGrowthState>(
                        checkpoint["growthState"].ToString(Formatting.None)),
                    JsonUtility.FromJson<CharacterNarrativeLedger>(
                        checkpoint["narrativeLedger"].ToString(
                            Formatting.None)),
                    JsonUtility.FromJson<CharacterAcquiredTraitAggregateState>(
                        checkpoint["acquiredTraitState"].ToString(
                            Formatting.None))));
            if (checkpoint["traitInferencePublications"] is not JObject publications)
            {
                throw new InvalidOperationException(
                    "INCOMPLETE_OWNER_CHECKPOINT: trait-inference publications are missing.");
            }
            CharacterTraitInferencePublicationSaveData savedPublications =
                CharacterTraitInferencePublicationRuntime.ValidateAndClone(
                    JsonUtility.FromJson<CharacterTraitInferencePublicationSaveData>(
                        publications.ToString(Formatting.None)));
            foreach (CharacterTraitInferencePublicationData observation
                in savedPublications.observations)
            {
                CharacterTraitInferredFactPreflight.ValidateObservation(
                    observation,
                    Ledger.CaptureGameplayOutcomes());
            }
            traitInferences.Restore(savedPublications);
        }

    }

    private sealed class SkillSettingsProvider :
        ICharacterSkillSystemSettingsProvider
    {
        internal SkillSettingsProvider(CharacterSkillSystemSettingsSO settings) =>
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        public CharacterSkillSystemSettingsSO Settings { get; }
    }

    private sealed class MissingLlmRuntimeProvider : ILocalLlmRuntimeProvider
    {
        internal static readonly MissingLlmRuntimeProvider Instance = new();
        public bool TryGetRuntime(out ILocalLlmRuntime runtime)
        {
            runtime = null;
            return false;
        }
        public ILocalLlmRuntime GetRequiredRuntime() =>
            throw new InvalidOperationException(
                "Source capture must not invoke an LLM runtime.");
    }

    private sealed class ZeroUiClock : IUiClock
    {
        internal static readonly ZeroUiClock Instance = new();
        public float DeltaTime => 0f;
        public float Time => 0f;
    }

    private sealed class ReplaySkillRuntimeProvider : ILocalLlmRuntimeProvider
    {
        private readonly ReplaySkillRuntime runtime;
        internal ReplaySkillRuntimeProvider(ReplaySkillRuntime runtime) =>
            this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        public bool TryGetRuntime(out ILocalLlmRuntime value)
        {
            value = runtime;
            return true;
        }
        public ILocalLlmRuntime GetRequiredRuntime() => runtime;
    }

    private sealed class ReplaySkillRuntime :
        ILocalLlmRuntime,
        ICharacterSkillModuleSelectionBatchLlmRuntime
    {
        private readonly string response;
        internal ReplaySkillRuntime(string response) =>
            this.response = string.IsNullOrWhiteSpace(response)
                ? throw new ArgumentException(
                    "A replay response is required.",
                    nameof(response))
                : response;
        internal int BatchRequestCount { get; private set; }
        public bool GenerateCharacterSkillModuleSelectionBatchAsync(
            string requestKey,
            string prompt,
            Action<LocalLlmResult> callback)
        {
            if (string.IsNullOrWhiteSpace(requestKey)
                || string.IsNullOrWhiteSpace(prompt)
                || callback == null)
                return false;
            BatchRequestCount++;
            callback(new LocalLlmResult(
                LocalLlmRequestStatus.Succeeded,
                response,
                string.Empty,
                string.Empty));
            return true;
        }
        public bool GenerateCharacterSkillAsync(
            string prompt, Action<LocalLlmResult> callback) => false;
        public bool GeneratePersonaAsync(
            string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateMacroGoalAsync(
            string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateMoodImpulseAsync(
            string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateSocialRumorAsync(
            string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateFacilityEvolutionAsync(
            string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateCharacterRecordAsync(
            string prompt,
            string originalText,
            Action<LocalLlmResult> callback) => false;
        public bool GenerateBubbleLineAsync(
            string prompt,
            string originalText,
            Action<LocalLlmResult> callback) => false;
    }
}
#endif
