using System;
using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

public delegate bool CaptiveCommand(string captiveId, out string failureReason);

public enum CaptivityMilestoneImportance
{
    Medium,
    High
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public interface ICaptivityPerformerPort
{
    bool IsActorAvailable(string captiveId);
    void ApplyAssignmentState(string captiveId, bool assigned);
    void Publish(CaptivePerformerMilestoneEvent gameEvent);
    void RaiseAlert(
        string title,
        string message,
        CaptivityMilestoneImportance importance,
        string category);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CaptivityPerformerRuntime
{
    private readonly Func<string, CaptiveState> findState;
    private readonly Func<string, CaptivePolicyData> findPolicy;
    private readonly CaptiveCommand recruit;
    private readonly CaptiveCommand release;
    private readonly ICaptivityPerformerPort port;

    public CaptivityPerformerRuntime(
        Func<string, CaptiveState> findState,
        Func<string, CaptivePolicyData> findPolicy,
        CaptiveCommand recruit,
        CaptiveCommand release,
        ICaptivityPerformerPort port)
    {
        this.findState = findState ?? throw new ArgumentNullException(nameof(findState));
        this.findPolicy = findPolicy ?? throw new ArgumentNullException(nameof(findPolicy));
        this.recruit = recruit ?? throw new ArgumentNullException(nameof(recruit));
        this.release = release ?? throw new ArgumentNullException(nameof(release));
        this.port = port ?? throw new ArgumentNullException(nameof(port));
    }

    public bool TryAssign(
        string captiveId,
        bool assigned,
        out string failureReason)
    {
        failureReason = string.Empty;
        CaptiveState state = findState(captiveId);
        CaptivePolicyData policy = state != null
            ? findPolicy(state.policyId)
            : null;
        if (state == null || !port.IsActorAvailable(captiveId) || !state.IsInCustody)
        {
            failureReason = "포로를 찾을 수 없습니다.";
            return false;
        }

        if (assigned && policy?.allowPerformance != true)
        {
            failureReason = "현재 포로 정책은 공연을 허용하지 않습니다.";
            return false;
        }

        state.status = assigned
            ? CaptivityStatus.Performer
            : CaptivityStatus.Confined;
        state.lastResult = assigned ? "공연 참가 준비" : "감방 복귀";
        port.ApplyAssignmentState(captiveId, assigned);
        return true;
    }

    public void Record(
        string captiveId,
        float fameGain,
        float skillGain,
        bool injured)
    {
        CaptiveState state = findState(captiveId);
        if (state == null)
        {
            return;
        }

        state.performerFame = ClampStat(state.performerFame + Mathf.Max(0f, fameGain));
        state.performerSkill = ClampStat(state.performerSkill + Mathf.Max(0f, skillGain));
        if (injured)
        {
            state.performerInjuries++;
        }

        int previousPrivilegeTier = state.privilegeTier;
        state.privilegeTier = state.performerFame >= 75f
            ? 2
            : state.performerFame >= 50f
                ? 1
                : 0;
        ApplyMilestones(state, previousPrivilegeTier);
    }

    public bool TryResolveMilestone(
        string captiveId,
        CaptivePerformerMilestoneChoice choice,
        out string failureReason)
    {
        failureReason = string.Empty;
        CaptiveState state = findState(captiveId);
        if (state == null || !state.IsInCustody)
        {
            failureReason = "공연자를 찾을 수 없습니다.";
            return false;
        }

        switch (choice)
        {
            case CaptivePerformerMilestoneChoice.StaffContract:
                if (!state.staffContractUnlocked)
                {
                    failureReason = "직원 계약 조건이 아직 열리지 않았습니다.";
                    return false;
                }

                if (!state.CanRecruit)
                {
                    failureReason = "신뢰·원한·타락 조건을 충족해야 직원 계약을 맺을 수 있습니다.";
                    return false;
                }

                return recruit(captiveId, out failureReason);

            case CaptivePerformerMilestoneChoice.ReleaseNegotiation:
                if (!state.finalContractPending)
                {
                    failureReason = "명성 100의 최종 계약 선택이 열리지 않았습니다.";
                    return false;
                }

                state.resolvedMilestoneChoice = choice;
                state.finalContractPending = false;
                return release(captiveId, out failureReason);

            case CaptivePerformerMilestoneChoice.ExclusiveFighterContract:
                if (!state.finalContractPending)
                {
                    failureReason = "명성 100의 최종 계약 선택이 열리지 않았습니다.";
                    return false;
                }

                state.exclusiveFighter = true;
                state.finalContractPending = false;
                state.resolvedMilestoneChoice = choice;
                state.status = CaptivityStatus.Performer;
                state.lastResult = "전속 투사 계약을 맺었습니다.";
                return true;

            default:
                failureReason = "선택할 계약이 없습니다.";
                return false;
        }
    }

    private void ApplyMilestones(CaptiveState state, int previousPrivilegeTier)
    {
        if (state.performerFame >= 50f && !state.carePriorityUnlocked)
        {
            state.carePriorityUnlocked = true;
            state.lastResult = "공연 명성으로 우선 식량·치료 특혜를 얻었습니다.";
            PublishMilestone(state, 50, state.lastResult);
        }

        if (state.performerFame >= 75f && !state.staffContractUnlocked)
        {
            state.staffContractUnlocked = true;
            state.lastResult = "조건을 충족하면 직원 계약을 제안할 수 있습니다.";
            PublishMilestone(state, 75, state.lastResult);
        }

        if (state.performerFame >= 100f
            && state.resolvedMilestoneChoice == CaptivePerformerMilestoneChoice.None
            && !state.finalContractPending)
        {
            state.finalContractPending = true;
            state.lastResult = "석방 협상과 전속 투사 계약 중 하나를 선택할 수 있습니다.";
            PublishMilestone(state, 100, state.lastResult);
        }

        if (state.privilegeTier > previousPrivilegeTier)
        {
            state.health = Mathf.Clamp(state.health + 5f, 0f, 100f);
        }
    }

    private void PublishMilestone(
        CaptiveState state,
        int threshold,
        string message)
    {
        port.Publish(new CaptivePerformerMilestoneEvent(
            state.captiveId,
            threshold,
            message));
        port.RaiseAlert(
            $"공연자 명성 {threshold}",
            message,
            threshold >= 100
                ? CaptivityMilestoneImportance.High
                : CaptivityMilestoneImportance.Medium,
            "포로·노역");
    }

    private static float ClampStat(float value)
    {
        return Mathf.Clamp(value, 0f, 100f);
    }
}
