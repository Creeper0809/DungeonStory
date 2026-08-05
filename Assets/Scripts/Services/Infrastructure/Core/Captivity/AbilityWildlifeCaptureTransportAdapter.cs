using System.Collections;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class AbilityWildlifeCaptureTransport : MonoBehaviour
{
    private IWildlifeCaptureTransportAbilityPort port;
    private Coroutine routine;
    private string activeWildlifeId = string.Empty;

    public bool IsTransporting => routine != null;

    public void Configure(IWildlifeCaptureTransportAbilityPort port)
    {
        this.port = port ?? throw new System.ArgumentNullException(nameof(port));
    }

    public void StartTransport(string wildlifeId)
    {
        CancelCurrent(reportFailure: false, string.Empty);
        if (port == null
            || !WildlifeCaptureTransportAbilityRules.TryNormalizeId(
                wildlifeId,
                out string normalizedId))
        {
            return;
        }

        activeWildlifeId = normalizedId;
        routine = StartCoroutine(TransportRoutine(activeWildlifeId));
    }

    private IEnumerator TransportRoutine(string wildlifeId)
    {
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

        if (!port.TryCreateMovement(
                wildlifePosition,
                CaptivityAbilityAccessKind.None,
                out IEnumerator pickupMovement))
        {
            Fail("생포한 동물에게 갈 수 없습니다.");
            yield break;
        }

        port.SetActionPhase("운반 상자를 들고 동물에게 이동", wildlifeName);
        yield return pickupMovement;
        if (!port.TryBeginCarry(wildlifeId, out failureReason))
        {
            Fail(failureReason);
            yield break;
        }

        using System.IDisposable pass = port.BeginTransportPass(wildlifeId);
        if (!port.TryCreateMovement(
                state.penPosition,
                CaptivityAbilityAccessKind.EscortPass,
                out IEnumerator penMovement))
        {
            Fail("우리까지 이어지는 운반 경로가 없습니다.");
            yield break;
        }

        port.SetActionPhase("포획 동물을 우리로 운반", state.penPosition.ToString());
        yield return penMovement;
        if (!port.TryCompleteCarry(wildlifeId, out failureReason))
        {
            Fail(failureReason);
            yield break;
        }

        activeWildlifeId = string.Empty;
        routine = null;
        port.SetActionPhase("포획 동물 수용 완료", wildlifeName);
        port.RequestImmediateReplan(clearFailures: true);
    }

    private void Fail(string reason)
    {
        string wildlifeId = activeWildlifeId;
        activeWildlifeId = string.Empty;
        routine = null;
        port?.FailCarry(wildlifeId, reason);
        port?.SetActionPhase("동물 운반 중단", reason);
        port?.RequestImmediateReplan(clearFailures: false);
    }

    private void CancelCurrent(bool reportFailure, string reason)
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        if (reportFailure && !string.IsNullOrWhiteSpace(activeWildlifeId))
        {
            port?.FailCarry(activeWildlifeId, reason);
        }

        activeWildlifeId = string.Empty;
    }
}
