#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Operation;

public static class MilestonePressurePresentationDebugScenarios
{
    public const int ExpectedDeliberateErrorCount = 1;

    public static void RunForBatch()
    {
        VerifyProjectionFamilies();
        VerifyOwnerEffectsWithoutPresentation();
        VerifyPresentationFailureCannotBlockLaterCommands();
    }

    private static void VerifyProjectionFamilies()
    {
        Expect(
            new MilestonePressureAppliedFact(
                MilestonePressureEffectKind.DefenseObligation,
                30,
                "ending:monster-accord",
                "monster-accord",
                affectedCount: 2,
                obligationDelta: 1),
            "공동방위 의무 발생",
            "대협약 회원 세력 모두에 공동방위 의무가 1 증가했습니다.",
            "milestone-pressure:monster-accord:30");
        ExpectDelay(
            MilestonePressureEffectKind.FarmingWorkDelay,
            "ending:sealed-paradise",
            "sealed-paradise",
            "farm",
            3,
            "폐쇄 생태 불균형",
            "폐쇄 생태계의 균형 조정으로 농업 작업이 3일간 지연됩니다.");
        ExpectDelay(
            MilestonePressureEffectKind.InheritanceWorkDelay,
            "ending:eternal-lineage",
            "eternal-lineage",
            "global",
            1,
            "계승권 분쟁",
            "계승권 조정으로 전체 작업이 하루 동안 지연됩니다.");
        ExpectDelay(
            MilestonePressureEffectKind.RepairWorkDelay,
            "ending:steel-apotheosis",
            "steel-apotheosis",
            "repair",
            2,
            "자동화 연쇄 고장",
            "연쇄 고장과 내부 점검으로 수리 작업이 2일간 지연됩니다.");
        Expect(
            new MilestonePressureAppliedFact(
                MilestonePressureEffectKind.TemporalMaintenanceAcceleration,
                30,
                "ending:timeless-sanctuary",
                "timeless-sanctuary",
                affectedCount: 2,
                maintenanceLeadDays: 3),
            "시간 이상 현상",
            "시간 고정 대상 2명의 다음 촉매 교체일이 3일 뒤로 앞당겨졌습니다.",
            "milestone-pressure:timeless-sanctuary:30");

        ExpectInvasion(
            "truth-revealed",
            75f,
            "진실 수호자 보복");
        ExpectInvasion(
            "surface-hegemony",
            90f,
            "인간 연합 반격");
        ExpectInvasion(
            "dungeon-sovereignty",
            100f,
            "국가 규모 공성전");
        ExpectInvasion(
            "arcane-ascension",
            95f,
            "비전 습격");
    }

    private static void VerifyOwnerEffectsWithoutPresentation()
    {
        var events = new GameEventBus();
        var facts = new List<MilestonePressureAppliedFact>();
        var alerts = new List<EventAlertRequest>();
        using IDisposable factCapture =
            events.Subscribe<MilestonePressureAppliedFact>(facts.Add);
        using IDisposable alertCapture =
            events.Subscribe<EventAlertRequestedEvent>(value =>
                alerts.Add(value.request));

        var pressure = new StrictMilestoneQuery(
            "ending:monster-accord",
            "ending:sealed-paradise",
            "ending:eternal-lineage",
            "ending:steel-apotheosis",
            "ending:timeless-sanctuary",
            "ending:truth-revealed",
            "ending:surface-hegemony",
            "ending:dungeon-sovereignty",
            "ending:arcane-ascension");
        var factions = new StrictFactionQuery("faction:a", "faction:b");
        var factionCommands = new StrictFactionCommands();
        var delays = new StrictWorkDelays();
        var life = new StrictLifeQuery(
            TemporalRecord("character:future", 100),
            TemporalRecord("character:already-due", 62));
        var lifeCommands = new StrictLifeCommands();
        var invasions = new StrictInvasions();
        var adapter = new MilestonePressureApplicationAdapter(
            pressure,
            invasions,
            factions,
            factionCommands,
            delays,
            life,
            lifeCommands,
            events);

        adapter.Start();
        events.Publish(new OperatingDayEndedEvent(60));
        events.Publish(new OperatingDayEndedEvent(40));
        adapter.Dispose();

        Require(alerts.Count == 0,
            "Milestone pressure owner rendered an alert without a presentation observer.");
        Require(factionCommands.Changes.Count == 2
                && factionCommands.Changes.All(value =>
                    value.rapport == 0
                    && value.grievance == 0
                    && value.obligation == 1),
            "Defense obligation commands differ from the original owner effects.");
        Require(delays.Delays.SequenceEqual(new[]
            {
                ("farm", 3, 60),
                ("repair", 2, 60),
                ("global", 1, 40),
                ("repair", 2, 40)
            }),
            "Milestone work-delay command order or quantities changed.");
        Require(lifeCommands.Configurations.Count == 1
                && lifeCommands.Configurations[0].characterId
                    == "character:future"
                && lifeCommands.Configurations[0].facilityId
                    == "facility:stasis"
                && lifeCommands.Configurations[0].operational
                && lifeCommands.Configurations[0].maintenanceDay == 63,
            "Temporal pressure did not preserve its exact owner command.");
        Require(invasions.Scheduled.Count == 2
                && invasions.Scheduled.All(value =>
                    value.threat == 100f
                    && value.source.StartsWith(
                        "milestone-pressure:dungeon-sovereignty:",
                        StringComparison.Ordinal)),
            "Invasion pressure precedence or owner identity changed.");
        Require(Count(facts, MilestonePressureEffectKind.DefenseObligation) == 1
                && Count(facts, MilestonePressureEffectKind.FarmingWorkDelay) == 1
                && Count(facts, MilestonePressureEffectKind.InheritanceWorkDelay) == 1
                && Count(facts, MilestonePressureEffectKind.RepairWorkDelay) == 2
                && Count(facts,
                    MilestonePressureEffectKind.TemporalMaintenanceAcceleration) == 1
                && Count(facts, MilestonePressureEffectKind.InvasionScheduled) == 2,
            "Milestone owner did not emit one fact per applied effect.");
    }

    private static void VerifyPresentationFailureCannotBlockLaterCommands()
    {
        var events = new GameEventBus();
        var facts = new List<MilestonePressureAppliedFact>();
        var alerts = new List<EventAlertRequest>();
        using IDisposable factCapture =
            events.Subscribe<MilestonePressureAppliedFact>(facts.Add);
        using IDisposable alertCapture =
            events.Subscribe<EventAlertRequestedEvent>(value =>
                alerts.Add(value.request));
        int deliberateErrors = 0;
        using IDisposable fault = events.Subscribe<EventAlertRequestedEvent>(_ =>
        {
            if (deliberateErrors != 0) return;
            deliberateErrors++;
            throw new InvalidOperationException(
                "b129-deliberate-alert-subscriber-failure");
        });

        var invasions = new StrictInvasions();
        var observer = new MilestonePressurePresentationObserver(events);
        observer.Initialize();
        observer.Initialize();
        var adapter = new MilestonePressureApplicationAdapter(
            new StrictMilestoneQuery(
                "ending:sealed-paradise",
                "ending:surface-hegemony"),
            invasions,
            new StrictFactionQuery(),
            new StrictFactionCommands(),
            new StrictWorkDelays(),
            new StrictLifeQuery(),
            new StrictLifeCommands(),
            events);

        adapter.Start();
        events.Publish(new OperatingDayEndedEvent(30));
        adapter.Dispose();

        Require(deliberateErrors == ExpectedDeliberateErrorCount,
            "Milestone presentation did not isolate the deliberate display failure.");
        Require(invasions.Scheduled.Count == 1
                && invasions.Scheduled[0].threat == 90f,
            "A display failure blocked the later invasion owner command.");
        Require(facts.Count == 2 && alerts.Count == 2,
            "Double initialization duplicated presentation or effect facts were lost.");

        observer.Dispose();
        observer.Dispose();
        events.Publish(new MilestonePressureAppliedFact(
            MilestonePressureEffectKind.RepairWorkDelay,
            40,
            "ending:steel-apotheosis",
            "steel-apotheosis",
            workScopeId: "repair",
            workDelayDays: 2));
        Require(alerts.Count == 2,
            "Disposed milestone presentation observer still rendered alerts.");
    }

    private static void ExpectDelay(
        MilestonePressureEffectKind kind,
        string pressureId,
        string sourceId,
        string scopeId,
        int days,
        string title,
        string detail) =>
        Expect(
            new MilestonePressureAppliedFact(
                kind,
                40,
                pressureId,
                sourceId,
                workScopeId: scopeId,
                workDelayDays: days),
            title,
            detail,
            $"milestone-pressure:{sourceId}:40");

    private static void ExpectInvasion(
        string sourceId,
        float threat,
        string title) =>
        Expect(
            new MilestonePressureAppliedFact(
                MilestonePressureEffectKind.InvasionScheduled,
                60,
                "ending:" + sourceId,
                sourceId,
                invasionOperationId: "invasion-operation:b129",
                invasionThreat: threat),
            title,
            $"위협도 {threat:0}의 침입 작전 invasion-operation:b129이 예약되었습니다.",
            $"milestone-pressure:{sourceId}:60");

    private static void Expect(
        MilestonePressureAppliedFact fact,
        string title,
        string detail,
        string sourceId)
    {
        EventAlertRequest request =
            MilestonePressurePresentationObserver.Build(fact);
        Require(request.Title == title
                && request.Detail == detail
                && request.Category == "V21 이정표 압력"
                && request.Importance == EventAlertImportance.High
                && request.SourceId == sourceId
                && request.Choices.Count == 0
                && !request.IsResolved
                && request.ResultSummary.Length == 0,
            "Milestone pressure projection differs for " + fact.Kind + ".");
    }

    private static int Count(
        IEnumerable<MilestonePressureAppliedFact> facts,
        MilestonePressureEffectKind kind) =>
        facts.Count(value => value.Kind == kind);

    private static CharacterLifeRecord TemporalRecord(
        string characterId,
        int maintenanceDay)
    {
        var speciesId = new CharacterSpeciesId("species:b129");
        return new CharacterLifeRecord(
            new CharacterId(characterId),
            speciesId,
            100,
            100d,
            1,
            new SpeciesLifeHistoryDefinition(
                speciesId,
                infantEndAgeYears: 1,
                adolescentStartAgeYears: 2,
                adultAgeYears: 3,
                elderAgeYears: 50,
                untreatedExpectedLifeYears: 80f,
                construct: false),
            requestedAgingCareMode: AgingCareMode.TemporalStasis,
            effectiveAgingCareMode: AgingCareMode.TemporalStasis,
            temporalStasisFacilityId: "facility:stasis",
            temporalStasisNextMaintenanceAbsoluteDay: maintenanceDay);
    }

    private static void Require(bool condition, string failure)
    {
        if (!condition) throw new InvalidOperationException(failure);
    }

    private sealed class StrictMilestoneQuery : IMilestoneGameplayModifierQuery
    {
        private readonly HashSet<string> pressures;
        internal StrictMilestoneQuery(params string[] pressures) =>
            this.pressures = new HashSet<string>(
                pressures ?? Array.Empty<string>(),
                StringComparer.Ordinal);
        public bool EnemyCounterIntelVisible => false;
        public float ExpeditionTravelTimeMultiplier => 1f;
        public float FacilityMaintenanceGoldMultiplier => 1f;
        public float WaterAndFertilizerConsumptionMultiplier => 1f;
        public int MentorshipDailyXpCap => CareerRules.MaximumDailyMentoringXp;
        public int TemporalStasisWarningDays => 0;
        public float ManaTransferLossMultiplier => 1f;
        public float AutomaticMaintenanceWorkMultiplier => 1f;
        public bool IsAccordSignalSupportDay(int absoluteDay) => false;
        public bool IsAccordSignalSupportActive(int absoluteDay) => false;
        public bool HasReward(string milestoneId) => false;
        public bool HasPressure(string milestoneId) =>
            pressures.Contains(milestoneId);
    }

    private sealed class StrictFactionQuery : IFactionCampaignQuery
    {
        internal StrictFactionQuery(params string[] factionIds) =>
            Factions = (factionIds ?? Array.Empty<string>())
                .Select(value => new FactionCampaignStateSaveData
                {
                    factionId = value
                })
                .ToArray();
        public IReadOnlyList<FactionCampaignStateSaveData> Factions { get; }
        public bool TryGetFaction(
            string factionId,
            out FactionCampaignStateSaveData state)
        {
            state = Factions.FirstOrDefault(value =>
                value.factionId == factionId);
            return state != null;
        }
    }

    private sealed class StrictFactionCommands : IFactionCampaignCommand
    {
        internal List<(string id, int rapport, int grievance, int obligation)>
            Changes { get; } = new();

        public void ApplyFactionChange(
            string factionId,
            int rapportDelta,
            int grievanceDelta,
            int obligationDelta) =>
            Changes.Add((factionId, rapportDelta, grievanceDelta,
                obligationDelta));

        public bool TryResolveChapter(
            string factionId,
            string choiceId,
            RunMilestoneEvaluationSnapshot requirements,
            out V20ResolvedEventResult result,
            out string failure) =>
            throw new InvalidOperationException("Unexpected faction command.");

        public bool TryAcceptContract(
            string factionId,
            string contractId,
            int absoluteDay,
            out string failure) =>
            throw new InvalidOperationException("Unexpected faction command.");

        public bool TryResolveContract(
            string factionId,
            bool success,
            RunMilestoneEvaluationSnapshot requirements,
            out V20ResolvedEventResult result,
            out string failure) =>
            throw new InvalidOperationException("Unexpected faction command.");
    }

    private sealed class StrictWorkDelays : IContentWorkDelayCommand
    {
        internal List<(string scope, int days, int absoluteDay)> Delays
        { get; } = new();
        public void ApplyWorkDelay(
            string scopeId,
            int days,
            int absoluteDay) =>
            Delays.Add((scopeId, days, absoluteDay));
    }

    private sealed class StrictLifeQuery : ICharacterLifeQuery
    {
        internal StrictLifeQuery(params CharacterLifeRecord[] records) =>
            Records = records ?? Array.Empty<CharacterLifeRecord>();
        public int Version => 1;
        public IReadOnlyCollection<CharacterLifeRecord> Records { get; }
        public bool TryGet(
            CharacterId characterId,
            out CharacterLifeRecord record)
        {
            record = Records.FirstOrDefault(value =>
                value.CharacterId.Equals(characterId));
            return record != null;
        }
    }

    private sealed class StrictLifeCommands : ICharacterLifeCommand
    {
        internal List<(string characterId, string facilityId,
            bool operational, int maintenanceDay)> Configurations { get; } =
            new();

        public void ConfigureTemporalStasis(
            CharacterId characterId,
            string facilityId,
            bool operational,
            int nextMaintenanceAbsoluteDay) =>
            Configurations.Add((characterId.Value, facilityId, operational,
                nextMaintenanceAbsoluteDay));

        public CharacterLifeRecord Register(
            CharacterId characterId,
            CharacterSpeciesId phenotypeSpeciesId,
            int chronologicalAgeDays,
            double biologicalAgeDayUnits,
            int birthdayDayOfYear) =>
            throw new InvalidOperationException("Unexpected life command.");
        public IReadOnlyList<AgeConditionChange> AdvanceDay(
            CharacterId characterId) =>
            throw new InvalidOperationException("Unexpected life command.");
        public IReadOnlyList<AgeConditionChange> AddInitialAgeConditions(
            CharacterId characterId,
            IReadOnlyList<string> conditionIds) =>
            throw new InvalidOperationException("Unexpected life command.");
        public void ApplyRejuvenation(
            CharacterId characterId,
            int biologicalYears) =>
            throw new InvalidOperationException("Unexpected life command.");
        public bool TryApplyBloodRejuvenation(
            CharacterId characterId,
            int absoluteDay,
            out DomainFailure failure) =>
            throw new InvalidOperationException("Unexpected life command.");
        public IReadOnlyList<AgeConditionChange> ApplyWholeBodyRegeneration(
            CharacterId characterId) =>
            throw new InvalidOperationException("Unexpected life command.");
        public IReadOnlyList<AgeConditionChange> ReduceAgeConditions(
            CharacterId characterId,
            int severityLevels) =>
            throw new InvalidOperationException("Unexpected life command.");
        public void ConfigureLongTermCare(
            CharacterId characterId,
            bool geriatricMedicineActive,
            bool chronicCareActive,
            AgingCareMode requestedMode) =>
            throw new InvalidOperationException("Unexpected life command.");
        public bool Remove(CharacterId characterId) =>
            throw new InvalidOperationException("Unexpected life command.");
    }

    private sealed class StrictInvasions : IInvasionCampaignRuntime
    {
        internal List<(float threat, string source, int day)> Scheduled
        { get; } = new();
        public IReadOnlyList<HumanInvasionBranchState> Branches =>
            Array.Empty<HumanInvasionBranchState>();
        public IReadOnlyList<HumanSupportSiteState> SupportSites =>
            Array.Empty<HumanSupportSiteState>();
        public IReadOnlyList<ScheduledInvasionOperationState> Operations =>
            Array.Empty<ScheduledInvasionOperationState>();

        public ScheduledInvasionOperationState
            ScheduleMilestonePressureOperation(
                float threat,
                string sourceOwnerId,
                int sourceAbsoluteDay,
                out bool created)
        {
            Scheduled.Add((threat, sourceOwnerId, sourceAbsoluteDay));
            created = true;
            return new ScheduledInvasionOperationState
            {
                operationId = "invasion-operation:b129:"
                    + Scheduled.Count,
                sourceOwnerId = sourceOwnerId,
                sourceAbsoluteDay = sourceAbsoluteDay
            };
        }

        public bool TryGetBranch(
            string branchId,
            out HumanInvasionBranchState branch) =>
            throw new InvalidOperationException("Unexpected invasion query.");
        public bool TryDestroySupportSite(
            string siteId,
            out string message) =>
            throw new InvalidOperationException("Unexpected invasion command.");
        public ScheduledInvasionOperationState ScheduleNextOperation(
            float threat) =>
            throw new InvalidOperationException("Unexpected invasion command.");
        public ScheduledInvasionOperationState ClaimOrScheduleNextOperation(
            float threat) =>
            throw new InvalidOperationException("Unexpected invasion command.");
        public void RecordOperationStarted(
            string operationId,
            string invasionRuntimeId,
            string publicationKey) =>
            throw new InvalidOperationException("Unexpected invasion command.");
        public float GetBranchStrengthMultiplier(string branchId) =>
            throw new InvalidOperationException("Unexpected invasion query.");
        public InvasionCampaignSaveData Capture() =>
            throw new InvalidOperationException("Unexpected invasion query.");
        public void ReplaceFromValidatedSnapshot(
            InvasionCampaignSaveData state) =>
            throw new InvalidOperationException("Unexpected invasion command.");
        public void PublishRestoreProjection() =>
            throw new InvalidOperationException("Unexpected invasion command.");
        public void RollbackPublishedRestoreProjection() =>
            throw new InvalidOperationException("Unexpected invasion command.");
        public void CompleteRestoreProjection() =>
            throw new InvalidOperationException("Unexpected invasion command.");
    }
}
#endif
