using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public sealed class MinionSettlementSocialRuntime :
    IInitializable,
    IDisposable
{
    private readonly IGameEventBus events;
    private readonly ICharacterWorldQuery characters;
    private readonly ICharacterSettlementStandingQuery standings;
    private readonly ICaptivityRuntime captivity;
    private readonly IMinionSettlementCommand commands;
    private readonly CharacterMoodPolicyService moods;
    private readonly IRandomStream random;
    private readonly ICombatResolutionService combat;
    private readonly ICharacterBodyHealthQuery bodyHealth;
    private readonly ICharacterBodyHealthCommand bodyHealthCommands;
    private readonly ICharacterPerformanceQuery performance;
    private readonly ICombatEquipmentRuntime equipment;
    private readonly CharacterCombatTransactionFacade combatTransactions;
    private readonly SocialInteractionPublicationRuntime socialOutcomes;
    private IDisposable dayStartedSubscription;

    public MinionSettlementSocialRuntime(
        IGameEventBus events,
        ICharacterWorldQuery characters,
        ICharacterSettlementStandingQuery standings,
        ICaptivityRuntime captivity,
        IMinionSettlementCommand commands,
        CharacterMoodPolicyService moods,
        IRandomStreamProvider randomStreams,
        ICombatResolutionService combat,
        ICharacterBodyHealthQuery bodyHealth,
        ICharacterBodyHealthCommand bodyHealthCommands,
        ICharacterPerformanceQuery performance,
        ICombatEquipmentRuntime equipment,
        CharacterCombatTransactionFacade combatTransactions,
        SocialInteractionPublicationRuntime socialOutcomes)
    {
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.characters = characters
            ?? throw new ArgumentNullException(nameof(characters));
        this.standings = standings
            ?? throw new ArgumentNullException(nameof(standings));
        this.captivity = captivity
            ?? throw new ArgumentNullException(nameof(captivity));
        this.commands = commands
            ?? throw new ArgumentNullException(nameof(commands));
        this.moods = moods ?? throw new ArgumentNullException(nameof(moods));
        random = (randomStreams
                ?? throw new ArgumentNullException(nameof(randomStreams)))
            .Get("captivity.minion-social");
        this.combat = combat ?? throw new ArgumentNullException(nameof(combat));
        this.bodyHealth = bodyHealth
            ?? throw new ArgumentNullException(nameof(bodyHealth));
        this.bodyHealthCommands = bodyHealthCommands
            ?? throw new ArgumentNullException(nameof(bodyHealthCommands));
        this.performance = performance
            ?? throw new ArgumentNullException(nameof(performance));
        this.equipment = equipment
            ?? throw new ArgumentNullException(nameof(equipment));
        this.combatTransactions = combatTransactions
            ?? throw new ArgumentNullException(nameof(combatTransactions));
        this.socialOutcomes = socialOutcomes
            ?? throw new ArgumentNullException(nameof(socialOutcomes));
    }

    public void Initialize()
    {
        dayStartedSubscription ??= events.Subscribe<OperatingDayStartedEvent>(
            OnDayStarted);
    }

    public void Dispose()
    {
        dayStartedSubscription?.Dispose();
        dayStartedSubscription = null;
    }

    private void OnDayStarted(OperatingDayStartedEvent started)
    {
        List<CaptiveState> minions = captivity.Captives
            .Where(state => state?.IsMinion == true)
            .OrderBy(state => state.captiveId, StringComparer.Ordinal)
            .ToList();
        List<CaptiveState> pending = new();
        foreach (CaptiveState minion in minions)
        {
            if (commands.TryBeginDailySocialEvaluation(
                    minion.captiveId,
                    started.day,
                    out CaptiveState state))
            {
                pending.Add(state);
            }
        }
        if (pending.Count == 0)
        {
            return;
        }

        List<CharacterActor> residents = characters.Characters
            .Where(actor => actor != null
                && !actor.IsDead
                && standings.IsFormalResident(actor))
            .OrderBy(actor => actor.Identity?.PersistentId, StringComparer.Ordinal)
            .ToList();
        float ratio = MinionIntegrationRules.ResolveMinionRatio(
            residents.Count,
            minions.Count);
        int residentMoodDelta =
            MinionIntegrationRules.ResolveResidentMoodDelta(ratio);
        foreach (CharacterActor resident in residents)
        {
            moods.Apply(
                resident,
                "captivity:minion-ratio",
                residentMoodDelta,
                1,
                "하수인 비율");
        }

        int conflictLimit = MinionIntegrationRules.ResolveDailyConflictLimit(
            minions.Count);
        int conflicts = 0;
        foreach (CaptiveState state in pending)
        {
            CharacterActor minion = FindActor(state.captiveId);
            if (minion == null || minion.IsDead)
            {
                continue;
            }

            if (conflicts < conflictLimit
                && residents.Count > 0
                && random.Chance(
                    MinionIntegrationRules.ResolveConflictChancePercent(
                        ratio,
                        state.grudge,
                        minion.Mood.Value) / 100f))
            {
                CharacterActor resident = residents[
                    random.NextInt(0, residents.Count)];
                ResolveConflict(started.day, state, minion, resident);
                conflicts++;
            }

            float breakChance =
                MinionIntegrationRules.ResolveControlBreakChancePercent(
                    state.corruption,
                    state.trust,
                    state.grudge,
                    minion.Mood.Value);
            if (breakChance > 0f && random.Chance(breakChance / 100f))
            {
                commands.TryBreakMinionControl(
                    state.captiveId,
                    "통제 안정도가 무너져 정착지 통제에서 벗어남",
                    out _);
            }
        }
    }

    private void ResolveConflict(
        int day,
        CaptiveState state,
        CharacterActor minion,
        CharacterActor resident)
    {
        float relationship = Mathf.Min(
            minion.SocialMemory?.GetRelationshipSentiment(resident) ?? 0f,
            resident.SocialMemory?.GetRelationshipSentiment(minion) ?? 0f);
        float brawlChance = MinionIntegrationRules.ResolveBrawlChancePercent(
            relationship,
            minion.Mood.Value,
            resident.Mood.Value);
        bool brawl = random.Chance(brawlChance / 100f);
        string conflictOperationId =
            $"minion-social:{day}:{state.captiveId}";
        if (brawl)
        {
            var parent = new CombatOutcomeParentContext(new CombatConflictFactData
            {
                operationId = conflictOperationId,
                instigator = new CombatFactParticipantData { kind = CombatFactParticipantKind.Character,
                    id = CharacterPersistentIdentity.Require(minion).Value, name = minion.Identity?.DisplayName ?? minion.name },
                target = new CombatFactParticipantData { kind = CombatFactParticipantKind.Character,
                    id = CharacterPersistentIdentity.Require(resident).Value, name = resident.Identity?.DisplayName ?? resident.name },
                conflictId = "minion-brawl", severity = 5f, committedMoodDelta = 0f,
                origin = CharacterCommandOrigin.Autonomous, absoluteDay = Mathf.Max(1, day)
            });
            if (!ApplyUnarmedAttack(
                conflictOperationId,
                0L,
                minion,
                resident,
                parent))
            {
                return;
            }
            if (!minion.IsDead && !resident.IsDead)
            {
                ApplyUnarmedAttack(
                    conflictOperationId,
                    1L,
                    resident,
                    minion,
                    parent.AsCommitted());
            }
        }
        else
        {
            var fact = new SocialInteractionFact(new SocialInteractionFactData
            {
                kind = SocialInteractionKind.MinionArgument, operationId = conflictOperationId,
                actorId = CharacterPersistentIdentity.Require(minion).Value,
                actorName = minion.Identity?.DisplayName ?? minion.name,
                targetId = CharacterPersistentIdentity.Require(resident).Value,
                targetName = resident.Identity?.DisplayName ?? resident.name,
                conflictId = "minion-argument", severity = 2f,
                origin = CharacterCommandOrigin.Autonomous, absoluteDay = day
            }, true);
            if (!socialOutcomes.TryPrepare(fact, out var prepared, out string failure))
            { Debug.LogWarning("Minion social conflict preparation failed: " + failure); return; }
            using (prepared)
            {
                if (!prepared.PublishApplied().MayBeCommitted)
                { Debug.LogWarning("Minion social conflict publication failed: " + socialOutcomes.LastFailure); return; }
            }
        }

        string result = brawl
            ? $"{resident.Identity?.DisplayName ?? resident.name}와 몸싸움"
            : $"{resident.Identity?.DisplayName ?? resident.name}와 말다툼";
        commands.RecordSocialConflict(state.captiveId, result);
        try
        {
            events.Publish(new MinionSocialConflictAppliedEvent(
                CharacterPersistentIdentity.Require(minion).Value,
                CharacterPersistentIdentity.Require(resident).Value,
                resident.Identity?.DisplayName ?? resident.name, brawl));
        }
        catch (Exception exception) { Debug.LogError("Minion conflict presentation failed: " + exception.Message); }
    }

    private bool ApplyUnarmedAttack(
        string conflictOperationId,
        long attackRevision,
        CharacterActor attacker,
        CharacterActor defender,
        CombatOutcomeParentContext parent)
    {
        string attackerId = CharacterPersistentIdentity.Require(attacker).Value;
        string defenderId = CharacterPersistentIdentity.Require(defender).Value;
        var request = new CombatAttackRequest(
            conflictOperationId,
            attackerId,
            defenderId,
            CombatRuntimeStatFactory.Create(
                attacker,
                bodyHealth.GetSnapshot(attacker),
                performance),
            CombatRuntimeStatFactory.Create(
                defender,
                bodyHealth.GetSnapshot(defender),
                performance),
            CombatWeaponSnapshot.CreateUnarmed(),
            1,
            CombatFireMode.Aimed,
            default,
            defenderDowned: bodyHealth.GetSnapshot(defender).Downed,
            attackPowerMultiplier: attacker.GetCombatPowerMultiplier(),
            defenderArmor: equipment.GetArmor(defenderId),
            defenderShield: equipment.GetShield(defenderId));
        CharacterCombatTransactionResult transaction =
            combatTransactions.ResolveAndApply(
            request,
            new CombatParticipantRef(attacker),
            defender,
            CombatDamageType.Blunt,
            "하수인 사회 충돌",
            CharacterCommandOrigin.Autonomous,
            attackRevision,
            parent);
        if (!transaction.Succeeded)
        {
            Debug.LogWarning(
                "Minion social combat transaction failed: "
                + transaction.FailureReason);
        }
        return transaction.Succeeded;
    }

    private CharacterActor FindActor(string persistentId) =>
        characters.Characters.FirstOrDefault(actor => actor != null
            && string.Equals(
                actor.Identity?.PersistentId,
                persistentId,
                StringComparison.Ordinal));
}
