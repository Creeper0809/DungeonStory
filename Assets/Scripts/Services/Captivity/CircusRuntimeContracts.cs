using System;
using System.Collections.Generic;
using UnityEngine;

public interface ICircusRuntime
{
    IReadOnlyList<CircusProgramModule> Programs { get; }
    IReadOnlyList<CircusShowOrder> Orders { get; }
    CircusProgramForecast GetForecast(
        BuildableObject stage,
        string programId,
        CircusLethalityPolicy lethality,
        IReadOnlyList<string> performerIds,
        IReadOnlyList<string> wildlifeIds);
    bool TrySchedule(
        BuildableObject stage,
        string programId,
        CircusLethalityPolicy lethality,
        IReadOnlyList<string> performerIds,
        IReadOnlyList<string> wildlifeIds,
        out CircusShowOrder order,
        out string failureReason);
    bool AdvancePreparation(
        string orderId,
        CharacterActor worker,
        float workAmount,
        out string status);
    bool Cancel(string orderId, string reason);
}

public interface IWildlifeCaptureRuntime
{
    bool IsCaptured(string wildlifeId);
    bool TryCapture(WildlifeActor wildlife, BuildableObject pen, out string failureReason);
    bool TryOrderCapture(
        WildlifeActor wildlife,
        CharacterActor carrier,
        BuildableObject pen,
        out string failureReason);
    bool TryGetCaptured(string wildlifeId, out CapturedWildlifeState state);
    bool TrySetTamed(string wildlifeId, bool tamed, out string failureReason);
    bool TryRegisterPenBorn(
        WildlifeActor wildlife,
        string penId,
        Vector2Int penPosition,
        out string failureReason);
    bool TryGetPenCapacity(string penId, out int capacity);
    bool TryRelease(string wildlifeId, out string failureReason);
    bool TryAssignToShow(string wildlifeId, string orderId, out string failureReason);
    void CompleteShowAssignment(string wildlifeId, string orderId);
    IReadOnlyList<CapturedWildlifeState> CapturedAnimals { get; }
    void CopyCapturedAnimalReferences(List<CapturedWildlifeState> destination);
    IReadOnlyList<CapturedWildlifeState> Capture();
    void ValidateRestore(CircusSaveData saveData, DungeonGameRestoreReport report);
    void StageRestore(CircusRestoreCandidate candidate);
    void PublishRestoreProjection();
    WildlifeCaptureProjectionPublication BeginRestoreProjectionPublication();
    void RollbackRestoreProjection(
        WildlifeCaptureProjectionPublication publication);
    void CompleteRestoreProjection(
        WildlifeCaptureProjectionPublication publication);
}

public sealed class WildlifeCaptureProjectionPublication
{
    private readonly object owner;
    private Action rollback;
    private Action complete;

    internal WildlifeCaptureProjectionPublication(
        object owner,
        Action rollback,
        Action complete)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        this.rollback = rollback ?? throw new ArgumentNullException(nameof(rollback));
        this.complete = complete ?? throw new ArgumentNullException(nameof(complete));
    }

    internal void Rollback(object expectedOwner)
    {
        Action action = RequireActive(expectedOwner, rollback);
        action();
        rollback = null;
        complete = null;
    }

    internal void Complete(object expectedOwner)
    {
        Action action = RequireActive(expectedOwner, complete);
        action();
        complete = null;
        rollback = null;
    }

    private Action RequireActive(object expectedOwner, Action action)
    {
        if (!ReferenceEquals(owner, expectedOwner) || action == null)
        {
            throw new InvalidOperationException(
                "Wildlife-capture projection publication has the wrong owner or is already finished.");
        }

        return action;
    }
}

public interface IWildlifeCaptureTransportRuntime
{
    bool TryGetTransportState(
        string wildlifeId,
        CharacterActor carrier,
        out CapturedWildlifeState state,
        out WildlifeActor wildlife,
        out string failureReason);
    IDisposable BeginTransportPass(CharacterActor carrier, string wildlifeId);
    bool TryBeginCarry(string wildlifeId, CharacterActor carrier, out string failureReason);
    bool TryCompleteCarry(string wildlifeId, CharacterActor carrier, out string failureReason);
    void FailCarry(string wildlifeId, CharacterActor carrier, string reason);
}
