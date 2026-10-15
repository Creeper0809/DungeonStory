using System;
using DungeonStory.Narrative.Korean;

public static class DoorHeldOpenFactMapping
{
    public static PolicyStatusChangeReceipt Capture(DoorHeldOpenFact fact)
    {
        if (fact == null) throw new ArgumentNullException(nameof(fact));
        var pending = fact.Snapshot();
        const string NameRevision = "door-held-open-name-v1";
        return new PolicyStatusChangeReceipt(
            new GameplayEntityId(new GameplayEntityKindId("facility"), pending.doorId),
            new KoreanNameSnapshot("문", NameRevision,
                KoreanPronunciationHint.AutoHangulDisplay(NameRevision), "ko-KR"),
            DoorHeldOpenPendingSaveData.PolicyStatusKey, pending.commandOrigin, pending.operationId,
            pending.beforeHeld, pending.afterHeld, pending.revision, pending.absoluteDay,
            new GameplayLocationReference("dungeon", string.Empty, pending.positionX, pending.positionY));
    }
}
