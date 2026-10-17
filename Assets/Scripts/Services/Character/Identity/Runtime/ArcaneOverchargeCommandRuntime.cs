using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;

public interface IArcaneOverchargeCommand
{
    bool TryActivate(
        CharacterActor actor,
        string equipmentInstanceId,
        out ArcaneOverchargeActivation activation,
        out string failureReason);
}

public interface IArcaneOverchargeSelfDamagePreparation
{
    float PreviousHealth { get; }
    float ResultingHealth { get; }
    void Complete();
    bool TryRollback(out string failureReason);
}

public interface IArcaneOverchargeSelfDamageRuntime
{
    bool TryPrepare(
        CharacterActor actor,
        float amount,
        out IArcaneOverchargeSelfDamagePreparation preparation,
        out string failureReason);
}

public sealed class ArcaneOverchargeSelfDamageRuntime :
    IArcaneOverchargeSelfDamageRuntime
{
    private sealed class Preparation :
        IArcaneOverchargeSelfDamagePreparation
    {
        private readonly ICharacterBodyHealthMutationTransaction transaction;
        private readonly CharacterActor actor;
        private readonly CharacterBodyHealthMutationSnapshot before;
        private readonly CharacterPreparedAggregateDamageReceipt receipt;
        private bool finished;

        internal Preparation(
            ICharacterBodyHealthMutationTransaction transaction,
            CharacterActor actor,
            in CharacterBodyHealthMutationSnapshot before,
            in CharacterPreparedAggregateDamageReceipt receipt)
        {
            this.transaction = transaction;
            this.actor = actor;
            this.before = before;
            this.receipt = receipt;
        }

        public float PreviousHealth => receipt.PreviousHealth;
        public float ResultingHealth => receipt.ResultingHealth;

        public void Complete()
        {
            if (finished)
                return;
            transaction.CompletePreparedAggregateDamage(actor, receipt);
            finished = true;
        }

        public bool TryRollback(out string failureReason)
        {
            failureReason = string.Empty;
            if (finished)
            {
                failureReason = "arcane-overcharge.self-damage-already-completed";
                return false;
            }
            try
            {
                transaction.RestoreCombatMutation(
                    actor,
                    before,
                    "arcane-overcharge:self-damage-rollback");
                finished = true;
                return true;
            }
            catch (Exception exception)
            {
                failureReason =
                    "arcane-overcharge.self-damage-rollback-failed:"
                    + exception.GetType().Name;
                return false;
            }
        }
    }

    private readonly ICharacterBodyHealthMutationTransaction transaction;

    public ArcaneOverchargeSelfDamageRuntime(
        ICharacterBodyHealthMutationTransaction transaction)
    {
        this.transaction = transaction
            ?? throw new ArgumentNullException(nameof(transaction));
    }

    public bool TryPrepare(
        CharacterActor actor,
        float amount,
        out IArcaneOverchargeSelfDamagePreparation preparation,
        out string failureReason)
    {
        preparation = null;
        failureReason = string.Empty;
        if (actor == null || !float.IsFinite(amount) || amount <= 0f)
        {
            failureReason = "arcane-overcharge.self-damage-invalid";
            return false;
        }

        CharacterBodyHealthMutationSnapshot before = default;
        bool captured = false;
        try
        {
            before = transaction.CaptureCombatMutation(actor);
            if (!before.IsValid)
            {
                failureReason = "arcane-overcharge.self-damage-unavailable";
                return false;
            }
            captured = true;
            CharacterPreparedAggregateDamageReceipt receipt =
                transaction.ApplyPreparedAggregateDamage(
                    actor,
                    amount,
                    CharacterDeathCauseCode.Combat,
                    "trait:arcane-overcharge",
                    allowDeath: true);
            if (!receipt.IsValid || receipt.AppliedDamage <= 0f)
            {
                transaction.RestoreCombatMutation(
                    actor,
                    before,
                    "arcane-overcharge:self-damage-rejected");
                failureReason = "arcane-overcharge.self-damage-rejected";
                return false;
            }
            preparation = new Preparation(
                transaction,
                actor,
                before,
                receipt);
            return true;
        }
        catch (Exception exception)
        {
            if (captured)
            {
                try
                {
                    transaction.RestoreCombatMutation(
                        actor,
                        before,
                        "arcane-overcharge:self-damage-exception");
                }
                catch (Exception rollbackException)
                {
                    failureReason =
                        "arcane-overcharge.self-damage-and-rollback-failed:"
                        + exception.GetType().Name
                        + ":"
                        + rollbackException.GetType().Name;
                    return false;
                }
            }
            failureReason = "arcane-overcharge.self-damage-rejected:"
                + exception.GetType().Name;
            return false;
        }
    }
}

public sealed class ArcaneOverchargeCommandRuntime : IArcaneOverchargeCommand
{
    private readonly ExtremeTraitRuntime extremeTraits;
    private readonly ICombatEquipmentRuntime equipment;
    private readonly IGameClock gameClock;
    private readonly ICharacterManaQuery mana;
    private readonly IArcaneOverchargeSelfDamageRuntime selfDamage;

    public ArcaneOverchargeCommandRuntime(
        ExtremeTraitRuntime extremeTraits,
        ICombatEquipmentRuntime equipment,
        IGameClock gameClock,
        ICharacterManaQuery mana,
        IArcaneOverchargeSelfDamageRuntime selfDamage)
    {
        this.extremeTraits = extremeTraits
            ?? throw new ArgumentNullException(nameof(extremeTraits));
        this.equipment = equipment
            ?? throw new ArgumentNullException(nameof(equipment));
        this.gameClock = gameClock
            ?? throw new ArgumentNullException(nameof(gameClock));
        this.mana = mana ?? throw new ArgumentNullException(nameof(mana));
        this.selfDamage = selfDamage
            ?? throw new ArgumentNullException(nameof(selfDamage));
    }

    [GameplayEntryPoint(
        "StaffManagementSurfacePanel overcharge button; V26 extreme-trait focused audit")]
    public bool TryActivate(
        CharacterActor actor,
        string equipmentInstanceId,
        out ArcaneOverchargeActivation activation,
        out string failureReason)
    {
        activation = default;
        failureReason = string.Empty;
        if (actor == null
            || string.IsNullOrWhiteSpace(actor.Identity?.PersistentId))
        {
            failureReason = "유효한 시전자가 필요합니다.";
            return false;
        }
        string instanceId = equipmentInstanceId?.Trim() ?? string.Empty;
        if (!IsEquippedBy(actor.Identity.PersistentId, instanceId)
            || !equipment.TryGetDerivedStats(
                instanceId,
                out CombatEquipmentDerivedStats stats)
            || !CharacterArcaneWeaponRules.IsArcane(stats))
        {
            failureReason = "시전자가 실제 착용한 마력 장비가 필요합니다.";
            return false;
        }
        if (!extremeTraits.TryBeginOrResumeArcaneOvercharge(
                actor,
                $"arcane-overcharge:{actor.Identity.PersistentId}:"
                    + Math.Max(0, (int)(gameClock.Time * 1000f)),
                instanceId,
                mana.GetMana(actor).Ratio,
                gameClock.Time,
                out ArcaneOverchargeOperationSnapshot operation))
        {
            failureReason = "마력이 30% 미만이 아니거나 과충전 후유증이 남아 있습니다.";
            return false;
        }

        activation = new ArcaneOverchargeActivation(
            operation.SelfDamageFraction,
            operation.EquipmentDurabilityFraction);
        if (!operation.SelfDamageApplied)
        {
            if (!selfDamage.TryPrepare(
                    actor,
                    actor.MaxHealth * operation.SelfDamageFraction,
                    out IArcaneOverchargeSelfDamagePreparation preparation,
                    out string selfDamageFailure))
            {
                extremeTraits.TryRecordArcaneOverchargeFailure(
                    actor,
                    operation.ActivationId,
                    "self-damage-rejected");
                failureReason = string.IsNullOrWhiteSpace(selfDamageFailure)
                    ? "과충전 자기 피해 적용이 거절되었습니다."
                    : selfDamageFailure;
                return false;
            }
            if (!extremeTraits.TryRecordArcaneOverchargeSelfDamage(
                    actor,
                    operation.ActivationId,
                    preparation.PreviousHealth,
                    preparation.ResultingHealth))
            {
                bool rolledBack = preparation.TryRollback(
                    out string rollbackFailure);
                failureReason = rolledBack
                    ? "과충전 자기 피해 결과 기록이 거절되었습니다."
                    : rollbackFailure;
                return false;
            }
            preparation.Complete();
            if (!extremeTraits.TryGetArcaneOverchargeOperation(
                    actor,
                    out operation))
            {
                failureReason = "과충전 자기 피해 결과를 다시 읽을 수 없습니다.";
                return false;
            }
        }

        if (!operation.EquipmentDurabilityApplied)
        {
            if (!equipment.TryGetInstance(
                    instanceId,
                    out CombatEquipmentInstance beforeEquipment))
            {
                extremeTraits.TryRecordArcaneOverchargeFailure(
                    actor,
                    operation.ActivationId,
                    "equipment-durability-rejected");
                failureReason = "과충전 장비 내구도 적용이 거절되었습니다.";
                return false;
            }
            bool durabilityApplied = equipment
                .TryApplyArcaneOverchargeDurabilityDamage(
                    instanceId,
                    stats.MaxDurability
                        * operation.EquipmentDurabilityFraction);
            bool capturedAfter = equipment.TryGetInstance(
                instanceId,
                out CombatEquipmentInstance afterEquipment);
            if (!durabilityApplied
                || !capturedAfter
                || afterEquipment.durabilityRatio
                    >= beforeEquipment.durabilityRatio)
            {
                bool rollbackRequired = durabilityApplied;
                bool rolledBack = !rollbackRequired
                    || equipment.TryRollbackArcaneOverchargeDurabilityDamage(
                        instanceId,
                        beforeEquipment.durabilityRatio);
                extremeTraits.TryRecordArcaneOverchargeFailure(
                    actor,
                    operation.ActivationId,
                    rolledBack
                        ? "equipment-durability-rejected"
                        : "equipment-durability-rollback-failed");
                failureReason = rolledBack
                    ? "과충전 장비 내구도 적용이 거절되었습니다."
                    : "과충전 장비 내구도 적용 검증과 되돌리기가 모두 실패했습니다.";
                return false;
            }
            if (!extremeTraits.TryRecordArcaneOverchargeDurability(
                    actor,
                    operation.ActivationId,
                    beforeEquipment.durabilityRatio,
                    afterEquipment.durabilityRatio))
            {
                bool rolledBack = equipment
                    .TryRollbackArcaneOverchargeDurabilityDamage(
                        instanceId,
                        beforeEquipment.durabilityRatio);
                failureReason = rolledBack
                    ? "과충전 장비 내구도 결과 기록이 거절되었습니다."
                    : "과충전 장비 내구도 결과 기록과 되돌리기가 모두 실패했습니다.";
                return false;
            }
        }

        if (!extremeTraits.TryCompleteArcaneOvercharge(
                actor,
                operation.ActivationId))
        {
            failureReason = "과충전 효과 완료 상태를 기록하지 못했습니다.";
            return false;
        }
        return true;
    }

    private bool IsEquippedBy(string characterId, string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            return false;
        CharacterCombatLoadoutProfile loadout =
            equipment.GetActiveProfileSnapshot(characterId);
        IEnumerable<string> equipped = (loadout?.weaponInstanceIds
                ?? new List<string>())
            .Concat(loadout?.armorInstanceIds ?? new List<string>())
            .Append(loadout?.shieldInstanceId);
        return equipped.Any(value => string.Equals(
            value?.Trim(),
            instanceId,
            StringComparison.Ordinal));
    }
}
