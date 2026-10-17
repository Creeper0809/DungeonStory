using System;
using UnityEngine;

internal sealed class CharacterAiFailureDiagnosticsRuntime
{
    private static readonly int FailureKindCount = Enum.GetValues(typeof(AIActionFailureKind)).Length;
    private static readonly int BranchCount = Enum.GetValues(typeof(CharacterAiBranch)).Length;
    private readonly long[] executionFailuresByKind =
        new long[Enum.GetValues(typeof(AIActionFailureKind)).Length];
    private readonly long[] candidateRejectionsByKind =
        new long[Enum.GetValues(typeof(AIActionFailureKind)).Length];
    private readonly long[] jobGiverEvaluationRejectionsByBranchAndKind =
        new long[
            Enum.GetValues(typeof(CharacterAiBranch)).Length
            * Enum.GetValues(typeof(AIActionFailureKind)).Length];
    private long executionFailureCount;
    private long noActionFailureCount;
    private long candidateRejectionCount;
    private long jobGiverEvaluationRejectionCount;
    private long currentRepeatedFailureCount;
    private long peakRepeatedFailureCount;
    private AIActionFailureKind repeatedFailureKind;
    private UnityEngine.Object repeatedFailureActionSet;
    private float repeatedFailureLastAt = float.NegativeInfinity;
    private CharacterAiBranch currentJobGiverRejectedBranch;
    private AIActionFailureKind currentJobGiverRejectedFailureKind;
    private string currentJobGiverRejectedReason = string.Empty;
    private int currentJobGiverEvaluationPassRevision;
    private readonly int[] currentJobGiverRejectedRevisionByBranch =
        new int[BranchCount];
    private readonly AIActionFailureKind[] currentJobGiverRejectedKindByBranch =
        new AIActionFailureKind[BranchCount];
    private readonly string[] currentJobGiverRejectedReasonByBranch =
        new string[BranchCount];
    private long failureLoopCount;
    private string lastExecutionFailureDetail = string.Empty;

    internal string GetCurrentJobGiverEvaluationRejectionSummary(
        CharacterAiBranch branch)
    {
        int index = (int)branch;
        if (index < 0
            || index >= BranchCount
            || currentJobGiverRejectedRevisionByBranch[index]
                != currentJobGiverEvaluationPassRevision)
        {
            return "none";
        }

        return $"{branch}/{currentJobGiverRejectedKindByBranch[index]}: "
            + currentJobGiverRejectedReasonByBranch[index];
    }

    internal void BeginJobGiverEvaluationPass()
    {
        if (currentJobGiverEvaluationPassRevision == int.MaxValue)
        {
            Array.Clear(
                currentJobGiverRejectedRevisionByBranch,
                0,
                currentJobGiverRejectedRevisionByBranch.Length);
            currentJobGiverEvaluationPassRevision = 1;
        }
        else
        {
            currentJobGiverEvaluationPassRevision++;
        }

        currentJobGiverRejectedBranch = CharacterAiBranch.None;
        currentJobGiverRejectedFailureKind = AIActionFailureKind.None;
        currentJobGiverRejectedReason = string.Empty;
    }

    internal void TrackExecutionFailure(UnityEngine.Object actionSet, AIActionFailureKind kind, bool isNoAction,
        string detail, float cooldownSeconds, float now, CharacterAiExecutionLedger ledger, in CharacterAiTraceContext context)
    {
        lastExecutionFailureDetail = detail;
        executionFailureCount++;
        if (isNoAction)
        {
            noActionFailureCount++;
        }
        IncrementFailureKind(executionFailuresByKind, kind);
        ledger.RecordRuntimeTrace(context,
            CharacterAiRuntimeTraceKind.ExecutionFailure, kind);

        float repeatedFailureWindow = Mathf.Max(
            5f,
            Mathf.Max(0.1f, cooldownSeconds) * 4f);
        bool withinRepeatedFailureWindow =
            repeatedFailureLastAt > float.NegativeInfinity
            && now - repeatedFailureLastAt <= repeatedFailureWindow;
        if (withinRepeatedFailureWindow
            && kind == repeatedFailureKind
            && actionSet == repeatedFailureActionSet)
        {
            currentRepeatedFailureCount++;
        }
        else
        {
            repeatedFailureKind = kind;
            repeatedFailureActionSet = actionSet;
            currentRepeatedFailureCount = 1L;
        }

        if (currentRepeatedFailureCount > peakRepeatedFailureCount)
        {
            peakRepeatedFailureCount = currentRepeatedFailureCount;
        }
        repeatedFailureLastAt = now;
        if (currentRepeatedFailureCount == 4L)
        {
            failureLoopCount++;
        }
    }

    private static void IncrementFailureKind(long[] counts, AIActionFailureKind kind)
    {
        int index = (int)kind;
        if (index > 0 && index < counts.Length)
        {
            counts[index]++;
        }
    }

    internal void RecordCandidateRejection(AIActionFailureKind kind)
    {
        candidateRejectionCount++;
        IncrementFailureKind(candidateRejectionsByKind, kind);
    }

    internal void RecordJobGiverKind(CharacterAiBranch branch, AIActionFailureKind kind)
    {
        int index = (int)branch * FailureKindCount + (int)kind;
        if (index >= 0
            && index < jobGiverEvaluationRejectionsByBranchAndKind.Length)
        {
            jobGiverEvaluationRejectionsByBranchAndKind[index]++;
        }

    }

    internal void RecordHardJobGiverRejection(CharacterAiBranch branch, AIActionFailureKind kind, string reason)
    {
        jobGiverEvaluationRejectionCount++;
        currentJobGiverRejectedBranch = branch;
        currentJobGiverRejectedFailureKind = kind;
        currentJobGiverRejectedReason = reason;
        int branchIndex = (int)branch;
        if (branchIndex >= 0 && branchIndex < BranchCount)
        {
            currentJobGiverRejectedRevisionByBranch[branchIndex] =
                currentJobGiverEvaluationPassRevision;
            currentJobGiverRejectedKindByBranch[branchIndex] = kind;
            currentJobGiverRejectedReasonByBranch[branchIndex] =
                currentJobGiverRejectedReason;
        }
    }

    internal long[] CopyExecutionFailures() => (long[])executionFailuresByKind.Clone();
    internal long[] CopyCandidateRejections() => (long[])candidateRejectionsByKind.Clone();
    internal long[] CopyJobGiverRejections() => (long[])jobGiverEvaluationRejectionsByBranchAndKind.Clone();

    internal long ExecutionFailureCount => executionFailureCount;
    internal long NoActionFailureCount => noActionFailureCount;
    internal long CandidateRejectionCount => candidateRejectionCount;
    internal long JobGiverEvaluationRejectionCount => jobGiverEvaluationRejectionCount;
    internal long CurrentRepeatedFailureCount => currentRepeatedFailureCount;
    internal long PeakRepeatedFailureCount => peakRepeatedFailureCount;
    internal AIActionFailureKind RepeatedFailureKind => repeatedFailureKind;
    internal CharacterAiBranch CurrentJobGiverRejectedBranch => currentJobGiverRejectedBranch;
    internal AIActionFailureKind CurrentJobGiverRejectedFailureKind => currentJobGiverRejectedFailureKind;
    internal string CurrentJobGiverRejectedReason => currentJobGiverRejectedReason;
    internal long FailureLoopCount => failureLoopCount;
    internal string LastExecutionFailureDetail => lastExecutionFailureDetail;
}
