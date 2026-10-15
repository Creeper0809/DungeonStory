using System;
using System.Collections.Generic;
using DungeonStory.Foundation.Facts;
using UnityEngine;

internal sealed class CharacterDeprivationConsequences
{
    private readonly CharacterDeprivationStateStore stateStore;
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly IGameCalendar calendar;
    private readonly IDomainFactPublisher notifications;

    public CharacterDeprivationConsequences(
        CharacterDeprivationStateStore stateStore,
        ICharacterAiWorldRegistry worldRegistry,
        IGameCalendar calendar,
        IDomainFactPublisher notifications)
    {
        this.stateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
        this.worldRegistry = worldRegistry
            ?? throw new ArgumentNullException(nameof(worldRegistry));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
    }

    public void RecordTaboo(CharacterActor actor, string memory)
    {
        RecordTabooBeforeOutcomeCommit(actor, memory);
        PublishCommittedTaboo(actor, memory, string.Empty);
    }

    internal void RecordTabooBeforeOutcomeCommit(CharacterActor actor, string memory)
    {
        if (actor == null || string.IsNullOrWhiteSpace(memory))
        {
            return;
        }

        if (calendar.Day < 0)
            throw new ArgumentOutOfRangeException("absoluteDay");
        CharacterDeprivationState state = stateStore.Ensure(actor);
        state.tabooMemories ??= new List<string>();
        string normalized = memory.Trim();
        if (!state.tabooMemories.Contains(normalized))
        {
            state.tabooMemories.Add(normalized);
            while (state.tabooMemories.Count > 24)
            {
                state.tabooMemories.RemoveAt(0);
            }
        }

    }

    internal void PublishCommittedTaboo(
        CharacterActor actor, string memory, string subjectId)
    {
        if (actor == null || string.IsNullOrWhiteSpace(memory))
            return;
        int absoluteDay = calendar.Day;
        if (absoluteDay < 0)
            throw new ArgumentOutOfRangeException(nameof(absoluteDay));
        NotifyExperience(new CharacterDeprivationExperienceFact(
            CharacterDeprivationExperienceKind.PersonalTaboo, actor,
            subjectId ?? string.Empty, memory.Trim(), 1f, absoluteDay));
    }

    public void ApplyWitnessMood(
        CharacterActor source,
        Vector2Int position,
        string label,
        float mood,
        bool permanentMemory = false)
    {
        int absoluteDay = calendar.Day;
        if (absoluteDay < 0)
            throw new ArgumentOutOfRangeException(nameof(absoluteDay));
        IReadOnlyList<CharacterActor> characters = worldRegistry.Characters;
        for (int index = 0; index < characters.Count; index++)
        {
            CharacterActor witness = characters[index];
            if (!IsEligibleHumanoid(witness)
                || witness == source
                || witness.IsDead
                || Manhattan(witness.GetNowXY(), position) > 4)
            {
                continue;
            }

            string sourceId = RequirePersistentId(source);
            witness.ApplyMoodFactor(
                $"survival:witness:{sourceId}",
                label,
                mood,
                360f,
                1,
                absoluteDay);
            NotifyExperience(new CharacterDeprivationExperienceFact(
                CharacterDeprivationExperienceKind.WitnessedTaboo, witness,
                sourceId, label, mood, absoluteDay));
            if (permanentMemory)
            {
                witness.SocialMemory?.RememberCharacterExperience(
                    source,
                    Mathf.Clamp(mood / 12f, -1f, 1f),
                    label,
                    durationSeconds: 0f);
            }
        }
    }

    private void NotifyExperience(in CharacterDeprivationExperienceFact fact)
    {
        DomainFactLease lease = default;
        try
        {
            if (!notifications.TryReserve(fact, out lease, out string failure))
                throw new InvalidOperationException("deprivation-experience-observer-unavailable:" + failure);
            DomainFactDelivery delivery = notifications.Publish(lease, fact);
            if (!delivery.Completed)
                throw new InvalidOperationException("deprivation-experience-publication-failed:" + delivery.Detail);
        }
        finally { if (lease.IsValid) notifications.Cancel(lease); }
    }

    public void AddInfection(CharacterActor actor, float amount)
    {
        if (actor == null || amount <= 0f)
        {
            return;
        }

        CharacterDeprivationState state = stateStore.Ensure(actor);
        state.infectionBurden = Mathf.Clamp(
            state.infectionBurden + amount,
            0f,
            100f);
        DeprivationBurdenSaveData contamination =
            CharacterDeprivationStateStore.GetBurden(
                state,
                DeprivationKind.Contamination);
        contamination.burden = Mathf.Clamp(
            contamination.burden + amount * 0.5f,
            0f,
            100f);
    }

    public void EndBreakdown(
        CharacterActor actor,
        CharacterDeprivationState state,
        string reason,
        float reduceCauseTo)
    {
        if (state?.breakdown == null)
        {
            return;
        }

        DeprivationBurdenSaveData cause =
            CharacterDeprivationStateStore.GetBurden(
                state,
                state.breakdown.cause);
        cause.burden = Mathf.Min(cause.burden, reduceCauseTo);
        state.breakdown.active = false;
        state.breakdown.targetId = string.Empty;
        state.breakdown.lastReplanReason = reason ?? string.Empty;
        actor?.Stats?.RemoveMoodFactor("survival:breakdown");
        actor?.Brain?.EndExternallyDrivenAction(
            CharacterBreakdownActionRunner.IntentOwnerId,
            clearFailures: true);
    }

    public void EndActiveBreakdownIfRelieved(CharacterActor actor)
    {
        if (actor == null
            || !stateStore.TryGetWritable(actor, out CharacterDeprivationState state)
            || state.breakdown == null
            || !state.breakdown.active
            || !IsCauseRelieved(actor, state.breakdown.cause))
        {
            return;
        }

        EndBreakdown(actor, state, "욕구가 충족됨", reduceCauseTo: 45f);
    }

    private static bool IsCauseRelieved(CharacterActor actor, DeprivationKind kind)
    {
        float value = kind switch
        {
            DeprivationKind.Hunger => GetNeed(actor, CharacterCondition.HUNGER),
            DeprivationKind.Thirst => GetNeed(actor, CharacterCondition.THIRST),
            DeprivationKind.Bladder => GetNeed(actor, CharacterCondition.EXCRETION),
            DeprivationKind.Contamination => GetNeed(actor, CharacterCondition.HYGIENE),
            DeprivationKind.Exhaustion => GetNeed(actor, CharacterCondition.SLEEP),
            _ => actor?.Stats?.Mood ?? 50f
        };
        return value >= 30f;
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

    private static bool IsEligibleHumanoid(CharacterActor actor)
    {
        return actor != null
            && !actor.IsDead
            && actor.CurrentLifecycleState != CharacterLifecycleState.Despawned
            && actor.CurrentLifecycleState != CharacterLifecycleState.OnExpedition;
    }

    private static string RequirePersistentId(CharacterActor actor)
    {
        return CharacterPersistentIdentity.Require(actor).Value;
    }

    private static int Manhattan(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }
}
