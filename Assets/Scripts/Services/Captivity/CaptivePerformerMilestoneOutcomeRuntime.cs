using System;
using System.Collections.Generic;
using DungeonStory.Foundation.Facts;
using UnityEngine;

public sealed class CaptivePerformerMilestoneOutcomeRuntime
{
    private readonly IDomainFactStagingPublisher publisher;
    private readonly IDomainFactRecoveryPublisher recovery;
    private bool active;
    private readonly ICaptivityPerformerPort port;

    public CaptivePerformerMilestoneOutcomeRuntime(
        IDomainFactStagingPublisher publisher,
        IDomainFactRecoveryPublisher recovery,
        ICaptivityPerformerPort port)
    {
        this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));
        this.port = port ?? throw new ArgumentNullException(nameof(port));
    }

    public bool TryCommitEligibleMilestones(
        CaptiveState state,
        int absoluteDay,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (active) { failureReason = "captive-milestone-command-active"; return false; }
        if (!TryRecover(state, out failureReason)) return false;
        while (true)
        {
            int threshold = NextEligibleThreshold(state);
            if (threshold == 0)
                return true;
            if (!TryCommitMilestone(
                    state,
                    threshold,
                    absoluteDay,
                    out failureReason))
            {
                return false;
            }
            // Effects are applied. An uncertain delivery must not invite the caller to apply them again.
            if (HasPending(state)) return true;
        }
    }

    public bool TryCommitNextPendingMilestone(
        IReadOnlyList<CaptiveState> states,
        int absoluteDay,
        out bool attempted,
        out string failureReason)
    {
        attempted = false;
        failureReason = string.Empty;
        if (active) { failureReason = "captive-milestone-command-active"; return false; }
        if (states == null)
            return true;

        for (int index = 0; index < states.Count; index++)
        {
            CaptiveState state = states[index];
            if (HasPending(state))
            {
                attempted = true;
                return TryRecover(state, out failureReason);
            }
            int threshold = NextEligibleThreshold(state);
            if (threshold == 0)
                continue;
            attempted = true;
            return TryCommitMilestone(
                state,
                threshold,
                absoluteDay,
                out failureReason);
        }
        return true;
    }

    public void ValidateBeforeCapture()
    {
        if (active) throw new InvalidOperationException("captive-milestone-command-active");
    }

    private bool TryCommitMilestone(CaptiveState state, int threshold, int absoluteDay, out string failureReason)
    {
        failureReason = string.Empty;
        IDomainFactStage stage = null;
        bool applied = false;
        active = true;
        try
        {
            long revision = checked(state.performerMilestoneOutcomeRevision + 1L);
            var fact = new CaptivePerformerMilestoneUnlockedFact(new CaptivePerformerMilestoneFactData
            {
                captiveId = state.captiveId, displayName = state.displayName?.Trim(),
                threshold = threshold, fame = state.performerFame, revision = revision, absoluteDay = absoluteDay
            });
            if (!publisher.TryStage(fact, out stage, out failureReason)) return false;
            if (stage == null || !stage.IsActive || string.IsNullOrWhiteSpace(stage.OperationKey)
                || stage.OwnerRevision != revision)
            { failureReason = "captive-milestone-stage-invalid"; return false; }

            string previousResult = state.lastResult;
            long previousHighWater = state.performerMilestoneOutcomeRevision;
            long previousThresholdRevision = ThresholdRevision(state, threshold);
            bool previousFlag = ThresholdFlag(state, threshold);
            var observation = new CaptivePerformerMilestoneObservationData
            {
                fact = fact.Snapshot(), publicationKey = stage.OperationKey,
                pending = true, observersPending = !stage.IsReplay
            };
            state.performerMilestones.Add(observation);
            ApplyMilestoneState(state, threshold, revision, MilestoneMessage(threshold));
            applied = true;
            DomainFactDelivery delivered;
            try { delivered = stage.Commit(); }
            catch (Exception exception) when (Recoverable(exception))
            { delivered = DomainFactDelivery.Uncertain("captive-milestone-commit:" + exception.GetType().Name); }
            if (!delivered.MayBeCommitted)
            {
                RestoreMilestoneState(state, threshold, previousHighWater, previousThresholdRevision, previousFlag, previousResult);
                state.performerMilestones.Remove(observation);
                applied = false;
                failureReason = delivered.Detail;
                return false;
            }
            if (Delivered(observation.publicationKey, delivered)) Complete(state, observation);
            else failureReason = "captive-milestone-delivery-pending:" + delivered.Detail;
            return true;
        }
        catch (Exception exception) when (Recoverable(exception))
        {
            failureReason = "captive-milestone-publication:" + exception.GetType().Name + ":" + exception.Message;
            return applied;
        }
        finally
        {
            try { stage?.Cancel(); }
            catch (Exception exception) when (Recoverable(exception))
            { Debug.LogWarning("captive-milestone-cancel:" + exception); }
            finally { active = false; }
        }
    }

    private bool TryRecover(CaptiveState state, out string failureReason)
    {
        failureReason = string.Empty;
        if (state == null) return true;
        active = true;
        try
        {
            foreach (var observation in state.performerMilestones)
            {
                if (!observation.pending) continue;
                var fact = new CaptivePerformerMilestoneUnlockedFact(observation.fact);
                var result = new DomainFactDelivery[1];
                if (!recovery.ReplayBatch(new[] { DomainFactReplay.Create(observation.publicationKey, fact) },
                        result, out failureReason) || !Delivered(observation.publicationKey, result[0]))
                {
                    failureReason = "captive-milestone-recovery-pending:" + failureReason + ":" + result[0].Detail;
                    return false;
                }
                Complete(state, observation);
            }
            return true;
        }
        catch (Exception exception) when (Recoverable(exception))
        { failureReason = "captive-milestone-recovery:" + exception.GetType().Name + ":" + exception.Message; return false; }
        finally { active = false; }
    }

    private void Complete(CaptiveState state, CaptivePerformerMilestoneObservationData observation)
    {
        bool notify = observation.observersPending;
        observation.pending = false;
        observation.observersPending = false;
        if (notify) NotifyObservers(state, observation.fact.threshold, MilestoneMessage(observation.fact.threshold));
    }
    private static bool HasPending(CaptiveState state) =>
        state?.performerMilestones?.Exists(value => value.pending) == true;
    private static bool Delivered(string key, DomainFactDelivery value)
    {
        if (!value.Completed || value.OperationKey != key || string.IsNullOrWhiteSpace(value.PublicationId)
            || value.Fingerprint.Length != 64) return false;
        foreach (char character in value.Fingerprint)
            if (!(character >= '0' && character <= '9' || character >= 'a' && character <= 'f'
                || character >= 'A' && character <= 'F')) return false;
        return true;
    }
    private static bool Recoverable(Exception value) => value is not OutOfMemoryException
        && value is not StackOverflowException && value is not AccessViolationException;

    private static int NextEligibleThreshold(CaptiveState state)
    {
        if (state == null)
            return 0;
        if (state.performerFame >= 50f && state.carePriorityOutcomeRevision == 0L)
            return 50;
        if (state.performerFame >= 75f && state.staffContractOutcomeRevision == 0L)
            return 75;
        if (state.performerFame >= 100f
            && state.resolvedMilestoneChoice == CaptivePerformerMilestoneChoice.None
            && state.finalContractOutcomeRevision == 0L)
        {
            return 100;
        }
        return 0;
    }

    private static void ApplyMilestoneState(
        CaptiveState state,
        int threshold,
        long ownerRevision,
        string message)
    {
        state.performerMilestoneOutcomeRevision = ownerRevision;
        state.lastResult = message;
        switch (threshold)
        {
            case 50:
                state.carePriorityUnlocked = true;
                state.carePriorityOutcomeRevision = ownerRevision;
                break;
            case 75:
                state.staffContractUnlocked = true;
                state.staffContractOutcomeRevision = ownerRevision;
                break;
            case 100:
                state.finalContractPending = true;
                state.finalContractOutcomeRevision = ownerRevision;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(threshold));
        }
    }

    private static void RestoreMilestoneState(
        CaptiveState state,
        int threshold,
        long previousHighWater,
        long previousThresholdRevision,
        bool previousFlag,
        string previousResult)
    {
        state.performerMilestoneOutcomeRevision = previousHighWater;
        state.lastResult = previousResult;
        switch (threshold)
        {
            case 50:
                state.carePriorityUnlocked = previousFlag;
                state.carePriorityOutcomeRevision = previousThresholdRevision;
                break;
            case 75:
                state.staffContractUnlocked = previousFlag;
                state.staffContractOutcomeRevision = previousThresholdRevision;
                break;
            case 100:
                state.finalContractPending = previousFlag;
                state.finalContractOutcomeRevision = previousThresholdRevision;
                break;
        }
    }

    private static long ThresholdRevision(CaptiveState state, int threshold) =>
        threshold switch
        {
            50 => state.carePriorityOutcomeRevision,
            75 => state.staffContractOutcomeRevision,
            100 => state.finalContractOutcomeRevision,
            _ => 0L
        };

    private static bool ThresholdFlag(CaptiveState state, int threshold) =>
        threshold switch
        {
            50 => state.carePriorityUnlocked,
            75 => state.staffContractUnlocked,
            100 => state.finalContractPending,
            _ => false
        };

    private static string MilestoneMessage(int threshold) => threshold switch
    {
        50 => "공연 명성으로 우선 식량·치료 특혜를 얻었습니다.",
        75 => "조건을 충족하면 직원 계약을 제안할 수 있습니다.",
        100 => "석방 협상과 전속 투사 계약 중 하나를 선택할 수 있습니다.",
        _ => throw new ArgumentOutOfRangeException(nameof(threshold))
    };

    private void NotifyObservers(
        CaptiveState state,
        int threshold,
        string message)
    {
        try
        {
            port.Publish(new CaptivePerformerMilestoneEvent(
                state.captiveId,
                threshold,
                message));
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "공연자 이정표 이벤트 관찰자가 실패했습니다: "
                + exception);
        }
        try
        {
            port.RaiseAlert(
                $"공연자 명성 {threshold}",
                message,
                threshold >= 100
                    ? CaptivityMilestoneImportance.High
                    : CaptivityMilestoneImportance.Medium,
                "포로·노역");
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "공연자 이정표 알림 관찰자가 실패했습니다: "
                + exception);
        }
    }
}
