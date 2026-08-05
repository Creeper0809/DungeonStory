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

public readonly struct CharacterInfectionBurdenReductionRequestedEvent
{
    public CharacterInfectionBurdenReductionRequestedEvent(
        CharacterActor actor,
        float amount)
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

public interface ICharacterDeprivationQuery
{
    bool HasActiveBreakdown(CharacterActor actor);
    bool HasBreakdownKind(CharacterActor actor, CharacterBreakdownKind kind);
    bool TryGetDisplayState(
        CharacterActor actor,
        out CharacterDeprivationDisplayState displayState);
    bool TryGetSnapshot(CharacterActor actor, out CharacterDeprivationSnapshot snapshot);
    bool NeedsRoutineDrink(CharacterActor actor, out string reason);
    bool NeedsSafeEmergencyRelief(CharacterActor actor, out string reason);
    CharacterDeprivationDiagnosticsSnapshot GetDiagnostics();
    bool IsSuppressible(CharacterActor actor);
    float GetMoveSpeedMultiplier(CharacterActor actor);
    float GetWorkSpeedMultiplier(CharacterActor actor);
}

public interface ICharacterDeprivationCommand
{
    bool TryRunActiveBreakdown(CharacterActor actor, out string status);
    bool TryRunRoutineDrink(CharacterActor actor, out string status);
    bool TryRunSafeEmergencyRelief(CharacterActor actor, out string status);
    void ResetDiagnostics();
    void BeginBreakdownAction(CharacterActor actor, CharacterBreakdownKind kind);
    bool ApplySuppression(CharacterActor actor, float amount, out bool ended);
    bool DebugForceBreakdown(CharacterActor actor, CharacterBreakdownKind kind);
    bool DebugClearBreakdown(CharacterActor actor);
}

public interface ICharacterDeprivationPersistence
{
    DungeonDarkSurvivalSaveData Capture();
    DarkSurvivalRestoreCandidate BuildRestoreCandidate(
        DungeonDarkSurvivalSaveData saveData);
    void PublishRestoreCandidate(DarkSurvivalRestoreCandidate candidate);
}

public interface ICharacterDeprivationIncidentSink
{
    void AddInfectionBurden(CharacterActor actor, float amount);
    void ReduceInfectionBurden(CharacterActor actor, float amount);
    void RecordTaboo(CharacterActor actor, string memory);
    void RecordTabooWitnesses(
        CharacterActor source,
        Vector2Int position,
        string memory,
        float moodPenalty);
}

public interface ICharacterDeprivationRuntime :
    ICharacterDeprivationQuery,
    ICharacterDeprivationCommand,
    ICharacterDeprivationPersistence,
    ICharacterDeprivationIncidentSink
{
}

public sealed class NoCharacterDeprivationBoundary :
    ICharacterDeprivationQuery,
    ICharacterDeprivationCommand
{
    public static readonly NoCharacterDeprivationBoundary Instance = new();

    private NoCharacterDeprivationBoundary()
    {
    }

    public bool HasActiveBreakdown(CharacterActor actor) => false;
    public bool HasBreakdownKind(
        CharacterActor actor,
        CharacterBreakdownKind kind) => false;
    public bool TryGetDisplayState(
        CharacterActor actor,
        out CharacterDeprivationDisplayState displayState)
    {
        displayState = default;
        return false;
    }
    public bool TryGetSnapshot(
        CharacterActor actor,
        out CharacterDeprivationSnapshot snapshot)
    {
        snapshot = default;
        return false;
    }
    public bool NeedsRoutineDrink(CharacterActor actor, out string reason)
    {
        reason = string.Empty;
        return false;
    }
    public bool NeedsSafeEmergencyRelief(
        CharacterActor actor,
        out string reason)
    {
        reason = string.Empty;
        return false;
    }
    public CharacterDeprivationDiagnosticsSnapshot GetDiagnostics() => default;
    public bool IsSuppressible(CharacterActor actor) => false;
    public float GetMoveSpeedMultiplier(CharacterActor actor) => 1f;
    public float GetWorkSpeedMultiplier(CharacterActor actor) => 1f;
    public bool TryRunActiveBreakdown(CharacterActor actor, out string status)
    {
        status = string.Empty;
        return false;
    }
    public bool TryRunRoutineDrink(CharacterActor actor, out string status)
    {
        status = string.Empty;
        return false;
    }
    public bool TryRunSafeEmergencyRelief(
        CharacterActor actor,
        out string status)
    {
        status = string.Empty;
        return false;
    }
    public void ResetDiagnostics()
    {
    }
    public void BeginBreakdownAction(
        CharacterActor actor,
        CharacterBreakdownKind kind)
    {
    }
    public bool ApplySuppression(
        CharacterActor actor,
        float amount,
        out bool ended)
    {
        ended = false;
        return false;
    }
    public bool DebugForceBreakdown(
        CharacterActor actor,
        CharacterBreakdownKind kind) => false;
    public bool DebugClearBreakdown(CharacterActor actor) => false;
}
