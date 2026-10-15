using System;
using DungeonStory.Infrastructure;
using DungeonStory.Narrative.Korean;

public static class EmergencyWorkSuspensionFactMapping
{
    public static EmergencyWorkSuspensionOutcomeReceipt Capture(
        EmergencyWorkSuspensionFact fact)
    {
        EmergencyWorkSuspensionFactSaveData value =
            (fact ?? throw new ArgumentNullException(nameof(fact)))
            .Snapshot();
        return new EmergencyWorkSuspensionOutcomeReceipt(
            value.characterId,
            Name(value.workerDisplayName, value.characterId, character: true),
            new WorkTypeId(value.workTypeId),
            value.workDisplayName,
            value.targetBuildingId,
            Name(
                value.targetDisplayName,
                value.targetBuildingId,
                character: false),
            value.alertEpochId,
            value.suspendedAtAbsoluteHour,
            value.progressExternallyPersisted,
            value.inlineCompletedWork,
            value.inlineRequiredWork,
            value.ownerRevision,
            value.absoluteDay);
    }

    public static GameplayDomainFactObserverBinding<
        EmergencyWorkSuspensionFact,
        EmergencyWorkSuspensionOutcomeReceipt> CreateBinding(
        IGameplayOutcomeRegistry registry,
        GameplayOutcomeLedger ledger,
        IGameplayOutcomeRecorder recorder,
        GameplayDomainFactCommitGroup commits) => new(
            "log:work.emergency-suspension",
            Capture,
            registry,
            ledger,
            recorder,
            commits);

    private static KoreanNameSnapshot Name(
        string displayName,
        string stableId,
        bool character)
    {
        string kind = character ? "character" : "facility";
        return new KoreanNameSnapshot(
            displayName,
            "runtime-" + kind + "-current-v1:" + stableId,
            KoreanPronunciationHint.AutoHangulDisplay(
                "runtime-" + kind + "-pronunciation-v1:" + stableId),
            "ko-KR");
    }
}
