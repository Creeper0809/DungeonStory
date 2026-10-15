using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static partial class FacilityEvolutionDebugScenarios
{
    private const double FacilityExactJsonMetric = 97.054885864257813d;

    /// <summary>
    /// Synthetic persistence contract for exact outcome evidence nested in a
    /// facility formula pending and its durable material result. The fixture
    /// uses formula/material authorities; it does not alter gameplay balance.
    /// </summary>
    public static bool VerifyExactNestedJsonRoundTrip()
    {
        using EvolutionScenarioWorld world =
            EvolutionScenarioWorld.CreateCombatDining();
        FacilityEvolutionRecipeSO recipe = CreateCombatRecipe(
            world.SourceData,
            world.CombatResultData,
            consumeRecordToken: false);
        GameObject restoredObject = new("FacilityExactJsonRestoreFixture");
        try
        {
            PrepareFormulaExecutionFixture(world, recipe);
            FacilityEvolutionStateComponent source = world.SourceFacility
                .GetComponent<FacilityEvolutionStateComponent>();
            Require(source != null, "Facility exact-JSON source state is missing.");

            FacilityEvolutionState formulaState = source.InstanceEvolution;
            GameplayOutcomeEvidenceBindingSnapshot binding =
                CreateFacilityExactJsonBinding(formulaState.facilityPersistentId);
            Require(
                GameplayOutcomeEvidenceBindingAuthority.TryValidate(
                    binding,
                    out string bindingFailure),
                "Facility exact-JSON binding fixture is invalid: " + bindingFailure);
            Require(
                FacilityFormulaEvolutionAuthority.TryPrepare(
                    formulaState,
                    recipe,
                    out FacilityEvolutionFormulaPresentationPendingSnapshot pending,
                    out string prepareFailure,
                    new[] { binding }),
                "Facility exact-JSON formula preparation failed: " + prepareFailure);
            Require(
                pending?.node?.gameplayOutcomeEvidence?.Count == 1,
                "Facility formula pending did not retain its exact binding.");

            source.ReplaceInstanceEvolution(formulaState);
            source.BeginFormulaPresentation(pending);
            string expectedBindingJson = JsonUtility.ToJson(binding);
            string pendingPayload = source.CaptureState();

            FacilityEvolutionStateComponent restored =
                restoredObject.AddComponent<FacilityEvolutionStateComponent>();
            Require(
                restored.TryRestoreState(
                    restored.CurrentVersion,
                    pendingPayload,
                    out string restoreFailure),
                "Facility exact-JSON pending restore failed: " + restoreFailure);
            GameplayOutcomeEvidenceBindingSnapshot restoredPendingBinding =
                restored.PendingFormulaPresentation?.node?.gameplayOutcomeEvidence
                    ?.SingleOrDefault();
            RequireFacilityExactJsonBinding(
                restoredPendingBinding,
                expectedBindingJson,
                "formula pending");

            FacilityEvolutionStateSnapshot invalidPending =
                DungeonSaveJson.Deserialize<FacilityEvolutionStateSnapshot>(
                    pendingPayload);
            invalidPending.pendingFormulaPresentation.node
                .gameplayOutcomeEvidence.Single().metrics.Single().sampleCount = 0;
            RequireRejectedFacilityRestoreUnchanged(
                restored,
                restored.CurrentVersion,
                JsonUtility.ToJson(invalidPending),
                "invalid exact binding in formula pending");

            FacilityEvolutionFormulaPresentationPendingSnapshot finalized =
                source.RequireAndClearFormulaPresentation(pending.presentationId);
            finalized.displayName = "정밀한 시설 계보";
            finalized.narrativeFlavor =
                "시설에 남은 정확한 성과가 다음 쓰임으로 이어졌다.";
            FacilityEvolutionProposal proposal = new(
                "정밀한 전투 식당",
                new[] { recipe.EffectiveId },
                null,
                Array.Empty<string>(),
                finalized.narrativeFlavor,
                1f,
                FacilityEvolutionProposalSources.RuleBased);
            FacilityEvolutionStateSnapshot resolved =
                FacilityEvolutionStateComponent.BuildResolvedEvolutionSnapshot(
                    source.CreateSnapshot(),
                    world.SourceData,
                    world.CombatResultData,
                    recipe,
                    proposal,
                    FacilityShopService.GetBuildingName(world.SourceData),
                    profile: null,
                    resolvedMutationTags: Array.Empty<string>(),
                    resolvedRecord: source.GetRecord());
            Require(
                FacilityFormulaEvolutionAuthority.TryFinalizeApprovedPresentation(
                    resolved,
                    recipe,
                    finalized,
                    out string finalizeFailure),
                "Facility exact-JSON formula finalization failed: "
                    + finalizeFailure);
            FacilityEvolutionAggregateAdapter.Prepare(resolved);

            string finalizedPayload = JsonUtility.ToJson(resolved);
            Require(
                restored.TryRestoreState(
                    restored.CurrentVersion,
                    finalizedPayload,
                    out string finalizedRestoreFailure),
                "Facility exact-JSON finalized restore failed: "
                    + finalizedRestoreFailure);
            GameplayOutcomeEvidenceBindingSnapshot finalizedBinding =
                restored.InstanceEvolution.evolutionNodes
                    .Single(node => node != null
                        && string.Equals(
                            node.presentationId,
                            pending.presentationId,
                            StringComparison.Ordinal))
                    .gameplayOutcomeEvidence.Single();
            RequireFacilityExactJsonBinding(
                finalizedBinding,
                expectedBindingJson,
                "top-level finalized state");

            FacilityEvolutionStateSnapshot invalidFinalized =
                DungeonSaveJson.Deserialize<FacilityEvolutionStateSnapshot>(
                    finalizedPayload);
            invalidFinalized.instanceEvolution.evolutionNodes
                .Single(node => node != null
                    && string.Equals(
                        node.presentationId,
                        pending.presentationId,
                        StringComparison.Ordinal))
                .gameplayOutcomeEvidence.Single().metrics.Single().sampleCount = 0;
            RequireRejectedFacilityRestoreUnchanged(
                restored,
                restored.CurrentVersion,
                JsonUtility.ToJson(invalidFinalized),
                "invalid exact binding in top-level finalized state");

            FacilityEvolutionStateSnapshot duplicateFinalized =
                DungeonSaveJson.Deserialize<FacilityEvolutionStateSnapshot>(
                    finalizedPayload);
            EvolutionNode duplicateNode = duplicateFinalized.instanceEvolution
                .evolutionNodes.Single(node => node != null
                    && string.Equals(
                        node.presentationId,
                        pending.presentationId,
                        StringComparison.Ordinal));
            duplicateNode.gameplayOutcomeEvidence.Add(
                duplicateNode.gameplayOutcomeEvidence.Single().Clone());
            RequireRejectedFacilityRestoreUnchanged(
                restored,
                restored.CurrentVersion,
                JsonUtility.ToJson(duplicateFinalized),
                "duplicate exact binding in top-level finalized state");

            int historySequence = resolved.evolutionHistory.Count;
            string operationId = "facility-evolution-material:"
                + formulaState.facilityPersistentId
                + ":sequence:"
                + historySequence.ToString("D8");
            string reasonCode = "facility-evolution-material-incorporated:"
                + recipe.EffectiveId;
            FacilityEvolutionMaterialCommitReceipt receipt = new(
                operationId,
                reasonCode,
                "physical-batch-disposition:1:" + operationId + ":1:1",
                new[] { "stack:facility-exact-json" },
                quantity: 1,
                inputMassGrams: 1L);
            source.RecordPendingEvolution(
                new FacilityEvolutionCompletedFactData
                {
                    operationId = receipt.OperationId,
                    historySequence = historySequence,
                    facilityPersistentId = formulaState.facilityPersistentId,
                    facilityDisplayName = FacilityShopService.GetBuildingName(
                        world.SourceData),
                    recipeId = recipe.EffectiveId,
                    resultFacilityDefinitionId =
                        FacilityEvolutionUtility.GetFacilityId(
                            recipe.resultBuilding),
                    resultStarGrade = recipe.resultStarGrade,
                    materialCommitId = receipt.CommitId,
                    sourceStackIds = receipt.SourceStackIds.ToArray(),
                    materialQuantity = receipt.Quantity,
                    materialInputMassGrams = receipt.InputMassGrams,
                    absoluteDay = 23
                },
                reasonCode,
                FacilityEvolutionUtility.GetFacilityId(world.SourceData),
                resolved,
                Array.Empty<string>());

            FacilityEvolutionPendingMaterialCommitSnapshot materialPending =
                source.PendingMaterialCommit;
            Require(
                materialPending != null,
                "Facility exact-JSON material pending is missing.");
            Require(
                materialPending.phase
                    == FacilityEvolutionMaterialCommitPhase.MaterialCommitted
                && materialPending.completedFact != null
                && string.Equals(
                    materialPending.completedFact.operationId,
                    operationId,
                    StringComparison.Ordinal)
                && string.Equals(
                    materialPending.completedFact.facilityDisplayName,
                    FacilityShopService.GetBuildingName(world.SourceData),
                    StringComparison.Ordinal)
                && materialPending.completedFact.absoluteDay == 23
                && materialPending.completedFact.materialQuantity
                    == receipt.Quantity
                && materialPending.completedFact.materialInputMassGrams
                    == receipt.InputMassGrams
                && string.IsNullOrEmpty(materialPending.publicationKey)
                && string.IsNullOrEmpty(materialPending.publicationId)
                && string.IsNullOrEmpty(
                    materialPending.publicationFingerprint)
                && !materialPending.observationAccepted
                && !materialPending.observationDelivered,
                "Facility exact-JSON pending did not retain the neutral frozen fact boundary.");
            FacilityEvolutionStateSnapshot nestedResolved =
                materialPending.ReadResolvedResultState();
            GameplayOutcomeEvidenceBindingSnapshot restoredResolvedBinding =
                nestedResolved.instanceEvolution.evolutionNodes
                    .Single(node => node != null
                        && string.Equals(
                            node.presentationId,
                            pending.presentationId,
                            StringComparison.Ordinal))
                    .gameplayOutcomeEvidence.Single();
            RequireFacilityExactJsonBinding(
                restoredResolvedBinding,
                expectedBindingJson,
                "material resolved result");

            string canonical = source.CaptureState();
            RequireRejectedFacilityRestoreUnchanged(
                source,
                source.CurrentVersion,
                canonical + " {}",
                "malformed trailing JSON");
            RequireRejectedFacilityRestoreUnchanged(
                source,
                source.CurrentVersion + 1,
                canonical,
                "unsupported state version");
            RequireRejectedFacilityRestoreUnchanged(
                source,
                source.CurrentVersion - 1,
                canonical,
                "legacy state version");

            FacilityEvolutionStateSnapshot invalidCompletedFact =
                DungeonSaveJson.Deserialize<FacilityEvolutionStateSnapshot>(canonical);
            invalidCompletedFact.pendingMaterialCommit.completedFact.materialQuantity += 1;
            RequireRejectedFacilityRestoreUnchanged(
                source,
                source.CurrentVersion,
                JsonUtility.ToJson(invalidCompletedFact),
                "completed fact conflicting with its pending owner");

            FacilityEvolutionStateSnapshot invalidFrozen =
                DungeonSaveJson.Deserialize<FacilityEvolutionStateSnapshot>(canonical);
            FacilityEvolutionStateSnapshot invalidResolved =
                invalidFrozen.pendingMaterialCommit.ReadResolvedResultState();
            GameplayOutcomeEvidenceBindingSnapshot invalidBinding =
                invalidResolved.instanceEvolution.evolutionNodes
                    .Single(node => node != null
                        && string.Equals(
                            node.presentationId,
                            pending.presentationId,
                            StringComparison.Ordinal))
                    .gameplayOutcomeEvidence.Single();
            invalidBinding.metrics.Single().sampleCount = 0;
            invalidFrozen.pendingMaterialCommit.resolvedResultPayload =
                JsonUtility.ToJson(invalidResolved);
            RequireRejectedFacilityRestoreUnchanged(
                source,
                source.CurrentVersion,
                JsonUtility.ToJson(invalidFrozen),
                "frozen invalid exact binding");

            FacilityEvolutionPendingMaterialCommitSnapshot malformedNested =
                materialPending.Clone();
            malformedNested.resolvedResultPayload += " {}";
            bool malformedNestedRejected = false;
            try
            {
                malformedNested.ReadResolvedResultState();
            }
            catch (Exception exception) when (exception is InvalidOperationException
                or Newtonsoft.Json.JsonException)
            {
                malformedNestedRejected = true;
            }
            Require(
                malformedNestedRejected,
                "Facility material resolved-result reader accepted malformed JSON.");
            return true;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(restoredObject);
            UnityEngine.Object.DestroyImmediate(recipe);
        }
    }

    private static GameplayOutcomeEvidenceBindingSnapshot
        CreateFacilityExactJsonBinding(string facilityPersistentId)
    {
        const string metricId = "facility-throughput";
        GameplayOutcomeEvidenceBindingSnapshot binding = new()
        {
            schemaVersion = GameplayOutcomeEvidenceBindingAuthority.CurrentSchemaVersion,
            sourceKind = "exact",
            publicFactId = "public-fact:sha256:" + new string('9', 64),
            outcomeRunId = "run:facility-exact-json",
            outcomeSequence = 17L,
            outcomeTypeId = "facility.visit",
            subjectKindId = "facility",
            subjectId = facilityPersistentId,
            perspectiveKind = (int)NarrativePerspectiveKind.Facility,
            firstDay = 9,
            lastDay = 9,
            occurrenceCount = 1,
            anchorRevision = 1,
            status = (int)GameplayOutcomeStatus.Succeeded,
            subjectSalience = 1f,
            influenceUseCount = 0,
            influenceRevision = 1,
            canonicalFactText = "시설의 정확한 처리량 성과가 확정됐다.",
            roleIds = new List<string> { "facility" },
            metricIds = new List<string> { metricId },
            metricReferenceIds = new List<string>(),
            factIds = new List<string> { "result" },
            semanticTags = new List<string> { "facility" },
            participants = new List<
                GameplayOutcomeEvidenceParticipantBindingSnapshot>
            {
                new()
                {
                    entityKindId = "facility",
                    entityId = facilityPersistentId,
                    roleId = "facility",
                    participationKind = (int)GameplayParticipationKind.Direct,
                    hasPerceptionEvidence = true,
                    displayText = "정밀 검증 시설"
                }
            },
            metrics = new List<GameplayOutcomeEvidenceMetricBindingSnapshot>
            {
                new()
                {
                    metricId = metricId,
                    unitId = "points",
                    value = FacilityExactJsonMetric,
                    minimum = FacilityExactJsonMetric,
                    maximum = FacilityExactJsonMetric,
                    sampleCount = 1
                }
            },
            facts = new List<GameplayOutcomeEvidenceFactBindingSnapshot>
            {
                new() { factId = "result", value = "confirmed" }
            },
            provenance = new List<
                GameplayOutcomeEvidenceProvenanceBindingSnapshot>()
        };
        binding.experience = GameplayExperienceObserverRegistration.Default
            .Observe(binding);
        return binding;
    }

    private static void RequireFacilityExactJsonBinding(
        GameplayOutcomeEvidenceBindingSnapshot actual,
        string expectedJson,
        string label)
    {
        Require(actual != null, "Facility " + label + " binding is missing.");
        Require(
            GameplayOutcomeEvidenceBindingAuthority.TryValidate(
                actual,
                out string bindingFailure),
            "Facility " + label + " binding is invalid: " + bindingFailure);
        GameplayOutcomeEvidenceMetricBindingSnapshot metric =
            actual.metrics.Single();
        long expectedBits = BitConverter.DoubleToInt64Bits(
            FacilityExactJsonMetric);
        Require(
            BitConverter.DoubleToInt64Bits(metric.value) == expectedBits
            && BitConverter.DoubleToInt64Bits(metric.minimum) == expectedBits
            && BitConverter.DoubleToInt64Bits(metric.maximum) == expectedBits,
            "Facility " + label + " changed exact metric bits.");
        Require(
            string.Equals(
                JsonUtility.ToJson(actual),
                expectedJson,
                StringComparison.Ordinal),
            "Facility " + label + " changed the full exact-binding JSON.");
    }

    private static void RequireRejectedFacilityRestoreUnchanged(
        FacilityEvolutionStateComponent state,
        int version,
        string payload,
        string label)
    {
        string before = state.CaptureState();
        bool rejected = !state.TryRestoreState(version, payload, out _);
        Require(rejected, "Facility restore accepted " + label + ".");
        Require(
            string.Equals(state.CaptureState(), before, StringComparison.Ordinal),
            "Facility restore changed state after rejecting " + label + ".");
    }
}
