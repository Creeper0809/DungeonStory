#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class EmergencyLaborDebugScenarios
{
    [MenuItem("Tools/Dungeon Story/Validation/Phase 157 Emergency Labor")]
    public static void RunAll()
    {
        ValidateBaselineAndProjectCurves();
        ValidateProjectWorkforceAndRiskForecasts();
        ValidateDailyLaborChannels();
        ValidateAutomaticWorkAccounting();
        ValidateDisasterShadowSimulation();
        ValidateWorkClassificationAndAccounting();
        ValidateAlertHysteresisAndPersistence();
        Debug.Log(
            $"PHASE157_EMERGENCY_LABOR=PASS; schedule=180s/99WU-historical-envelope; actual={SettlementLaborAuthority.ActualWuPerAdultDay:0.##}WU; effective={SettlementLaborAuthority.EffectiveOutputWuPerAdultDay:0.##}WU; workTypes=31; "
            + "landmark8=5.00; research4=2.40; redToGreen=4h; save=stable");
    }

    private static void ValidateAutomaticWorkAccounting()
    {
        ProductionBillSnapshot before = new ProductionBillSnapshot
        {
            RequiredWork = 10f,
            CompletedWork = 8.5f
        };
        ProductionBillSnapshot progressed = new ProductionBillSnapshot
        {
            RequiredWork = 10f,
            CompletedWork = 9.25f
        };
        ProductionWorkExecutionResult progress = new ProductionWorkExecutionResult(
            true,
            false,
            ProductionBillOutcomeCode.WorkProgressed,
            DomainFailure.None);
        Require(Mathf.Approximately(
                AutomationLaborAccountingRules.CalculateAcceptedWork(
                    before,
                    progressed,
                    progress),
                0.75f),
            "Automation accounting did not use actual bill progress.");

        ProductionWorkExecutionResult completed = new ProductionWorkExecutionResult(
            true,
            true,
            ProductionBillOutcomeCode.CycleCompleted,
            DomainFailure.None);
        Require(Mathf.Approximately(
                AutomationLaborAccountingRules.CalculateAcceptedWork(
                    before,
                    null,
                    completed),
                1.5f),
            "Automation final tick did not clamp to remaining work.");
        Require(Mathf.Approximately(
                AutomationLaborAccountingRules.CalculateNetAutomaticWork(
                    before,
                    null,
                    completed,
                    0.25f),
                1.25f),
            "Automation net WU did not deduct its same-tick maintenance burden.");
    }

    private static void ValidateDailyLaborChannels()
    {
        GameEventBus eventBus = new GameEventBus();
        FixedReserveTargetQuery reserve = new FixedReserveTargetQuery(
            productiveAdults: 3,
            targetMilliWu: 12_000L);
        SettlementLaborAccountingRuntime labor =
            new SettlementLaborAccountingRuntime(eventBus, reserve);
        labor.Start();
        SettlementLaborContribution contribution = new SettlementLaborContribution(
            "labor:test:day1",
            1L,
            SettlementLaborContributionChannel.ActualLabor,
            150_000L,
            "work:test");
        Require(labor.Record(contribution).Success, "Daily labor record failed.");
        Require(labor.Record(contribution).Success,
            "Duplicate daily labor record must be idempotent.");
        Require(labor.Record(new SettlementLaborContribution(
                "automation:test:food",
                1L,
                SettlementLaborContributionChannel.DomainAutomation,
                20_000L,
                "domain:food")).Success,
            "Domain automation record failed.");
        Require(labor.Record(new SettlementLaborContribution(
                "maintenance:test:day1",
                1L,
                SettlementLaborContributionChannel.EssentialMaintenance,
                15_000L,
                "survival:routine")).Success,
            "Essential-maintenance labor record failed.");
        SettlementLaborAccountingSnapshot live = labor.Capture();
        Require(live.OutputEquivalentMilliWu == 170_000L
            && live.RealizedGrowthMilliWu == 135_000L
            && live.GuaranteedGrowthMilliWu == 123_000L,
            "Live settlement WU projection diverged from the day-end authority.");
        eventBus.Publish(new OperatingDayEndedEvent(1));
        SettlementLaborAccountingSnapshot snapshot = labor.Capture();
        Require(snapshot.LatestDay.ActualLaborMilliWu == 150_000L
            && snapshot.LatestDay.OutputEquivalentMilliWu == 170_000L
            && snapshot.LatestDay.RealizedGrowthMilliWu == 135_000L
            && snapshot.LatestDay.GuaranteedGrowthMilliWu == 123_000L
            && Mathf.Approximately(
                snapshot.RollingPerCapitaNetWuMedian,
                1f),
            "Daily WU channels or per-capita rolling median are incorrect.");
        DungeonStory.Infrastructure.SettlementLaborSaveData saved =
            labor.CaptureLaborSaveData();
        SettlementLaborAccountingRuntime restored =
            new SettlementLaborAccountingRuntime(eventBus, reserve);
        restored.RestoreLaborSaveData(saved);
        SettlementLaborAccountingSnapshot restoredSnapshot = restored.Capture();
        Require(restoredSnapshot.LatestDay.AbsoluteDay == 1
            && Mathf.Approximately(
                restoredSnapshot.RollingPerCapitaNetWuMedian,
                snapshot.RollingPerCapitaNetWuMedian),
            "Daily labor save round trip changed the rolling authority.");
        labor.Dispose();
    }

    private static void ValidateDisasterShadowSimulation()
    {
        DisasterShadowScenarioInput day120 = new DisasterShadowScenarioInput(
            productiveAdultCount: 11,
            unavailableAdultCount: 3,
            emergencyResponderCount: 2,
            adultWuPerDay: SettlementLaborAuthority.EffectiveOutputWuPerAdultDay,
            essentialWuPerDay: 254.25f,
            foodSupplyDays: 7,
            waterSupplyDays: 7,
            crisisDurationDays: 3,
            recoveredAdultsByDaySeven: 2);
        DisasterShadowSimulationSnapshot viable =
            SettlementLaborBalanceRules.EvaluateDisasterShadow(in day120);
        Require(viable.AvailableAdults == 6
            && viable.EssentialCoverage >= 1f
            && viable.FoodDaysAfterCrisis == 4
            && viable.WaterDaysAfterCrisis == 4
            && viable.DaySevenEssentialCoverage >= 1.10f
            && viable.Passed,
            "Day-120 disaster shadow did not protect three days of essentials and seven-day recovery.");

        DisasterShadowScenarioInput deathSpiral =
            new DisasterShadowScenarioInput(
                productiveAdultCount: 11,
                unavailableAdultCount: 4,
                emergencyResponderCount: 3,
                adultWuPerDay: SettlementLaborAuthority.EffectiveOutputWuPerAdultDay,
                essentialWuPerDay: 254.25f,
                foodSupplyDays: 2,
                waterSupplyDays: 2,
                crisisDurationDays: 3,
                recoveredAdultsByDaySeven: 0);
        DisasterShadowSimulationSnapshot failed =
            SettlementLaborBalanceRules.EvaluateDisasterShadow(in deathSpiral);
        Require(!failed.Passed
            && failed.EssentialDeficitWuPerDay > 0f
            && failed.GrowthWuPerDay == 0f,
            "Disaster shadow did not cut growth first or expose an essential-work deficit.");
    }

    private static void ValidateProjectWorkforceAndRiskForecasts()
    {
        ProjectWorkforceRuntime workforce = new ProjectWorkforceRuntime();
        List<ProjectWorkerLease> leases = new List<ProjectWorkerLease>();
        for (int index = 0; index < 8; index++)
        {
            Require(workforce.TryJoin(
                    "project:test-landmark",
                    $"worker:{index}",
                    ProjectScale.Landmark,
                    8,
                    out ProjectWorkerLease lease,
                    out string failure),
                failure);
            Require(workforce.UpdateWorkerRate(
                    "project:test-landmark",
                    $"worker:{index}",
                    1f),
                "A joined landmark worker did not publish its live WU rate.");
            leases.Add(lease);
        }
        Require(!workforce.CanJoin(
                "project:test-landmark",
                "worker:overflow",
                8),
            "A ninth landmark worker passed the hard cap.");
        Require(workforce.TryCapture(
                "project:test-landmark",
                out ProjectWorkforceSnapshot project)
            && project.ActiveWorkers == 8
            && project.DefaultAutomaticWorkerLimit == 5
            && Mathf.Approximately(project.EffectiveWorkerCount, 5f)
            && Mathf.Approximately(project.EffectiveWuPerSecond, 5f)
            && Mathf.Approximately(project.ReferenceWorkerWuPerSecond, 1f),
            "Live project workforce did not expose the 8-worker/5-effective authority.");
        Require(SettlementLaborBalanceRules.GetMaximumWorkers(
                ProjectScale.SmallFacility) == 2
            && SettlementLaborBalanceRules.GetMaximumWorkers(
                ProjectScale.MediumFacility) == 3
            && SettlementLaborBalanceRules.GetMaximumWorkers(
                ProjectScale.IndustrialFacility) == 4,
            "Facility construction project caps are not 2/3/4 workers.");
        for (int index = 0; index < leases.Count; index++)
        {
            leases[index].Dispose();
        }

        EmergencyRiskForecastRegistry forecasts =
            new EmergencyRiskForecastRegistry();
        Require(forecasts.SetP90Requirement("risk:fire", 18_000L).Success,
            "Fire risk forecast registration failed.");
        Require(forecasts.SetP90Requirement("risk:medical", 24_000L).Success,
            "Medical risk forecast registration failed.");
        EmergencyRiskForecastSnapshot risk = forecasts.Capture();
        Require(risk.HighestP90MilliWu == 24_000L
            && risk.LimitingSourceId == "risk:medical"
            && risk.SourceCount == 2,
            "P90 risk forecast maximum was not deterministic.");
        Require(forecasts.Remove("risk:medical").Success
            && forecasts.Capture().HighestP90MilliWu == 18_000L,
            "Removing the limiting risk did not reveal the next P90 requirement.");

        CharacterMedicalOrder first = new CharacterMedicalOrder
        {
            requiredStabilizationWork = 8f,
            completedStabilizationWork = 2f,
            requiredTreatmentWork = 14f,
            completedTreatmentWork = 2f,
            state = CharacterMedicalOrderState.AwaitingStabilization
        };
        CharacterMedicalOrder second = new CharacterMedicalOrder
        {
            requiredStabilizationWork = 5f,
            requiredTreatmentWork = 8f,
            completedTreatmentWork = 1f,
            state = CharacterMedicalOrderState.AwaitingRescue
        };
        Require(SettlementThreatEventAdapter.CalculateMedicalP90MilliWu(
                2,
                new[] { first, second }) == 30_000L,
            "Live medical P90 did not use remaining authored treatment WU with the 30-WU response window.");
        Require(SettlementThreatEventAdapter.CalculateMedicalP90MilliWu(
                2,
                Array.Empty<CharacterMedicalOrder>()) == 24_000L,
            "Medical P90 floor no longer reserves 12 WU per downed patient.");
    }

    private static void ValidateBaselineAndProjectCurves()
    {
        DailyLaborBudget budget = SettlementLaborBalanceRules.CreateBaselineDailyBudget();
        Require(Mathf.Approximately(budget.TotalSeconds, 180f), "Daily budget is not 180 seconds.");
        Require(Mathf.Approximately(
                budget.NetLaborWu,
                SettlementLaborAuthority.HistoricalTheoreticalCapacityWuPerAdultDay),
            "The historical daily schedule envelope is not 99 seconds.");

        ProjectContributionSnapshot landmark = SettlementLaborBalanceRules.EvaluateProject(
            ProjectScale.Landmark,
            Enumerable.Repeat(1f, 8).ToArray(),
            100f);
        Require(Mathf.Approximately(landmark.EffectiveWuPerSecond, 5f),
            "Eight landmark workers must equal five effective workers.");
        Require(landmark.MaximumWorkers == 8, "Landmark worker cap must be eight.");

        ProjectContributionSnapshot research = SettlementLaborBalanceRules.EvaluateProject(
            ProjectScale.MajorResearch,
            Enumerable.Repeat(1f, 4).ToArray(),
            100f);
        Require(Mathf.Approximately(research.EffectiveWuPerSecond, 2.4f),
            "Four-researcher contribution curve must total 2.40.");

        IReadOnlyList<TechnologyWuCheckpoint> checkpoints =
            SettlementLaborBalanceRules.TechnologyCheckpoints;
        Require(checkpoints.Count == 6, "Technology checkpoint count changed.");
        Require(Mathf.Abs(checkpoints[5].Index - 2f) <= 0.001f,
            "Day 960 output-equivalent WU index must be 2.00.");
        for (int index = 0; index < checkpoints.Count; index++)
        {
            SettlementTechnologyStage stage = (SettlementTechnologyStage)index;
            TechnologyDailyRoutineSnapshot routine =
                SettlementLaborBalanceRules.EvaluateTechnologyDailyRoutine(stage);
            Require(Mathf.Approximately(
                    routine.Budget.TotalSeconds,
                    SettlementLaborBalanceRules.SecondsPerDay),
                $"{stage} routine no longer totals one 180-second day.");
            Require(Mathf.Abs(
                    routine.ActualLaborWu - checkpoints[index].ActualLaborWu)
                    <= 0.25f,
                $"{stage} routine WU does not reproduce its checkpoint target.");
        }
        TechnologyDailyRoutineSnapshot late =
            SettlementLaborBalanceRules.EvaluateTechnologyDailyRoutine(
                SettlementTechnologyStage.Late);
        TechnologyDailyRoutineSnapshot endless =
            SettlementLaborBalanceRules.EvaluateTechnologyDailyRoutine(
                SettlementTechnologyStage.Endless);
        Require(Mathf.Approximately(late.Savings.TotalSeconds, 23f)
            && Mathf.Approximately(endless.Savings.TotalSeconds, 23f)
            && late.ActiveWorkPerformance > 1f
            && endless.ActiveWorkPerformance > late.ActiveWorkPerformance,
            "Technology-stage work performance no longer reproduces the approved targets.");

        SettlementLaborSnapshot domainLockedAutomation =
            SettlementLaborBalanceRules.EvaluateSettlementLabor(
                actualWorkSeconds: SettlementLaborAuthority.ActualWuPerAdultDay,
                averagePerformance: 1f,
                convertedProcessOutputWu: 0f,
                netDomainAutomationWu: 100f,
                fuelMaintenanceAccidentSpoilageLossWu: 0f,
                essentialMaintenanceWu: 0f,
                equipmentFacilityMaintenanceWu: 0f,
                emergencyReserveWu: 0f);
        Require(Mathf.Approximately(
                domainLockedAutomation.OutputEquivalentWu,
                150f)
            && Mathf.Approximately(
                domainLockedAutomation.RealizedGrowthWu,
                SettlementLaborAuthority.ActualWuPerAdultDay),
            "Domain automation leaked into transferable growth WU.");
    }

    private static void ValidateWorkClassificationAndAccounting()
    {
        Require(WorkTypeCatalog.All.Count == 31, "All 31 work types require emergency flags.");
        Require(WorkTypeCatalog.All.All(value => value.EmergencyFlags != EmergencyWorkFlags.None),
            "A work type has no emergency classification.");
        Require(WorkTypeCatalog.TryGet(BuiltInWorkTypeIds.Surgery, out WorkTypeDefinition surgery)
            && surgery.EmergencyFlags == EmergencyWorkFlags.CriticalNonInterruptible,
            "Surgery must be critical and non-interruptible.");
        Require(WorkTypeCatalog.TryGet(BuiltInWorkTypeIds.Rest, out WorkTypeDefinition rest)
            && rest.EmergencyFlags == EmergencyWorkFlags.ProtectedRecovery,
            "Rest must be protected recovery.");
        Require(SettlementLaborBalanceRules.TryGetMaintenanceChannel(
                BuiltInWorkTypeIds.Cook,
                out SettlementLaborContributionChannel foodMaintenance)
            && foodMaintenance
                == SettlementLaborContributionChannel.EssentialMaintenance,
            "Cooking must reduce transferable growth WU as essential upkeep.");
        Require(SettlementLaborBalanceRules.TryGetMaintenanceChannel(
                BuiltInWorkTypeIds.Repair,
                out SettlementLaborContributionChannel facilityMaintenance)
            && facilityMaintenance
                == SettlementLaborContributionChannel.EquipmentFacilityMaintenance,
            "Repair must reduce growth WU as equipment/facility upkeep.");
        Require(!SettlementLaborBalanceRules.TryGetMaintenanceChannel(
                BuiltInWorkTypeIds.Research,
                out _),
            "Research was incorrectly classified as settlement maintenance.");

        GameEventBus eventBus = new GameEventBus();
        EmergencyWorkAccountingRuntime accounting = new EmergencyWorkAccountingRuntime(eventBus);
        accounting.Start();
        EmergencyAccountingResult registered = accounting.Register(
            new EmergencyWorkLedgerEntry(
                "work:character:test:1:work:haul",
                "character:test",
                BuiltInWorkTypeIds.Haul,
                EmergencyWorkFlags.ReserveEligible | EmergencyWorkFlags.InterruptImmediately,
                40_000L,
                30_000L,
                0,
                0L));
        Require(registered.Success, registered.Code + ": " + registered.Message);
        EmergencyAccountingResult progressed = accounting.ApplyProgress(
            new EmergencyWorkProgress(
                "work:character:test:1:work:haul",
                20_000L,
                20_000L,
                1L));
        Require(progressed.Success, progressed.Code + ": " + progressed.Message);
        EmergencyReserveSnapshot snapshot = accounting.CaptureSnapshot();
        Require(snapshot.ReserveEligibleMilliWu == 20_000L
            && snapshot.InterruptImmediatelyMilliWu == 20_000L
            && snapshot.ActiveOperationCount == 1,
            "Progress did not update cached reserve totals exactly once.");
        Require(accounting.ApplyProgress(new EmergencyWorkProgress(
            "work:character:test:1:work:haul", 20_000L, 0L, 1L)).Success,
            "Duplicate progress must be an idempotent no-op.");
        Require(accounting.CaptureSnapshot().ReserveEligibleMilliWu == 20_000L,
            "Duplicate progress mutated reserve totals.");
        EmergencyAccountingReconciliationResult reconciliation = accounting.Reconcile(
            EmergencyAccountingReconciliationTrigger.DeveloperAudit);
        Require(reconciliation.Success && !reconciliation.DriftDetected,
            "Clean accounting did not reconcile exactly.");
        Require(accounting.Remove(new EmergencyWorkCompletion(
            "work:character:test:1:work:haul",
            "complete:test:1",
            2L)).Success,
            "Accounting completion failed.");
        Require(accounting.CaptureSnapshot().ActiveOperationCount == 0,
            "Completed operation remained in the ledger.");
        accounting.Dispose();
    }

    private static void ValidateAlertHysteresisAndPersistence()
    {
        GameEventBus eventBus = new GameEventBus();
        FakeCalendar calendar = new FakeCalendar();
        EmergencyWorkAccountingRuntime accounting = new EmergencyWorkAccountingRuntime(eventBus);
        SettlementAlertRuntime alert = new SettlementAlertRuntime(
            calendar,
            eventBus,
            accounting,
            accounting);

        Require(alert.PublishIncidentSignal(new SettlementIncidentSignal(
            "incident:test-fire",
            SettlementThreatAlertLevel.Red,
            1L,
            "test",
            "spreading fire")).Success,
            "Red incident signal failed.");
        Require(alert.Capture().CommittedLevel == SettlementThreatAlertLevel.Red,
            "Red escalation was not immediate.");
        SettlementAlertSnapshot cachedFirst = alert.Capture();
        SettlementAlertSnapshot cachedSecond = alert.Capture();
        Require(ReferenceEquals(
                cachedFirst.ActiveIncidentIds,
                cachedSecond.ActiveIncidentIds)
            && ReferenceEquals(
                cachedFirst.SuspendedWork,
                cachedSecond.SuspendedWork),
            "Unchanged alert snapshots rebuilt collection payloads on the hot read path.");
        long epoch = alert.Capture().AlertEpochId;
        Require(alert.RecordSuspendedWork(
                new SettlementSuspendedWorkSnapshot(
                    "character:test",
                    BuiltInWorkTypeIds.Construct,
                    "building:test",
                    epoch,
                    calendar.AbsoluteHour,
                    progressExternallyPersisted: true)).Success,
            "Externally persisted work could not enter the suspended-work journal.");
        Require(alert.ResolveIncident("incident:test-fire", 2L).Success,
            "Incident resolution failed.");

        calendar.SetDateTime(1, 1);
        alert.Tick();
        Require(alert.Capture().CommittedLevel == SettlementThreatAlertLevel.Red,
            "Red downgraded before two game hours.");
        calendar.SetDateTime(1, 2);
        alert.Tick();
        Require(alert.Capture().CommittedLevel == SettlementThreatAlertLevel.Amber,
            "Red did not downgrade to Amber after two stable hours.");
        calendar.SetDateTime(1, 3);
        alert.Tick();
        Require(alert.Capture().CommittedLevel == SettlementThreatAlertLevel.Amber,
            "Amber downgraded before its independent two-hour window.");
        calendar.SetDateTime(1, 4);
        alert.Tick();
        Require(alert.Capture().CommittedLevel == SettlementThreatAlertLevel.Green,
            "Amber did not downgrade to Green after four total stable hours.");
        Require(alert.Capture().AlertEpochId == epoch,
            "Downgrades must not create a new alert epoch.");

        Require(alert.UpdateReserveCoverage(70L, 100L).Success
            && alert.Capture().ReserveCoverageBand == EmergencyReserveCoverageBand.CollapseRisk,
            "Coverage deterioration must be immediate.");
        Require(alert.UpdateReserveCoverage(90L, 100L).Success,
            "Coverage recovery update failed.");
        calendar.SetDateTime(1, 5);
        alert.Tick();
        Require(alert.Capture().ReserveCoverageBand == EmergencyReserveCoverageBand.CollapseRisk,
            "Coverage upgraded before two stable hours.");
        calendar.SetDateTime(1, 6);
        alert.Tick();
        Require(alert.Capture().ReserveCoverageBand == EmergencyReserveCoverageBand.Vulnerable,
            "Coverage did not cross the 0.85 Schmitt recovery threshold.");

        DungeonStory.Infrastructure.SettlementThreatAlertSaveData saved =
            alert.CaptureAlertSaveData();
        SettlementAlertRuntime restored = new SettlementAlertRuntime(
            calendar,
            eventBus,
            accounting,
            accounting);
        restored.RestoreAlertSaveData(saved);
        SettlementAlertSnapshot before = alert.Capture();
        SettlementAlertSnapshot after = restored.Capture();
        Require(before.CommittedLevel == after.CommittedLevel
            && before.DesiredLevel == after.DesiredLevel
            && before.AlertEpochId == after.AlertEpochId
            && before.DowngradeStableSinceAbsoluteHour == after.DowngradeStableSinceAbsoluteHour
            && before.ReserveCoverageBand == after.ReserveCoverageBand
            && after.SuspendedWork.Count == 1
            && after.SuspendedWork[0].CharacterId == "character:test",
            "Alert save round trip changed authoritative state.");
        Require(restored.MarkSuspendedWorkResumed(
                "character:test",
                epoch).Success
            && restored.Capture().SuspendedWork.Count == 0,
            "Resumed work remained in the saved suspension journal.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class FakeCalendar : IGameCalendar
    {
        private int day = 1;
        private int hour;
        public int Day => day;
        public int Hour => hour;
        public int Year => Current.Year;
        public int DayOfYear => Current.DayOfYear;
        public Season Season => Current.Season;
        public int DayOfSeason => Current.DayOfSeason;
        public long AbsoluteHour => Current.AbsoluteHour;
        public float ElapsedSeconds => hour / 24f * GameCalendarRules.SecondsPerDay;
        public TimeOfDay TimeOfDay => TimeOfDay.Noon;
        public bool IsRunning { get; private set; }
        public CalendarDateTime Current => GameCalendarRules.Project(day, hour);
        public CalendarDateTime GetRegionalTime(int utcOffsetHours) =>
            GameCalendarRules.ProjectRegional(day, hour, utcOffsetHours);
        public void Start() => IsRunning = true;
        public void SetDateTime(int nextDay, int nextHour)
        {
            day = Math.Max(1, nextDay);
            hour = Math.Clamp(nextHour, 0, 23);
        }
    }

    private sealed class FixedReserveTargetQuery :
        ISettlementEmergencyReserveTargetQuery
    {
        private readonly SettlementEmergencyReserveTargetSnapshot snapshot;

        public FixedReserveTargetQuery(int productiveAdults, long targetMilliWu)
        {
            snapshot = new SettlementEmergencyReserveTargetSnapshot(
                productiveAdults,
                targetMilliWu,
                0L,
                string.Empty,
                1f,
                targetMilliWu,
                targetMilliWu,
                1f,
                1L);
        }

        public SettlementEmergencyReserveTargetSnapshot CaptureTarget() => snapshot;
    }
}
#endif
