using System;
using System.Linq;
using DungeonStory.Narrative.Korean;
using VContainer;

public static class ProductionCompletionFactObservation
{
    public static ProductionCompletedOutcomeReceipt Capture(
        ProductionCycleCompletedFact fact)
    {
        ProductionCycleCompletedFactData data =
            (fact ?? throw new ArgumentNullException(nameof(fact))).Snapshot();
        ProductionOutcomeLineSnapshot[] lines = data.lines
            .Select(value => new ProductionOutcomeLineSnapshot(
                value.outputLineId,
                value.commitIds,
                value.itemId,
                value.quantity,
                value.massGrams,
                value.capabilityFingerprint))
            .ToArray();
        ProductionOutcomePhysicalStackSnapshot[] stacks = data.stacks
            .Select(value => new ProductionOutcomePhysicalStackSnapshot(
                value.stackId,
                value.outputLineId,
                value.itemId,
                value.quantity,
                value.massGrams,
                value.itemInstanceId,
                value.sourceCommitId,
                value.componentSignature))
            .ToArray();
        return new ProductionCompletedOutcomeReceipt(
            new ProductionBillId(data.billId),
            data.cycleSequence,
            data.recipeId,
            new BuildingInstanceId(data.facilityId),
            FacilityName(data.facilityId, data.facilityDisplayName),
            data.workerId,
            WorkerName(data.workerId, data.workerDisplayName),
            data.batchCommitId,
            data.outcomeFingerprint,
            data.plannedOutputFingerprint,
            data.destinationId,
            data.wipInputCommitId,
            data.wipInputQuantity,
            data.wipInputMassGrams,
            data.processCleanWaterMassGrams,
            data.processWastewaterMassGrams,
            data.declaredLossMassGrams,
            data.declaredExternalInputMassGrams,
            lines,
            stacks,
            data.absoluteDay);
    }

    public static void RegisterProductionCompletionFactObservation(
        this IContainerBuilder builder) =>
        builder.RegisterGameplayDomainFact<
            ProductionCycleCompletedFact,
            ProductionCompletedOutcomeReceipt>(
                "production.completion-observer",
                Capture);

    private static KoreanNameSnapshot FacilityName(
        string stableId,
        string displayName) => new(
        displayName,
        "runtime-facility-current-v1:" + stableId,
        KoreanPronunciationHint.AutoHangulDisplay(
            "runtime-facility-pronunciation-v1:" + stableId),
        "ko-KR");

    private static KoreanNameSnapshot WorkerName(
        string stableId,
        string displayName) => string.IsNullOrEmpty(stableId)
        ? default
        : new KoreanNameSnapshot(
            displayName,
            "runtime-character-current-v1:" + stableId,
            KoreanPronunciationHint.AutoHangulDisplay(
                "runtime-character-pronunciation-v1:" + stableId),
            "ko-KR");
}
