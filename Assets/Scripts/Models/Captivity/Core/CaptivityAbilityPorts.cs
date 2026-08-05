using System;
using System.Collections;
using UnityEngine;

public enum CaptivityAbilityAccessKind
{
    None,
    EscortPass,
    CaptiveEscape
}

public interface ICaptiveEscapeAbilityPort
{
    bool IsAlive { get; }
    Vector2Int Position { get; }
    bool TryGetEscapeState(
        string captiveId,
        out Vector2Int destination,
        out string failureReason);
    IDisposable BeginEscapePass(string captiveId);
    bool TryStartSystemMove(Vector2Int destination, out string failureReason);
    void CompleteEscape(string captiveId);
    void FailEscape(string captiveId, string reason);
    void SetActionPhase(string phase, Vector2Int destination);
}

public interface ICaptiveEscortAbilityPort
{
    bool TryGetState(
        string captiveId,
        out CaptiveState state,
        out Vector2Int subjectPosition,
        out string subjectDisplayName,
        out string failureReason);
    bool TryCreateMovement(
        Vector2Int destination,
        CaptivityAbilityAccessKind accessKind,
        out IEnumerator movement);
    bool TryPickupReservedRestraint(
        CaptiveState state,
        out string failureReason);
    float AdvanceStabilization(string captiveId, float deltaSeconds);
    bool TryBeginEscort(string captiveId, out string failureReason);
    IDisposable BeginEscortPass(string captiveId);
    bool TryCompleteEscort(string captiveId, out string failureReason);
    void FailEscort(string captiveId, string reason);
    void SetActionPhase(string phase, string detail);
    void RequestImmediateReplan(bool clearFailures);
}

public interface IWildlifeCaptureTransportAbilityPort
{
    bool TryGetTransportState(
        string wildlifeId,
        out CapturedWildlifeState state,
        out Vector2Int wildlifePosition,
        out string wildlifeDisplayName,
        out string failureReason);
    bool TryCreateMovement(
        Vector2Int destination,
        CaptivityAbilityAccessKind accessKind,
        out IEnumerator movement);
    bool TryBeginCarry(string wildlifeId, out string failureReason);
    IDisposable BeginTransportPass(string wildlifeId);
    bool TryCompleteCarry(string wildlifeId, out string failureReason);
    void FailCarry(string wildlifeId, string reason);
    void SetActionPhase(string phase, string detail);
    void RequestImmediateReplan(bool clearFailures);
}
