using System;
using DungeonStory.Operation;
using System.Collections.Generic;
using System.Linq;

public enum CodexFeatureViewMode
{
    Codex,
    Reports,
    Events
}

public sealed class CodexFeatureSurfaceModel
{
    public bool RuntimeAvailable { get; set; }
    public string Summary { get; set; } = string.Empty;
    public IReadOnlyList<CodexFeatureEntryRow> CodexEntries { get; set; }
        = Array.Empty<CodexFeatureEntryRow>();
    public IReadOnlyList<CodexFeatureReportRow> Reports { get; set; }
        = Array.Empty<CodexFeatureReportRow>();
    public IReadOnlyList<CodexFeatureEventRow> Events { get; set; }
        = Array.Empty<CodexFeatureEventRow>();
}

public sealed class CodexFeatureEntryRow
{
    public string EntryId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}

public sealed class CodexFeatureReportRow
{
    public string ActionId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string Feedback { get; set; } = string.Empty;
}

public sealed class CodexFeatureEventRow
{
    public int RecordId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ButtonText { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}

public readonly struct CodexFeatureCommandResult
{
    public CodexFeatureCommandResult(bool succeeded, string message)
    {
        Succeeded = succeeded;
        Message = message ?? string.Empty;
    }

    public bool Succeeded { get; }
    public string Message { get; }
}

public interface ICodexFeatureQueryService
{
    CodexFeatureSurfaceModel Capture(
        CodexFeatureViewMode mode,
        CodexEntryCategory category,
        EventAlertImportance? eventImportance);
}

public interface ICodexFeatureCommandService
{
    CodexFeatureCommandResult OpenEvent(int recordId);
    CodexFeatureCommandResult QueueMemoryResidueAnalysis();
}

public sealed class CodexFeatureQueryService : ICodexFeatureQueryService
{
    private readonly CodexRuntime codex;
    private readonly InvasionCombatReportRuntime invasionReports;
    private readonly IOffenseQuery offense;
    private readonly OperatingDaySettlementRuntime settlement;
    private readonly EventAlertRuntime eventAlerts;

    public CodexFeatureQueryService(
        FacilityFeatureSceneRuntimeReferences facilityRuntimes,
        InvasionSceneRuntimeReferences invasionRuntimes,
        IOffenseQuery offense,
        DungeonSceneRuntimeReferences sceneRuntimes)
    {
        codex = (facilityRuntimes
                ?? throw new ArgumentNullException(nameof(facilityRuntimes)))
            .Codex
            ?? throw new InvalidOperationException(
                $"{nameof(CodexFeatureQueryService)} requires a loaded {nameof(CodexRuntime)}.");
        invasionReports = (invasionRuntimes
                ?? throw new ArgumentNullException(nameof(invasionRuntimes)))
            .CombatReport
            ?? throw new InvalidOperationException(
                $"{nameof(CodexFeatureQueryService)} requires a loaded {nameof(InvasionCombatReportRuntime)}.");
        this.offense = offense ?? throw new ArgumentNullException(nameof(offense));
        settlement = (sceneRuntimes
                ?? throw new ArgumentNullException(nameof(sceneRuntimes)))
            .Settlement
            ?? throw new InvalidOperationException(
                $"{nameof(CodexFeatureQueryService)} requires a loaded {nameof(OperatingDaySettlementRuntime)}.");
        eventAlerts = sceneRuntimes.Alerts
            ?? throw new InvalidOperationException(
                $"{nameof(CodexFeatureQueryService)} requires a loaded {nameof(EventAlertRuntime)}.");
    }

    public CodexFeatureSurfaceModel Capture(
        CodexFeatureViewMode mode,
        CodexEntryCategory category,
        EventAlertImportance? eventImportance)
    {
        return mode switch
        {
            CodexFeatureViewMode.Codex => CaptureCodex(category),
            CodexFeatureViewMode.Reports => CaptureReports(),
            CodexFeatureViewMode.Events => CaptureEvents(eventImportance),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
    }

    private CodexFeatureSurfaceModel CaptureCodex(CodexEntryCategory category)
    {
        CodexRuntime runtime = codex;
        if (runtime == null)
        {
            return new CodexFeatureSurfaceModel
            {
                Summary = "도감 런타임이 현재 씬에 없습니다."
            };
        }

        return new CodexFeatureSurfaceModel
        {
            RuntimeAvailable = true,
            Summary = $"전체 기록 {runtime.State.Entries.Count}개 / 현재 {category}",
            CodexEntries = runtime.GetEntries(category)
                .Take(CodexFeatureSurfacePresenter.MaxVisibleCardsPerSection)
                .Select(entry => new CodexFeatureEntryRow
                {
                    EntryId = entry.entryId,
                    Title = entry.title,
                    Summary =
                        $"정보 {entry.lines?.Length ?? 0}개 / {(entry.discovered ? "발견" : "미발견")}",
                    Detail = entry.ToDisplayText()
                })
                .ToArray()
        };
    }

    private CodexFeatureSurfaceModel CaptureReports()
    {
        InvasionCombatReportRuntime invasion = invasionReports;
        OffenseCampaignSnapshot offenseCampaign = offense.Capture();
        OperatingDaySettlementRuntime operation = settlement;
        List<CodexFeatureReportRow> reports = new List<CodexFeatureReportRow>();

        if (invasion?.ReportHistory.FirstOrDefault()
            is InvasionCombatReportSnapshot invasionReport)
        {
            reports.Add(new CodexFeatureReportRow
            {
                ActionId = "P2Action_ArchiveInvasionReport",
                Title = "최근 침공 보고서",
                Detail = invasionReport.ToDetailText(),
                Feedback = "최근 침공 보고서를 확인했습니다."
            });
        }

        if (offenseCampaign.ResultHistory.FirstOrDefault()
            is OffenseExpeditionResult offenseResult)
        {
            reports.Add(new CodexFeatureReportRow
            {
                ActionId = "P2Action_ArchiveExpeditionReport",
                Title = "최근 원정 보고서",
                Detail = offenseResult.ToDetailText(),
                Feedback = "최근 원정 보고서를 확인했습니다."
            });
        }

        if (operation?.ReportHistory.FirstOrDefault()
            is OperatingDayReport operationReport)
        {
            reports.Add(new CodexFeatureReportRow
            {
                ActionId = "P2Action_ArchiveOperationReport",
                Title = "최근 운영 보고서",
                Detail = operationReport.ToDetailText(),
                Feedback = "최근 운영 보고서를 확인했습니다."
            });
        }

        return new CodexFeatureSurfaceModel
        {
            RuntimeAvailable = true,
            Summary =
                $"침공 {invasion?.ReportHistory.Count ?? 0} / " +
                $"원정 {offenseCampaign.ResultHistory.Count} / " +
                $"운영 {operation?.ReportHistory.Count ?? 0}",
            Reports = reports
        };
    }

    private CodexFeatureSurfaceModel CaptureEvents(
        EventAlertImportance? eventImportance)
    {
        EventAlertRuntime runtime = eventAlerts;

        EventAlertRecord[] records = runtime.EventLog
            .Where(record => record != null
                && (!eventImportance.HasValue
                    || record.Importance == eventImportance.Value))
            .Reverse()
            .Take(CodexFeatureSurfacePresenter.MaxVisibleCardsPerSection)
            .ToArray();
        return new CodexFeatureSurfaceModel
        {
            RuntimeAvailable = true,
            Summary =
                $"전체 {runtime.EventLog.Count}건 / " +
                $"필터 {(eventImportance?.ToString() ?? "전체")}",
            Events = records.Select(record => new CodexFeatureEventRow
                {
                    RecordId = record.Id,
                    Title = record.Title,
                    ButtonText = record.ButtonText,
                    Summary =
                        $"{record.Importance} / {record.Category} / " +
                        $"선택지 {record.Choices.Count}개",
                    Detail = record.ToDetailText()
                })
                .ToArray()
        };
    }
}

public sealed class CodexFeatureCommandService : ICodexFeatureCommandService
{
    private readonly EventAlertRuntime eventAlerts;
    private readonly IKnowledgeResidueProcessingRuntime knowledgeProcessing;

    public CodexFeatureCommandService(
        DungeonSceneRuntimeReferences sceneRuntimes,
        IKnowledgeResidueProcessingRuntime knowledgeProcessing)
    {
        eventAlerts = (sceneRuntimes
                ?? throw new ArgumentNullException(nameof(sceneRuntimes)))
            .Alerts
            ?? throw new InvalidOperationException(
                $"{nameof(CodexFeatureCommandService)} requires a loaded {nameof(EventAlertRuntime)}.");
        this.knowledgeProcessing = knowledgeProcessing
            ?? throw new ArgumentNullException(nameof(knowledgeProcessing));
    }

    public CodexFeatureCommandResult OpenEvent(int recordId)
    {
        EventAlertRuntime runtime = eventAlerts;
        EventAlertRecord record = runtime.EventLog.FirstOrDefault(
            candidate => candidate != null && candidate.Id == recordId);
        if (record == null)
        {
            return new CodexFeatureCommandResult(
                false,
                "선택한 이벤트 기록을 찾을 수 없습니다.");
        }

        runtime.Open(record);
        return new CodexFeatureCommandResult(
            true,
            $"이벤트 선택: {record.Title}");
    }

    public CodexFeatureCommandResult QueueMemoryResidueAnalysis()
    {
        bool succeeded = knowledgeProcessing.TryQueueCodexAnalysis(
            out string message);
        return new CodexFeatureCommandResult(succeeded, message);
    }
}

public sealed class CodexFeatureSurfacePresenter : IFeatureSurfaceTabPresenter
{
    internal const int MaxVisibleCardsPerSection = 8;

    private readonly ICodexFeatureQueryService queryService;
    private readonly ICodexFeatureCommandService commandService;
    private CodexFeatureViewMode viewMode;
    private CodexEntryCategory category = CodexEntryCategory.Monster;
    private EventAlertImportance? eventImportance;
    private string selectedEntryId = string.Empty;
    private int selectedEventId = -1;

    public CodexFeatureSurfacePresenter(
        ICodexFeatureQueryService queryService,
        ICodexFeatureCommandService commandService)
    {
        this.queryService = queryService
            ?? throw new ArgumentNullException(nameof(queryService));
        this.commandService = commandService
            ?? throw new ArgumentNullException(nameof(commandService));
    }

    public TabId Id => TabId.Codex;

    public void Present(IFeatureSurfaceView view)
    {
        if (view == null)
        {
            throw new ArgumentNullException(nameof(view));
        }

        view.AddSection(
            "도감/기록",
            "도감, 전투·운영 보고서, 이벤트 히스토리를 조회합니다.");
        AddModeButton(view, CodexFeatureViewMode.Codex, "도감", "P2Action_ArchiveCodex");
        AddModeButton(view, CodexFeatureViewMode.Reports, "보고서", "P2Action_ArchiveReports");
        AddModeButton(view, CodexFeatureViewMode.Events, "이벤트", "P2Action_ArchiveEvents");

        CodexFeatureSurfaceModel model =
            queryService.Capture(viewMode, category, eventImportance);
        switch (viewMode)
        {
            case CodexFeatureViewMode.Codex:
                PresentCodex(view, model);
                break;
            case CodexFeatureViewMode.Reports:
                PresentReports(view, model);
                break;
            case CodexFeatureViewMode.Events:
                PresentEvents(view, model);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void PresentCodex(
        IFeatureSurfaceView view,
        CodexFeatureSurfaceModel model)
    {
        if (!model.RuntimeAvailable)
        {
            view.AddLabel(model.Summary, 20f, 64f);
            return;
        }

        view.AddSection("도감 분류", model.Summary);
        view.AddDataCard(
            "P2Action_CodexMemoryResidue",
            "기억 잔재 분석",
            "기억 잔재 1개를 연구 시설로 운반하고 작업량을 채워 새로운 단서를 정리합니다.",
            "분석 준비",
            () =>
            {
                CodexFeatureCommandResult result =
                    commandService.QueueMemoryResidueAnalysis();
                view.ShowFeedback(result.Message);
                view.RequestRefresh();
            },
            82f);
        AddCategoryButton(view, CodexEntryCategory.Monster, "몬스터", 0);
        AddCategoryButton(view, CodexEntryCategory.Invasion, "침공", 1);
        AddCategoryButton(view, CodexEntryCategory.Facility, "시설", 2);
        if (model.CodexEntries.Count == 0)
        {
            view.AddLabel("이 분류에서 발견한 기록이 없습니다.", 18f, 40f);
        }

        int index = 0;
        foreach (CodexFeatureEntryRow row in model.CodexEntries)
        {
            CodexFeatureEntryRow captured = row;
            bool selected = selectedEntryId == captured.EntryId;
            view.AddDataCard(
                $"P2Action_CodexEntry_{index++}",
                captured.Title,
                selected ? captured.Detail : captured.Summary,
                selected ? "선택됨" : "상세",
                () =>
                {
                    selectedEntryId = captured.EntryId;
                    view.ShowFeedback($"도감 선택: {captured.Title}");
                },
                selected ? 150f : 66f);
        }
    }

    private static void PresentReports(
        IFeatureSurfaceView view,
        CodexFeatureSurfaceModel model)
    {
        view.AddSection("보고서 아카이브", model.Summary);
        if (model.Reports.Count == 0)
        {
            view.AddLabel("아직 완료된 보고서가 없습니다.", 18f, 40f);
        }

        foreach (CodexFeatureReportRow row in model.Reports)
        {
            CodexFeatureReportRow captured = row;
            view.AddDataCard(
                captured.ActionId,
                captured.Title,
                captured.Detail,
                "확인",
                () => view.ShowFeedback(captured.Feedback),
                180f);
        }
    }

    private void PresentEvents(
        IFeatureSurfaceView view,
        CodexFeatureSurfaceModel model)
    {
        if (!model.RuntimeAvailable)
        {
            view.AddLabel(model.Summary, 20f, 64f);
            return;
        }

        view.AddSection("알림/이벤트 히스토리", model.Summary);
        AddEventFilterButton(view, null, "전체", 0);
        AddEventFilterButton(view, EventAlertImportance.High, "높음", 1);
        AddEventFilterButton(view, EventAlertImportance.Medium, "중간", 2);
        AddEventFilterButton(view, EventAlertImportance.Low, "낮음", 3);
        if (model.Events.Count == 0)
        {
            view.AddLabel("현재 필터에 해당하는 이벤트 기록이 없습니다.", 18f, 40f);
        }

        int index = 0;
        foreach (CodexFeatureEventRow row in model.Events)
        {
            CodexFeatureEventRow captured = row;
            bool selected = selectedEventId == captured.RecordId;
            view.AddDataCard(
                $"P2Action_EventRecord_{index++}",
                captured.ButtonText,
                selected ? captured.Detail : captured.Summary,
                selected ? "선택됨" : "상세",
                () =>
                {
                    selectedEventId = captured.RecordId;
                    view.ShowFeedback(
                        commandService.OpenEvent(captured.RecordId).Message);
                },
                selected ? 170f : 66f);
        }
    }

    private void AddModeButton(
        IFeatureSurfaceView view,
        CodexFeatureViewMode mode,
        string label,
        string actionId)
    {
        view.AddDataCard(
            actionId,
            viewMode == mode ? $"{label} 선택됨" : label,
            "기록 화면을 전환합니다.",
            label,
            () =>
            {
                viewMode = mode;
                view.ShowFeedback($"기록 화면 전환: {label}");
            },
            66f);
    }

    private void AddCategoryButton(
        IFeatureSurfaceView view,
        CodexEntryCategory nextCategory,
        string label,
        int index)
    {
        view.AddDataCard(
            $"P2Action_CodexCategory_{index}",
            category == nextCategory ? $"{label} 선택됨" : label,
            "도감 분류를 전환합니다.",
            label,
            () =>
            {
                category = nextCategory;
                selectedEntryId = string.Empty;
                view.ShowFeedback($"도감 분류: {label}");
            },
            66f);
    }

    private void AddEventFilterButton(
        IFeatureSurfaceView view,
        EventAlertImportance? importance,
        string label,
        int index)
    {
        view.AddDataCard(
            $"P2Action_EventFilter_{index}",
            eventImportance == importance ? $"{label} 선택됨" : label,
            "이벤트 중요도 필터를 전환합니다.",
            label,
            () =>
            {
                eventImportance = importance;
                selectedEventId = -1;
                view.ShowFeedback($"이벤트 필터: {label}");
            },
            66f);
    }
}
