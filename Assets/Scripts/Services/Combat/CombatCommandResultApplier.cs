using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public readonly struct VisitorFacilityCombatDamageCommittedEvent
{
    public VisitorFacilityCombatDamageCommittedEvent(
        string attackOperationId,
        CharacterId customerCharacterId,
        CharacterId otherParticipantCharacterId,
        bool customerWasAttacker,
        float actualDamage,
        BuildingInstanceId facilityInstanceId,
        CoreGridCell location,
        int absoluteDay)
    {
        AttackOperationId = attackOperationId ?? string.Empty;
        CustomerCharacterId = customerCharacterId;
        OtherParticipantCharacterId = otherParticipantCharacterId;
        CustomerWasAttacker = customerWasAttacker;
        ActualDamage = actualDamage;
        FacilityInstanceId = facilityInstanceId;
        Location = location;
        AbsoluteDay = absoluteDay;
    }

    public string AttackOperationId { get; }
    public CharacterId CustomerCharacterId { get; }
    public CharacterId OtherParticipantCharacterId { get; }
    public bool CustomerWasAttacker { get; }
    public float ActualDamage { get; }
    public BuildingInstanceId FacilityInstanceId { get; }
    public CoreGridCell Location { get; }
    public int AbsoluteDay { get; }
}

public sealed class CombatCommandResultApplier
{
    private readonly ICombatEquipmentRuntime equipment;
    private readonly ICharacterBodyHealthCommand bodyHealth;
    private readonly ICharacterBodyHealthQuery bodyHealthQuery;
    private readonly ICharacterBodyHealthMutationTransaction bodyHealthMutation;
    private readonly CombatDamageOutcomeBridge damageOutcomes;
    private readonly ICombatCoverDurabilityRegistry coverDurability;
    private readonly IGameClock gameClock;
    private readonly IWorldUiHierarchy worldUiHierarchy;
    private readonly IGameEventBus gameEvents;
    private readonly ICharacterWorldQuery characterWorld;
    private readonly IBuildingWorldQuery buildingWorld;
    private readonly IRoomFacilityPolicy roomFacilityPolicy;
    private readonly IGameCalendar calendar;
    private readonly CharacterIdentityEventPublisher identityEvents;
    private readonly IMigratedProducerOutcomeTransaction migratedOutcomes;
    private readonly IMigratedProducerOutcomeExternalBatchTransaction
        migratedExternalBatch;

    public CombatCommandResultApplier(
        ICombatEquipmentRuntime equipment,
        ICharacterBodyHealthCommand bodyHealth,
        ICharacterBodyHealthQuery bodyHealthQuery,
        ICharacterBodyHealthMutationTransaction bodyHealthMutation,
        CombatDamageOutcomeBridge damageOutcomes,
        ICombatCoverDurabilityRegistry coverDurability,
        IGameClock gameClock,
        IWorldUiHierarchy worldUiHierarchy,
        IGameEventBus gameEvents,
        ICharacterWorldQuery characterWorld,
        IBuildingWorldQuery buildingWorld,
        IRoomFacilityPolicy roomFacilityPolicy,
        IGameCalendar calendar,
        CharacterIdentityEventPublisher identityEvents = null,
        IMigratedProducerOutcomeTransaction migratedOutcomes = null,
        IMigratedProducerOutcomeExternalBatchTransaction migratedExternalBatch = null)
    {
        this.equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
        this.bodyHealth = bodyHealth ?? throw new ArgumentNullException(nameof(bodyHealth));
        this.bodyHealthQuery = bodyHealthQuery
            ?? throw new ArgumentNullException(nameof(bodyHealthQuery));
        this.bodyHealthMutation = bodyHealthMutation
            ?? throw new ArgumentNullException(nameof(bodyHealthMutation));
        this.damageOutcomes = damageOutcomes
            ?? throw new ArgumentNullException(nameof(damageOutcomes));
        this.coverDurability = coverDurability
            ?? throw new ArgumentNullException(nameof(coverDurability));
        this.gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
        this.worldUiHierarchy = worldUiHierarchy
            ?? throw new ArgumentNullException(nameof(worldUiHierarchy));
        this.gameEvents = gameEvents ?? throw new ArgumentNullException(nameof(gameEvents));
        this.characterWorld = characterWorld ?? throw new ArgumentNullException(nameof(characterWorld));
        this.buildingWorld = buildingWorld
            ?? throw new ArgumentNullException(nameof(buildingWorld));
        this.roomFacilityPolicy = roomFacilityPolicy
            ?? throw new ArgumentNullException(nameof(roomFacilityPolicy));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.identityEvents = identityEvents;
        this.migratedOutcomes = migratedOutcomes;
        this.migratedExternalBatch = migratedExternalBatch;
    }

    public bool TryReserveDamageOutcome(
        CombatParticipantRef target,
        string attackOperationId,
        long attackRevision,
        out ReservedCombatDamageOutcome reserved,
        out bool capacityDeferred,
        out string failureReason)
    {
        PreparedMigratedProducerOutcome attackOutcome = default;
        if (migratedOutcomes != null
            && !migratedOutcomes.TryReserve(
                MigratedProducerOutcomeKind.CombatAttackResult,
                $"{attackOperationId}:{attackRevision}",
                Mathf.Max(1, calendar.Day),
                GameplayOutcomeStatus.Succeeded,
                participantCount: 2,
                metricCount: 0,
                subjectCount: 2,
                additionalFactCount: 1,
                out attackOutcome,
                out failureReason))
        {
            reserved = default;
            capacityDeferred = failureReason.Contains(
                "CapacityDeferred",
                StringComparison.Ordinal);
            return false;
        }

        if (!target.IsCharacter)
        {
            reserved = attackOutcome.IsValid
                ? new ReservedCombatDamageOutcome(
                    default,
                    attackOperationId,
                    attackRevision,
                    attackOutcome)
                : default;
            capacityDeferred = false;
            failureReason = string.Empty;
            return true;
        }

        if (!damageOutcomes.TryReserve(
            attackOperationId,
            attackRevision,
            Mathf.Max(1, calendar.Day),
            out reserved,
            out capacityDeferred,
            out failureReason))
        {
            if (attackOutcome.IsValid)
                migratedOutcomes.Cancel(attackOutcome);
            return false;
        }
        if (attackOutcome.IsValid)
            reserved = reserved.WithAttackOutcome(attackOutcome);
        return true;
    }

    public void CancelDamageOutcome(in ReservedCombatDamageOutcome reserved)
    {
        if (reserved.HasDamageReservation)
            damageOutcomes.Cancel(reserved);
        if (reserved.HasAttackOutcome)
            migratedOutcomes?.Cancel(reserved.AttackOutcome);
    }

    // Non-command combat producers retain their existing authority path.  They
    // do not have a stable command operation/revision reservation and therefore
    // must not be disguised as the direct-command ledger producer below.
    public void Apply(
        CombatParticipantRef target,
        CombatAttackResult result,
        CharacterActor attacker,
        string attackerName,
        CombatDamageType damageType)
    {
        ApplySignalSupport(result, attacker);
        if (result.CoverBlocked)
        {
            coverDurability.TryApplyDamage(result.CoverSourceId, result.CoverDamage);
            CombatImpactPresentation.Play(
                target.IsCharacter
                    ? target.Character.transform.position
                    : target.Wildlife.transform.position,
                damageType,
                gameClock,
                worldUiHierarchy,
                coverHit: true);
            if (target.IsCharacter)
                bodyHealth.AddSuppression(target.Character, result.Suppression);
            return;
        }

        if (target.IsCharacter)
        {
            CombatAttackResult appliedResult = damageType == CombatDamageType.Blunt
                ? result.WithAppliedDamageMultiplier(
                    target.Character.GetDetailedStatMultiplier(
                        "damage:blunt-taken"))
                : result;
            bool wasAlive = !target.Character.IsDead;
            if (appliedResult.Hit)
            {
                bodyHealth.ApplyCombatResult(
                    target.Character,
                    appliedResult,
                    $"직접 전투 명령: {attackerName}");
                if (appliedResult.AppliedDamage > 0f
                    && CharacterPersistentIdentity.TryGet(
                        target.Character,
                        out CharacterId injuredId))
                {
                    CharacterId attackerId = attacker != null
                        && CharacterPersistentIdentity.TryGet(
                            attacker,
                            out CharacterId resolvedAttackerId)
                            ? resolvedAttackerId
                            : default;
                    identityEvents?.Publish(new CharacterInjuredIdentityEvent(
                        injuredId,
                        attackerId,
                        damageType,
                        appliedResult.AppliedDamage,
                        calendar.Day));
                }
                DefenseCombatPresentation.Ensure(target.Character)?.PlayHit(
                    appliedResult.AppliedDamage,
                    damageType,
                    worldUiHierarchy);
                if (wasAlive && target.Character.IsDead)
                {
                    PublishKilledEvent(
                        attacker,
                        target.Character,
                        CharacterCommandOrigin.DirectPlayerOrder);
                }
            }
            else
            {
                bodyHealth.AddSuppression(target.Character, result.Suppression);
            }
        }
        else if (target.IsWildlife)
        {
            target.Wildlife.ApplyCombatDamage(result, attacker);
        }
    }

    public CombatOutcomeApplyResult Apply(
        CombatParticipantRef target,
        CombatAttackResult result,
        CharacterActor attacker,
        string attackerName,
        CombatDamageType damageType,
        string attackOperationId,
        long attackRevision,
        in ReservedCombatDamageOutcome reservedDamage,
        in CombatOutcomeMechanicalMutation mechanicalMutation)
    {
        return Apply(
            target,
            result,
            attacker,
            attackerName,
            damageType,
            attackOperationId,
            attackRevision,
            reservedDamage,
            mechanicalMutation,
            $"직접 전투 명령: {attackerName}",
            CharacterCommandOrigin.DirectPlayerOrder);
    }

    public CombatOutcomeApplyResult Apply(
        CombatParticipantRef target,
        CombatAttackResult result,
        CharacterActor attacker,
        string attackerName,
        CombatDamageType damageType,
        string attackOperationId,
        long attackRevision,
        in ReservedCombatDamageOutcome reservedDamage,
        in CombatOutcomeMechanicalMutation mechanicalMutation,
        string damageSource,
        CharacterCommandOrigin origin)
    {
        bool signalAppliedInTransaction = false;
        PreparedMigratedProducerOutcome[] transitionOutcomes = null;
        if (!mechanicalMutation.IsValid)
        {
            CancelDamageOutcome(reservedDamage);
            return CombatOutcomeApplyResult.Failed(
                "combat-mechanical-mutation-boundary-missing");
        }
        if (string.IsNullOrWhiteSpace(damageSource))
        {
            CancelDamageOutcome(reservedDamage);
            SafeRollbackMechanical(mechanicalMutation);
            return CombatOutcomeApplyResult.Failed(
                "combat-damage-source-missing");
        }
        string normalizedDamageSource = damageSource.Trim();
        if (result.CoverBlocked)
        {
            if (target.IsCharacter && reservedDamage.HasAttackOutcome)
            {
                CombatOutcomeApplyResult applied = ApplyCharacterNonDamageOutcome(
                    target.Character,
                    result.WithAppliedDamageMultiplier(0f),
                    attacker,
                    reservedDamage,
                    mechanicalMutation,
                    normalizedDamageSource);
                if (!applied.Succeeded)
                    return applied;
                coverDurability.TryApplyDamage(
                    result.CoverSourceId,
                    result.CoverDamage);
                CombatImpactPresentation.Play(
                    target.Character.transform.position,
                    damageType,
                    gameClock,
                    worldUiHierarchy,
                    coverHit: true);
                return CombatOutcomeApplyResult.Success();
            }
            if (target.IsWildlife && reservedDamage.HasAttackOutcome)
            {
                damageOutcomes.Cancel(reservedDamage);
                if (!TryApplyMechanical(
                        mechanicalMutation,
                        out string wildlifeCoverMechanicalFailure))
                {
                    migratedOutcomes.Cancel(reservedDamage.AttackOutcome);
                    return CombatOutcomeApplyResult.Failed(
                        wildlifeCoverMechanicalFailure);
                }
                if (!TryCommitAttackOutcome(
                        reservedDamage.AttackOutcome,
                        attacker,
                        target,
                        result,
                        out string wildlifeCoverCommitFailure))
                {
                    SafeRollbackMechanical(mechanicalMutation);
                    return CombatOutcomeApplyResult.Failed(
                        wildlifeCoverCommitFailure);
                }
                mechanicalMutation.Complete();
                coverDurability.TryApplyDamage(
                    result.CoverSourceId,
                    result.CoverDamage);
                CombatImpactPresentation.Play(
                    target.Wildlife.transform.position,
                    damageType,
                    gameClock,
                    worldUiHierarchy,
                    coverHit: true);
                ApplySignalSupport(result, attacker);
                return CombatOutcomeApplyResult.Success();
            }
            CancelDamageOutcome(reservedDamage);
            if (!TryApplyUntrackedMechanical(
                    mechanicalMutation,
                    out string mechanicalFailure))
            {
                return CombatOutcomeApplyResult.Failed(mechanicalFailure);
            }
            coverDurability.TryApplyDamage(result.CoverSourceId, result.CoverDamage);
            CombatImpactPresentation.Play(
                target.IsCharacter
                    ? target.Character.transform.position
                    : target.Wildlife.transform.position,
                damageType,
                gameClock,
                worldUiHierarchy,
                coverHit: true);
            if (target.IsCharacter)
            {
                bodyHealth.AddSuppression(target.Character, result.Suppression);
            }
            ApplySignalSupport(result, attacker);
            return CombatOutcomeApplyResult.Success();
        }

        if (target.IsCharacter)
        {
            CombatAttackResult appliedResult = damageType == CombatDamageType.Blunt
                ? result.WithAppliedDamageMultiplier(
                    target.Character.GetDetailedStatMultiplier(
                        "damage:blunt-taken"))
                : result;
            bool wasAlive = !target.Character.IsDead;
            if (appliedResult.Hit)
            {
                float healthBefore = bodyHealthQuery.GetVitals(
                    target.Character).CurrentHealth;
                float expectedActualDamage = CalculateExpectedActualDamage(
                    target.Character,
                    appliedResult,
                    healthBefore);
                if (expectedActualDamage <= 0f)
                {
                    if (reservedDamage.HasAttackOutcome)
                    {
                        return ApplyCharacterNonDamageOutcome(
                            target.Character,
                            appliedResult.WithAppliedDamageMultiplier(0f),
                            attacker,
                            reservedDamage,
                            mechanicalMutation,
                            normalizedDamageSource);
                    }
                    CancelDamageOutcome(reservedDamage);
                    if (!TryApplyUntrackedMechanical(
                            mechanicalMutation,
                            out string noDamageMechanicalFailure))
                    {
                        return CombatOutcomeApplyResult.Failed(
                            noDamageMechanicalFailure);
                    }
                    bodyHealth.ApplyCombatResult(
                        target.Character,
                        appliedResult,
                        normalizedDamageSource);
                    ApplySignalSupport(result, attacker);
                    return CombatOutcomeApplyResult.Success();
                }
                if (attacker == null
                    || !CharacterPersistentIdentity.TryGet(
                        attacker,
                        out CharacterId attackerId)
                    || !CharacterPersistentIdentity.TryGet(
                        target.Character,
                        out CharacterId victimId))
                {
                    CancelDamageOutcome(reservedDamage);
                    SafeRollbackMechanical(mechanicalMutation);
                    return CombatOutcomeApplyResult.Failed(
                        "combat-outcome-participant-id-missing");
                }

                CombatDamageOutcomeReceipt damageReceipt;
                try
                {
                    damageReceipt = new CombatDamageOutcomeReceipt(
                        attackOperationId,
                        attackRevision,
                        attackerId,
                        ProductionCombatOutcomeNames.Snapshot(
                            attacker.Identity?.DisplayName ?? attackerName,
                            attackerId.Value),
                        victimId,
                        ProductionCombatOutcomeNames.Snapshot(
                            target.Character.Identity?.DisplayName
                                ?? target.Character.name,
                            victimId.Value),
                        damageType,
                        appliedResult.BodyPart,
                        expectedActualDamage,
                        Mathf.Max(1, calendar.Day),
                        target.Character.GetNowXY());
                }
                catch (Exception exception) when (exception is ArgumentException
                                                   or InvalidOperationException
                                                   or OverflowException)
                {
                    CancelDamageOutcome(reservedDamage);
                    SafeRollbackMechanical(mechanicalMutation);
                    return CombatOutcomeApplyResult.Failed(
                        "combat-outcome-receipt-invalid:" + exception.Message);
                }
                if (!damageOutcomes.TryPrepare(
                        damageReceipt,
                        reservedDamage,
                        out PreparedOutcomeToken prepared,
                        out string prepareFailure))
                {
                    if (reservedDamage.HasAttackOutcome)
                        migratedOutcomes?.Cancel(reservedDamage.AttackOutcome);
                    SafeRollbackMechanical(mechanicalMutation);
                    return CombatOutcomeApplyResult.Failed(prepareFailure);
                }

                CharacterBodyHealthMutationSnapshot rollback;
                CharacterBodyHealthMutationSnapshot attackerRollback = default;
                bool hasAttackerRollback = false;
                try
                {
                    rollback = bodyHealthMutation.CaptureCombatMutation(
                        target.Character);
                    if ((result.SpecialEffects
                            & CombatSpecialEffectFlags.SignalSupport) != 0
                        && attacker != null)
                    {
                        attackerRollback =
                            bodyHealthMutation.CaptureCombatMutation(attacker);
                        hasAttackerRollback = true;
                    }
                }
                catch (Exception exception) when (exception is ArgumentException
                                                   or InvalidOperationException)
                {
                    damageOutcomes.CancelPrepared(prepared);
                    if (reservedDamage.HasAttackOutcome)
                        migratedOutcomes?.Cancel(reservedDamage.AttackOutcome);
                    SafeRollbackMechanical(mechanicalMutation);
                    return CombatOutcomeApplyResult.Failed(
                        "combat-health-rollback-capture-failed:" + exception.Message);
                }
                if (reservedDamage.HasAttackOutcome
                    && !TryReserveCharacterCombatTransitions(
                        attackOperationId,
                        attackRevision,
                        out transitionOutcomes,
                        out string transitionReserveFailure))
                {
                    damageOutcomes.CancelPrepared(prepared);
                    migratedOutcomes.Cancel(reservedDamage.AttackOutcome);
                    SafeRollbackMechanical(mechanicalMutation);
                    return CombatOutcomeApplyResult.Failed(
                        transitionReserveFailure);
                }
                try
                {
                    bodyHealthMutation.ApplyPreparedCombatMutation(
                        target.Character,
                        appliedResult,
                        normalizedDamageSource);
                }
                catch (Exception exception) when (
                    exception is not OutOfMemoryException
                    && exception is not StackOverflowException
                    && exception is not AccessViolationException)
                {
                    bodyHealthMutation.RestoreCombatMutation(
                        target.Character,
                        rollback,
                        "combat-health-mutation-exception-rollback");
                    RestoreSignalSupportMutation(
                        attacker,
                        attackerRollback,
                        hasAttackerRollback,
                        "combat-signal-mutation-exception-rollback");
                    damageOutcomes.CancelPrepared(prepared);
                    CancelMigratedReservations(transitionOutcomes);
                    if (reservedDamage.HasAttackOutcome)
                        migratedOutcomes?.Cancel(reservedDamage.AttackOutcome);
                    SafeRollbackMechanical(mechanicalMutation);
                    return CombatOutcomeApplyResult.Failed(
                        "combat-health-mutation-failed:" + exception.GetType().Name);
                }
                float actualDamage = Mathf.Max(
                    0f,
                    healthBefore - bodyHealthQuery.GetVitals(
                        target.Character).CurrentHealth);
                if (!Mathf.Approximately(actualDamage, expectedActualDamage))
                {
                    bodyHealthMutation.RestoreCombatMutation(
                        target.Character,
                        rollback,
                        "combat-outcome-post-clamp-mismatch-rollback");
                    RestoreSignalSupportMutation(
                        attacker,
                        attackerRollback,
                        hasAttackerRollback,
                        "combat-signal-post-clamp-rollback");
                    damageOutcomes.CancelPrepared(prepared);
                    CancelMigratedReservations(transitionOutcomes);
                    if (reservedDamage.HasAttackOutcome)
                        migratedOutcomes?.Cancel(reservedDamage.AttackOutcome);
                    SafeRollbackMechanical(mechanicalMutation);
                    return CombatOutcomeApplyResult.Failed(
                        "combat-outcome-post-clamp-mismatch");
                }
                if (hasAttackerRollback)
                {
                    bodyHealthMutation.ApplyPreparedSuppressionReduction(
                        attacker,
                        result.StatusPotency * 100f);
                    signalAppliedInTransaction = true;
                }
                if (!TryApplyMechanical(
                        mechanicalMutation,
                        out string mechanicalFailure))
                {
                    bodyHealthMutation.RestoreCombatMutation(
                        target.Character,
                        rollback,
                        "combat-mechanical-mutation-rollback");
                    RestoreSignalSupportMutation(
                        attacker,
                        attackerRollback,
                        hasAttackerRollback,
                        "combat-signal-mechanical-rollback");
                    damageOutcomes.CancelPrepared(prepared);
                    CancelMigratedReservations(transitionOutcomes);
                    if (reservedDamage.HasAttackOutcome)
                        migratedOutcomes?.Cancel(reservedDamage.AttackOutcome);
                    return CombatOutcomeApplyResult.Failed(mechanicalFailure);
                }
                if (!TryCommitCharacterCombatBatch(
                        prepared,
                        reservedDamage,
                        transitionOutcomes,
                        attackRevision,
                        attacker,
                        target.Character,
                        appliedResult,
                        rollback,
                        actualDamage,
                        normalizedDamageSource,
                        out string commitFailure))
                {
                    bodyHealthMutation.RestoreCombatMutation(
                        target.Character,
                        rollback,
                        "combat-outcome-commit-rollback");
                    RestoreSignalSupportMutation(
                        attacker,
                        attackerRollback,
                        hasAttackerRollback,
                        "combat-signal-commit-rollback");
                    SafeRollbackMechanical(mechanicalMutation);
                    return CombatOutcomeApplyResult.Failed(commitFailure);
                }
                mechanicalMutation.Complete();
                bodyHealthMutation.CompletePreparedCombatMutation(
                    target.Character,
                    rollback,
                    appliedResult,
                    normalizedDamageSource);
                if (hasAttackerRollback)
                {
                    bodyHealthMutation.CompletePreparedSuppressionReduction(
                        attacker,
                        attackerRollback);
                }
                if (actualDamage > 0f
                    && CharacterPersistentIdentity.TryGet(
                        target.Character,
                        out CharacterId injuredId))
                {
                    CharacterId injuryAttackerId = attacker != null
                        && CharacterPersistentIdentity.TryGet(
                            attacker,
                            out CharacterId resolvedAttackerId)
                            ? resolvedAttackerId
                            : default;
                    identityEvents?.Publish(new CharacterInjuredIdentityEvent(
                        injuredId,
                        injuryAttackerId,
                        damageType,
                        actualDamage,
                        calendar.Day));
                }
                PublishCommittedCharacterDamage(
                    attackOperationId,
                    attacker,
                    target.Character,
                    actualDamage,
                    origin);
                DefenseCombatPresentation.Ensure(target.Character)?.PlayHit(
                    appliedResult.AppliedDamage,
                    damageType,
                    worldUiHierarchy);
                if (wasAlive && target.Character.IsDead)
                {
                    PublishKilledEvent(attacker, target.Character, origin);
                }
            }
            else
            {
                if (reservedDamage.HasAttackOutcome)
                {
                    return ApplyCharacterNonDamageOutcome(
                        target.Character,
                        appliedResult,
                        attacker,
                        reservedDamage,
                        mechanicalMutation,
                        normalizedDamageSource);
                }
                CancelDamageOutcome(reservedDamage);
                if (!TryApplyUntrackedMechanical(
                        mechanicalMutation,
                        out string mechanicalFailure))
                {
                    return CombatOutcomeApplyResult.Failed(mechanicalFailure);
                }
                bodyHealth.AddSuppression(target.Character, result.Suppression);
            }
        }
        else if (target.IsWildlife)
        {
            if (!TryApplyMechanical(
                    mechanicalMutation,
                    out string mechanicalFailure))
            {
                if (reservedDamage.HasAttackOutcome)
                    migratedOutcomes?.Cancel(reservedDamage.AttackOutcome);
                return CombatOutcomeApplyResult.Failed(mechanicalFailure);
            }
            if (reservedDamage.HasAttackOutcome
                && !TryCommitAttackOutcome(
                    reservedDamage.AttackOutcome,
                    attacker,
                    target,
                    result,
                    out string attackCommitFailure))
            {
                SafeRollbackMechanical(mechanicalMutation);
                return CombatOutcomeApplyResult.Failed(attackCommitFailure);
            }
            mechanicalMutation.Complete();
            target.Wildlife.ApplyCombatDamage(result, attacker);
        }
        if (!signalAppliedInTransaction)
            ApplySignalSupport(result, attacker);
        return CombatOutcomeApplyResult.Success();
    }

    private CombatOutcomeApplyResult ApplyCharacterNonDamageOutcome(
        CharacterActor target,
        CombatAttackResult result,
        CharacterActor attacker,
        in ReservedCombatDamageOutcome reserved,
        in CombatOutcomeMechanicalMutation mechanicalMutation,
        string damageSource)
    {
        if (target == null || attacker == null)
        {
            CancelDamageOutcome(reserved);
            SafeRollbackMechanical(mechanicalMutation);
            return CombatOutcomeApplyResult.Failed(
                "combat-non-damage-participant-missing");
        }
        damageOutcomes.Cancel(reserved);

        CharacterBodyHealthMutationSnapshot targetBefore;
        CharacterBodyHealthMutationSnapshot attackerBefore = default;
        bool hasSignal = (result.SpecialEffects
                & CombatSpecialEffectFlags.SignalSupport) != 0;
        try
        {
            targetBefore = bodyHealthMutation.CaptureCombatMutation(target);
            if (hasSignal)
            {
                attackerBefore = bodyHealthMutation.CaptureCombatMutation(
                    attacker);
            }
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException)
        {
            migratedOutcomes.Cancel(reserved.AttackOutcome);
            SafeRollbackMechanical(mechanicalMutation);
            return CombatOutcomeApplyResult.Failed(
                "combat-non-damage-snapshot-failed:" + exception.Message);
        }
        if (!TryReserveCharacterCombatTransitions(
                reserved.OperationId,
                reserved.AttackRevision,
                out PreparedMigratedProducerOutcome[] transitions,
                out string reserveFailure))
        {
            migratedOutcomes.Cancel(reserved.AttackOutcome);
            SafeRollbackMechanical(mechanicalMutation);
            return CombatOutcomeApplyResult.Failed(reserveFailure);
        }

        try
        {
            bodyHealthMutation.ApplyPreparedCombatMutation(
                target,
                result,
                damageSource);
            if (hasSignal)
            {
                bodyHealthMutation.ApplyPreparedSuppressionReduction(
                    attacker,
                    result.StatusPotency * 100f);
            }
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException
            && exception is not StackOverflowException
            && exception is not AccessViolationException)
        {
            bodyHealthMutation.RestoreCombatMutation(
                target,
                targetBefore,
                "combat-non-damage-mutation-rollback");
            RestoreSignalSupportMutation(
                attacker,
                attackerBefore,
                hasSignal,
                "combat-non-damage-signal-rollback");
            migratedOutcomes.Cancel(reserved.AttackOutcome);
            CancelMigratedReservations(transitions);
            SafeRollbackMechanical(mechanicalMutation);
            return CombatOutcomeApplyResult.Failed(
                "combat-non-damage-mutation-failed:"
                + exception.GetType().Name);
        }
        if (!TryApplyMechanical(mechanicalMutation, out string mechanicalFailure))
        {
            bodyHealthMutation.RestoreCombatMutation(
                target,
                targetBefore,
                "combat-non-damage-mechanical-rollback");
            RestoreSignalSupportMutation(
                attacker,
                attackerBefore,
                hasSignal,
                "combat-non-damage-signal-mechanical-rollback");
            migratedOutcomes.Cancel(reserved.AttackOutcome);
            CancelMigratedReservations(transitions);
            return CombatOutcomeApplyResult.Failed(mechanicalFailure);
        }

        CharacterBodyHealthMutationSnapshot targetAfter;
        try
        {
            targetAfter = bodyHealthMutation.CaptureCombatMutation(target);
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException)
        {
            bodyHealthMutation.RestoreCombatMutation(
                target,
                targetBefore,
                "combat-non-damage-post-state-rollback");
            RestoreSignalSupportMutation(
                attacker,
                attackerBefore,
                hasSignal,
                "combat-non-damage-signal-post-state-rollback");
            migratedOutcomes.Cancel(reserved.AttackOutcome);
            CancelMigratedReservations(transitions);
            SafeRollbackMechanical(mechanicalMutation);
            return CombatOutcomeApplyResult.Failed(
                "combat-non-damage-post-state-failed:" + exception.Message);
        }

        var selected = new List<PreparedMigratedProducerOutcome>(2)
        {
            reserved.AttackOutcome
        };
        var receipts = new List<MigratedProducerOutcomeReceipt>(2);
        try
        {
            receipts.Add(CreateTwoCharacterReceipt(
                reserved.AttackOutcome,
                attacker,
                target,
                $"{attacker.Identity?.DisplayName ?? attacker.name}의 공격이 "
                    + $"{target.Identity?.DisplayName ?? target.name}에게 판정됐다.",
                $"executed={result.Executed};hit={result.Hit};"
                    + $"coverBlocked={result.CoverBlocked};damage=0"));
            bool becameDowned = !targetBefore.State.downed
                && targetAfter.State.downed;
            for (int index = 0; index < transitions.Length; index++)
            {
                if (index != 1 || !becameDowned)
                {
                    migratedOutcomes.Cancel(transitions[index]);
                    continue;
                }
                selected.Add(transitions[index]);
                receipts.Add(CreateSingleCharacterReceipt(
                    transitions[index],
                    target,
                    $"{target.Identity?.DisplayName ?? target.name}이(가) 전투 불능이 됐다.",
                    $"beforeDowned={targetBefore.State.downed};"
                        + $"afterDowned={targetAfter.State.downed}"));
            }
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            bodyHealthMutation.RestoreCombatMutation(
                target,
                targetBefore,
                "combat-non-damage-receipt-rollback");
            RestoreSignalSupportMutation(
                attacker,
                attackerBefore,
                hasSignal,
                "combat-non-damage-signal-receipt-rollback");
            CancelMigratedReservations(selected);
            SafeRollbackMechanical(mechanicalMutation);
            return CombatOutcomeApplyResult.Failed(
                "combat-non-damage-receipt-invalid:" + exception.Message);
        }

        var results = new MigratedProducerOutcomeCommitResult[selected.Count];
        if (!migratedOutcomes.CommitBatch(
                selected.ToArray(),
                receipts.ToArray(),
                results,
                out string commitFailure))
        {
            bodyHealthMutation.RestoreCombatMutation(
                target,
                targetBefore,
                "combat-non-damage-outcome-rollback");
            RestoreSignalSupportMutation(
                attacker,
                attackerBefore,
                hasSignal,
                "combat-non-damage-signal-outcome-rollback");
            SafeRollbackMechanical(mechanicalMutation);
            return CombatOutcomeApplyResult.Failed(commitFailure);
        }

        mechanicalMutation.Complete();
        bodyHealthMutation.CompletePreparedCombatMutation(
            target,
            targetBefore,
            result,
            damageSource);
        if (hasSignal)
        {
            bodyHealthMutation.CompletePreparedSuppressionReduction(
                attacker,
                attackerBefore);
        }
        return CombatOutcomeApplyResult.Success();
    }

    private bool TryCommitAttackOutcome(
        in PreparedMigratedProducerOutcome prepared,
        CharacterActor attacker,
        in CombatParticipantRef target,
        CombatAttackResult result,
        out string failureReason)
    {
        failureReason = string.Empty;
        try
        {
            MigratedProducerOutcomeReceipt receipt;
            if (target.IsCharacter)
            {
                receipt = CreateTwoCharacterReceipt(
                    prepared,
                    attacker,
                    target.Character,
                    $"{attacker.Identity?.DisplayName ?? attacker.name}의 공격 판정이 끝났다.",
                    $"executed={result.Executed};hit={result.Hit};"
                        + $"coverBlocked={result.CoverBlocked};"
                        + $"damage={result.AppliedDamage:0.###}");
            }
            else if (target.IsWildlife
                && attacker != null
                && CharacterPersistentIdentity.TryGet(
                    attacker,
                    out CharacterId attackerId)
                && target.Wildlife != null
                && !string.IsNullOrWhiteSpace(target.Wildlife.WildlifeId))
            {
                var actorSubject = new MigratedProducerOutcomeSubject(
                    MigratedProducerOutcomeIds.CharacterKind,
                    attackerId.Value,
                    attacker.Identity?.DisplayName ?? attacker.name,
                    MigratedProducerOutcomeIds.ActorRole);
                var targetSubject = new MigratedProducerOutcomeSubject(
                    new GameplayEntityKindId("wildlife"),
                    target.Wildlife.WildlifeId,
                    target.Wildlife.DisplayName,
                    MigratedProducerOutcomeIds.TargetRole);
                MigratedProducerOutcomePayloadBuilder builder =
                    migratedOutcomes.CreatePayloadBuilder(prepared);
                if (!AddParticipantAndSubject(
                        ref builder,
                        prepared,
                        actorSubject)
                    || !AddParticipantAndSubject(
                        ref builder,
                        prepared,
                        targetSubject)
                    || !builder.AddFact(new GameplayOutcomeFact(
                        MigratedProducerOutcomeIds.SummaryFact,
                        $"{attacker.Identity?.DisplayName ?? attacker.name}의 야생동물 공격 판정이 끝났다."))
                    || !builder.AddFact(new GameplayOutcomeFact(
                        MigratedProducerOutcomeIds.DetailFact,
                        $"executed={result.Executed};hit={result.Hit};"
                            + $"coverBlocked={result.CoverBlocked};"
                            + $"damage={result.AppliedDamage:0.###}")))
                {
                    throw new InvalidOperationException(
                        "The wildlife attack payload exceeded its reserved capacity.");
                }
                receipt = builder.Build();
            }
            else
            {
                throw new InvalidOperationException(
                    "Stable combat attack participants are required.");
            }

            MigratedProducerOutcomeCommitResult committed =
                migratedOutcomes.Commit(prepared, receipt);
            if (committed.DurablyCommitted)
                return true;
            failureReason = committed.DetailCode;
            return false;
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            migratedOutcomes.Cancel(prepared);
            failureReason = "combat-attack-outcome-invalid:" + exception.Message;
            return false;
        }
    }

    private bool TryReserveCharacterCombatTransitions(
        string attackOperationId,
        long attackRevision,
        out PreparedMigratedProducerOutcome[] prepared,
        out string failureReason)
    {
        prepared = new PreparedMigratedProducerOutcome[4];
        failureReason = string.Empty;
        MigratedProducerOutcomeKind[] kinds =
        {
            MigratedProducerOutcomeKind.HealthThresholdCrossedEvent,
            MigratedProducerOutcomeKind.CharacterBodyHealthDownedEvent,
            MigratedProducerOutcomeKind.CharacterDeathEvent,
            MigratedProducerOutcomeKind.CharacterKilledEvent
        };
        for (int index = 0; index < kinds.Length; index++)
        {
            bool twoSubjects = kinds[index]
                == MigratedProducerOutcomeKind.CharacterKilledEvent;
            if (migratedOutcomes.TryReserve(
                    kinds[index],
                    $"{attackOperationId}:{attackRevision}:{(int)kinds[index]}",
                    Mathf.Max(1, calendar.Day),
                    GameplayOutcomeStatus.Succeeded,
                    participantCount: twoSubjects ? 2 : 1,
                    metricCount: 0,
                    subjectCount: twoSubjects ? 2 : 1,
                    additionalFactCount: 1,
                    out prepared[index],
                    out failureReason))
            {
                continue;
            }
            CancelMigratedReservations(prepared);
            failureReason = "combat-transition-outcome-reserve-failed:"
                + failureReason;
            return false;
        }
        return true;
    }

    private bool TryCommitCharacterCombatBatch(
        in PreparedOutcomeToken damagePrepared,
        in ReservedCombatDamageOutcome reserved,
        PreparedMigratedProducerOutcome[] transitionOutcomes,
        long attackRevision,
        CharacterActor attacker,
        CharacterActor victim,
        CombatAttackResult result,
        in CharacterBodyHealthMutationSnapshot before,
        float actualDamage,
        string damageSource,
        out string failureReason)
    {
        if (!reserved.HasAttackOutcome)
        {
            CancelMigratedReservations(transitionOutcomes);
            return damageOutcomes.TryCommit(
                damagePrepared,
                attackRevision,
                out failureReason);
        }
        if (migratedExternalBatch == null
            || migratedOutcomes == null
            || transitionOutcomes == null
            || transitionOutcomes.Length != 4)
        {
            damageOutcomes.CancelPrepared(damagePrepared);
            migratedOutcomes?.Cancel(reserved.AttackOutcome);
            CancelMigratedReservations(transitionOutcomes);
            failureReason = "combat-migrated-external-batch-boundary-missing";
            return false;
        }

        CharacterBodyHealthMutationSnapshot after;
        try
        {
            after = bodyHealthMutation.CaptureCombatMutation(victim);
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException)
        {
            damageOutcomes.CancelPrepared(damagePrepared);
            migratedOutcomes.Cancel(reserved.AttackOutcome);
            CancelMigratedReservations(transitionOutcomes);
            failureReason = "combat-transition-post-state-capture-failed:"
                + exception.Message;
            return false;
        }

        float beforeRatio = Mathf.Clamp01(
            before.State.currentHealth / Mathf.Max(1f, before.State.maxHealth));
        float afterRatio = Mathf.Clamp01(
            after.State.currentHealth / Mathf.Max(1f, after.State.maxHealth));
        bool thresholdCrossed = (beforeRatio > 0.20f && afterRatio <= 0.20f)
            || (beforeRatio <= 0.20f && afterRatio > 0.20f);
        bool downed = !before.State.downed && after.State.downed;
        bool died = before.State.currentHealth > 0f
            && after.State.currentHealth <= 0f;
        bool killed = died && attacker != null;

        var selected = new List<PreparedMigratedProducerOutcome>(5)
        {
            reserved.AttackOutcome
        };
        var receipts = new List<MigratedProducerOutcomeReceipt>(5);
        try
        {
            receipts.Add(CreateTwoCharacterReceipt(
                reserved.AttackOutcome,
                attacker,
                victim,
                $"{attacker.Identity?.DisplayName ?? attacker.name}의 공격이 "
                    + $"{victim.Identity?.DisplayName ?? victim.name}에게 판정됐다.",
                $"executed={result.Executed};hit={result.Hit};"
                    + $"coverBlocked={result.CoverBlocked};"
                    + $"bodyPart={result.BodyPart};damage={actualDamage:0.###}"));

            bool[] include = { thresholdCrossed, downed, died, killed };
            for (int index = 0; index < transitionOutcomes.Length; index++)
            {
                PreparedMigratedProducerOutcome candidate =
                    transitionOutcomes[index];
                if (!include[index])
                {
                    migratedOutcomes.Cancel(candidate);
                    continue;
                }
                selected.Add(candidate);
                if (index == 3)
                {
                    receipts.Add(CreateTwoCharacterReceipt(
                        candidate,
                        attacker,
                        victim,
                        $"{attacker.Identity?.DisplayName ?? attacker.name}의 공격으로 "
                            + $"{victim.Identity?.DisplayName ?? victim.name}이(가) 쓰러졌다.",
                        $"origin=combat;source={damageSource};damage={actualDamage:0.###}"));
                }
                else
                {
                    string summary = index switch
                    {
                        0 => $"{victim.Identity?.DisplayName ?? victim.name}의 건강이 임계치를 넘었다.",
                        1 => $"{victim.Identity?.DisplayName ?? victim.name}이(가) 전투 불능이 됐다.",
                        _ => $"{victim.Identity?.DisplayName ?? victim.name}이(가) 전투 피해로 사망했다."
                    };
                    string detail = index switch
                    {
                        0 => $"beforeRatio={beforeRatio:0.###};afterRatio={afterRatio:0.###}",
                        1 => $"beforeDowned={before.State.downed};afterDowned={after.State.downed}",
                        _ => $"source={damageSource};damage={actualDamage:0.###}"
                    };
                    receipts.Add(CreateSingleCharacterReceipt(
                        candidate,
                        victim,
                        summary,
                        detail));
                }
            }
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            damageOutcomes.CancelPrepared(damagePrepared);
            CancelMigratedReservations(selected.ToArray());
            failureReason = "combat-transition-receipt-invalid:"
                + exception.Message;
            return false;
        }

        var commitResults =
            new MigratedProducerOutcomeCommitResult[selected.Count];
        return migratedExternalBatch.CommitWithExternalPrepared(
            damagePrepared,
            attackRevision,
            selected.ToArray(),
            receipts.ToArray(),
            commitResults,
            out failureReason);
    }

    private MigratedProducerOutcomeReceipt CreateSingleCharacterReceipt(
        in PreparedMigratedProducerOutcome prepared,
        CharacterActor character,
        string summary,
        string detail)
    {
        if (character == null
            || !CharacterPersistentIdentity.TryGet(
                character,
                out CharacterId characterId))
        {
            throw new InvalidOperationException(
                "A persistent character identity is required for a combat transition outcome.");
        }
        var subject = new MigratedProducerOutcomeSubject(
            MigratedProducerOutcomeIds.CharacterKind,
            characterId.Value,
            character.Identity?.DisplayName ?? character.name,
            MigratedProducerOutcomeIds.TargetRole);
        MigratedProducerOutcomePayloadBuilder builder =
            migratedOutcomes.CreatePayloadBuilder(prepared);
        if (!builder.AddParticipant(new GameplayOutcomeParticipant(
                subject.EntityId,
                subject.Role,
                GameplayParticipationKind.Direct,
                true,
                subject.DisplayName))
            || !builder.AddSubject(new GameplayOutcomeSubjectLink(
                subject.EntityId,
                prepared.Salience,
                prepared.Tier,
                prepared.Tier == NarrativeMemoryTier.Core,
                false,
                0))
            || !builder.AddFact(new GameplayOutcomeFact(
                MigratedProducerOutcomeIds.SummaryFact,
                summary))
            || !builder.AddFact(new GameplayOutcomeFact(
                MigratedProducerOutcomeIds.DetailFact,
                detail)))
        {
            throw new InvalidOperationException(
                "The combat transition payload exceeded its reserved capacity.");
        }
        return builder.Build();
    }

    private MigratedProducerOutcomeReceipt CreateTwoCharacterReceipt(
        in PreparedMigratedProducerOutcome prepared,
        CharacterActor attacker,
        CharacterActor victim,
        string summary,
        string detail)
    {
        if (attacker == null
            || victim == null
            || !CharacterPersistentIdentity.TryGet(attacker, out CharacterId attackerId)
            || !CharacterPersistentIdentity.TryGet(victim, out CharacterId victimId))
        {
            throw new InvalidOperationException(
                "Persistent attacker and victim identities are required for a combat outcome.");
        }
        var actorSubject = new MigratedProducerOutcomeSubject(
            MigratedProducerOutcomeIds.CharacterKind,
            attackerId.Value,
            attacker.Identity?.DisplayName ?? attacker.name,
            MigratedProducerOutcomeIds.ActorRole);
        var targetSubject = new MigratedProducerOutcomeSubject(
            MigratedProducerOutcomeIds.CharacterKind,
            victimId.Value,
            victim.Identity?.DisplayName ?? victim.name,
            MigratedProducerOutcomeIds.TargetRole);
        MigratedProducerOutcomePayloadBuilder builder =
            migratedOutcomes.CreatePayloadBuilder(prepared);
        if (!AddParticipantAndSubject(ref builder, prepared, actorSubject)
            || !AddParticipantAndSubject(ref builder, prepared, targetSubject)
            || !builder.AddFact(new GameplayOutcomeFact(
                MigratedProducerOutcomeIds.SummaryFact,
                summary))
            || !builder.AddFact(new GameplayOutcomeFact(
                MigratedProducerOutcomeIds.DetailFact,
                detail)))
        {
            throw new InvalidOperationException(
                "The combat outcome payload exceeded its reserved capacity.");
        }
        return builder.Build();
    }

    private static bool AddParticipantAndSubject(
        ref MigratedProducerOutcomePayloadBuilder builder,
        in PreparedMigratedProducerOutcome prepared,
        in MigratedProducerOutcomeSubject subject) =>
        builder.AddParticipant(new GameplayOutcomeParticipant(
            subject.EntityId,
            subject.Role,
            GameplayParticipationKind.Direct,
            true,
            subject.DisplayName))
        && builder.AddSubject(new GameplayOutcomeSubjectLink(
            subject.EntityId,
            prepared.Salience,
            prepared.Tier,
            prepared.Tier == NarrativeMemoryTier.Core,
            false,
            0));

    private void CancelMigratedReservations(
        IReadOnlyList<PreparedMigratedProducerOutcome> prepared)
    {
        if (prepared == null || migratedOutcomes == null)
            return;
        for (int index = 0; index < prepared.Count; index++)
        {
            if (prepared[index].IsValid)
                migratedOutcomes.Cancel(prepared[index]);
        }
    }

    private void RestoreSignalSupportMutation(
        CharacterActor attacker,
        in CharacterBodyHealthMutationSnapshot snapshot,
        bool hasSnapshot,
        string reason)
    {
        if (hasSnapshot && attacker != null)
        {
            bodyHealthMutation.RestoreCombatMutation(
                attacker,
                snapshot,
                reason);
        }
    }

    private static bool TryApplyUntrackedMechanical(
        in CombatOutcomeMechanicalMutation mutation,
        out string failureReason)
    {
        if (!TryApplyMechanical(mutation, out failureReason))
            return false;
        mutation.Complete();
        return true;
    }

    private static bool TryApplyMechanical(
        in CombatOutcomeMechanicalMutation mutation,
        out string failureReason)
    {
        failureReason = string.Empty;
        try
        {
            failureReason = mutation.Apply()?.Trim() ?? string.Empty;
            if (failureReason.Length == 0)
                return true;
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException
            && exception is not StackOverflowException
            && exception is not AccessViolationException)
        {
            failureReason = "combat-mechanical-mutation-failed:"
                + exception.GetType().Name;
        }

        SafeRollbackMechanical(mutation);
        return false;
    }

    private static void SafeRollbackMechanical(
        in CombatOutcomeMechanicalMutation mutation)
    {
        try
        {
            mutation.Rollback();
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException
            && exception is not StackOverflowException
            && exception is not AccessViolationException)
        {
            Debug.LogError(
                "Combat mechanical rollback failed: "
                + exception.GetType().Name);
        }
    }

    private void ApplySignalSupport(
        CombatAttackResult result,
        CharacterActor attacker)
    {
        if ((result.SpecialEffects & CombatSpecialEffectFlags.SignalSupport) != 0
            && attacker != null)
        {
            bodyHealth.ReduceSuppression(
                attacker,
                result.StatusPotency * 100f);
        }
    }

    private float CalculateExpectedActualDamage(
        CharacterActor target,
        CombatAttackResult result,
        float currentHealth)
    {
        if (target == null || !result.Hit || result.AppliedDamage <= 0f)
            return 0f;
        CharacterBodyHealthSnapshot snapshot = bodyHealthQuery.GetSnapshot(target);
        CharacterBodyPartHealthState part = snapshot.Parts?.FirstOrDefault(
            item => item != null && item.bodyPart == result.BodyPart);
        if (part == null)
            return 0f;

        float appliedDamage = result.Nonlethal
            ? Mathf.Min(
                result.AppliedDamage,
                Mathf.Max(0f, currentHealth - 1f),
                Mathf.Max(0f, part.currentHealth - 1f))
            : result.AppliedDamage;
        bool fatalCorePart = !result.Nonlethal
            && (result.BodyPart == CombatBodyPart.Head
                || result.BodyPart == CombatBodyPart.Torso)
            && part.currentHealth - appliedDamage <= 0f;
        return fatalCorePart
            ? Mathf.Max(0f, currentHealth)
            : Mathf.Min(appliedDamage, Mathf.Max(0f, currentHealth - 1f));
    }

    private void PublishCommittedCharacterDamage(
        string attackOperationId,
        CharacterActor attacker,
        CharacterActor defender,
        float actualDamage,
        CharacterCommandOrigin origin)
    {
        if (attacker == null
            || defender == null
            || !float.IsFinite(actualDamage)
            || actualDamage <= 0f
            || attacker.Identity?.CharacterType == CharacterType.Intruder
            || defender.Identity?.CharacterType == CharacterType.Intruder
            || !CharacterPersistentIdentity.TryGet(
                attacker,
                out CharacterId attackerId)
            || !CharacterPersistentIdentity.TryGet(
                defender,
                out CharacterId defenderId))
        {
            return;
        }

        bool attackerCustomer = attacker.Identity.CharacterType
            == CharacterType.Customer;
        bool defenderCustomer = defender.Identity.CharacterType
            == CharacterType.Customer;
        bool eligibleRoles = attackerCustomer
            && (defenderCustomer
                || CharacterWorkRoleUtility.IsWorker(defender))
            || defenderCustomer
            && (attackerCustomer
                || CharacterWorkRoleUtility.IsWorker(attacker));
        if (eligibleRoles
            && !string.IsNullOrWhiteSpace(attackOperationId)
            && string.Equals(
                attackOperationId,
                attackOperationId.Trim(),
                StringComparison.Ordinal))
        {
            CharacterId customerId = defenderCustomer
                ? defenderId
                : attackerId;
            CharacterId otherParticipantId = defenderCustomer
                ? attackerId
                : defenderId;
            if (TryResolveVisitorFacilityReceipt(
                    attacker,
                    defender,
                    customerId,
                    attackerCustomer && defenderCustomer
                        ? otherParticipantId
                        : default,
                    out BuildableObject facility,
                    out CoreGridCell location))
            {
                gameEvents.Publish(
                    new VisitorFacilityCombatDamageCommittedEvent(
                        attackOperationId,
                        customerId,
                        otherParticipantId,
                        attackerCustomer
                            && customerId.Equals(attackerId),
                        actualDamage,
                        facility.PersistentInstanceId,
                        location,
                        Mathf.Max(1, calendar.Day)));
            }
        }

        gameEvents.Publish(new SocialConflictEvent(
            attackerId,
            defenderId,
            "betrayal-or-assault",
            Mathf.Clamp(actualDamage / 10f, 1f, 10f),
            origin,
            calendar.Day,
            attackOperationId));
    }

    private bool TryResolveVisitorFacilityReceipt(
        CharacterActor attacker,
        CharacterActor defender,
        CharacterId customerId,
        CharacterId secondCustomerId,
        out BuildableObject facility,
        out CoreGridCell location)
    {
        facility = null;
        location = default;
        Vector2Int attackerCell = attacker.GetNowXY();
        Vector2Int defenderCell = defender.GetNowXY();
        foreach (BuildableObject candidate in buildingWorld.Buildings
                     .Where(value => value != null
                         && value.Facility?.IsVisitorFacility == true
                         && value.PersistentInstanceId.IsValid
                         && value.IsActiveUser(customerId)
                         && (!secondCustomerId.IsValid
                             || value.IsActiveUser(secondCustomerId)))
                     .OrderBy(
                         value => value.PersistentInstanceId.Value,
                         StringComparer.Ordinal))
        {
            FacilityRoomOperationalProfile profile =
                roomFacilityPolicy.GetOperationalProfile(candidate);
            if (profile?.Room == null
                || !profile.Room.ContainsCell(attackerCell)
                || !profile.Room.ContainsCell(defenderCell))
            {
                continue;
            }

            facility = candidate;
            location = new CoreGridCell(defenderCell.x, defenderCell.y);
            return true;
        }

        return false;
    }

    private void PublishKilledEvent(
        CharacterActor killer,
        CharacterActor victim,
        CharacterCommandOrigin origin)
    {
        if (killer == null || victim == null
            || !CharacterPersistentIdentity.TryGet(killer, out CharacterId killerId)
            || !CharacterPersistentIdentity.TryGet(victim, out CharacterId victimId))
        {
            return;
        }

        const float witnessRadius = 12f;
        float radiusSquared = witnessRadius * witnessRadius;
        CharacterId[] witnesses = characterWorld.Characters
            .Where(actor => actor != null && !actor.IsDead && actor != killer && actor != victim)
            .Where(actor => (actor.transform.position - victim.transform.position).sqrMagnitude <= radiusSquared)
            .Select(actor => CharacterPersistentIdentity.TryGet(actor, out CharacterId id)
                ? id
                : default)
            .Where(id => id.IsValid)
            .OrderBy(id => id.Value, StringComparer.Ordinal)
            .ToArray();

        gameEvents.Publish(new CharacterKilledEvent(
            killerId,
            victimId,
            witnesses,
            wasHostile: true,
            wasPrisoner: false,
            wasInnocent: false,
            origin,
            calendar.Day));
    }

    public bool TryApplyArmorDurabilityDamage(
        CombatAttackResult result,
        out string failureReason)
    {
        failureReason = string.Empty;
        foreach (CombatArmorDurabilityHit hit in result.ArmorDurabilityHits)
        {
            if (!equipment.TryApplyDurabilityDamage(hit.InstanceId, hit.Damage))
            {
                failureReason = "combat-armor-durability-apply-failed:"
                    + hit.InstanceId;
                return false;
            }
        }
        if (result.ArmorDurabilityHits.Count == 0
            && !string.IsNullOrWhiteSpace(result.ArmorInstanceId))
        {
            if (!equipment.TryApplyDurabilityDamage(
                    result.ArmorInstanceId,
                    result.ArmorDurabilityDamage))
            {
                failureReason = "combat-armor-durability-apply-failed:"
                    + result.ArmorInstanceId;
                return false;
            }
        }
        return true;
    }

    public void ApplyArmorDurabilityDamage(CombatAttackResult result) =>
        TryApplyArmorDurabilityDamage(result, out _);
}
