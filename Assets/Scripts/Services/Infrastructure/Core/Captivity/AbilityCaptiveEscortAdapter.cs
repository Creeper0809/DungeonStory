using System.Collections;
using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class AbilityCaptiveEscort : MonoBehaviour
{
    private enum EscortMovementTarget
    {
        Restraint,
        Subject,
        Housing
    }

    private sealed class EscortMovementRequest
    {
        public Vector2Int Destination;
        public string SubjectName = string.Empty;
        public IEnumerator Movement;
        public string FailureReason = string.Empty;
        public bool IsReady;
    }

    private ICaptiveEscortAbilityPort port;
    private IGameClock gameClock;
    private Coroutine routine;
    private bool escortExecutionActive;
    private string activeCaptiveId = string.Empty;
#if UNITY_EDITOR
    public System.Action<string> DebugBeforeEscortRoutineStart;
#endif

    public bool IsEscorting => escortExecutionActive;

    public void Configure(ICaptiveEscortAbilityPort port, IGameClock gameClock)
    {
        this.port = port ?? throw new System.ArgumentNullException(nameof(port));
        this.gameClock = gameClock
            ?? throw new System.ArgumentNullException(nameof(gameClock));
    }

    public void StartEscort(string captiveId)
    {
        StopEscort("새 호송 명령");
        if (port == null
            || gameClock == null
            || !CaptiveEscortAbilityRules.TryNormalizeId(
                captiveId,
                out string normalizedId))
        {
            return;
        }

        if (!port.TryBeginActionOwnership(
                normalizedId,
                out string ownershipFailure))
        {
            port.FailEscort(
                normalizedId,
                ownershipFailure);
            port.SetActionPhase(
                "Escort cancelled",
                ownershipFailure);
            return;
        }

        activeCaptiveId = normalizedId;
        escortExecutionActive = true;
#if UNITY_EDITOR
        DebugBeforeEscortRoutineStart?.Invoke(activeCaptiveId);
#endif
        Coroutine started = StartCoroutine(EscortRoutine(activeCaptiveId));
        routine = escortExecutionActive ? started : null;
    }

    public void StopEscort(string reason)
    {
        escortExecutionActive = false;
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        if (!string.IsNullOrWhiteSpace(activeCaptiveId))
        {
            port?.FailEscort(activeCaptiveId, reason);
        }

        activeCaptiveId = string.Empty;
        ReleaseActionIntent(clearFailures: false);
    }

    private void OnDisable()
    {
        StopEscort("Captive escort actor disabled.");
    }

    private IEnumerator EscortRoutine(string captiveId)
    {
        if (!OwnsActionIntent())
        {
            Fail("Captive escort started without AI action ownership.");
            yield break;
        }

        if (!TryGetState(
                captiveId,
                out CaptiveState state,
                out _,
                out _,
                out string failure))
        {
            Fail(failure);
            yield break;
        }

        if (!string.IsNullOrWhiteSpace(state.restraintStackId))
        {
            EscortMovementRequest restraintRequest = new();
            yield return ResolveMovement(
                captiveId,
                EscortMovementTarget.Restraint,
                CaptivityStatus.Stabilizing,
                CaptivityAbilityAccessKind.None,
                "구속구 경로 계산 중",
                "구속구 보관 위치로 갈 수 없습니다.",
                restraintRequest);
            if (!restraintRequest.IsReady)
            {
                Fail(restraintRequest.FailureReason);
                yield break;
            }

            port.SetActionPhase(
                "구속구를 가지러 이동",
                restraintRequest.Destination.ToString());
            yield return restraintRequest.Movement;
            if (!OwnsActionIntent())
            {
                Fail("Captive escort lost AI action ownership while collecting restraints.");
                yield break;
            }
            if (!TryGetState(
                    captiveId,
                    out state,
                    out _,
                    out _,
                    out failure)
                || state.status != CaptivityStatus.Stabilizing)
            {
                Fail(string.IsNullOrWhiteSpace(failure)
                    ? "Captive escort state changed while collecting restraints."
                    : failure);
                yield break;
            }
            if (!port.TryPickupReservedRestraint(state, out failure))
            {
                Fail(failure);
                yield break;
            }
        }

        EscortMovementRequest subjectRequest = new();
        yield return ResolveMovement(
            captiveId,
            EscortMovementTarget.Subject,
            CaptivityStatus.Stabilizing,
            CaptivityAbilityAccessKind.None,
            "포로 접근 경로 계산 중",
            "쓰러진 침입자에게 갈 수 없습니다.",
            subjectRequest);
        if (!subjectRequest.IsReady)
        {
            Fail(subjectRequest.FailureReason);
            yield break;
        }

        string subjectName = subjectRequest.SubjectName;
        port.SetActionPhase("포로에게 이동", subjectName);
        yield return subjectRequest.Movement;
        if (!OwnsActionIntent())
        {
            Fail("Captive escort lost AI action ownership while approaching the captive.");
            yield break;
        }

        while (TryGetState(
                   captiveId,
                   out state,
                   out _,
                   out subjectName,
                   out failure)
               && !state.stabilized)
        {
            if (!OwnsActionIntent())
            {
                Fail("Captive escort lost AI action ownership during stabilization.");
                yield break;
            }

            float progress = port.AdvanceStabilization(
                captiveId,
                gameClock.DeltaTime);
            port.SetActionPhase(
                $"포로 안정화 {Mathf.RoundToInt(progress * 100f)}%",
                subjectName);
            yield return null;
        }

        if (!OwnsActionIntent())
        {
            Fail("Captive escort lost AI action ownership before transport.");
            yield break;
        }

        if (!port.TryBeginEscort(captiveId, out failure))
        {
            Fail(failure);
            yield break;
        }

        if (!TryGetState(
                captiveId,
                out state,
                out _,
                out subjectName,
                out failure))
        {
            Fail(failure);
            yield break;
        }

        using System.IDisposable escortPass = port.BeginEscortPass(captiveId);
        EscortMovementRequest housingRequest = new();
        yield return ResolveMovement(
            captiveId,
            EscortMovementTarget.Housing,
            CaptivityStatus.Escorting,
            CaptivityAbilityAccessKind.EscortPass,
            "호송 경로 계산 중",
            "감방까지 안전한 호송 경로가 없습니다.",
            housingRequest);
        if (!housingRequest.IsReady)
        {
            Fail(housingRequest.FailureReason);
            yield break;
        }

        port.SetActionPhase(
            "포로 호송",
            housingRequest.Destination.ToString());
        yield return housingRequest.Movement;
        if (!OwnsActionIntent())
        {
            Fail("Captive escort lost AI action ownership while moving to housing.");
            yield break;
        }
        if (!port.TryCompleteEscort(captiveId, out failure))
        {
            Fail(failure);
            yield break;
        }

        activeCaptiveId = string.Empty;
        escortExecutionActive = false;
        routine = null;
        port.SetActionPhase("포로 수용 완료", subjectName);
        ReleaseActionIntent(clearFailures: true);
    }

    private IEnumerator ResolveMovement(
        string captiveId,
        EscortMovementTarget target,
        CaptivityStatus expectedStatus,
        CaptivityAbilityAccessKind accessKind,
        string pendingPhase,
        string unreachableReason,
        EscortMovementRequest request)
    {
        while (true)
        {
            if (!OwnsActionIntent())
            {
                request.FailureReason =
                    "Captive escort lost AI action ownership while resolving a path.";
                yield break;
            }
            if (!TryGetState(
                    captiveId,
                    out CaptiveState state,
                    out Vector2Int subjectPosition,
                    out string subjectName,
                    out string failure)
                || state.status != expectedStatus)
            {
                request.FailureReason = string.IsNullOrWhiteSpace(failure)
                    ? $"Captive escort state changed while resolving {target}."
                    : failure;
                yield break;
            }

            Vector2Int destination = target switch
            {
                EscortMovementTarget.Restraint => state.restraintPickupPosition,
                EscortMovementTarget.Subject => subjectPosition,
                EscortMovementTarget.Housing => state.housingPosition,
                _ => throw new System.ArgumentOutOfRangeException(nameof(target))
            };
            CaptivityMovementResolution resolution = port.ResolveMovement(
                destination,
                accessKind,
                out IEnumerator movement);
            if (resolution == CaptivityMovementResolution.Pending)
            {
                port.SetActionPhase(pendingPhase, destination.ToString());
                yield return null;
                continue;
            }
            if (resolution != CaptivityMovementResolution.Ready
                || movement == null)
            {
                request.FailureReason = unreachableReason;
                yield break;
            }

            request.Destination = destination;
            request.SubjectName = subjectName;
            request.Movement = movement;
            request.IsReady = true;
            yield break;
        }
    }

    private bool TryGetState(
        string captiveId,
        out CaptiveState state,
        out Vector2Int subjectPosition,
        out string subjectName,
        out string failure) =>
        port.TryGetState(
            captiveId,
            out state,
            out subjectPosition,
            out subjectName,
            out failure);

    private void Fail(string reason)
    {
        port?.FailEscort(activeCaptiveId, reason);
        activeCaptiveId = string.Empty;
        escortExecutionActive = false;
        routine = null;
        port?.SetActionPhase("호송 중단", reason);
        ReleaseActionIntent(clearFailures: false);
    }

    private bool OwnsActionIntent()
    {
        return port?.HasActionOwnership() == true;
    }

    private void ReleaseActionIntent(bool clearFailures)
    {
        port?.EndActionOwnership(clearFailures);
    }
}
