using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;

internal sealed class CharacterCorpseConsumptionTransaction
{
    private readonly CharacterBreakdownWorld world;
    private readonly CharacterDeprivationStateStore stateStore;
    private readonly CharacterDeprivationConsequences consequences;
    private readonly IGameCalendar calendar;

    internal CharacterCorpseConsumptionTransaction(CharacterBreakdownWorld world,
        CharacterDeprivationStateStore stateStore, CharacterDeprivationConsequences consequences,
        IGameCalendar calendar)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        this.consequences = consequences ?? throw new ArgumentNullException(nameof(consequences));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
    }

    internal void ApplyCannibalismConsequences(CharacterActor actor, WorldItemStackSnapshot consumed)
    {
        ResolveCannibalismReaction(actor, consumed,
            out bool sameSpecies, out float mood, out string reaction);
        actor.ApplyMoodFactor(
            sameSpecies ? "survival:same-species-cannibalism" : "survival:cannibalism",
            reaction, mood, 900f, 1);
        actor.ChangesStat(CharacterCondition.HYGIENE, -20f);
        consequences.AddInfection(actor, sameSpecies ? 20f : 12f);
        string victim = string.IsNullOrWhiteSpace(consumed.SourceDisplayName) ? "이름 모를 사체" : consumed.SourceDisplayName;
        consequences.RecordTaboo(actor, $"극한의 굶주림 속에서 {victim}을 먹었다");
        consequences.ApplyWitnessMood(actor, actor.GetNowXY(), "금기의 포식을 목격함",
            sameSpecies ? -12f : -8f, permanentMemory: true);
    }

    internal void RecoverCorpsePublications() => world.RecoverCorpsePublications(stateStore);

    internal bool TryConsumeHumanoidCorpse(
        CharacterActor actor, WorldItemStackSnapshot selected, float recovery, int standDistance,
        string operationId, string reasonCode)
    {
        if (!world.TryGetStack(selected.StackId, out WorldItemStackSnapshot source))
            return false; // The exact consumed corpse cannot be replayed as another food/victim.
        if (actor?.Stats == null
            || source.ItemId != DarkSurvivalItemDefinitions.HumanoidCorpseItemId
            || source.SourceCharacterId != selected.SourceCharacterId
            || source.SourceDisplayName != selected.SourceDisplayName
            || source.SourceSpeciesTag != selected.SourceSpeciesTag
            || source.SourceDeathReason != selected.SourceDeathReason
            || source.Position != selected.Position
            || Manhattan(actor.GetNowXY(), source.Position) > standDistance
            || source.TotalQuantity != 1 || source.AvailableQuantity != 1
            || !DomainFactValueSyntax.IsIdentity(source.SourceCharacterId)
            || string.IsNullOrWhiteSpace(source.SourceDisplayName))
            throw new InvalidOperationException("corpse-consumption-exact-source-invalid");

        CharacterId actorId = CharacterPersistentIdentity.Require(actor);
        ResolveCannibalismReaction(actor, source,
            out bool sameSpecies, out float mood, out string reaction);
        string moodId = sameSpecies ? "survival:same-species-cannibalism" : "survival:cannibalism";
        string memory = $"극한의 굶주림 속에서 {source.SourceDisplayName}을 먹었다";
        int day = Math.Max(1, calendar.Day);
        CharacterMoodDeliveryTransactionSnapshot statsBefore =
            actor.Stats.CaptureMoodDeliveryTransactionState();
        CharacterDeprivationAggregateState deprivationBefore = stateStore.CaptureTransactionState();
        float hungerBefore = GetNeed(actor, CharacterCondition.HUNGER);
        float hygieneBefore = GetNeed(actor, CharacterCondition.HYGIENE);
        float moodBefore = actor.Stats.Mood;
        CharacterDeprivationState before = stateStore.Ensure(actor);
        float infectionBefore = before.infectionBurden;
        int memoriesBefore = before.tabooMemories?.Count ?? 0;
        bool committed = false;
        bool moodApplied = false;
        float resolvedMood = 0f;
        PhysicalItemBatchDispositionReceipt physicalReceipt = default;
        string failure = string.Empty;
        try
        {
            actor.Stats.RecoverEmergencyNeedBeforeOutcomeCommit(CharacterCondition.HUNGER, recovery);
            resolvedMood = actor.ApplyMoodFactorBeforeOutcomeCommit(
                moodId, reaction, mood, 900f, 1, out moodApplied);
            actor.Stats.ChangeConditionBeforeOutcomeCommit(CharacterCondition.HYGIENE, -20f);
            consequences.AddInfection(actor, sameSpecies ? 20f : 12f);
            consequences.RecordTabooBeforeOutcomeCommit(actor, memory);
            CharacterDeprivationState after = stateStore.Ensure(actor);
            float hungerAfter = GetNeed(actor, CharacterCondition.HUNGER);
            float hygieneAfter = GetNeed(actor, CharacterCondition.HYGIENE);
            float moodAfter = actor.Stats.Mood;
            float infectionAfter = after.infectionBurden;
            int memoriesAfter = after.tabooMemories?.Count ?? 0;
            var experience = new CharacterCorpseConsumptionExperienceData
            {
                characterId = actorId.Value, characterName = actor.Identity.DisplayName,
                victimId = source.SourceCharacterId, victimName = source.SourceDisplayName,
                victimSpecies = source.SourceSpeciesTag, deathReason = source.SourceDeathReason,
                memory = memory, sameSpecies = sameSpecies, absoluteDay = day,
                hungerBefore = hungerBefore, hungerAfter = hungerAfter,
                hygieneBefore = hygieneBefore, hygieneAfter = hygieneAfter,
                infectionBefore = infectionBefore, infectionAfter = infectionAfter,
                moodBefore = moodBefore, moodAfter = moodAfter,
                memoriesBefore = memoriesBefore, memoriesAfter = memoriesAfter
            };
            committed = world.TryConsumeCorpse(source, operationId, reasonCode,
                experience, stateStore, out physicalReceipt, out failure);
            if (!committed)
                throw new InvalidOperationException("corpse-consumption-commit-rejected:" + failure);
        }
        finally
        {
            if (!committed)
            {
                // The item authority rolls back only before its canonical commit.
                // Never restore all items after a durable physical outcome exists.
                stateStore.RestoreTransactionState(deprivationBefore);
                actor.Stats.RestoreMoodDeliveryTransactionState(statsBefore, publish: false);
            }
        }

        var failures = new List<Exception>();
        void Publish(Action callback)
        {
            try { callback(); }
            catch (Exception exception) when (exception is not OutOfMemoryException
                && exception is not StackOverflowException && exception is not AccessViolationException)
            { failures.Add(exception); }
        }
        Publish(() => consequences.EndActiveBreakdownIfRelieved(actor));
        if (!world.AcknowledgeCorpseConsumption(physicalReceipt, stateStore, out string ackFailure))
            failures.Add(new InvalidOperationException("corpse-consumption-ack-pending:" + ackFailure));
        if (failure.Length > 0)
            failures.Add(new InvalidOperationException(failure));
        if (moodApplied)
            Publish(() => actor.Stats.PublishCommittedMoodFactor(moodId, resolvedMood, day));
        Publish(() => actor.Stats.PublishCommittedConditionChange(CharacterCondition.HUNGER,
            hungerBefore, GetNeed(actor, CharacterCondition.HUNGER), day));
        Publish(() => actor.Stats.PublishCommittedConditionChange(CharacterCondition.HYGIENE,
            hygieneBefore, GetNeed(actor, CharacterCondition.HYGIENE), day));
        Publish(() => consequences.PublishCommittedTaboo(actor, memory, source.SourceCharacterId));
        Publish(() => consequences.ApplyWitnessMood(actor, actor.GetNowXY(),
            "금기의 포식을 목격함", sameSpecies ? -12f : -8f, permanentMemory: true));
        if (failures.Count > 0)
            throw new AggregateException("Corpse consumption committed; publication or acknowledgement failed.", failures);
        return true;
    }

    private static void ResolveCannibalismReaction(CharacterActor actor,
        WorldItemStackSnapshot consumed, out bool sameSpecies, out float mood, out string reaction)
    {
        sameSpecies = !string.IsNullOrWhiteSpace(consumed.SourceSpeciesTag)
            && string.Equals(actor.Identity?.SpeciesTag, consumed.SourceSpeciesTag, StringComparison.OrdinalIgnoreCase);
        CharacterAiPersonality personality = GetPersonality(actor);
        float conscience01 = personality != null
            ? (Mathf.InverseLerp(0.25f, 2f, personality.selfCare)
                + Mathf.InverseLerp(0.25f, 2f, personality.orderliness)
                + Mathf.InverseLerp(0.25f, 2f, personality.routineAdherence)) / 3f
            : 0.5f;
        float appetite01 = personality != null
            ? (Mathf.InverseLerp(0.25f, 2f, personality.riskTaking)
                + Mathf.InverseLerp(0.25f, 2f, personality.noveltySeeking)) * 0.5f
            : 0.5f;
        mood = (sameSpecies ? -18f : -11f) * Mathf.Lerp(0.45f, 1.25f, conscience01);
        reaction = appetite01 > 0.72f && conscience01 < 0.45f
            ? "금기의 맛을 다시 떠올림"
            : conscience01 < 0.35f
                ? "금기에 무감각해짐"
                : sameSpecies ? "동족을 먹었다" : "인간형 사체를 먹었다";
    }

    private static float GetNeed(
        CharacterActor actor,
        CharacterCondition condition)
    {
        return actor != null
            && actor.Stats != null
            && actor.Stats.Stats.TryGetValue(condition, out float value)
                ? Mathf.Clamp(value, 0f, 100f)
                : 100f;
    }

    private static CharacterAiPersonality GetPersonality(CharacterActor actor)
    {
        return actor != null && actor.Identity != null && actor.Identity.Data != null
            ? actor.Identity.Data.aiPersonality
            : null;
    }

    private static int Manhattan(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

}
