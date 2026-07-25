using System;
using UnityEngine;

public readonly struct OperationTabSummary
{
    public OperationTabSummary(
        bool hasGameData,
        int day,
        int hour,
        int holdingMoney,
        int gameSpeed,
        int eventAlertCount,
        bool hasLatestReport,
        int latestReportDay,
        bool hasRunVariables)
    {
        HasGameData = hasGameData;
        Day = day;
        Hour = hour;
        HoldingMoney = holdingMoney;
        GameSpeed = gameSpeed;
        EventAlertCount = eventAlertCount;
        HasLatestReport = hasLatestReport;
        LatestReportDay = latestReportDay;
        HasRunVariables = hasRunVariables;
    }

    public bool HasGameData { get; }
    public int Day { get; }
    public int Hour { get; }
    public int HoldingMoney { get; }
    public int GameSpeed { get; }
    public int EventAlertCount { get; }
    public bool HasLatestReport { get; }
    public int LatestReportDay { get; }
    public bool HasRunVariables { get; }
}

public interface IOperationTabSummaryService
{
    OperationTabSummary Capture();
}

public sealed class OperationTabSummaryService : IOperationTabSummaryService
{
    private readonly DungeonSceneRuntimeReferences sceneReferences;

    public OperationTabSummaryService(DungeonSceneRuntimeReferences sceneReferences)
    {
        this.sceneReferences = sceneReferences ?? throw new ArgumentNullException(nameof(sceneReferences));
    }

    public OperationTabSummary Capture()
    {
        UIManager uiManager = sceneReferences.UIManager;
        GameData gameData = uiManager != null ? uiManager.gameData : null;
        OperatingDaySettlementRuntime settlement = sceneReferences.Settlement;
        EventAlertRuntime alerts = sceneReferences.Alerts;
        RunVariableRuntime runVariables = sceneReferences.RunVariables;

        bool hasGameData = gameData != null
            && gameData.day != null
            && gameData.hour != null
            && gameData.holdingMoney != null
            && gameData.gameSpeed != null;

        OperatingDayReport latestReport = settlement != null ? settlement.LatestReport : null;

        return new OperationTabSummary(
            hasGameData,
            hasGameData ? gameData.day.Value : 0,
            hasGameData ? gameData.hour.Value : 0,
            hasGameData ? gameData.holdingMoney.Value : 0,
            hasGameData ? gameData.gameSpeed.Value : 0,
            alerts != null ? alerts.EventLog.Count : 0,
            latestReport != null,
            latestReport != null ? latestReport.day : 0,
            runVariables != null);
    }
}
