using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using TMPro;

/// <summary>Projects AI decisions and records UI instrumentation cost.</summary>
public sealed class CharacterSummaryAiPresenter
{
    private readonly IDefenseEngagementRuntime defenseRuntime;
    private readonly ICharacterAiDiagnosticsQuery diagnostics;
    private readonly ICharacterAiPerformanceRecorder performanceRecorder;
    private TMP_Text summaryText;

    public CharacterSummaryAiPresenter(
        IDefenseEngagementRuntime defenseRuntime,
        ICharacterAiDiagnosticsQuery diagnostics,
        ICharacterAiPerformanceRecorder performanceRecorder)
    {
        this.defenseRuntime = defenseRuntime
            ?? throw new ArgumentNullException(nameof(defenseRuntime));
        this.diagnostics = diagnostics
            ?? throw new ArgumentNullException(nameof(diagnostics));
        this.performanceRecorder = performanceRecorder
            ?? throw new ArgumentNullException(nameof(performanceRecorder));
    }

    public void Bind(TMP_Text generatedSummaryText)
    {
        summaryText = generatedSummaryText;
    }

    public void Refresh(CharacterActor actor)
    {
        bool measure = performanceRecorder.DetailedCollectionEnabled;
        long allocatedBefore = measure ? GC.GetAllocatedBytesForCurrentThread() : 0L;
        long startedAt = measure ? Stopwatch.GetTimestamp() : 0L;
        try
        {
            RefreshCore(actor);
        }
        finally
        {
            if (measure)
            {
                double elapsedMilliseconds =
                    (Stopwatch.GetTimestamp() - startedAt) * 1000d / Stopwatch.Frequency;
                long allocatedBytes = Math.Max(
                    0L,
                    GC.GetAllocatedBytesForCurrentThread() - allocatedBefore);
                performanceRecorder.Record(
                    AiPerformanceCategory.UiFeedback,
                    elapsedMilliseconds,
                    allocatedBytes);
            }
        }
    }

    private void RefreshCore(CharacterActor actor)
    {
        if (summaryText == null)
        {
            return;
        }

        if (actor == null)
        {
            summaryText.text = "AI 정보가 없습니다.";
            return;
        }

        StringBuilder builder = new StringBuilder(1024);
        if (defenseRuntime.TryGetActorDefenseStatus(
                actor,
                out DefenseEngagement defenseEngagement,
                out string defenseRole,
                out string defenseStatus))
        {
            builder.AppendLine(
                $"방어 임무  {defenseRole} · {CharacterSummaryTextFormatter.Fallback(defenseStatus, "대기")}");
            builder.AppendLine(
                $"교전 위치  침입자 {defenseEngagement.IntruderStopCell} / 경비 {defenseEngagement.GuardCell}");
            builder.AppendLine($"공방 횟수  {defenseEngagement.ExchangeCount}");
            builder.AppendLine();
        }

        CharacterBlackboard blackboard = actor.Blackboard;
        if (blackboard != null)
        {
            builder.AppendLine($"현재 BT 분기  {CharacterAiUtilityText.GetBranchLabel(blackboard.CurrentBranch)}");
            builder.AppendLine(
                $"현재 의도  {CharacterSummaryTextFormatter.Fallback(blackboard.CurrentIntent, "없음")}");
            builder.AppendLine(
                $"행동 단계  {CharacterSummaryTextFormatter.Fallback(blackboard.CurrentTask, "대기")}"
                + $" · {CharacterSummaryTextFormatter.Fallback(blackboard.CurrentStatus, "판단 대기")}");
            builder.AppendLine($"의도 유지  {blackboard.GetSoftLockDebugSummary()}");

            CharacterAiWorldSignalSnapshot signals = actor.WorldSignalQuery?.Capture(
                    actor,
                    blackboard.CurrentBranch)
                ?? CharacterAiWorldSignalSnapshot.Neutral;
            builder.AppendLine($"주변 신호  {signals.ToCompactString()}");

            if (!string.IsNullOrWhiteSpace(blackboard.LastDecisionContextSummary))
            {
                builder.AppendLine();
                builder.AppendLine(blackboard.LastDecisionContextSummary);
            }

            IReadOnlyList<string> breakdowns = blackboard.TopUtilityBreakdowns;
            if (breakdowns != null && breakdowns.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("상위 후보 5개");
                foreach (string row in breakdowns.Take(5))
                {
                    builder.AppendLine($"- {row}");
                }
            }

            if (!string.IsNullOrWhiteSpace(blackboard.LastCommitBreakReason))
            {
                builder.AppendLine();
                builder.AppendLine($"최근 중단 사유  {blackboard.LastCommitBreakReason}");
            }
        }

        if (actor.Brain != null)
        {
            builder.AppendLine();
            builder.AppendLine("현재 행동");
            builder.AppendLine(actor.Brain.GetDebugSummary(5));
            builder.AppendLine();
            builder.AppendLine("AI 누적 진단");
            builder.Append("행동 시작 ").Append(actor.Brain.RuntimeActionStartCount)
                .Append(" / 전환 ").Append(actor.Brain.RuntimeActionSwitchCount)
                .Append(" / 단계 전환 ").Append(actor.Brain.RuntimePhaseTransitionCount)
                .Append(" / 재계획 ").AppendLine(actor.Brain.RuntimeImmediateReplanCount.ToString());
            builder.Append("실행 실패 ").Append(actor.Brain.RuntimeExecutionFailureCount)
                .Append(" / 후보 탈락 ").Append(actor.Brain.RuntimeCandidateRejectionCount)
                .Append(" / JobGiver 평가 탈락 ")
                .Append(actor.Brain.RuntimeJobGiverEvaluationRejectionCount)
                .Append(" / 동일 실패 최대 ").Append(actor.Brain.RuntimePeakRepeatedFailureCount)
                .Append(" (").Append(actor.Brain.RuntimeRepeatedFailureKind).AppendLine(")");
            builder.Append("최근 JobGiver 탈락: ")
                .AppendLine(actor.Brain.CurrentJobGiverEvaluationRejectionSummary);
        }

        builder.AppendLine();
        builder.AppendLine($"다음 판단  {diagnostics.GetNextDecisionDelay(actor):0.0}s 후");
        builder.AppendLine(
            $"AI 처리  {diagnostics.LastProcessingMilliseconds:0.00}ms"
            + $" · 경로 {diagnostics.LastPathSearchCount}/{diagnostics.CurrentPathSearchBudget}");

        if (actor.AiMemory != null)
        {
            builder.AppendLine();
            builder.AppendLine("최근 기억");
            builder.AppendLine(actor.AiMemory.GetRecentMemorySummary());
        }

        string text = builder.ToString().TrimEnd();
        summaryText.text = string.IsNullOrWhiteSpace(text)
            ? "아직 AI 판단 기록이 없습니다."
            : text;
    }
}
