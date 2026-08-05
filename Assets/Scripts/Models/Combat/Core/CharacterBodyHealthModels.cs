using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CharacterBodyPartHealthState
{
    public CombatBodyPart bodyPart;
    [Min(1f)] public float maxHealth = 20f;
    [Min(0f)] public float currentHealth = 20f;
    [Min(0f)] public float bleedingPerSecond;

    public float HealthRatio => currentHealth / Mathf.Max(1f, maxHealth);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct CharacterVitalsSnapshot
{
    public CharacterVitalsSnapshot(
        float maximumHealth,
        float currentHealth,
        float injurySeverity)
    {
        MaximumHealth = Mathf.Max(1f, maximumHealth);
        CurrentHealth = Mathf.Clamp(currentHealth, 0f, MaximumHealth);
        InjurySeverity = Mathf.Clamp01(injurySeverity);
    }

    public float MaximumHealth { get; }
    public float CurrentHealth { get; }
    public float InjurySeverity { get; }
    public bool IsDead => CurrentHealth <= 0f;
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct CharacterBodyHealthSnapshot
{
    public CharacterBodyHealthSnapshot(
        IReadOnlyList<CharacterBodyPartHealthState> parts,
        float bloodLoss,
        float suppression,
        float consciousness,
        float manipulation,
        float mobility,
        bool downed)
    {
        Parts = parts ?? Array.Empty<CharacterBodyPartHealthState>();
        BloodLoss = Mathf.Clamp(bloodLoss, 0f, 100f);
        Suppression = Mathf.Clamp(suppression, 0f, 100f);
        Consciousness = Mathf.Clamp01(consciousness);
        Manipulation = Mathf.Clamp01(manipulation);
        Mobility = Mathf.Clamp01(mobility);
        Downed = downed;
    }

    public IReadOnlyList<CharacterBodyPartHealthState> Parts { get; }
    public float BloodLoss { get; }
    public float Suppression { get; }
    public float Consciousness { get; }
    public float Manipulation { get; }
    public float Mobility { get; }
    public bool Downed { get; }
}
