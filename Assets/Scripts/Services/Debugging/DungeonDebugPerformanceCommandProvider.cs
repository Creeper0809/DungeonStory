using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public sealed class DungeonDebugPerformanceCommandProvider : IDungeonDebugCommandProvider
{
    private readonly ICharacterAiPerformanceRecorder recorder;
    private readonly ICharacterWorldQuery characterWorld;

    public DungeonDebugPerformanceCommandProvider(
        ICharacterAiPerformanceRecorder recorder,
        ICharacterWorldQuery characterWorld)
    {
        this.recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
        this.characterWorld = characterWorld ?? throw new ArgumentNullException(nameof(characterWorld));
    }

    public IEnumerable<IDungeonDebugCommand> GetCommands()
    {
        yield return new DelegateDungeonDebugCommand(
            "debug.ai.performance.summary",
            "AI 성능 요약",
            "최근 AI 측정값의 평균, p95, 최댓값, GC와 경로 캐시 적중률을 표시합니다.",
            DungeonDebugCategory.History,
            DungeonDebugTargetKind.None,
            _ => DungeonDebugCommandResult.Succeeded(FormatSummary(Capture())),
            mutatesWorld: false);

        yield return new DelegateDungeonDebugCommand(
            "debug.ai.performance.export",
            "AI 성능 리포트 저장",
            "최근 AI 측정값을 JSON 리포트로 저장합니다.",
            DungeonDebugCategory.History,
            DungeonDebugTargetKind.None,
            _ => ExportReport(),
            mutatesWorld: false);

        yield return new DelegateDungeonDebugCommand(
            "debug.ai.performance.reset",
            "AI 성능 측정 초기화",
            "현재까지 수집한 AI 성능 표본만 지웁니다.",
            DungeonDebugCategory.History,
            DungeonDebugTargetKind.None,
            _ =>
            {
                recorder.Reset();
                return DungeonDebugCommandResult.Succeeded("AI 성능 표본을 초기화했습니다.");
            },
            mutatesWorld: false);
    }

    private CharacterAiPerformanceReport Capture()
    {
        return recorder.CaptureReport(characterWorld.Characters?.Count ?? 0);
    }

    private DungeonDebugCommandResult ExportReport()
    {
        CharacterAiPerformanceReport report = Capture();
        string directory = Path.Combine(Application.persistentDataPath, "AiPerformanceReports");
        Directory.CreateDirectory(directory);
        string fileName = $"ai-performance-{DateTime.Now:yyyyMMdd-HHmmss}.json";
        string path = Path.Combine(directory, fileName);
        File.WriteAllText(path, JsonUtility.ToJson(report, true));
        return DungeonDebugCommandResult.Succeeded($"AI 성능 리포트를 저장했습니다: {path}");
    }

    private static string FormatSummary(CharacterAiPerformanceReport report)
    {
        double hitRate = report.brokerSearches + report.brokerCacheHits > 0
            ? report.brokerCacheHits * 100d / (report.brokerSearches + report.brokerCacheHits)
            : 0d;
        return $"{report.summary}\n"
            + $"최대 {report.scheduler.max:0.00}ms · GC {report.garbageCollection.average:0.0}KB/표본\n"
            + $"경로 검색 {report.brokerSearches} · 캐시 적중 {hitRate:0.0}% · "
            + $"예산 대기 {report.brokerBudgetDeferrals}";
    }
}
