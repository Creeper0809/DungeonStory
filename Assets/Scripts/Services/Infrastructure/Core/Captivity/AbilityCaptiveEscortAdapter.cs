using System.Collections;
using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class AbilityCaptiveEscort : MonoBehaviour
{
    private ICaptiveEscortAbilityPort port;
    private IGameClock gameClock;
    private Coroutine routine;
    private string activeCaptiveId = string.Empty;

    public bool IsEscorting => routine != null;

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

        activeCaptiveId = normalizedId;
        routine = StartCoroutine(EscortRoutine(activeCaptiveId));
    }

    public void StopEscort(string reason)
    {
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
    }

    private IEnumerator EscortRoutine(string captiveId)
    {
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
            if (!port.TryCreateMovement(
                    state.restraintPickupPosition,
                    CaptivityAbilityAccessKind.None,
                    out IEnumerator restraintMovement))
            {
                Fail("구속구 보관 위치로 갈 수 없습니다.");
                yield break;
            }

            port.SetActionPhase(
                "구속구를 가지러 이동",
                state.restraintPickupPosition.ToString());
            yield return restraintMovement;
            if (!port.TryPickupReservedRestraint(state, out failure))
            {
                Fail(failure);
                yield break;
            }
        }

        if (!TryGetState(
                captiveId,
                out state,
                out Vector2Int subjectPosition,
                out string subjectName,
                out failure)
            || !port.TryCreateMovement(
                subjectPosition,
                CaptivityAbilityAccessKind.None,
                out IEnumerator subjectMovement))
        {
            Fail(string.IsNullOrWhiteSpace(failure)
                ? "쓰러진 침입자에게 갈 수 없습니다."
                : failure);
            yield break;
        }

        port.SetActionPhase("포로에게 이동", subjectName);
        yield return subjectMovement;

        while (TryGetState(
                   captiveId,
                   out state,
                   out _,
                   out subjectName,
                   out failure)
               && !state.stabilized)
        {
            float progress = port.AdvanceStabilization(
                captiveId,
                gameClock.DeltaTime);
            port.SetActionPhase(
                $"포로 안정화 {Mathf.RoundToInt(progress * 100f)}%",
                subjectName);
            yield return null;
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
        if (!port.TryCreateMovement(
                state.housingPosition,
                CaptivityAbilityAccessKind.EscortPass,
                out IEnumerator housingMovement))
        {
            Fail("감방까지 안전한 호송 경로가 없습니다.");
            yield break;
        }

        port.SetActionPhase("포로 호송", state.housingPosition.ToString());
        yield return housingMovement;
        if (!port.TryCompleteEscort(captiveId, out failure))
        {
            Fail(failure);
            yield break;
        }

        activeCaptiveId = string.Empty;
        routine = null;
        port.SetActionPhase("포로 수용 완료", subjectName);
        port.RequestImmediateReplan(clearFailures: true);
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
        routine = null;
        port?.SetActionPhase("호송 중단", reason);
        port?.RequestImmediateReplan(clearFailures: false);
    }
}
