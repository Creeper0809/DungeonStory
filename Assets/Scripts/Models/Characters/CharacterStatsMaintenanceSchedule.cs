using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

/// <summary>
/// Owns non-persistent maintenance deadlines for one CharacterStats instance.
/// </summary>
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CharacterStatsMaintenanceSchedule
{
    private float nextNeedDecayAt = float.PositiveInfinity;
    private float nextMoodExpiryCheckAt;

    public void Run(
        float now,
        bool hasMoodFactors,
        Action applyNeedDecay,
        Action refreshMood)
    {
        if (now >= nextNeedDecayAt)
        {
            (applyNeedDecay ?? throw new ArgumentNullException(
                nameof(applyNeedDecay)))();
            nextNeedDecayAt = now + 5f;
        }

        if (!hasMoodFactors || now < nextMoodExpiryCheckAt)
        {
            return;
        }

        nextMoodExpiryCheckAt = now + 0.25f;
        (refreshMood ?? throw new ArgumentNullException(nameof(refreshMood)))();
    }

    public void BeginNeedDecay(CharacterId characterId, float now)
    {
        uint stableHash = PersistentEntityId.GetStableHash32(characterId);
        float stagger = (stableHash % 1000u) / 1000f * 5f;
        nextNeedDecayAt = now + 0.1f + stagger;
    }

    public void DeferMoodExpiry(float now)
    {
        nextMoodExpiryCheckAt = now + 0.25f;
    }

    public CharacterStatsMaintenanceScheduleSnapshot Capture() => new(
        nextNeedDecayAt,
        nextMoodExpiryCheckAt);

    public void Restore(CharacterStatsMaintenanceScheduleSnapshot snapshot)
    {
        nextNeedDecayAt = snapshot.NextNeedDecayAt;
        nextMoodExpiryCheckAt = snapshot.NextMoodExpiryCheckAt;
    }
}

public readonly struct CharacterStatsMaintenanceScheduleSnapshot
{
    public CharacterStatsMaintenanceScheduleSnapshot(
        float nextNeedDecayAt,
        float nextMoodExpiryCheckAt)
    {
        NextNeedDecayAt = nextNeedDecayAt;
        NextMoodExpiryCheckAt = nextMoodExpiryCheckAt;
    }

    public float NextNeedDecayAt { get; }
    public float NextMoodExpiryCheckAt { get; }
}
