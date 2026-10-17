#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using Newtonsoft.Json.Linq;

internal sealed class PrimitiveSkillModuleSelectionCapture
{
    internal PrimitiveSkillModuleSelectionCapture(
        string ownerId,
        string characterId,
        string displayName,
        JObject checkpoint,
        CharacterSkillDraft draft,
        IReadOnlyList<NarrativeFormulaModuleSelectionRequest> requests,
        IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot> evidence,
        string prompt)
    {
        OwnerId = ownerId;
        CharacterId = characterId;
        DisplayName = displayName;
        Checkpoint = (JObject)checkpoint.DeepClone();
        Draft = draft.Clone();
        Requests = requests.ToArray();
        Evidence = evidence.Select(value => value.Clone()).ToArray();
        Prompt = prompt;
    }

    internal string OwnerId { get; }
    internal string CharacterId { get; }
    internal string DisplayName { get; }
    internal JObject Checkpoint { get; }
    internal CharacterSkillDraft Draft { get; }
    internal IReadOnlyList<NarrativeFormulaModuleSelectionRequest> Requests { get; }
    internal IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot> Evidence { get; }
    internal string Prompt { get; }
}

internal sealed class PrimitiveTraitModuleSelectionCapture
{
    internal PrimitiveTraitModuleSelectionCapture(
        string ownerId,
        string characterId,
        string displayName,
        JObject checkpoint,
        CharacterAcquiredTraitPendingRequestState pending,
        CharacterAcquiredTraitRequestPacketDto packet,
        NarrativeFormulaModuleSelectionRequest request,
        IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot> evidence,
        string prompt)
    {
        OwnerId = ownerId;
        CharacterId = characterId;
        DisplayName = displayName;
        Checkpoint = (JObject)checkpoint.DeepClone();
        Pending = pending.Clone();
        Packet = packet.Clone();
        Request = request;
        Evidence = evidence.Select(value => value.Clone()).ToArray();
        Prompt = prompt;
    }

    internal string OwnerId { get; }
    internal string CharacterId { get; }
    internal string DisplayName { get; }
    internal JObject Checkpoint { get; }
    internal CharacterAcquiredTraitPendingRequestState Pending { get; }
    internal CharacterAcquiredTraitRequestPacketDto Packet { get; }
    internal NarrativeFormulaModuleSelectionRequest Request { get; }
    internal IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot> Evidence { get; }
    internal string Prompt { get; }
}

public static partial class PrimitiveSurvivalCausalProductionEntryDebugScenarios
{
    internal static GameplayModuleSelectionRuntimeReplayResult
        ReplaySkillModuleSelection(
            string ownerId,
            JObject checkpoint,
            CharacterSkillSystemSettingsSO settings,
            string responseJson)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));
        string canonicalOwner = CausalGameplaySyntax.RequireId(
            ownerId,
            nameof(ownerId));
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
                "Skill replay response is invalid: " + responseError);
        }

        JObject before;
        JObject applied;
        string requestKey;
        string[] resultIds;
        using (var context = new Context(
                   "run:module-replay:skill:apply",
                   canonicalOwner,
                   checkpoint))
        {
            CharacterProgression progression = context.SourceActor.Progression;
            CharacterSkillDraft[] pendingDrafts = progression.GrowthState.drafts
                .Where(value => value != null
                    && value.formulaVersion >= CharacterSkillFormulaGeneration
                        .ModuleSelectionFormulaVersion
                    && value.requestSubmitted
                    && value.presentationState
                        == CharacterSkillPresentationState.PresentationPending
                    && (value.candidates == null || value.candidates.Count == 0))
                .ToArray();
            if (pendingDrafts.Length != 1)
            {
                string states = string.Join(",", progression.GrowthState.drafts
                    .Where(value => value != null)
                    .Select(value => $"{value.requestKey}|v{value.formulaVersion}|ready={value.isReady}|state={value.presentationState}|submitted={value.requestSubmitted}"));
                throw new InvalidOperationException(
                    "Skill replay checkpoint must restore exactly one pending formula draft; "
                    + $"found={pendingDrafts.Length}; drafts=[{states}]");
            }
            CharacterSkillDraft draft = pendingDrafts[0];
            // CharacterActor's editor fixture service marks every restored
            // pending draft ready during initialization without producing a
            // candidate.  The persisted source checkpoint explicitly stores
            // isReady=false. Restore that byte before hashing and hand the
            // request to the real generation service below.
            draft.isReady = false;
            before = context.CaptureOwnerCheckpoint();
            requestKey = draft.requestKey;
            BindCharacterReplayDependencies(
                progression,
                settings,
                context.SourceOutcomeLedger);
            progression.SetPublicSkillNotificationsSuppressed(true);
            var runtime = new ReplaySkillRuntime(canonicalResponse);
            CharacterSkillGenerationService service = new(
                NarrativeRequestContextTestFixture.CreateEmpty(), new SourceSkillSettingsProvider(settings),
                new ReplaySkillRuntimeProvider(runtime),
                SourceUiClock.Instance);
            service.RequestDraft(progression, draft);
            service.Tick();
            if (runtime.BatchRequestCount != 1
                || !draft.isReady
                || draft.presentationState != CharacterSkillPresentationState.Ready
                || draft.requestSubmitted
                || service.PendingRequestCount != 0
                || draft.candidates.Count != response.candidates.Count)
            {
                throw new InvalidOperationException(
                    "The live CharacterSkill parser/service did not accept and finish the replay response. "
                    + service.LastDiagnostic);
            }
            resultIds = draft.candidates
                .Select(value => value?.presentationId)
                .ToArray();
            applied = context.CaptureOwnerCheckpoint();
        }

        JObject restoredCheckpoint;
        using (var restored = new Context(
                   "run:module-replay:skill:restore",
                   canonicalOwner,
                   applied))
        {
            CharacterSkillDraft restoredDraft = restored.SourceActor.Progression
                .GrowthState.drafts.Single(value => value != null
                    && string.Equals(
                        value.requestKey,
                        requestKey,
                        StringComparison.Ordinal));
            if (!restoredDraft.isReady
                || restoredDraft.candidates.Count != resultIds.Length
                || !restoredDraft.candidates.Select(value => value.presentationId)
                    .SequenceEqual(resultIds, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    "Applied CharacterSkill module selections did not survive save restore.");
            }
            restoredCheckpoint = restored.CaptureOwnerCheckpoint();
        }

        return new GameplayModuleSelectionRuntimeReplayResult(
            before,
            applied,
            restoredCheckpoint,
            resultIds,
            response.candidates
                .SelectMany(value => value.evidenceFactIds)
                .Distinct(StringComparer.Ordinal));
    }

    internal static GameplayModuleSelectionRuntimeReplayResult
        ReplayTraitModuleSelection(
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
        string canonicalOwner = CausalGameplaySyntax.RequireId(
            ownerId,
            nameof(ownerId));
        string canonicalResponse = responseJson?.Trim() ?? string.Empty;

        JObject before;
        JObject applied;
        string requestId;
        string resultId;
        string[] evidenceIds;
        using (var context = new Context(
                   "run:module-replay:trait:apply",
                   canonicalOwner,
                   checkpoint))
        {
            before = context.CaptureOwnerCheckpoint();
            CharacterProgression progression = context.SourceActor.Progression;
            BindGameplayOutcomeEvidenceUseTransaction(
                progression,
                context.SourceOutcomeLedger);
            CharacterAcquiredTraitAggregateState current =
                progression.CaptureAcquiredTraitState();
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
                        progression,
                        new CharacterAcquiredTraitCompletionCommand(
                            pending.targetPersistentId,
                            pending.requestId,
                            pending.requestKey,
                            pending.candidatePacketHash,
                            pending.registeredRevision,
                            current.revision,
                            canonicalResponse,
                            NarrativeInferenceTimestamp.FromGameTick(
                                17L * GameCalendarRules.HoursPerDay)),
                        packet,
                        context.SourceTraitInferenceFacts,
                        context.SourceTraitInferences);
            if (!completion.Succeeded)
            {
                throw new InvalidOperationException(
                    "The live acquired-trait parser/service rejected the replay response: "
                    + completion.Audit.ValidationError);
            }
            CharacterAcquiredTraitAggregateState committed =
                progression.CaptureAcquiredTraitState();
            CharacterAcquiredTraitInstanceState instance = committed.instances
                .Single(value => value != null
                    && string.Equals(
                        value.originatingRequestId,
                        requestId,
                        StringComparison.Ordinal));
            resultId = instance.instanceId;
            evidenceIds = instance.evidenceFactIds
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            applied = context.CaptureOwnerCheckpoint();
        }

        JObject restoredCheckpoint;
        using (var restored = new Context(
                   "run:module-replay:trait:restore",
                   canonicalOwner,
                   applied))
        {
            CharacterAcquiredTraitAggregateState state = restored.SourceActor
                .Progression.CaptureAcquiredTraitState();
            if (state.pendingRequests.Any(value => value != null
                    && string.Equals(value.requestId, requestId,
                        StringComparison.Ordinal))
                || state.instances.Count(value => value != null
                    && string.Equals(value.instanceId, resultId,
                        StringComparison.Ordinal)) != 1)
            {
                throw new InvalidOperationException(
                    "Applied acquired trait did not survive save restore exactly once.");
            }
            restoredCheckpoint = restored.CaptureOwnerCheckpoint();
        }

        return new GameplayModuleSelectionRuntimeReplayResult(
            before,
            applied,
            restoredCheckpoint,
            new[] { resultId },
            evidenceIds);
    }

    internal static PrimitiveSkillModuleSelectionCapture
        CaptureSkillModuleSelectionSource(
            string ownerId,
            CharacterSkillSystemSettingsSO settings,
            CharacterSkillKind kind,
            int revision)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));
        string canonicalOwner = CausalGameplaySyntax.RequireId(
            ownerId,
            nameof(ownerId));
        JObject checkpoint;
        string requestKey;
        using (var source = new Context(
                   "run:module-source:skill:write",
                   canonicalOwner,
                   checkpoint: null))
        {
            if (!source.CompleteFirstRest())
                throw new InvalidOperationException(
                    "The actual primitive-survival skill source did not commit.");
            IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot> evidence =
                CaptureExact(source, maximumCount: 1);
            CharacterSkillGenerationService service =
                new(
                    NarrativeRequestContextTestFixture.CreateEmpty(), new SourceSkillSettingsProvider(settings),
                    SourceMissingLlmRuntimeProvider.Instance,
                    SourceUiClock.Instance);
            CharacterSkillDraft draft = service.CreateDraft(
                source.SourceActor.Progression,
                kind,
                unlockLevel: kind == CharacterSkillKind.Ultimate ? 50 : 10,
                revision: Math.Max(0, revision));
            CharacterSkillFormulaGeneration.InitializeModuleSelection(
                source.SourceActor.Progression,
                draft,
                settings,
                evidence);
            source.SourceActor.Progression.GrowthState.drafts.Add(draft);
            source.SourceActor.Progression.MarkGenerationRequestPending(
                draft.requestKey);
            draft.requestSubmitted = true;
            requestKey = draft.requestKey;
            checkpoint = source.CaptureOwnerCheckpoint();
        }

        using var restored = new Context(
            "run:module-source:skill:restore",
            canonicalOwner,
            checkpoint);
        CharacterSkillDraft restoredDraft = restored.SourceActor.Progression
            .GrowthState.drafts.Single(value => value != null
                && string.Equals(
                    value.requestKey,
                    requestKey,
                    StringComparison.Ordinal));
        IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot> restoredEvidence =
            CaptureExact(restored, maximumCount: 1);
        if (!restoredDraft.outcomeEvidenceBindings
                .Select(value => value.publicFactId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .SequenceEqual(
                    restoredEvidence
                        .Select(value => value.publicFactId)
                        .OrderBy(value => value, StringComparer.Ordinal),
                    StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "The pending skill lost its exact evidence across save restore.");
        }
        NarrativePublicContextMaterial publicMaterial =
            CharacterSkillPromptBuilder.BuildPublicMaterial(
                NarrativeRequestContextTestFixture.CreateEmpty(), restored.SourceActor.Progression);
        publicMaterial = GameplayOutcomeEvidencePublicMaterialComposer
            .AddExactEvidence(publicMaterial, restoredEvidence);
        string prompt = CharacterSkillPromptBuilder.BuildEnvelope(
            restored.SourceActor.Progression,
            restoredDraft,
            settings,
            publicMaterial,
            batchModuleSelection: true).Prompt;
        NarrativeFormulaModuleSelectionRequest[] requests =
            Enumerable.Range(
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
            restored.SourceActor.Identity.PersistentId,
            restored.SourceActor.Identity.DisplayName,
            checkpoint,
            restoredDraft,
            requests,
            restoredEvidence,
            prompt);
    }

    internal static PrimitiveTraitModuleSelectionCapture
        CaptureTraitModuleSelectionSource(
            string ownerId,
            CharacterAcquiredTraitSettingsSO settings,
            IReadOnlyList<CharacterAcquiredTraitModuleSO> modules)
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
        JObject checkpoint;
        string requestId;
        using (var source = new Context(
                   "run:module-source:trait:write",
                   canonicalOwner,
                   checkpoint: null))
        {
            if (!source.CompleteFirstRest()
                || !source.CompleteRepeatRest()
                || !source.CompleteThirdRest())
            {
                throw new InvalidOperationException(
                    "The actual three-event trait source did not commit.");
            }
            IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot> exact =
                CaptureExact(source, maximumCount: 3);
            CharacterProgression progression = source.SourceActor.Progression;
            string[] meaningful = CharacterAcquiredTraitMeaningfulLedgerValidator
                .RequireMeaningfulFactIds(progression.NarrativeLedger)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            HashSet<string> meaningfulSet = meaningful.ToHashSet(StringComparer.Ordinal);
            string[] local = GameplayOutcomeEvidenceFormulaProjection
                .GetRemainingLegacyMilestoneCounts(progression.NarrativeLedger.Facts, exact).Keys
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
                .AddExactEvidence(publicMaterial, exact);
            IReadOnlyList<string> projectedLocal = local.Length == 0
                ? Array.Empty<string>()
                : CharacterAcquiredTraitPromptBuilder
                    .BuildProjectedEvidenceFactIds(publicMaterial, local);
            string[] evidenceIds = projectedLocal
                .Concat(exact.Select(value => value.publicFactId))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            CharacterAcquiredTraitManifestationRuntime
                .BuildProductionRequestIdentity(
                    settings,
                    source.SourceActor.Identity.PersistentId,
                    milestone: 3,
                    evidenceIds,
                    publicMaterial.SemanticHash,
                    CharacterNarrativeRequestOwnerBinding.Capture(progression),
                    progression.AcquiredTraitRevision,
                    out requestId,
                    out string requestKey);
            CharacterAcquiredTraitAggregateState current =
                progression.CaptureAcquiredTraitState();
            CharacterAcquiredTraitInferenceCommandResult submitted =
                new CharacterAcquiredTraitInferenceService(settings, definitions)
                    .SubmitMilestone(
                        progression,
                        new CharacterAcquiredTraitSubmissionCommand(
                            source.SourceActor.Identity.PersistentId,
                            requestId,
                            requestKey,
                            3,
                            evidenceIds,
                            exact,
                            current.revision,
                            NarrativeInferenceTimestamp.FromGameTick(16 * 24L)));
            if (!submitted.Succeeded || submitted.Packet == null)
            {
                throw new InvalidOperationException(
                    "Actual acquired-trait pending registration failed: "
                    + submitted.Audit.ValidationError);
            }
            checkpoint = source.CaptureOwnerCheckpoint();
        }

        using var restored = new Context(
            "run:module-source:trait:restore",
            canonicalOwner,
            checkpoint);
        CharacterProgression restoredProgression =
            restored.SourceActor.Progression;
        CharacterAcquiredTraitAggregateState restoredState =
            restoredProgression.CaptureAcquiredTraitState();
        if (!restoredState.TryGetPendingRequest(
                requestId,
                out CharacterAcquiredTraitPendingRequestState pending)
            || pending.presentationState
                != CharacterAcquiredTraitPresentationState.ModuleSelectionPending)
        {
            throw new InvalidOperationException(
                "The acquired-trait pending request did not survive save restore.");
        }
        HashSet<string> exactIds = pending.evidenceBindings
            .Select(value => value.publicFactId)
            .ToHashSet(StringComparer.Ordinal);
        string[] localEvidence = pending.evidenceFactIds
            .Where(value => !exactIds.Contains(value))
            .ToArray();
        NarrativePublicContextMaterial restoredMaterial =
            CharacterAcquiredTraitPromptBuilder.BuildPublicMaterial(
                NarrativeRequestContextTestFixture.CreateEmpty(), restoredProgression,
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
                restoredProgression,
                pending.requestId,
                pending.requestKey,
                pending.manifestationMilestone,
                settings,
                definitions,
                pending.evidenceFactIds,
                pending.evidenceBindings);
        string prompt = CharacterAcquiredTraitPromptBuilder.BuildEnvelope(
            restoredProgression,
            packet,
            restoredMaterial,
            settings,
            definitions).Prompt;
        return new PrimitiveTraitModuleSelectionCapture(
            canonicalOwner,
            restored.SourceActor.Identity.PersistentId,
            restored.SourceActor.Identity.DisplayName,
            checkpoint,
            pending,
            packet,
            request,
            pending.evidenceBindings,
            prompt);
    }

    private static IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot>
        CaptureExact(Context source, int maximumCount)
    {
        IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot> evidence =
            source.CaptureNarrativeEvidence(
                new GameplayEntityId(
                    MigratedProducerOutcomeIds.CharacterKind,
                    source.SourceActor.Identity.PersistentId),
                maximumCount);
        if (evidence.Count != maximumCount)
        {
            throw new InvalidOperationException(
                "Primitive-survival exact evidence count changed.");
        }
        return evidence;
    }

    private sealed class SourceSkillSettingsProvider :
        ICharacterSkillSystemSettingsProvider
    {
        internal SourceSkillSettingsProvider(
            CharacterSkillSystemSettingsSO settings) =>
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));

        public CharacterSkillSystemSettingsSO Settings { get; }
    }

    private sealed class SourceMissingLlmRuntimeProvider :
        ILocalLlmRuntimeProvider
    {
        internal static readonly SourceMissingLlmRuntimeProvider Instance = new();

        public bool TryGetRuntime(out ILocalLlmRuntime runtime)
        {
            runtime = null;
            return false;
        }

        public ILocalLlmRuntime GetRequiredRuntime() =>
            throw new InvalidOperationException(
                "Source capture must not invoke an LLM runtime.");
    }

    private sealed class SourceUiClock : IUiClock
    {
        internal static readonly SourceUiClock Instance = new();
        public float DeltaTime => 0f;
        public float Time => 0f;
    }

    private static void BindCharacterReplayDependencies(
        CharacterProgression progression,
        CharacterSkillSystemSettingsSO settings,
        GameplayOutcomeLedger ledger)
    {
        if (progression == null)
            throw new ArgumentNullException(nameof(progression));
        if (ledger == null)
            throw new ArgumentNullException(nameof(ledger));
        SetProgressionField(
            progression,
            "settingsProvider",
            new SourceSkillSettingsProvider(settings));
        BindGameplayOutcomeEvidenceUseTransaction(progression, ledger);
    }

    private static void BindGameplayOutcomeEvidenceUseTransaction(
        CharacterProgression progression,
        GameplayOutcomeLedger ledger)
    {
        if (progression == null)
            throw new ArgumentNullException(nameof(progression));
        if (ledger == null)
            throw new ArgumentNullException(nameof(ledger));
        SetProgressionField(
            progression,
            "evidenceUseTransaction",
            new GameplayOutcomeEvidenceUseTransaction(
                ledger,
                new GameplayOutcomeNarrativeEvidenceQuery(ledger)));
    }

    private static void SetProgressionField(
        CharacterProgression progression,
        string fieldName,
        object value)
    {
        FieldInfo field = typeof(CharacterProgression).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(
                typeof(CharacterProgression).FullName,
                fieldName);
        field.SetValue(progression, value);
    }

    private sealed class ReplaySkillRuntimeProvider : ILocalLlmRuntimeProvider
    {
        private readonly ReplaySkillRuntime runtime;

        internal ReplaySkillRuntimeProvider(ReplaySkillRuntime runtime) =>
            this.runtime = runtime
                ?? throw new ArgumentNullException(nameof(runtime));

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
            string prompt,
            Action<LocalLlmResult> callback) => false;
        public bool GeneratePersonaAsync(
            string prompt,
            Action<LocalLlmResult> callback) => false;
        public bool GenerateMacroGoalAsync(
            string prompt,
            Action<LocalLlmResult> callback) => false;
        public bool GenerateMoodImpulseAsync(
            string prompt,
            Action<LocalLlmResult> callback) => false;
        public bool GenerateSocialRumorAsync(
            string prompt,
            Action<LocalLlmResult> callback) => false;
        public bool GenerateFacilityEvolutionAsync(
            string prompt,
            Action<LocalLlmResult> callback) => false;
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
