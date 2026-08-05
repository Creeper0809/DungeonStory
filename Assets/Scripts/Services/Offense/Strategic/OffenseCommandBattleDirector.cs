using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal sealed class OffenseBattleDirectorRestoreCandidate
{
    internal OffenseBattleDirectorRestoreCandidate(
        OffenseBattleDirectorStateData state,
        Dictionary<string, OffenseFormationPosition> formations)
    {
        State = state;
        Formations = formations;
    }

    internal OffenseBattleDirectorStateData State { get; }
    internal Dictionary<string, OffenseFormationPosition> Formations { get; }
}

public sealed class OffenseBattleMemberDeckSeed
{
    public string characterId;
    public OffenseFormationPosition formation;
    public List<OffenseCommandCardStateData> cards =
        new List<OffenseCommandCardStateData>();
}

public readonly struct OffenseClashStageResult
{
    public OffenseClashStageResult(
        int allyStagesRemaining,
        int enemyStagesRemaining)
    {
        AllyStagesRemaining = Mathf.Max(0, allyStagesRemaining);
        EnemyStagesRemaining = Mathf.Max(0, enemyStagesRemaining);
    }

    public int AllyStagesRemaining { get; }
    public int EnemyStagesRemaining { get; }
    public bool AllyExecuted => AllyStagesRemaining > 0;
    public bool EnemyExecuted => EnemyStagesRemaining > 0;
}

public sealed class OffenseCommandExecutionRequest
{
    public string battleId;
    public string actorId;
    public string targetCombatantId;
    public string sourceSkillId;
    public int survivingExecutionStages;
    public float chainMultiplier;
}

public readonly struct OffenseCommandExecutionResult
{
    public OffenseCommandExecutionResult(
        OffenseCommandOutcome outcome,
        bool appliedAtLeastOneEffect,
        string finalTargetId)
    {
        Outcome = outcome;
        AppliedAtLeastOneEffect = appliedAtLeastOneEffect;
        FinalTargetId = finalTargetId ?? string.Empty;
    }

    public OffenseCommandOutcome Outcome { get; }
    public bool AppliedAtLeastOneEffect { get; }
    public string FinalTargetId { get; }
}

public interface IOffenseCommandResolutionAdapter
{
    OffenseCommandExecutionResult Execute(OffenseCommandExecutionRequest request);
    void FinalizeTurn();
}

public sealed class OffenseResolvedCommand
{
    public int order;
    public string characterId;
    public string cardInstanceId;
    public string targetIntentId;
    public OffenseClashStageResult clash;
    public OffenseChainResolution chain;
    public OffenseCommandExecutionResult execution;
}

public interface IOffenseBattleDirector
{
    OffenseBattleDirectorStateData State { get; }
    event Action Changed;
    bool TryStartBattle(
        string battleId,
        IEnumerable<OffenseBattleMemberDeckSeed> members,
        IEnumerable<OffenseEnemyIntentStateData> enemyIntents,
        int deterministicSeed,
        out string reason);
    bool TryDrawTurn(out string reason);
    bool TryReplaceEnemyIntents(
        IEnumerable<OffenseEnemyIntentStateData> enemyIntents,
        out string reason);
    bool TryCommitCommand(
        string characterId,
        string cardInstanceId,
        string targetIntentId,
        string targetCombatantId,
        out string reason);
    bool TryRemoveCommittedCommand(string characterId);
    IReadOnlyList<OffenseResolvedCommand> ResolveTurn();
    bool TryGainResolve(string characterId, float amount);
    bool TryConsumeUltimate(string characterId);
    OffenseBattleDirectorStateData Capture();
    void Clear();
}

public sealed class OffenseBattleDirector : IOffenseBattleDirector
{
    private const int DeckCardCount = 8;
    private const int MaximumPartySize = 5;
    private readonly IOffenseCommandResolutionAdapter resolutionAdapter;
    private Dictionary<string, OffenseFormationPosition> formations =
        new Dictionary<string, OffenseFormationPosition>(StringComparer.Ordinal);

    public OffenseBattleDirector(IOffenseCommandResolutionAdapter resolutionAdapter)
    {
        this.resolutionAdapter = resolutionAdapter
            ?? throw new ArgumentNullException(nameof(resolutionAdapter));
    }

    public OffenseBattleDirectorStateData State { get; private set; }
    public event Action Changed;

    public bool TryStartBattle(
        string battleId,
        IEnumerable<OffenseBattleMemberDeckSeed> members,
        IEnumerable<OffenseEnemyIntentStateData> enemyIntents,
        int deterministicSeed,
        out string reason)
    {
        if (State != null)
        {
            reason = "이미 진행 중인 오펜스 전투가 있습니다.";
            return false;
        }

        List<OffenseBattleMemberDeckSeed> party = (members
                ?? Array.Empty<OffenseBattleMemberDeckSeed>())
            .Where(member => member != null)
            .ToList();
        if (party.Count < 1 || party.Count > MaximumPartySize)
        {
            reason = "원정 전투 인원은 1명에서 5명이어야 합니다.";
            return false;
        }

        if (party.Any(member =>
                string.IsNullOrWhiteSpace(member.characterId)
                || member.cards == null
                || member.cards.Count != DeckCardCount
                || member.cards.Any(card =>
                    card == null
                    || string.IsNullOrWhiteSpace(card.instanceId)
                    || card.executionStages < 1
                    || card.executionStages > 3))
            || party.Select(member => member.characterId).Distinct().Count()
                != party.Count
            || party.Select(member => member.formation).Distinct().Count()
                != party.Count)
        {
            reason = "원정대 명령 덱 또는 진형 구성이 올바르지 않습니다.";
            return false;
        }

        List<OffenseEnemyIntentStateData> intents = (enemyIntents
                ?? Array.Empty<OffenseEnemyIntentStateData>())
            .Where(intent => intent != null
                && !string.IsNullOrWhiteSpace(intent.intentId)
                && !string.IsNullOrWhiteSpace(intent.enemyId)
                && intent.executionStages is >= 1 and <= 3)
            .Select(CloneIntent)
            .ToList();
        if (intents.Select(intent => intent.intentId).Distinct().Count()
            != intents.Count)
        {
            reason = "적 의도 식별자가 중복되었습니다.";
            return false;
        }

        formations.Clear();
        State = new OffenseBattleDirectorStateData
        {
            battleId = string.IsNullOrWhiteSpace(battleId)
                ? Guid.NewGuid().ToString("N")
                : battleId,
            turn = 0,
            rngState = unchecked((uint)(deterministicSeed == 0 ? 1 : deterministicSeed)),
            enemyIntents = intents
        };

        foreach (OffenseBattleMemberDeckSeed member in party)
        {
            formations.Add(member.characterId, member.formation);
            OffenseCommandDeckStateData deck = new OffenseCommandDeckStateData
            {
                characterId = member.characterId,
                drawPile = member.cards.Select(CloneCard).ToList()
            };
            Shuffle(deck);
            State.decks.Add(deck);
        }

        State.decks.Sort((left, right) =>
            formations[left.characterId].CompareTo(formations[right.characterId]));
        reason = string.Empty;
        Changed?.Invoke();
        return true;
    }

    public bool TryDrawTurn(out string reason)
    {
        if (State == null)
        {
            reason = "진행 중인 오펜스 전투가 없습니다.";
            return false;
        }

        if (State.commandQueue.Count > 0)
        {
            reason = "현재 명령열을 먼저 해결해야 합니다.";
            return false;
        }

        State.turn++;
        foreach (OffenseCommandDeckStateData deck in State.decks)
        {
            DiscardUnchosenExpiredCandidates(deck);
            deck.candidates.Clear();

            if (!string.IsNullOrWhiteSpace(deck.heldCardInstanceId))
            {
                OffenseCommandCardStateData held = RemoveByInstanceId(
                    deck.drawPile,
                    deck.heldCardInstanceId)
                    ?? RemoveByInstanceId(deck.discardPile, deck.heldCardInstanceId);
                if (held != null)
                {
                    held.heldFromPreviousTurn = true;
                    deck.candidates.Add(held);
                }

                deck.heldCardInstanceId = string.Empty;
            }

            while (deck.candidates.Count < 2)
            {
                if (deck.drawPile.Count == 0)
                {
                    if (deck.discardPile.Count == 0)
                    {
                        break;
                    }

                    deck.drawPile.AddRange(deck.discardPile);
                    deck.discardPile.Clear();
                    Shuffle(deck);
                }

                OffenseCommandCardStateData card = deck.drawPile[0];
                deck.drawPile.RemoveAt(0);
                card.heldFromPreviousTurn = false;
                deck.candidates.Add(card);
            }
        }

        reason = string.Empty;
        Changed?.Invoke();
        return true;
    }

    public bool TryReplaceEnemyIntents(
        IEnumerable<OffenseEnemyIntentStateData> enemyIntents,
        out string reason)
    {
        if (State == null)
        {
            reason = "진행 중인 오펜스 전투가 없습니다.";
            return false;
        }

        if (State.commandQueue.Count > 0)
        {
            reason = "현재 명령열을 먼저 해결해야 합니다.";
            return false;
        }

        List<OffenseEnemyIntentStateData> intents = (enemyIntents
                ?? Array.Empty<OffenseEnemyIntentStateData>())
            .Where(intent => intent != null
                && !string.IsNullOrWhiteSpace(intent.intentId)
                && !string.IsNullOrWhiteSpace(intent.enemyId)
                && intent.executionStages is >= 1 and <= 3)
            .Select(CloneIntent)
            .ToList();
        if (intents.Select(intent => intent.intentId).Distinct().Count()
            != intents.Count)
        {
            reason = "적 의도 ID가 중복되었습니다.";
            return false;
        }

        State.enemyIntents = intents;
        reason = string.Empty;
        Changed?.Invoke();
        return true;
    }

    public bool TryCommitCommand(
        string characterId,
        string cardInstanceId,
        string targetIntentId,
        string targetCombatantId,
        out string reason)
    {
        if (State == null)
        {
            reason = "진행 중인 오펜스 전투가 없습니다.";
            return false;
        }

        OffenseCommandDeckStateData deck = FindDeck(characterId);
        OffenseCommandCardStateData selected = deck?.candidates.FirstOrDefault(
            card => card != null && card.instanceId == cardInstanceId);
        if (selected == null)
        {
            reason = "선택한 명령 카드를 찾을 수 없습니다.";
            return false;
        }

        if (State.commandQueue.Any(entry => entry.characterId == characterId))
        {
            reason = "이 캐릭터의 명령은 이미 확정되었습니다.";
            return false;
        }

        OffenseEnemyIntentStateData intent = string.IsNullOrWhiteSpace(targetIntentId)
            ? null
            : State.enemyIntents.FirstOrDefault(candidate =>
                candidate.intentId == targetIntentId);
        if (!string.IsNullOrWhiteSpace(targetIntentId) && intent == null)
        {
            reason = "연결할 적 의도를 찾을 수 없습니다.";
            return false;
        }

        State.commandQueue.Add(new OffenseCommandQueueEntryData
        {
            order = State.commandQueue.Count + 1,
            characterId = characterId,
            cardInstanceId = cardInstanceId,
            targetIntentId = targetIntentId ?? string.Empty,
            targetCombatantId = !string.IsNullOrWhiteSpace(targetCombatantId)
                ? targetCombatantId
                : intent?.enemyId ?? string.Empty,
            chainState = OffenseChainState.Full,
            inheritedChainMultiplier = 1f
        });
        reason = string.Empty;
        Changed?.Invoke();
        return true;
    }

    public bool TryRemoveCommittedCommand(string characterId)
    {
        if (State == null)
        {
            return false;
        }

        int removed = State.commandQueue.RemoveAll(entry =>
            entry != null && entry.characterId == characterId);
        if (removed == 0)
        {
            return false;
        }

        for (int index = 0; index < State.commandQueue.Count; index++)
        {
            State.commandQueue[index].order = index + 1;
        }

        Changed?.Invoke();
        return true;
    }

    public IReadOnlyList<OffenseResolvedCommand> ResolveTurn()
    {
        if (State == null)
        {
            return Array.Empty<OffenseResolvedCommand>();
        }

        List<OffenseResolvedCommand> resolved = new List<OffenseResolvedCommand>();
        OffenseChainResolution chain = new OffenseChainResolution(
            OffenseChainState.Full,
            1f,
            OffenseTacticalTag.None,
            0);
        HashSet<string> resolvedIntentIds =
            new HashSet<string>(StringComparer.Ordinal);

        foreach (OffenseCommandQueueEntryData entry in State.commandQueue
                     .OrderBy(candidate => candidate.order))
        {
            OffenseCommandDeckStateData deck = FindDeck(entry.characterId);
            OffenseCommandCardStateData card = deck?.candidates.FirstOrDefault(
                candidate => candidate.instanceId == entry.cardInstanceId);
            if (card == null)
            {
                chain = OffenseTacticalChainRules.Advance(
                    chain,
                    OffenseTacticalTag.None,
                    OffenseCommandOutcome.Unavailable,
                    appliedAtLeastOneEffect: false);
                resolved.Add(new OffenseResolvedCommand
                {
                    order = entry.order,
                    characterId = entry.characterId,
                    cardInstanceId = entry.cardInstanceId,
                    targetIntentId = entry.targetIntentId,
                    clash = new OffenseClashStageResult(0, 0),
                    chain = chain,
                    execution = new OffenseCommandExecutionResult(
                        OffenseCommandOutcome.Unavailable,
                        false,
                        string.Empty)
                });
                continue;
            }

            OffenseEnemyIntentStateData intent = State.enemyIntents
                .FirstOrDefault(candidate =>
                    candidate.intentId == entry.targetIntentId);
            bool firstInterception = intent != null
                && resolvedIntentIds.Add(intent.intentId);
            OffenseClashStageResult clash = ResolveClash(
                card,
                firstInterception ? intent : null);
            OffenseCommandExecutionResult execution;
            if (clash.AllyStagesRemaining <= 0)
            {
                execution = new OffenseCommandExecutionResult(
                    OffenseCommandOutcome.ClashLost,
                    false,
                    entry.targetCombatantId);
            }
            else
            {
                execution = resolutionAdapter.Execute(
                    new OffenseCommandExecutionRequest
                    {
                        battleId = State.battleId,
                        actorId = entry.characterId,
                        targetCombatantId = entry.targetCombatantId,
                        sourceSkillId = card.sourceSkillId,
                        survivingExecutionStages = clash.AllyStagesRemaining,
                        chainMultiplier = chain.Multiplier
                    });
            }

            if (firstInterception && clash.EnemyStagesRemaining > 0)
            {
                resolutionAdapter.Execute(
                    new OffenseCommandExecutionRequest
                    {
                        battleId = State.battleId,
                        actorId = intent.enemyId,
                        targetCombatantId = intent.targetCharacterId,
                        sourceSkillId = string.Empty,
                        survivingExecutionStages = clash.EnemyStagesRemaining,
                        chainMultiplier = 1f
                    });
            }

            chain = OffenseTacticalChainRules.Advance(
                chain,
                card.tacticalTag,
                execution.Outcome,
                execution.AppliedAtLeastOneEffect);
            entry.chainState = chain.State;
            entry.inheritedChainMultiplier = chain.Multiplier;
            resolved.Add(new OffenseResolvedCommand
            {
                order = entry.order,
                characterId = entry.characterId,
                cardInstanceId = entry.cardInstanceId,
                targetIntentId = entry.targetIntentId,
                clash = clash,
                chain = chain,
                execution = execution
            });
        }

        foreach (OffenseEnemyIntentStateData intent in State.enemyIntents
                     .Where(intent => intent != null
                         && !resolvedIntentIds.Contains(intent.intentId)))
        {
            resolutionAdapter.Execute(
                new OffenseCommandExecutionRequest
                {
                    battleId = State.battleId,
                    actorId = intent.enemyId,
                    targetCombatantId = intent.targetCharacterId,
                    sourceSkillId = string.Empty,
                    survivingExecutionStages = intent.executionStages,
                    chainMultiplier = 1f
                });
        }

        FinishTurnCards();
        State.commandQueue.Clear();
        resolutionAdapter.FinalizeTurn();
        Changed?.Invoke();
        return resolved;
    }

    public bool TryGainResolve(string characterId, float amount)
    {
        OffenseCommandDeckStateData deck = FindDeck(characterId);
        if (deck == null || deck.ultimateUsed)
        {
            return false;
        }

        deck.resolve = Mathf.Clamp(deck.resolve + Mathf.Max(0f, amount), 0f, 100f);
        Changed?.Invoke();
        return true;
    }

    public bool TryConsumeUltimate(string characterId)
    {
        OffenseCommandDeckStateData deck = FindDeck(characterId);
        if (deck == null || deck.ultimateUsed || deck.resolve < 100f)
        {
            return false;
        }

        deck.resolve = 0f;
        deck.ultimateUsed = true;
        Changed?.Invoke();
        return true;
    }

    public OffenseBattleDirectorStateData Capture()
    {
        return State != null ? CloneState(State) : null;
    }

    internal OffenseBattleDirectorRestoreCandidate PreparePersistentState(
        OffenseBattleDirectorStateData state)
    {
        if (state == null)
        {
            return new OffenseBattleDirectorRestoreCandidate(
                state: null,
                new Dictionary<string, OffenseFormationPosition>(
                    StringComparer.Ordinal));
        }

        if (string.IsNullOrWhiteSpace(state.battleId)
            || state.decks == null
            || state.decks.Count is < 1 or > MaximumPartySize)
        {
            throw new InvalidOperationException(
                "Invalid Strategic offense battle state.");
        }

        return new OffenseBattleDirectorRestoreCandidate(
            CloneState(state),
            new Dictionary<string, OffenseFormationPosition>(
                StringComparer.Ordinal));
    }

    internal void PublishPersistentState(
        OffenseBattleDirectorRestoreCandidate candidate)
    {
        candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
        State = candidate.State;
        formations = candidate.Formations;
    }

    public void Clear()
    {
        State = null;
        formations.Clear();
        Changed?.Invoke();
    }

    private OffenseClashStageResult ResolveClash(
        OffenseCommandCardStateData card,
        OffenseEnemyIntentStateData intent)
    {
        if (intent == null)
        {
            return new OffenseClashStageResult(card.executionStages, 0);
        }

        int allyRemaining = card.executionStages;
        int enemyRemaining = intent.executionStages;
        int rounds = Mathf.Min(allyRemaining, enemyRemaining);
        for (int index = 0; index < rounds; index++)
        {
            int allyScore = card.power + card.speed + NextRoll(1, 7);
            int enemyScore = intent.threat + intent.speed + NextRoll(1, 7);
            if (allyScore >= enemyScore)
            {
                enemyRemaining--;
            }
            else
            {
                allyRemaining--;
            }
        }

        if (card.speed < intent.speed && enemyRemaining > 0)
        {
            allyRemaining = Mathf.Max(0, allyRemaining - 1);
        }

        return new OffenseClashStageResult(allyRemaining, enemyRemaining);
    }

    private void FinishTurnCards()
    {
        foreach (OffenseCommandDeckStateData deck in State.decks)
        {
            string selectedId = State.commandQueue
                .FirstOrDefault(entry => entry.characterId == deck.characterId)
                ?.cardInstanceId;
            foreach (OffenseCommandCardStateData candidate in deck.candidates)
            {
                if (candidate == null)
                {
                    continue;
                }

                if (candidate.instanceId == selectedId || candidate.heldFromPreviousTurn)
                {
                    candidate.heldFromPreviousTurn = false;
                    deck.discardPile.Add(candidate);
                }
                else
                {
                    candidate.heldFromPreviousTurn = true;
                    deck.heldCardInstanceId = candidate.instanceId;
                    deck.drawPile.Insert(0, candidate);
                }
            }

            deck.candidates.Clear();
        }
    }

    private static void DiscardUnchosenExpiredCandidates(
        OffenseCommandDeckStateData deck)
    {
        foreach (OffenseCommandCardStateData candidate in deck.candidates)
        {
            if (candidate != null)
            {
                candidate.heldFromPreviousTurn = false;
                deck.discardPile.Add(candidate);
            }
        }
    }

    private void Shuffle(OffenseCommandDeckStateData deck)
    {
        for (int index = deck.drawPile.Count - 1; index > 0; index--)
        {
            int swap = NextRoll(0, index + 1);
            (deck.drawPile[index], deck.drawPile[swap]) =
                (deck.drawPile[swap], deck.drawPile[index]);
        }

        deck.shuffleCount++;
    }

    private int NextRoll(int minimumInclusive, int maximumExclusive)
    {
        ulong value = State.rngState;
        if (value == 0UL)
        {
            value = 0x9E3779B97F4A7C15UL;
        }

        value ^= value << 13;
        value ^= value >> 7;
        value ^= value << 17;
        State.rngState = value;
        uint range = (uint)(maximumExclusive - minimumInclusive);
        return minimumInclusive + (int)(value % range);
    }

    private OffenseCommandDeckStateData FindDeck(string characterId)
    {
        return State?.decks.FirstOrDefault(deck =>
            deck != null && deck.characterId == characterId);
    }

    private static OffenseCommandCardStateData RemoveByInstanceId(
        IList<OffenseCommandCardStateData> source,
        string instanceId)
    {
        for (int index = 0; index < source.Count; index++)
        {
            OffenseCommandCardStateData candidate = source[index];
            if (candidate?.instanceId != instanceId)
            {
                continue;
            }

            source.RemoveAt(index);
            return candidate;
        }

        return null;
    }

    private static OffenseBattleDirectorStateData CloneState(
        OffenseBattleDirectorStateData source)
    {
        return new OffenseBattleDirectorStateData
        {
            battleId = source.battleId,
            turn = source.turn,
            rngState = source.rngState,
            decks = (source.decks ?? new List<OffenseCommandDeckStateData>())
                .Select(CloneDeck)
                .ToList(),
            enemyIntents = (source.enemyIntents
                    ?? new List<OffenseEnemyIntentStateData>())
                .Select(CloneIntent)
                .ToList(),
            commandQueue = (source.commandQueue
                    ?? new List<OffenseCommandQueueEntryData>())
                .Select(entry => new OffenseCommandQueueEntryData
                {
                    order = entry.order,
                    characterId = entry.characterId,
                    cardInstanceId = entry.cardInstanceId,
                    targetIntentId = entry.targetIntentId,
                    targetCombatantId = entry.targetCombatantId,
                    chainState = entry.chainState,
                    inheritedChainMultiplier = entry.inheritedChainMultiplier
                })
                .ToList()
        };
    }

    private static OffenseCommandDeckStateData CloneDeck(
        OffenseCommandDeckStateData source)
    {
        return new OffenseCommandDeckStateData
        {
            characterId = source.characterId,
            drawPile = (source.drawPile ?? new List<OffenseCommandCardStateData>())
                .Select(CloneCard)
                .ToList(),
            discardPile = (source.discardPile
                    ?? new List<OffenseCommandCardStateData>())
                .Select(CloneCard)
                .ToList(),
            candidates = (source.candidates
                    ?? new List<OffenseCommandCardStateData>())
                .Select(CloneCard)
                .ToList(),
            heldCardInstanceId = source.heldCardInstanceId,
            shuffleCount = source.shuffleCount,
            resolve = source.resolve,
            ultimateUsed = source.ultimateUsed
        };
    }

    private static OffenseCommandCardStateData CloneCard(
        OffenseCommandCardStateData source)
    {
        return new OffenseCommandCardStateData
        {
            instanceId = source.instanceId,
            sourceSkillId = source.sourceSkillId,
            displayName = source.displayName,
            tacticalTag = source.tacticalTag,
            damageType = source.damageType,
            executionStages = source.executionStages,
            speed = source.speed,
            power = source.power,
            heldFromPreviousTurn = source.heldFromPreviousTurn
        };
    }

    private static OffenseEnemyIntentStateData CloneIntent(
        OffenseEnemyIntentStateData source)
    {
        return new OffenseEnemyIntentStateData
        {
            intentId = source.intentId,
            enemyId = source.enemyId,
            targetCharacterId = source.targetCharacterId,
            actionId = source.actionId,
            displayName = source.displayName,
            tacticalTag = source.tacticalTag,
            executionStages = source.executionStages,
            speed = source.speed,
            threat = source.threat
        };
    }
}

public static class OffenseTacticalChainRules
{
    public static OffenseChainResolution Advance(
        OffenseChainResolution previous,
        OffenseTacticalTag currentTag,
        OffenseCommandOutcome outcome,
        bool appliedAtLeastOneEffect)
    {
        if (previous.SkippedUnavailableSlots >= 1
            && outcome == OffenseCommandOutcome.Unavailable)
        {
            return new OffenseChainResolution(
                OffenseChainState.Broken,
                0f,
                previous.LastTag,
                2);
        }

        switch (outcome)
        {
            case OffenseCommandOutcome.Executed:
                if (!appliedAtLeastOneEffect)
                {
                    return new OffenseChainResolution(
                        OffenseChainState.Degraded,
                        previous.Multiplier * 0.5f,
                        previous.LastTag,
                        0);
                }

                float transition = IsValidTransition(previous.LastTag, currentTag)
                    ? 1f
                    : previous.LastTag == OffenseTacticalTag.None ? 1f : 0f;
                return transition > 0f
                    ? new OffenseChainResolution(
                        OffenseChainState.Full,
                        previous.Multiplier,
                        currentTag,
                        0)
                    : new OffenseChainResolution(
                        OffenseChainState.Broken,
                        0f,
                        currentTag,
                        0);

            case OffenseCommandOutcome.Retargeted:
                return new OffenseChainResolution(
                    OffenseChainState.Degraded,
                    previous.Multiplier * 0.75f,
                    currentTag,
                    0);

            case OffenseCommandOutcome.ClashLost:
                return new OffenseChainResolution(
                    OffenseChainState.Degraded,
                    previous.Multiplier * 0.5f,
                    previous.LastTag,
                    0);

            case OffenseCommandOutcome.Unavailable:
                return new OffenseChainResolution(
                    OffenseChainState.Residual,
                    previous.Multiplier * 0.25f,
                    previous.LastTag,
                    previous.SkippedUnavailableSlots + 1);

            default:
                return new OffenseChainResolution(
                    OffenseChainState.Broken,
                    0f,
                    previous.LastTag,
                    0);
        }
    }

    public static bool IsValidTransition(
        OffenseTacticalTag previous,
        OffenseTacticalTag next)
    {
        if (previous == OffenseTacticalTag.None)
        {
            return next != OffenseTacticalTag.None;
        }

        return previous switch
        {
            OffenseTacticalTag.Intercept =>
                next is OffenseTacticalTag.Maneuver or OffenseTacticalTag.Support,
            OffenseTacticalTag.Maneuver =>
                next is OffenseTacticalTag.Break or OffenseTacticalTag.Support,
            OffenseTacticalTag.Break =>
                next is OffenseTacticalTag.Execute or OffenseTacticalTag.Support,
            OffenseTacticalTag.Support => next != OffenseTacticalTag.None,
            OffenseTacticalTag.Execute => next == OffenseTacticalTag.Support,
            _ => false
        };
    }
}
