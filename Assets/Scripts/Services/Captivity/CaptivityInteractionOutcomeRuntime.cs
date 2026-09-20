using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Narrative.Korean;

internal sealed class CaptivityInteractionOutcomeRuntime
{
    public const string OutputReasonCode = "captivity-interaction-output";

    private readonly ICaptivityInteractionOutcomeCommitter outcomes;
    private readonly IPhysicalItemSourcePublicationService physicalSources;

    public CaptivityInteractionOutcomeRuntime(
        ICaptivityInteractionOutcomeCommitter outcomes,
        IPhysicalItemSourcePublicationService physicalSources)
    {
        this.outcomes = outcomes
            ?? throw new ArgumentNullException(nameof(outcomes));
        this.physicalSources = physicalSources
            ?? throw new ArgumentNullException(nameof(physicalSources));
    }

    public bool TryPrepare(
        CaptiveState state,
        int absoluteDay,
        out PreparedCaptivityInteractionOutcome prepared,
        out long ownerRevision,
        out string failureReason)
    {
        prepared = default;
        ownerRevision = 0L;
        failureReason = string.Empty;
        CaptivityInteractionTerminalState terminal = state?.interactionTerminal;
        if (state == null || terminal?.HasOutcome != true)
        {
            failureReason = "기록할 포로 상호작용 결과가 없습니다.";
            return false;
        }
        if (terminal.outcomeRevision > 0L)
        {
            ownerRevision = terminal.outcomeRevision;
            if (state.interactionOutcomeRevision != ownerRevision)
            {
                failureReason = "포로 상호작용 원장 리비전이 저장 상태와 다릅니다.";
                return false;
            }
        }
        else
        {
            try
            {
                ownerRevision = checked(state.interactionOutcomeRevision + 1L);
            }
            catch (OverflowException)
            {
                failureReason = "포로 상호작용 원장 리비전이 범위를 초과했습니다.";
                return false;
            }
        }

        CaptivityInteractionOutcomeReceipt receipt;
        try
        {
            receipt = new CaptivityInteractionOutcomeReceipt(
                state.captiveId,
                Name("captive", state.captiveId, state.displayName),
                terminal.wardenId,
                Name("warden", terminal.wardenId, terminal.wardenDisplayName),
                terminal.facilityId,
                Name("facility", terminal.facilityId,
                    terminal.facilityDisplayName),
                terminal.resultGridX,
                terminal.resultGridY,
                terminal.interactionId,
                terminal.interactionKind,
                terminal.interactionDisplayName,
                terminal.success,
                terminal.message,
                terminal.willAfter - terminal.willBefore,
                terminal.fearAfter - terminal.fearBefore,
                terminal.trustAfter - terminal.trustBefore,
                terminal.grudgeAfter - terminal.grudgeBefore,
                terminal.corruptionAfter - terminal.corruptionBefore,
                terminal.bodyDamageAmount,
                terminal.bodyHealthBefore,
                terminal.bodyHealthAfter,
                terminal.outputItemId,
                terminal.outputAmount,
                terminal.attemptId,
                ownerRevision,
                absoluteDay);
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException)
        {
            failureReason = "포로 상호작용 영수증을 만들 수 없습니다: "
                + exception.Message;
            return false;
        }
        return outcomes.TryPrepare(receipt, out prepared, out failureReason);
    }

    public OwnerOutcomeCommitResult Commit(
        in PreparedCaptivityInteractionOutcome prepared) =>
        outcomes.Commit(prepared);

    public void Cancel(in PreparedCaptivityInteractionOutcome prepared) =>
        outcomes.Cancel(prepared);

    public bool TryEnsureOutput(
        CaptiveState state,
        out string failureReason)
    {
        failureReason = string.Empty;
        CaptivityInteractionTerminalState terminal = state?.interactionTerminal;
        if (terminal?.HasCommittedOutcome != true)
        {
            failureReason = "확정된 포로 상호작용 출력 상태가 없습니다.";
            return false;
        }
        if (!terminal.HasOutput)
            return true;
        string expectedOperation = CaptivityInteractionAttemptIdentity
            .FormatOutputOperationId(
            state.captiveId,
            terminal.attemptId);
        if (!string.Equals(
                terminal.outputOperationId,
                expectedOperation,
                StringComparison.Ordinal))
        {
            failureReason = "포로 상호작용 출력 작업 ID가 일치하지 않습니다.";
            return false;
        }
        if (terminal.outputPublished)
        {
            if (string.IsNullOrWhiteSpace(terminal.outputCommitId))
            {
                failureReason = "포로 상호작용 출력 커밋 ID가 없습니다.";
                return false;
            }
            return true;
        }

        Dictionary<string, int> outputs = new(StringComparer.Ordinal)
        {
            [terminal.outputItemId] = terminal.outputAmount
        };
        if (!physicalSources.TryEnsureLooseOutputs(
                outputs,
                terminal.ResultPosition,
                terminal.outputOperationId,
                OutputReasonCode,
                out PhysicalItemSourcePublicationReceipt receipt,
                out failureReason))
        {
            return false;
        }
        string commitId = receipt.OutputCommitIds?.SingleOrDefault()
            ?? string.Empty;
        if (!receipt.IsCommitted
            || !string.Equals(
                receipt.OperationId,
                terminal.outputOperationId,
                StringComparison.Ordinal)
            || !string.Equals(
                receipt.ReasonCode,
                OutputReasonCode,
                StringComparison.Ordinal)
            || receipt.OutputQuantity != terminal.outputAmount
            || string.IsNullOrWhiteSpace(commitId))
        {
            failureReason = "포로 상호작용 출력 영수증이 요청과 다릅니다.";
            return false;
        }
        terminal.outputCommitId = commitId;
        terminal.outputPublished = true;
        return true;
    }

    private static KoreanNameSnapshot Name(
        string role,
        string id,
        string display) => CaptivityInteractionOutcomeNames.Snapshot(
        role,
        id,
        string.IsNullOrWhiteSpace(display) ? id : display);
}
