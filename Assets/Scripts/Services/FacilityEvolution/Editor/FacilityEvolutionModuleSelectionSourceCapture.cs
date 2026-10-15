#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

internal sealed class FacilityEvolutionModuleSelectionSourceCapture
{
    internal FacilityEvolutionModuleSelectionSourceCapture(
        string ownerId,
        string facilityId,
        string displayName,
        JObject checkpoint,
        FacilityEvolutionFormulaPresentationPendingSnapshot pending,
        NarrativeFormulaModuleSelectionRequest request,
        IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot> evidence,
        string prompt)
    {
        OwnerId = ownerId;
        FacilityId = facilityId;
        DisplayName = displayName;
        Checkpoint = (JObject)checkpoint.DeepClone();
        Pending = pending.Clone();
        Request = request;
        Evidence = evidence.Select(value => value.Clone()).ToArray();
        Prompt = prompt;
    }

    internal string OwnerId { get; }
    internal string FacilityId { get; }
    internal string DisplayName { get; }
    internal JObject Checkpoint { get; }
    internal FacilityEvolutionFormulaPresentationPendingSnapshot Pending { get; }
    internal NarrativeFormulaModuleSelectionRequest Request { get; }
    internal IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot> Evidence { get; }
    internal string Prompt { get; }
}

public static partial class FacilityEvolutionDebugScenarios
{
    internal static GameplayModuleSelectionRuntimeReplayResult
        ReplayModuleSelection(
            string ownerId,
            JObject checkpoint,
            string responseJson)
    {
        string canonicalOwner = CausalGameplaySyntax.RequireId(
            ownerId,
            nameof(ownerId));
        string canonicalResponse = responseJson?.Trim() ?? string.Empty;
        if (!NarrativeExactKeyContract.TryValidateProfileResponse(
                LocalLlmRequestProfiles.FacilityEvolutionModuleSelection.Id,
                canonicalResponse,
                out _,
                out string responseError)
            || !LlmJsonResponseParser.TryParse(
                canonicalResponse,
                out FacilityFormulaEvolutionModuleSelectionDto response,
                out responseError))
        {
            throw new InvalidOperationException(
                "Facility replay response is invalid: " + responseError);
        }

        JObject before;
        JObject applied;
        string resultFormulaId;
        using (var context = new CausalEvolutionContext(
                   "run:module-replay:facility:apply",
                   canonicalOwner,
                   checkpoint))
        {
            before = context.CaptureOwnerCheckpoint();
            FacilityEvolutionFormulaPresentationPendingSnapshot pending =
                context.SourceState.PendingFormulaPresentation?.Clone()
                ?? throw new InvalidOperationException(
                    "Facility replay checkpoint has no pending formula selection.");
            string persistedPositiveModuleId = pending.node.moduleSelectionOffers
                .Where(value => value != null
                    && value.polarity == EvolutionModuleOfferPolarity.Positive)
                .Select(value => value.moduleId)
                .Single();
            P1FacilityEvolutionAssetBuilder
                .ApplyFormulaAuthoringForControlledSimulation(
                    context.SourceSecondRecipe,
                    persistedPositiveModuleId);
            resultFormulaId = pending.node?.nodeId ?? string.Empty;
            if (!context.CommitSecondFormulaSelection(
                    response,
                    out FacilityEvolutionResult result,
                    out string failureReason)
                || !result.Success
                || result.ResultBuilding == null)
            {
                throw new InvalidOperationException(
                    "The live facility parser/service rejected the replay response: "
                    + failureReason);
            }
            FacilityEvolutionStateComponent state = context.SourceState;
            EvolutionNode committed = state.InstanceEvolution.evolutionNodes
                .Single(value => value != null
                    && string.Equals(
                        value.nodeId,
                        resultFormulaId,
                        StringComparison.Ordinal));
            if (committed.presentationState
                    != EquipmentEvolutionPresentationState.Ready
                || !committed.active
                || !committed.mechanicallyUnlocked
                || state.PendingFormulaPresentation != null)
            {
                throw new InvalidOperationException(
                    "Facility module selection did not atomically publish its formula.");
            }
            applied = context.CaptureOwnerCheckpoint();
        }

        JObject restoredCheckpoint;
        using (var restored = new CausalEvolutionContext(
                   "run:module-replay:facility:restore",
                   canonicalOwner,
                   applied))
        {
            if (restored.SourceStage != 2
                || restored.SourceState.PendingFormulaPresentation != null
                || restored.SourceState.InstanceEvolution.evolutionNodes.Count(
                    value => value != null
                        && string.Equals(
                            value.nodeId,
                            resultFormulaId,
                            StringComparison.Ordinal)
                        && value.presentationState
                            == EquipmentEvolutionPresentationState.Ready) != 1)
            {
                throw new InvalidOperationException(
                    "Applied facility formula did not survive save restore exactly once.");
            }
            restoredCheckpoint = restored.CaptureOwnerCheckpoint();
        }

        return new GameplayModuleSelectionRuntimeReplayResult(
            before,
            applied,
            restoredCheckpoint,
            new[] { resultFormulaId },
            response.evidenceFactIds.Distinct(StringComparer.Ordinal));
    }

    internal static FacilityEvolutionModuleSelectionSourceCapture
        CaptureModuleSelectionSource(string ownerId, int variant = 0)
    {
        string canonicalOwner = CausalGameplaySyntax.RequireId(
            ownerId,
            nameof(ownerId));
        JObject evolvedCheckpoint;
        using (var source = new CausalEvolutionContext(
                   "run:module-source:facility:first",
                   canonicalOwner,
                   checkpoint: null))
        {
            if (!source.ExecuteFirst()
                || !source.RequireFirstOutcome().HasCanonicalPayloadHash)
            {
                throw new InvalidOperationException(
                    "The actual first facility evolution did not commit.");
            }
            source.RecordFacilityVisits(
                1 + Math.Abs(variant % 4),
                variant);
            evolvedCheckpoint = source.CaptureOwnerCheckpoint();
        }

        JObject pendingCheckpoint;
        string selectionId;
        using (var restored = new CausalEvolutionContext(
                   "run:module-source:facility:pending",
                   canonicalOwner,
                   evolvedCheckpoint))
        {
            IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot> evidence =
                restored.CaptureNarrativeEvidence(
                    new GameplayEntityId(
                        new GameplayEntityKindId("facility"),
                        restored.SourceFacilityPersistentId),
                    8);
            // The causal owner fixture deliberately starts with the legacy
            // formula-v1 execution contract so its committed first evolution
            // remains a regression oracle.  The training-source capture opens
            // the second, still-uncommitted recipe through the same v3
            // authoring path used by real P1 assets.  Survival output is
            // reachable by the Meal|Training result building and is therefore
            // revalidated by TryPrepare rather than trusted from this fixture.
            P1FacilityEvolutionAssetBuilder
                .ApplyFormulaAuthoringForControlledSimulation(
                    restored.SourceSecondRecipe,
                    "facility:survival");
            FacilityEvolutionStateComponent state = restored.SourceState;
            FacilityEvolutionState instanceState = state.InstanceEvolution;
            // CausalEvolutionContext uses direct legacy UsageLedger fixtures to
            // qualify the first recipe.  Those rows have no production outcome
            // identity and must not enter a source-v8 model request.  Retain a
            // legacy row only when the exact gameplay ledger proves the same
            // origin; the authority then de-duplicates that paired projection.
            // The first committed evolution remains available below as exact
            // gameplay-outcome evidence and is the sole source fact when no
            // linked legacy projection exists.
            instanceState.usageLedger.currentGenerationEvents =
                (instanceState.usageLedger.currentGenerationEvents
                    ?? new List<UsageLedgerEvent>())
                .Where(legacy => legacy != null && evidence.Any(exact =>
                    GameplayOutcomeEvidenceFormulaProjection.HasCommonOrigin(
                        exact,
                        legacy)))
                .Select(legacy => legacy.Clone())
                .ToList();
            if (!FacilityFormulaEvolutionAuthority.TryPrepare(
                    instanceState,
                    restored.SourceSecondRecipe,
                    out FacilityEvolutionFormulaPresentationPendingSnapshot pending,
                    out string failureReason,
                    evidence))
            {
                throw new InvalidOperationException(
                    "Actual facility module-selection preparation failed: "
                    + failureReason);
            }
            state.ReplaceInstanceEvolution(instanceState);
            state.BeginFormulaPresentation(pending);
            NarrativeFormulaModuleSelectionRequest request =
                FacilityFormulaEvolutionAuthority.BuildModuleSelectionRequest(
                    pending.node,
                    restored.SourceSecondRecipe);
            string prompt = FacilityFormulaEvolutionPresentationRuntime.BuildPrompt(
                restored.SourceFacility,
                pending);
            pendingCheckpoint = restored.CaptureOwnerCheckpoint();
            selectionId = pending.presentationId;
            string displayName = restored.SourceDisplayName;
            var result = new FacilityEvolutionModuleSelectionSourceCapture(
                canonicalOwner,
                restored.SourceFacilityPersistentId,
                displayName,
                pendingCheckpoint,
                pending,
                request,
                evidence,
                prompt);

            using var verified = new CausalEvolutionContext(
                "run:module-source:facility:verify-restore",
                canonicalOwner,
                pendingCheckpoint);
            if (!string.Equals(
                    verified.SourceState.PendingFormulaPresentation
                        ?.presentationId,
                    selectionId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Facility module-selection pending state did not survive save restore.");
            }
            return result;
        }
    }
}
#endif
