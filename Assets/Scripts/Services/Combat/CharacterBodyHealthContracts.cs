using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class CharacterBodyHealthState
{
    public string characterId = string.Empty;
    [Min(1f)] public float maxHealth = 100f;
    [Min(0f)] public float currentHealth = 100f;
    [Range(0f, 1f)] public float injurySeverity;
    public string anatomyProfileId = string.Empty;
    public List<CharacterBodyPartHealthState> parts = new List<CharacterBodyPartHealthState>();
    public List<AnatomyNodeHealthState> anatomyNodes = new List<AnatomyNodeHealthState>();
    [Range(0f, 100f)] public float bloodLoss;
    [Range(0f, 100f)] public float suppression;
    [Min(0f)] public float burningDamagePerSecond;
    [Min(0f)] public float burningRemainingSeconds;
    [Range(0f, 1f)] public float sedationRatio;
    [Min(0f)] public float sedationRemainingSeconds;
    [Min(0f)] public float manaBlockedRemainingSeconds;
    [Min(1f)] public float maxMana = 100f;
    [Min(0f)] public float currentMana = 100f;
    public bool downed;
    public string lastDamageReason = string.Empty;
}

[Serializable]
public sealed class DungeonCharacterBodyHealthSaveData
{
    public const int CurrentVersion = 7;

    public int version = CurrentVersion;
    public List<CharacterBodyHealthState> characters = new List<CharacterBodyHealthState>();
    public List<CharacterHealingPendingFactData> pendingHealingFacts = new();
    public List<CharacterFatalityPendingFactData> pendingFatalityFacts = new();
}

public readonly struct CharacterBodyHealthDownedEvent
{
    public CharacterBodyHealthDownedEvent(CharacterActor actor)
    {
        Actor = actor;
    }

    public CharacterActor Actor { get; }
}

public readonly struct CharacterBodyHealthRecoveredEvent
{
    public CharacterBodyHealthRecoveredEvent(CharacterActor actor)
    {
        Actor = actor;
    }

    public CharacterActor Actor { get; }
}

public interface ICharacterBodyHealthQuery
{
    CharacterVitalsSnapshot GetVitals(CharacterActor actor);
    CharacterVitalsSnapshot GetVitals(string characterId);
    CharacterBodyHealthSnapshot GetSnapshot(CharacterActor actor);
    CharacterBodyHealthSnapshot GetSnapshot(string characterId);
    float GetTotalBleeding(CharacterActor target);
    float GetMissingPartHealth(CharacterActor target);
}

public readonly struct CharacterCombatSpecialStatusSnapshot
{
    public CharacterCombatSpecialStatusSnapshot(
        float burningDamagePerSecond,
        float burningRemainingSeconds,
        float sedationRatio,
        float sedationRemainingSeconds,
        float manaBlockedRemainingSeconds)
    {
        BurningDamagePerSecond = Mathf.Max(0f, burningDamagePerSecond);
        BurningRemainingSeconds = Mathf.Max(0f, burningRemainingSeconds);
        SedationRatio = Mathf.Clamp01(sedationRatio);
        SedationRemainingSeconds = Mathf.Max(0f, sedationRemainingSeconds);
        ManaBlockedRemainingSeconds = Mathf.Max(0f, manaBlockedRemainingSeconds);
    }

    public float BurningDamagePerSecond { get; }
    public float BurningRemainingSeconds { get; }
    public float SedationRatio { get; }
    public float SedationRemainingSeconds { get; }
    public float ManaBlockedRemainingSeconds { get; }
    public bool IsManaBlocked => ManaBlockedRemainingSeconds > 0f;
}

public interface ICharacterCombatSpecialStatusQuery
{
    CharacterCombatSpecialStatusSnapshot GetCombatSpecialStatus(
        CharacterId characterId);
}

public readonly struct CharacterManaSnapshot
{
    public CharacterManaSnapshot(float current, float maximum, bool recoveryBlocked)
    {
        Maximum = Mathf.Max(1f, maximum);
        Current = Mathf.Clamp(current, 0f, Maximum);
        RecoveryBlocked = recoveryBlocked;
    }

    public float Current { get; }
    public float Maximum { get; }
    public float Ratio => Current / Maximum;
    public bool RecoveryBlocked { get; }
}

public interface ICharacterManaQuery
{
    CharacterManaSnapshot GetMana(CharacterActor actor);
    bool CanSpendMana(CharacterActor actor, float amount, out string failureReason);
}

public interface ICharacterManaCommand
{
    bool TrySpendMana(CharacterActor actor, float amount, out string failureReason);
    void RefundFailedManaSpend(CharacterActor actor, float amount);
}

public interface ICharacterBodyHealthCommand
{
    void ConfigureVitals(CharacterActor actor, float maximumHealth, bool resetCurrentHealth);
    void RefreshMaximumHealthPreservingCurrent(
        CharacterActor actor,
        float maximumHealth);
    void RestoreLegacyVitalsProjection(
        CharacterActor actor,
        float maximumHealth,
        float currentHealth,
        float injurySeverity);
    void ApplyLegacyDamage(
        CharacterActor actor,
        float amount,
        string reason,
        bool allowDeath);
    void ApplyLegacyDamageWithCause(
        CharacterActor actor,
        float amount,
        CharacterDeathCauseCode deathCause,
        string reasonCode,
        bool allowDeath);
    void ApplyLocalizedDamage(
        CharacterActor actor,
        CombatBodyPart bodyPart,
        float amount,
        string reason,
        bool allowDeath);
    void HealLegacyVitals(CharacterActor actor, float amount);
    void ScaleLegacyVitals(CharacterActor actor, float multiplier);
    void SetLegacyInjurySeverity(CharacterActor actor, float injurySeverity);
    void Kill(CharacterActor actor, string reason);
    void Kill(
        CharacterActor actor,
        CharacterDeathCauseCode cause,
        string reasonCode);
    void ApplyCombatResult(CharacterActor target, CombatAttackResult result, string reason);
    void ApplySnapshot(CharacterActor target, CharacterBodyHealthSnapshot snapshot, string reason);
    void AddSuppression(CharacterActor target, float amount);
    void ReduceSuppression(CharacterActor target, float amount);
    void Heal(CharacterActor target, float amount, bool stopBleeding);
    bool Stabilize(CharacterActor target);
    bool ApplyTreatment(CharacterActor target, float partHealthAmount, float bloodLossReduction);
}

public readonly struct CharacterBodyHealthMutationSnapshot
{
    internal CharacterBodyHealthMutationSnapshot(
        CharacterId characterId,
        CharacterBodyHealthState state)
    {
        CharacterId = characterId;
        State = state;
    }

    public CharacterId CharacterId { get; }
    internal CharacterBodyHealthState State { get; }
    public bool IsValid => CharacterId.IsValid && State != null;
}

public readonly struct CharacterPreparedAggregateDamageReceipt
{
    public CharacterPreparedAggregateDamageReceipt(
        CharacterId characterId,
        float requestedDamage,
        float previousHealth,
        float resultingHealth,
        float maximumHealth,
        CharacterDeathCauseCode deathCause,
        string reasonCode,
        bool allowDeath)
    {
        CharacterId = characterId;
        RequestedDamage = Mathf.Max(0f, requestedDamage);
        PreviousHealth = Mathf.Max(0f, previousHealth);
        ResultingHealth = Mathf.Max(0f, resultingHealth);
        MaximumHealth = Mathf.Max(1f, maximumHealth);
        DeathCause = deathCause;
        ReasonCode = reasonCode ?? string.Empty;
        AllowDeath = allowDeath;
    }

    public CharacterId CharacterId { get; }
    public float RequestedDamage { get; }
    public float PreviousHealth { get; }
    public float ResultingHealth { get; }
    public float MaximumHealth { get; }
    public CharacterDeathCauseCode DeathCause { get; }
    public string ReasonCode { get; }
    public bool AllowDeath { get; }
    public float AppliedDamage => Mathf.Max(0f, PreviousHealth - ResultingHealth);
    public bool IsValid => CharacterId.IsValid
        && RequestedDamage > 0f
        && PreviousHealth > 0f
        && MaximumHealth >= PreviousHealth
        && ResultingHealth <= PreviousHealth
        && !string.IsNullOrWhiteSpace(ReasonCode);
}

public interface ICharacterBodyHealthMutationTransaction
{
    void ApplyPreparedMedicalStabilization(CharacterActor actor);
    void ApplyPreparedMedicalTreatment(
        CharacterActor actor,
        float partHealthAmount,
        float bloodLossReduction);
    void CompletePreparedMedicalMutation(
        CharacterActor actor,
        in CharacterBodyHealthMutationSnapshot before);
    CharacterBodyHealthMutationSnapshot CaptureCombatMutation(
        CharacterActor actor);
    void RestoreCombatMutation(
        CharacterActor actor,
        in CharacterBodyHealthMutationSnapshot snapshot,
        string reason);
    void ApplyPreparedCombatMutation(
        CharacterActor actor,
        CombatAttackResult result,
        string reason);
    void CompletePreparedCombatMutation(
        CharacterActor actor,
        in CharacterBodyHealthMutationSnapshot before,
        CombatAttackResult result,
        string reason);
    CharacterPreparedAggregateDamageReceipt ApplyPreparedAggregateDamage(
        CharacterActor actor,
        float amount,
        CharacterDeathCauseCode deathCause,
        string reasonCode,
        bool allowDeath);
    void CompletePreparedAggregateDamage(
        CharacterActor actor,
        in CharacterPreparedAggregateDamageReceipt receipt);
    void ApplyPreparedSuppressionReduction(
        CharacterActor actor,
        float amount);
    void CompletePreparedSuppressionReduction(
        CharacterActor actor,
        in CharacterBodyHealthMutationSnapshot before);
}

public interface IAgeConditionAnatomyMutationTransaction
{
    CharacterPreparedAgeConditionAnatomyReceipt
        ApplyPreparedAgeConditionAnatomy(
            CharacterActor actor,
            IReadOnlyList<string> nodeIds,
            float damageFraction,
            bool destroyNode,
            string reasonCode);
    void CompletePreparedAgeConditionAnatomy(
        CharacterActor actor,
        in CharacterPreparedAgeConditionAnatomyReceipt receipt);
    void RestorePreparedAgeConditionAnatomy(
        CharacterActor actor,
        in CharacterBodyHealthMutationSnapshot snapshot,
        string reason);
}

public interface IDiseaseAnatomyBurdenMutationTransaction
{
    CharacterPreparedDiseaseAnatomyBurdenReceipt
        ApplyPreparedDiseaseAnatomyBurden(
            CharacterActor actor,
            string nodeId,
            float infectionBurden,
            string reasonCode);
    void RestorePreparedDiseaseAnatomyBurden(
        CharacterActor actor,
        in CharacterBodyHealthMutationSnapshot snapshot,
        string reason);
}

public readonly struct CharacterPreparedDiseaseAnatomyBurdenReceipt
{
    public CharacterPreparedDiseaseAnatomyBurdenReceipt(
        CharacterBodyHealthMutationSnapshot before,
        string nodeId,
        float requestedInfectionBurden,
        float infectionBefore,
        float infectionAfter,
        string reasonCode)
    {
        Before = before;
        CharacterId = before.CharacterId;
        NodeId = nodeId?.Trim() ?? string.Empty;
        RequestedInfectionBurden = requestedInfectionBurden;
        InfectionBefore = infectionBefore;
        InfectionAfter = infectionAfter;
        ReasonCode = reasonCode?.Trim() ?? string.Empty;
    }

    public CharacterBodyHealthMutationSnapshot Before { get; }
    public CharacterId CharacterId { get; }
    public string NodeId { get; }
    public float RequestedInfectionBurden { get; }
    public float InfectionBefore { get; }
    public float InfectionAfter { get; }
    public string ReasonCode { get; }
    public bool IsValid => CharacterId.IsValid
        && NodeId.Length > 0
        && RequestedInfectionBurden > 0f
        && InfectionBefore >= 0f
        && InfectionAfter >= InfectionBefore
        && ReasonCode.Length > 0;
}

public readonly struct CharacterAnatomyDamageDelta
{
    public CharacterAnatomyDamageDelta(
        string nodeId,
        float maximumHealth,
        float healthBefore,
        float requestedDamage,
        float healthAfter,
        float bleedingBefore,
        float bleedingAfter)
    {
        NodeId = nodeId ?? string.Empty;
        MaximumHealth = maximumHealth;
        HealthBefore = healthBefore;
        RequestedDamage = requestedDamage;
        HealthAfter = healthAfter;
        BleedingBefore = bleedingBefore;
        BleedingAfter = bleedingAfter;
    }

    public string NodeId { get; }
    public float MaximumHealth { get; }
    public float HealthBefore { get; }
    public float RequestedDamage { get; }
    public float HealthAfter { get; }
    public float BleedingBefore { get; }
    public float BleedingAfter { get; }
}

public readonly struct CharacterPreparedAgeConditionAnatomyReceipt
{
    public CharacterPreparedAgeConditionAnatomyReceipt(
        CharacterBodyHealthMutationSnapshot before,
        IReadOnlyList<CharacterAnatomyDamageDelta> nodeDeltas,
        float aggregateHealthBefore,
        float aggregateHealthAfter,
        string reasonCode)
    {
        Before = before;
        CharacterId = before.CharacterId;
        NodeDeltas = nodeDeltas ?? Array.Empty<CharacterAnatomyDamageDelta>();
        AggregateMaximumHealth = before.State?.maxHealth ?? 0f;
        AggregateHealthBefore = aggregateHealthBefore;
        AggregateHealthAfter = aggregateHealthAfter;
        AggregateWasDowned = before.State?.downed ?? false;
        ReasonCode = reasonCode ?? string.Empty;
    }

    public CharacterPreparedAgeConditionAnatomyReceipt(
        CharacterId characterId,
        IReadOnlyList<CharacterAnatomyDamageDelta> nodeDeltas,
        float aggregateMaximumHealth,
        float aggregateHealthBefore,
        float aggregateHealthAfter,
        bool aggregateWasDowned,
        string reasonCode)
    {
        Before = default;
        CharacterId = characterId;
        NodeDeltas = nodeDeltas ?? Array.Empty<CharacterAnatomyDamageDelta>();
        AggregateMaximumHealth = aggregateMaximumHealth;
        AggregateHealthBefore = aggregateHealthBefore;
        AggregateHealthAfter = aggregateHealthAfter;
        AggregateWasDowned = aggregateWasDowned;
        ReasonCode = reasonCode ?? string.Empty;
    }

    public CharacterBodyHealthMutationSnapshot Before { get; }
    public CharacterId CharacterId { get; }
    public IReadOnlyList<CharacterAnatomyDamageDelta> NodeDeltas { get; }
    public float AggregateMaximumHealth { get; }
    public float AggregateHealthBefore { get; }
    public float AggregateHealthAfter { get; }
    public bool AggregateWasDowned { get; }
    public string ReasonCode { get; }
    public bool IsValid => CharacterId.IsValid
        && NodeDeltas.Count > 0
        && AggregateMaximumHealth > 0f
        && AggregateHealthBefore <= AggregateMaximumHealth
        && AggregateHealthBefore >= AggregateHealthAfter
        && !string.IsNullOrWhiteSpace(ReasonCode);
}

public interface ICharacterBodyHealthPersistence
{
    DungeonCharacterBodyHealthSaveData Capture();
    CharacterBodyHealthRestoreCandidate PrepareRestore(
        DungeonCharacterBodyHealthSaveData saveData);
    void PublishRestore(CharacterBodyHealthRestoreCandidate candidate);
}

public abstract class CharacterBodyHealthRestoreCandidate
{
    protected CharacterBodyHealthRestoreCandidate()
    {
    }
}
