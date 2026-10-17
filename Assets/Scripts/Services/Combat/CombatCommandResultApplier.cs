using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
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
    private readonly CombatActionPublicationRuntime publications;
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
        CombatActionPublicationRuntime publications,
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
        this.publications = publications
            ?? throw new ArgumentNullException(nameof(publications));
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
        CombatParticipantRef target, string attackOperationId, long attackRevision,
        out ReservedCombatAction reserved, out bool capacityDeferred, out string failureReason,
        CombatOutcomeParentContext parent = default)
    {
        reserved = default; capacityDeferred = false;
        try
        {
            if (!target.IsValid) { failureReason = "combat-action-target-invalid"; return false; }
            var proposal = CombatActionFact.Proposal(attackOperationId, attackRevision,
                Mathf.Max(1, calendar.Day), CaptureParticipant(target), parent.Snapshot());
            bool accepted = publications.TryReserve(proposal, out reserved, out failureReason);
            capacityDeferred = !accepted && failureReason.Contains("CapacityDeferred", StringComparison.Ordinal);
            return accepted;
        }
        catch (Exception exception) when (IsRecoverable(exception))
        { failureReason = "combat-action-reserve:" + exception.Message; return false; }
    }

    public void CancelDamageOutcome(in ReservedCombatAction reserved) => publications.Cancel(reserved);

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
        in ReservedCombatAction reservedDamage,
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
            CombatActionEvidenceData.CaptureUnknownRequest(result, damageType),
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
        in ReservedCombatAction reservedDamage,
        in CombatOutcomeMechanicalMutation mechanicalMutation,
        in CombatActionEvidenceData evidence)
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
            evidence,
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
        in ReservedCombatAction reservedDamage,
        in CombatOutcomeMechanicalMutation mechanicalMutation,
        string damageSource,
        CharacterCommandOrigin origin)
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
            CombatActionEvidenceData.CaptureUnknownRequest(result, damageType),
            damageSource,
            origin);
    }

    public CombatOutcomeApplyResult Apply(
        CombatParticipantRef target,
        CombatAttackResult result,
        CharacterActor attacker,
        string attackerName,
        CombatDamageType damageType,
        string attackOperationId,
        long attackRevision,
        in ReservedCombatAction reservedDamage,
        in CombatOutcomeMechanicalMutation mechanicalMutation,
        in CombatActionEvidenceData evidence,
        string damageSource,
        CharacterCommandOrigin origin,
        CombatOutcomeParentContext parent = default)
    {
        return Apply(
            target,
            result,
            new CombatParticipantRef(attacker),
            attackerName,
            damageType,
            attackOperationId,
            attackRevision,
            reservedDamage,
            mechanicalMutation,
            evidence,
            damageSource,
            origin,
            parent);
    }

    public CombatOutcomeApplyResult Apply(
        CombatParticipantRef target, CombatAttackResult result, CombatParticipantRef attacker,
        string attackerName, CombatDamageType damageType, string attackOperationId, long attackRevision,
        in ReservedCombatAction reservedDamage, in CombatOutcomeMechanicalMutation mechanicalMutation,
        in CombatActionEvidenceData evidence, string damageSource, CharacterCommandOrigin origin,
        CombatOutcomeParentContext parent = default)
    {
        var reserved = reservedDamage;
        var mechanical = mechanicalMutation;
        CharacterActor attackerCharacter = attacker.Character;
        CharacterBodyHealthMutationSnapshot targetBefore = default, attackerBefore = default;
        WildlifeActor.PreparedCombatDamage wildlifeMutation = null;
        bool bodyTouched = false, signalTouched = false, mechanicalTouched = false, accepted = false;
        CombatAttackResult applied = result;
        try
        {
            if (!reserved.IsValid || !mechanical.IsValid || !result.Executed || !target.IsValid || !attacker.IsValid
                || !DomainFactValueSyntax.IsIdentity(attacker.Id) || string.IsNullOrWhiteSpace(damageSource)
                || reserved.OperationId != attackOperationId || reserved.AttackRevision != attackRevision
                || evidence == null || !evidence.IsValid || evidence.damageType != damageType
                || evidence.executed != result.Executed || evidence.hit != result.Hit || evidence.coverBlocked != result.CoverBlocked
                || evidence.bodyPart != result.BodyPart || evidence.appliedDamage != result.AppliedDamage)
                throw new InvalidOperationException("combat-action-input-or-reservation-invalid");
            var data = reserved.Proposal.Snapshot();
            data.attacker = CaptureParticipant(attacker); data.target = CaptureParticipant(target);
            data.evidence = evidence.Clone(); data.damageSource = damageSource.Trim(); data.origin = origin;
            data.hasConflict = parent.HasParent; data.conflict = parent.Snapshot() ?? new CombatConflictFactData();
            bool hasSignal = attackerCharacter != null && (result.SpecialEffects & CombatSpecialEffectFlags.SignalSupport) != 0;
            if (hasSignal) attackerBefore = bodyHealthMutation.CaptureCombatMutation(attackerCharacter);
            if (target.IsCharacter)
            {
                applied = result.CoverBlocked ? result.WithAppliedDamageMultiplier(0f)
                    : damageType == CombatDamageType.Blunt ? result.WithAppliedDamageMultiplier(
                        target.Character.GetDetailedStatMultiplier("damage:blunt-taken")) : result;
                targetBefore = bodyHealthMutation.CaptureCombatMutation(target.Character);
                data.healthBefore = targetBefore.State.currentHealth; data.maxHealthBefore = targetBefore.State.maxHealth;
                data.downedBefore = targetBefore.State.downed;
                Vector2Int cell = target.Character.GetNowXY(); data.targetX = cell.x; data.targetY = cell.y;
                float expected = CalculateExpectedActualDamage(target.Character, applied, data.healthBefore);
                bodyTouched = true;
                bodyHealthMutation.ApplyPreparedCombatMutation(target.Character, applied, data.damageSource);
                var after = bodyHealthMutation.CaptureCombatMutation(target.Character);
                data.healthAfter = after.State.currentHealth; data.maxHealthAfter = after.State.maxHealth; data.downedAfter = after.State.downed;
                if (!Mathf.Approximately(data.healthBefore - data.healthAfter, expected))
                    throw new InvalidOperationException("combat-action-post-clamp-mismatch");
            }
            else
            {
                var animal = target.Wildlife;
                data.healthBefore = animal.CurrentHealth; data.maxHealthBefore = animal.MaxHealth;
                data.targetX = animal.GridPosition.x; data.targetY = animal.GridPosition.y;
                wildlifeMutation = animal.ApplyPreparedCombatDamage(result, attackerCharacter);
                data.healthAfter = animal.CurrentHealth; data.maxHealthAfter = animal.MaxHealth;
            }
            if (hasSignal)
            {
                signalTouched = true;
                bodyHealthMutation.ApplyPreparedSuppressionReduction(attackerCharacter, result.StatusPotency * 100f);
            }
            mechanicalTouched = true;
            string reason = mechanical.Apply();
            if (!string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException(reason);
            var fact = new CombatActionFact(data);
            var delivery = publications.Publish(reserved, fact);
            accepted = delivery.MayBeCommitted;
            if (!accepted) throw new InvalidOperationException("combat-action-observation-rejected:" + delivery.Detail);

            // Delivery may be pending, but the owned effect must not be repeated.
            mechanical.Complete();
            if (target.IsCharacter)
                bodyHealthMutation.CompletePreparedCombatMutation(target.Character, targetBefore, applied, data.damageSource);
            else target.Wildlife.CompletePreparedCombatDamage(wildlifeMutation);
            if (hasSignal) bodyHealthMutation.CompletePreparedSuppressionReduction(attackerCharacter, attackerBefore);
            if (result.CoverBlocked)
            {
                coverDurability.TryApplyDamage(result.CoverSourceId, result.CoverDamage);
                CombatImpactPresentation.Play(target.IsCharacter ? target.Character.transform.position : target.Wildlife.transform.position,
                    damageType, gameClock, worldUiHierarchy, coverHit: true);
            }
            float damage = data.healthBefore - data.healthAfter;
            if (target.IsCharacter && damage > 0f)
            {
                CharacterId attackerId = attackerCharacter != null && CharacterPersistentIdentity.TryGet(attackerCharacter, out var identity)
                    ? identity : default;
                identityEvents?.Publish(new CharacterInjuredIdentityEvent(new CharacterId(data.target.id), attackerId,
                    damageType, damage, data.absoluteDay));
                PublishCommittedCharacterDamage(attackOperationId, attackerCharacter, target.Character, damage, origin);
                DefenseCombatPresentation.Ensure(target.Character)?.PlayHit(applied.AppliedDamage, damageType, worldUiHierarchy);
                if (data.healthBefore > 0 && data.healthAfter <= 0) PublishKilledEvent(attackerCharacter, target.Character, origin);
            }
            return CombatOutcomeApplyResult.Success();
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            if (accepted)
            {
                // This is a visible post-acceptance lifecycle failure, not permission
                // to undo damage already observed or retry its mechanical mutation.
                Debug.LogError("combat-action-post-acceptance-completion-failed:" + attackOperationId + ":" + exception);
                return CombatOutcomeApplyResult.Success();
            }
            if (bodyTouched) bodyHealthMutation.RestoreCombatMutation(target.Character, targetBefore, "combat-action-rejected");
            if (wildlifeMutation != null) target.Wildlife.RollbackPreparedCombatDamage(wildlifeMutation);
            if (signalTouched) RestoreSignalSupportMutation(attackerCharacter, attackerBefore, true, "combat-action-signal-rejected");
            if (mechanicalTouched) SafeRollbackMechanical(mechanical);
            else if (mechanical.IsValid) SafeRollbackMechanical(mechanical); // detached resolver state also needs cancellation
            return CombatOutcomeApplyResult.Failed("combat-action-failed:" + exception.Message);
        }
        finally { publications.Cancel(reserved); }
    }

    private static CombatFactParticipantData CaptureParticipant(in CombatParticipantRef participant)
    {
        if (!participant.IsValid) throw new InvalidOperationException("combat-action-participant-invalid");
        return new CombatFactParticipantData { kind = participant.IsCharacter ? CombatFactParticipantKind.Character : CombatFactParticipantKind.Wildlife,
            id = participant.Id, name = participant.DisplayName };
    }
    private static bool IsRecoverable(Exception value) => value is not OutOfMemoryException
        && value is not StackOverflowException && value is not AccessViolationException;

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
        // Predict the owned health value first, using the same float subtraction
        // and floor as ApplyPreparedCombatMutation. The ledger records the actual
        // representable health delta, not the unquantized requested damage.
        float healthAfter = fatalCorePart
            ? 0f
            : Mathf.Max(1f, currentHealth - appliedDamage);
        return Mathf.Max(0f, currentHealth - healthAfter);
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
