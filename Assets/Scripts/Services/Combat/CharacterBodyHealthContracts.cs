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
    public bool downed;
    public string lastDamageReason = string.Empty;
}

[Serializable]
public sealed class DungeonCharacterBodyHealthSaveData
{
    public const int CurrentVersion = 4;

    public int version = CurrentVersion;
    public List<CharacterBodyHealthState> characters = new List<CharacterBodyHealthState>();
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

public interface ICharacterBodyHealthCommand
{
    void ConfigureVitals(CharacterActor actor, float maximumHealth, bool resetCurrentHealth);
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
    void HealLegacyVitals(CharacterActor actor, float amount);
    void ScaleLegacyVitals(CharacterActor actor, float multiplier);
    void SetLegacyInjurySeverity(CharacterActor actor, float injurySeverity);
    void Kill(CharacterActor actor, string reason);
    void ApplyCombatResult(CharacterActor target, CombatAttackResult result, string reason);
    void ApplySnapshot(CharacterActor target, CharacterBodyHealthSnapshot snapshot, string reason);
    void AddSuppression(CharacterActor target, float amount);
    void Heal(CharacterActor target, float amount, bool stopBleeding);
    bool Stabilize(CharacterActor target);
    bool ApplyTreatment(CharacterActor target, float partHealthAmount, float bloodLossReduction);
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
