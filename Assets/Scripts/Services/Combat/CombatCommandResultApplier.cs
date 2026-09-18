using System;
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
        CharacterIdentityEventPublisher identityEvents = null)
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
    }

    public bool TryReserveDamageOutcome(
        CombatParticipantRef target,
        string attackOperationId,
        long attackRevision,
        out ReservedCombatDamageOutcome reserved,
        out bool capacityDeferred,
        out string failureReason)
    {
        if (!target.IsCharacter)
        {
            reserved = default;
            capacityDeferred = false;
            failureReason = string.Empty;
            return true;
        }

        return damageOutcomes.TryReserve(
            attackOperationId,
            attackRevision,
            Mathf.Max(1, calendar.Day),
            out reserved,
            out capacityDeferred,
            out failureReason);
    }

    public void CancelDamageOutcome(in ReservedCombatDamageOutcome reserved) =>
        damageOutcomes.Cancel(reserved);

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
                    PublishKilledEvent(attacker, target.Character);
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
        bool signalAppliedInTransaction = false;
        if (!mechanicalMutation.IsValid)
        {
            damageOutcomes.Cancel(reservedDamage);
            return CombatOutcomeApplyResult.Failed(
                "combat-mechanical-mutation-boundary-missing");
        }
        if (result.CoverBlocked)
        {
            damageOutcomes.Cancel(reservedDamage);
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
                    damageOutcomes.Cancel(reservedDamage);
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
                        $"직접 전투 명령: {attackerName}");
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
                    damageOutcomes.Cancel(reservedDamage);
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
                    damageOutcomes.Cancel(reservedDamage);
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
                    SafeRollbackMechanical(mechanicalMutation);
                    return CombatOutcomeApplyResult.Failed(
                        "combat-health-rollback-capture-failed:" + exception.Message);
                }
                try
                {
                    bodyHealthMutation.ApplyPreparedCombatMutation(
                        target.Character,
                        appliedResult,
                        $"직접 전투 명령: {attackerName}");
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
                    return CombatOutcomeApplyResult.Failed(mechanicalFailure);
                }
                if (!damageOutcomes.TryCommit(
                        prepared,
                        attackRevision,
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
                    $"직접 전투 명령: {attackerName}");
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
                    actualDamage);
                DefenseCombatPresentation.Ensure(target.Character)?.PlayHit(
                    appliedResult.AppliedDamage,
                    damageType,
                    worldUiHierarchy);
                if (wasAlive && target.Character.IsDead)
                {
                    PublishKilledEvent(attacker, target.Character);
                }
            }
            else
            {
                damageOutcomes.Cancel(reservedDamage);
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
            if (!TryApplyUntrackedMechanical(
                    mechanicalMutation,
                    out string mechanicalFailure))
            {
                return CombatOutcomeApplyResult.Failed(mechanicalFailure);
            }
            target.Wildlife.ApplyCombatDamage(result, attacker);
        }
        if (!signalAppliedInTransaction)
            ApplySignalSupport(result, attacker);
        return CombatOutcomeApplyResult.Success();
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
        float actualDamage)
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
            CharacterCommandOrigin.DirectPlayerOrder,
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

    private void PublishKilledEvent(CharacterActor killer, CharacterActor victim)
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
            CharacterCommandOrigin.DirectPlayerOrder,
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
