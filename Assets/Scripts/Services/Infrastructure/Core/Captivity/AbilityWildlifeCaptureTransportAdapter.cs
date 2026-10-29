using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class AbilityWildlifeCaptureTransport : MonoBehaviour
{
    private IWildlifeCaptureTransportAbilityPort port;
    private Coroutine routine;
    private string activeWildlifeId = string.Empty;
    private System.IDisposable activeTransportPass;
    private bool transportExecutionActive;

    public bool IsTransporting => transportExecutionActive;
    public string LastTerminalFailureReasonForDiagnostics { get; private set; }
        = string.Empty;
    public int DeliveryStandSearchDeferralCountForDiagnostics
        { get; private set; }
    public int PickupSearchDeferralCountForDiagnostics { get; private set; }
    public int PickupRetargetCountForDiagnostics { get; private set; }

    public void Configure(IWildlifeCaptureTransportAbilityPort port)
    {
        IWildlifeCaptureTransportAbilityPort replacement = port
            ?? throw new System.ArgumentNullException(nameof(port));
        if (this.port != null
            && !ReferenceEquals(this.port, replacement)
            && transportExecutionActive)
        {
            CancelCurrent(
                reportFailure: true,
                reason: "wildlife-transport-port-reconfigured");
        }
        this.port = replacement;
    }

    public bool TryStartTransport(string wildlifeId, out string failureReason)
    {
        failureReason = string.Empty;
        if (port == null
            || !WildlifeCaptureTransportAbilityRules.TryNormalizeId(
                wildlifeId,
                out string normalizedId))
        {
            failureReason = "동물 운반 행동의 필수 연결이나 야생동물 ID가 없습니다.";
            return false;
        }

        CancelCurrent(
            reportFailure: transportExecutionActive,
            reason: "wildlife-transport-restarted");
        activeWildlifeId = normalizedId;
        transportExecutionActive = true;
        LastTerminalFailureReasonForDiagnostics = string.Empty;
        DeliveryStandSearchDeferralCountForDiagnostics = 0;
        PickupSearchDeferralCountForDiagnostics = 0;
        PickupRetargetCountForDiagnostics = 0;
        if (!port.TryBeginActionOwnership(
                activeWildlifeId,
                out string ownershipFailure))
        {
            Fail(ownershipFailure);
            failureReason = ownershipFailure;
            return false;
        }
        Coroutine started = StartCoroutine(TransportRoutine(activeWildlifeId));
        routine = transportExecutionActive ? started : null;
        failureReason = LastTerminalFailureReasonForDiagnostics;
        return string.IsNullOrEmpty(failureReason);
    }

    public void StopForLifecycleTransition(string reason)
    {
        // Lifecycle loss is a typed cancellation, not an execution failure.
        // Close the external epoch before physical rollback so any synchronous
        // state/event callbacks cannot report the same lease as Failed.
        port?.CancelActionOwnership();
        CancelCurrent(
            reportFailure: transportExecutionActive,
            reason: string.IsNullOrWhiteSpace(reason)
                ? "wildlife-transport-carrier-lifecycle-ended"
                : reason.Trim());
    }

    private IEnumerator TransportRoutine(string wildlifeId)
    {
        // StartCoroutine runs synchronously until its first yield. Publish the
        // caller's capture-order receipt before carry can mutate the same owner
        // revision, including the already-adjacent pickup case. Ownership and
        // stale-movement cancellation are still acquired atomically by StartTransport.
        yield return null;
        if (!OwnsActionIntent())
        {
            Fail("Wildlife transport started without AI action ownership.");
            yield break;
        }

        if (!port.TryGetTransportState(
                wildlifeId,
                out CapturedWildlifeState state,
                out Vector2Int wildlifePosition,
                out string wildlifeName,
                out string failureReason))
        {
            Fail(failureReason);
            yield break;
        }

        // An order reserves transport; it does not immobilize live wildlife.
        // Finish and validate each real approach leg before planning another.
        // Never treat aborted movement as successful pursuit of a moved target.
        while (transportExecutionActive)
        {
            if (!OwnsActionIntent())
            {
                Fail("Wildlife transport lost AI action ownership while approaching the animal.");
                yield break;
            }
            if (!port.TryGetTransportState(
                    wildlifeId, out state, out wildlifePosition,
                    out wildlifeName, out failureReason))
            {
                Fail(failureReason);
                yield break;
            }
            Vector2Int carrierPosition = port.Position;
            if (Mathf.Abs(carrierPosition.x - wildlifePosition.x)
                + Mathf.Abs(carrierPosition.y - wildlifePosition.y) <= 1)
            {
                break;
            }

            CaptivityMovementResolution resolution = port.ResolvePickupMovement(
                wildlifePosition, out Vector2Int pickupStand,
                out IEnumerator pickupMovement);
            if (resolution == CaptivityMovementResolution.Pending)
            {
                PickupSearchDeferralCountForDiagnostics = checked(
                    PickupSearchDeferralCountForDiagnostics + 1);
                port.SetActionPhase("포획 동물 접근 경로 계산", wildlifeName);
                yield return null;
                continue;
            }
            if (resolution != CaptivityMovementResolution.Ready)
            {
                Fail("생포한 동물에게 갈 수 없습니다.");
                yield break;
            }

            port.SetActionPhase("운반 상자를 들고 동물에게 이동", wildlifeName);
            yield return RunMovementWhileTransportValid(
                pickupMovement, wildlifeId, "approaching the animal");
            if (!transportExecutionActive) yield break;
            if (!port.TryValidateMovementArrival(pickupStand, out failureReason))
            {
                Fail(failureReason);
                yield break;
            }
            if (!port.TryGetTransportState(
                    wildlifeId, out state, out Vector2Int currentTarget,
                    out wildlifeName, out failureReason))
            {
                Fail(failureReason);
                yield break;
            }
            if (currentTarget != wildlifePosition)
            {
                PickupRetargetCountForDiagnostics = checked(
                    PickupRetargetCountForDiagnostics + 1);
            }
        }
        if (!transportExecutionActive)
        {
            yield break;
        }
        if (!OwnsActionIntent())
        {
            Fail("Wildlife transport lost AI action ownership while approaching the animal.");
            yield break;
        }
        if (!port.TryBeginCarry(wildlifeId, out failureReason))
        {
            Fail(failureReason);
            yield break;
        }

        // Door traversal and path selection must share the same live escort
        // authority that the delivery movement will use. The exact-path broker
        // is incremental, so Pending owns this action and resumes on a later
        // frame instead of being misreported as an unreachable pen.
        ReplaceTransportPass(port.BeginTransportPass(wildlifeId));
        while (transportExecutionActive)
        {
            WildlifeDeliveryStandResolution resolution =
                port.ResolveDeliveryStand(
                    wildlifeId,
                    out state,
                    out failureReason);
            if (resolution == WildlifeDeliveryStandResolution.Ready)
            {
                break;
            }
            if (resolution == WildlifeDeliveryStandResolution.Failed)
            {
                Fail(failureReason);
                yield break;
            }
            DeliveryStandSearchDeferralCountForDiagnostics = checked(
                DeliveryStandSearchDeferralCountForDiagnostics + 1);
            if (!OwnsActionIntent())
            {
                Fail("Wildlife transport lost AI action ownership while resolving the delivery stand.");
                yield break;
            }

            port.SetActionPhase(
                "포획 동물 우리 경로 계산",
                failureReason);
            yield return null;
        }
        if (!transportExecutionActive)
        {
            yield break;
        }
        if (!port.TryCreateDeliveryMovement(
                state.penPosition,
                out IEnumerator penMovement))
        {
            Fail("우리까지 이어지는 운반 경로가 없습니다.");
            yield break;
        }

        port.SetActionPhase("포획 동물을 우리로 운반", state.penPosition.ToString());
        yield return RunMovementWhileTransportValid(
            penMovement,
            wildlifeId,
            "moving to the pen");
        if (!transportExecutionActive)
        {
            yield break;
        }
        if (!OwnsActionIntent())
        {
            Fail("Wildlife transport lost AI action ownership while moving to the pen.");
            yield break;
        }
        if (!port.TryValidateMovementArrival(
                state.penPosition,
                out failureReason))
        {
            Fail(failureReason);
            yield break;
        }
        if (!port.TryCompleteCarry(wildlifeId, out failureReason))
        {
            Fail(failureReason);
            yield break;
        }

        transportExecutionActive = false;
        LastTerminalFailureReasonForDiagnostics = string.Empty;
        activeWildlifeId = string.Empty;
        routine = null;
        ReleaseTransportPass();
        port.SetActionPhase("포획 동물 수용 완료", wildlifeName);
        ReleaseActionIntent(clearFailures: true);
        port.RequestImmediateReplan(clearFailures: true);
    }

    private IEnumerator RunMovementWhileTransportValid(
        IEnumerator movement,
        string wildlifeId,
        string phase)
    {
        Stack<IEnumerator> stack = new Stack<IEnumerator>();
        stack.Push(movement);
        while (stack.Count > 0)
        {
            if (!OwnsActionIntent())
            {
                Fail($"Wildlife transport lost AI action ownership while {phase}.");
                yield break;
            }
            if (!port.TryGetTransportState(
                    wildlifeId,
                    out _,
                    out _,
                    out _,
                    out string failureReason))
            {
                Fail(failureReason);
                yield break;
            }

            IEnumerator current = stack.Peek();
            if (!current.MoveNext())
            {
                stack.Pop();
                continue;
            }
            if (current.Current is IEnumerator nested)
            {
                stack.Push(nested);
                continue;
            }
            yield return current.Current;
        }
    }

    private void Fail(string reason)
    {
        if (!transportExecutionActive)
        {
            return;
        }

        string wildlifeId = activeWildlifeId;
        LastTerminalFailureReasonForDiagnostics = reason ?? string.Empty;
        transportExecutionActive = false;
        activeWildlifeId = string.Empty;
        routine = null;
        ReleaseTransportPass();
        if (!string.IsNullOrWhiteSpace(wildlifeId))
        {
            port?.FailCarry(wildlifeId, reason);
        }
        port?.SetActionPhase("동물 운반 중단", reason);
        ReleaseActionIntent(clearFailures: false);
        port?.RequestImmediateReplan(clearFailures: false);
    }

    private void CancelCurrent(bool reportFailure, string reason)
    {
        bool wasActive = transportExecutionActive;
        string wildlifeId = activeWildlifeId;
        transportExecutionActive = false;
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        ReleaseTransportPass();
        if (reportFailure
            && wasActive
            && !string.IsNullOrWhiteSpace(wildlifeId))
        {
            port?.FailCarry(wildlifeId, reason);
        }

        activeWildlifeId = string.Empty;
        ReleaseActionIntent(clearFailures: false);
    }

    private void ReplaceTransportPass(System.IDisposable pass)
    {
        ReleaseTransportPass();
        activeTransportPass = pass;
    }

    private void ReleaseTransportPass()
    {
        System.IDisposable pass = activeTransportPass;
        activeTransportPass = null;
        pass?.Dispose();
    }

    private bool OwnsActionIntent() =>
        port?.HasActionOwnership() == true;

    private void ReleaseActionIntent(bool clearFailures) =>
        port?.EndActionOwnership(clearFailures);

    private void OnDisable()
    {
        if (Application.isPlaying)
        {
            CancelCurrent(
                reportFailure: transportExecutionActive,
                reason: "wildlife-transport-actor-disabled");
        }
    }
}
