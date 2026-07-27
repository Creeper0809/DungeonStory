using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct CharacterInfectionBurdenRequestedEvent
{
    public CharacterInfectionBurdenRequestedEvent(CharacterActor actor, float amount)
    {
        Actor = actor;
        Amount = Mathf.Max(0f, amount);
    }

    public CharacterActor Actor { get; }
    public float Amount { get; }
}

public readonly struct CharacterMentalInstabilityBurdenRequestedEvent
{
    public CharacterMentalInstabilityBurdenRequestedEvent(
        CharacterActor actor,
        float amount)
    {
        Actor = actor;
        Amount = Mathf.Max(0f, amount);
    }

    public CharacterActor Actor { get; }
    public float Amount { get; }
}

public interface ICharacterDeprivationRuntime
{
    bool HasActiveBreakdown(CharacterActor actor);
    bool HasBreakdownKind(CharacterActor actor, CharacterBreakdownKind kind);
    bool TryGetDisplayState(
        CharacterActor actor,
        out CharacterDeprivationDisplayState displayState);
    bool TryGetSnapshot(CharacterActor actor, out CharacterDeprivationSnapshot snapshot);
    bool TryRunActiveBreakdown(CharacterActor actor, out string status);
    bool TryRunSafeEmergencyRelief(CharacterActor actor, out string status);
    void BeginBreakdownAction(CharacterActor actor, CharacterBreakdownKind kind);
    bool IsSuppressible(CharacterActor actor);
    bool ApplySuppression(CharacterActor actor, float amount, out bool ended);
    float GetMoveSpeedMultiplier(CharacterActor actor);
    float GetWorkSpeedMultiplier(CharacterActor actor);
    void AddInfectionBurden(CharacterActor actor, float amount);
    void RecordTaboo(CharacterActor actor, string memory);
    void RecordTabooWitnesses(
        CharacterActor source,
        Vector2Int position,
        string memory,
        float moodPenalty);
    DungeonDarkSurvivalSaveData Capture();
    void Restore(DungeonDarkSurvivalSaveData saveData);
    bool DebugForceBreakdown(CharacterActor actor, CharacterBreakdownKind kind);
    bool DebugClearBreakdown(CharacterActor actor);
}
