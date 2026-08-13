using System;
using System.Text;

/// <summary>
/// Bounded counters for one character's AI lifecycle. Normal decision ticks only
/// mutate fixed arrays; snapshots copy them only when an audit requests evidence.
/// </summary>
public readonly struct CharacterAiRuntimeDiagnosticsSnapshot
{
    private readonly long[] executionFailuresByKind;
    private readonly long[] candidateRejectionsByKind;
    private readonly long[] jobGiverEvaluationRejectionsByBranchAndKind;

    public CharacterAiRuntimeDiagnosticsSnapshot(
        long actionStarts, long actionSwitches, long sameActionRestarts,
        long phaseTransitions, long immediateReplans, long interruptedReplans,
        long executionFailures, long noActionFailures, long candidateRejections,
        long duplicateExecutionSuppressions,
        long currentRepeatedFailureCount, long peakRepeatedFailureCount,
        AIActionFailureKind repeatedFailureKind,
        string lastExecutionFailureDetail,
        long[] executionFailuresByKind, long[] candidateRejectionsByKind,
        long jobGiverEvaluationRejections,
        long[] jobGiverEvaluationRejectionsByBranchAndKind)
    {
        ActionStarts = actionStarts;
        ActionSwitches = actionSwitches;
        SameActionRestarts = sameActionRestarts;
        PhaseTransitions = phaseTransitions;
        ImmediateReplans = immediateReplans;
        InterruptedReplans = interruptedReplans;
        ExecutionFailures = executionFailures;
        NoActionFailures = noActionFailures;
        CandidateRejections = candidateRejections;
        DuplicateExecutionSuppressions = duplicateExecutionSuppressions;
        CurrentRepeatedFailureCount = currentRepeatedFailureCount;
        PeakRepeatedFailureCount = peakRepeatedFailureCount;
        RepeatedFailureKind = repeatedFailureKind;
        LastExecutionFailureDetail = lastExecutionFailureDetail ?? string.Empty;
        this.executionFailuresByKind = executionFailuresByKind;
        this.candidateRejectionsByKind = candidateRejectionsByKind;
        JobGiverEvaluationRejections = jobGiverEvaluationRejections;
        this.jobGiverEvaluationRejectionsByBranchAndKind =
            jobGiverEvaluationRejectionsByBranchAndKind;
    }

    public long ActionStarts { get; }
    public long ActionSwitches { get; }
    public long SameActionRestarts { get; }
    public long PhaseTransitions { get; }
    public long ImmediateReplans { get; }
    public long InterruptedReplans { get; }
    public long ExecutionFailures { get; }
    public long NoActionFailures { get; }
    public long CandidateRejections { get; }
    public long DuplicateExecutionSuppressions { get; }
    public long JobGiverEvaluationRejections { get; }
    public long CurrentRepeatedFailureCount { get; }
    public long PeakRepeatedFailureCount { get; }
    public AIActionFailureKind RepeatedFailureKind { get; }
    public string LastExecutionFailureDetail { get; }

    public string FormatDeltaFrom(in CharacterAiRuntimeDiagnosticsSnapshot start)
    {
        StringBuilder builder = new StringBuilder(256);
        builder.Append("starts=").Append(ActionStarts - start.ActionStarts)
            .Append("; switches=").Append(ActionSwitches - start.ActionSwitches)
            .Append("; restarts=").Append(SameActionRestarts - start.SameActionRestarts)
            .Append("; phases=").Append(PhaseTransitions - start.PhaseTransitions)
            .Append("; replans=").Append(ImmediateReplans - start.ImmediateReplans)
            .Append("; interruptedReplans=").Append(InterruptedReplans - start.InterruptedReplans)
            .Append("; executionFailures=").Append(ExecutionFailures - start.ExecutionFailures)
            .Append("; noAction=").Append(NoActionFailures - start.NoActionFailures)
            .Append("; candidateRejections=").Append(CandidateRejections - start.CandidateRejections)
            .Append("; duplicateExecutionSuppressions=")
            .Append(DuplicateExecutionSuppressions - start.DuplicateExecutionSuppressions)
            .Append("; jobGiverEvaluationRejections=")
            .Append(JobGiverEvaluationRejections - start.JobGiverEvaluationRejections)
            .Append("; repeatedPeak=").Append(PeakRepeatedFailureCount)
            .Append('(').Append(RepeatedFailureKind).Append(')');
        if (ExecutionFailures > start.ExecutionFailures)
        {
            builder.Append("; lastExecutionFailure=")
                .Append(string.IsNullOrWhiteSpace(LastExecutionFailureDetail)
                    ? "unknown"
                    : LastExecutionFailureDetail);
        }
        AppendKinds(builder, "executionByKind", executionFailuresByKind, start.executionFailuresByKind);
        AppendKinds(builder, "candidateByKind", candidateRejectionsByKind, start.candidateRejectionsByKind);
        AppendJobGiverRejections(
            builder,
            jobGiverEvaluationRejectionsByBranchAndKind,
            start.jobGiverEvaluationRejectionsByBranchAndKind);
        return builder.ToString();
    }

    private static void AppendJobGiverRejections(
        StringBuilder builder,
        long[] end,
        long[] start)
    {
        bool wrote = false;
        int branchCount = Enum.GetValues(typeof(CharacterAiBranch)).Length;
        int failureKindCount = Enum.GetValues(typeof(AIActionFailureKind)).Length;
        for (int branchIndex = 1; branchIndex < branchCount; branchIndex++)
        {
            for (int failureIndex = 1;
                 failureIndex < failureKindCount;
                 failureIndex++)
            {
                int index = branchIndex * failureKindCount + failureIndex;
                long delta = At(end, index) - At(start, index);
                if (delta <= 0L)
                {
                    continue;
                }

                builder.Append(wrote ? ',' : ';');
                if (!wrote)
                {
                    builder.Append(" jobGiverByBranchKind=");
                }
                builder.Append((CharacterAiBranch)branchIndex)
                    .Append('/')
                    .Append((AIActionFailureKind)failureIndex)
                    .Append(':')
                    .Append(delta);
                wrote = true;
            }
        }

        if (!wrote)
        {
            builder.Append("; jobGiverByBranchKind=none");
        }
    }

    private static void AppendKinds(StringBuilder builder, string label, long[] end, long[] start)
    {
        bool wrote = false;
        int count = Enum.GetValues(typeof(AIActionFailureKind)).Length;
        for (int index = 1; index < count; index++)
        {
            long delta = At(end, index) - At(start, index);
            if (delta <= 0L) continue;
            builder.Append(wrote ? ',' : ';').Append(wrote ? string.Empty : " ").Append(wrote ? string.Empty : label + "=");
            builder.Append((AIActionFailureKind)index).Append(':').Append(delta);
            wrote = true;
        }
        if (!wrote) builder.Append("; ").Append(label).Append("=none");
    }

    private static long At(long[] values, int index) =>
        values != null && index >= 0 && index < values.Length ? values[index] : 0L;
}
