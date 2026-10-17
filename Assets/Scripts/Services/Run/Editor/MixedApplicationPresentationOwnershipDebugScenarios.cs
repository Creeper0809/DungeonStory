#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using UnityEngine;

public static class MixedApplicationPresentationOwnershipDebugScenarios
{
    public const int ExpectedDeliberateErrorCount = 2;

    public static void RunForBatch()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var unexpected = new List<string>();
            VerifyTemporalMaintenance(unexpected);
            VerifyCertifiedSeed(unexpected);
            VerifyFestival(unexpected);
            Require(unexpected.Count == 0,
                "Unexpected fixture calls: " + string.Join(", ", unexpected));
            Debug.Log("B129_MIXED_APPLICATION_PRESENTATION_OWNERSHIP=PASS");
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    private static void VerifyTemporalMaintenance(List<string> unexpected)
    {
        var events = new GameEventBus();
        var alerts = new List<EventAlertRequest>();
        using IDisposable capture = events.Subscribe<EventAlertRequestedEvent>(value =>
            alerts.Add(value.request));
        var maintenance = new MaintenanceOwner();
        var recovery = new MaintenanceRecovery();
        using var owner = new TemporalStasisMaintenanceAdapter(
            maintenance, recovery, events);

        owner.Start();
        owner.Start();
        events.Publish(new OperatingDayEndedEvent(20));
        Require(recovery.Calls == 1 && maintenance.Calls == 1 && alerts.Count == 0,
            "Temporal owner duplicated startup or depended on presentation.");

        CharacterLifeRecord record = TemporalRecord("character:b129", 23);
        ICharacterLifeQuery life = Port<ICharacterLifeQuery>(unexpected,
            (method, _) => method.Name == "get_Records"
                ? new[] { record }
                : Unexpected(unexpected, method));
        IMilestoneGameplayModifierQuery modifiers =
            Port<IMilestoneGameplayModifierQuery>(unexpected,
                (method, _) => method.Name == "get_TemporalStasisWarningDays"
                    ? 3
                    : Unexpected(unexpected, method));
        using var observer = new TemporalStasisMaintenanceWarningObserver(
            life, modifiers, events);
        observer.Initialize();
        observer.Initialize();
        events.Publish(new OperatingDayStartedEvent(20));
        Require(alerts.Count == 1
                && alerts[0].Title == "시간 고정 유지보수 예고"
                && alerts[0].Detail == "character:b129의 시간 고정 촉매 교체까지 3일 남았습니다. "
                    + "component:rune-conductor와 resource:mana-crystal을 시설 버퍼에 준비해야 합니다."
                && alerts[0].Importance == EventAlertImportance.High
                && alerts[0].Category == "V21 시간 고정"
                && alerts[0].SourceId
                    == "temporal-stasis-maintenance-warning:character:b129"
                && alerts[0].Choices.Count == 0,
            "Temporal warning text or identity changed.");

        observer.Dispose();
        observer.Dispose();
        events.Publish(new OperatingDayStartedEvent(20));
        Require(alerts.Count == 1, "Disposed temporal observer emitted.");
        observer.Initialize();
        events.Publish(new OperatingDayStartedEvent(20));
        Require(alerts.Count == 2, "Reinitialized temporal observer did not emit once.");

        owner.Dispose();
        owner.Dispose();
        events.Publish(new OperatingDayEndedEvent(21));
        Require(maintenance.Calls == 1, "Disposed temporal owner retained its subscription.");
        owner.Start();
        events.Publish(new OperatingDayEndedEvent(21));
        Require(recovery.Calls == 2 && maintenance.Calls == 2,
            "Temporal owner did not restart exactly once.");
    }

    private static void VerifyCertifiedSeed(List<string> unexpected)
    {
        var events = new GameEventBus();
        var alerts = new List<EventAlertRequest>();
        var facts = new List<CertifiedSeedPlansAdvancedFact>();
        using IDisposable alertCapture = events.Subscribe<EventAlertRequestedEvent>(value =>
            alerts.Add(value.request));
        using IDisposable factCapture = events.Subscribe<CertifiedSeedPlansAdvancedFact>(facts.Add);
        var commands = new CertifiedCommands(unexpected);
        using var owner = new CertifiedSeedApplicationAdapter(commands, events);
        owner.Start();
        owner.Start();
        events.Publish(new OperatingDayStartedEvent(30));
        Require(commands.Days.SequenceEqual(new[] { 30 })
                && facts.Count == 1 && facts[0].AbsoluteDay == 30
                && alerts.Count == 0,
            "Certified-seed owner did not complete and publish without presentation.");

        using var fixture = new CertifiedFixture();
        IFacilityCapabilityQuery facilities = Port<IFacilityCapabilityQuery>(
            unexpected,
            (method, _) => method.Name == "FindOperational"
                ? new[] { fixture.Facility }
                : Unexpected(unexpected, method));
        IResourceEconomyContentCatalog crops = Port<IResourceEconomyContentCatalog>(
            unexpected,
            (method, _) => method.Name == "get_Crops"
                ? fixture.Crops
                : Unexpected(unexpected, method));
        using var observer = new CertifiedSeedPresentationObserver(
            facilities, crops, events);
        observer.Initialize();
        observer.Initialize();
        events.Publish(new OperatingDayStartedEvent(31));
        Require(commands.Days.SequenceEqual(new[] { 30, 31 })
                && facts.Count == 2 && facts[1].AbsoluteDay == 31
                && alerts.Count == 2
                && alerts[0].Choices.Count == 4
                && alerts[1].Choices.Count == 1,
            "Certified-seed grouping or owner fact delivery changed.");
        for (int group = 0; group < alerts.Count; group++)
        {
            EventAlertRequest alert = alerts[group];
            Require(alert.Title == "인증 품종 종자 생산"
                    && alert.Detail
                        == "육종 온실에서 인증할 작물을 선택하십시오. 재료가 실제로 도착한 뒤 종자 로트가 배출됩니다."
                    && alert.Importance == EventAlertImportance.Medium
                    && alert.Category == "V21 농업"
                    && alert.SourceId == $"certified-seed:building:b129:0:{group}",
                "Certified-seed alert text or identity changed.");
        }
        CropDefinitionSO[] ordered = fixture.Crops
            .OrderBy(value => value.CropId, StringComparer.Ordinal)
            .ToArray();
        Require(alerts.SelectMany(value => value.Choices)
                    .Select(value => value.Label)
                    .SequenceEqual(ordered.Select(value => value.DisplayName))
                && alerts.SelectMany(value => value.Choices)
                    .Select(value => value.Description)
                    .All(value => value
                        == "기존 종자 로트와 인증 꾸러미를 운반해 품질을 높이고 병원체 부하를 낮춥니다.")
                && alerts.SelectMany(value => value.Choices)
                    .Select(value => value.ActionId)
                    .SequenceEqual(ordered.Select(value =>
                        V21ContentAlertActionIds.CertifiedSeed(
                            value.CropId, "building:b129:0"))),
            "Certified-seed labels, details, or action IDs changed.");

        observer.Dispose();
        observer.Dispose();
        int alertCount = alerts.Count;
        events.Publish(new OperatingDayStartedEvent(32));
        Require(commands.Days.Last() == 32 && facts.Last().AbsoluteDay == 32
                && alerts.Count == alertCount,
            "Disposed certified-seed observer affected the owner.");
        observer.Initialize();
        int deliberateCalls = 0;
        using (events.Subscribe<EventAlertRequestedEvent>(_ =>
        {
            deliberateCalls++;
            throw new InvalidOperationException(
                "b129-deliberate-certified-seed-alert-failure");
        }))
        {
            events.Publish(new OperatingDayStartedEvent(33));
        }
        Require(deliberateCalls == 1 && commands.Days.Last() == 33
                && facts.Last().AbsoluteDay == 33,
            "Certified-seed display failure escaped into the owner command.");
        owner.Dispose();
        events.Publish(new OperatingDayStartedEvent(34));
        Require(commands.Days.Last() == 33, "Disposed certified-seed owner emitted.");
    }

    private static void VerifyFestival(List<string> unexpected)
    {
        var events = new GameEventBus();
        var alerts = new List<EventAlertRequest>();
        using IDisposable alertCapture = events.Subscribe<EventAlertRequestedEvent>(value =>
            alerts.Add(value.request));
        FestivalDefinitionSO festival = ScriptableObject.CreateInstance<FestivalDefinitionSO>();
        try
        {
            festival.festivalId = "festival:b129";
            festival.displayName = "검증 축제";
            festival.description = "검증 축제 설명";
            FestivalExecutionPreview preview = FestivalPreview();
            IFestivalDefinitionCatalog festivals = Port<IFestivalDefinitionCatalog>(
                unexpected,
                (method, _) => method.Name == "get_All"
                    ? new[] { festival }
                    : Unexpected(unexpected, method));
            IFestivalExecutionQuery executions = Port<IFestivalExecutionQuery>(
                unexpected,
                (method, args) => method.Name switch
                {
                    "BuildPreview" when (string)args[0] == festival.StableId
                        && (int)args[1] == 2 => preview,
                    "HasOccurrence" when (string)args[0] == preview.OccurrenceId => false,
                    _ => Unexpected(unexpected, method)
                });
            IGameCalendar calendar = Port<IGameCalendar>(unexpected,
                (method, _) => method.Name switch
                {
                    "get_Day" => 10,
                    "get_Year" => 2,
                    _ => Unexpected(unexpected, method)
                });
            using var observer = new FestivalVenueAlertApplicationAdapter(
                festivals, executions, calendar, events);
            observer.Start();
            observer.Start();
            Require(alerts.Count == 1, "Festival observer initialized twice.");
            EventAlertRequest alert = alerts.Single();
            Require(alert.Title == "검증 축제"
                    && alert.Detail == "검증 축제 설명\n준비물(보유/필요): resource:b129 2/3\n"
                        + "준비 작업: 4.5 WU · 준비 기간 2일\n"
                        + "개최/마감: 12일 18:00 · 행사 3시간\n"
                        + "문화·신분·필수 업무 제외 후 배정: 2명\n"
                        + "장소: 검증 회관 (4, 5)\n"
                        + "정원/배정: 3/2명 · 좌석 6, 탁자 2, 서비스 1, 빈 침상 0, 행사 칸 3"
                    && alert.Importance == EventAlertImportance.High
                    && alert.Category == "V21 축제"
                    && alert.SourceId == "festival-occurrence:b129:2"
                    && alert.Choices.Count == 2
                    && alert.Choices[0].Label == "축제 개최"
                    && alert.Choices[0].Description
                        == "물자 운반과 실제 장소 준비를 시작합니다. 마감 미충족 시 이번 회차만 취소됩니다."
                    && alert.Choices[0].ActionId
                        == V21ContentAlertActionIds.Festival("festival:b129", 2)
                    && alert.Choices[1].Label == "이번 회차 미개최"
                    && alert.Choices[1].Description
                        == "비용 없이 이번 회차를 넘기며 자동 재시작하지 않습니다."
                    && alert.Choices[1].ActionId
                        == V21ContentAlertActionIds.FestivalSkip("festival:b129", 2),
                "Festival alert text or action routing changed.");

            observer.Dispose();
            observer.Dispose();
            events.Publish(new OperatingDayStartedEvent(10));
            Require(alerts.Count == 1, "Disposed festival observer emitted.");
            var driver = new FestivalDriver();
            var tick = new FestivalExecutionDriverAdapter(driver);
            tick.Tick();
            observer.Start();
            Require(alerts.Count == 2, "Reinitialized festival observer did not announce.");

            int deliberateCalls = 0;
            using (events.Subscribe<EventAlertRequestedEvent>(_ =>
            {
                deliberateCalls++;
                throw new InvalidOperationException(
                    "b129-deliberate-festival-alert-failure");
            }))
            {
                events.Publish(new OperatingDayStartedEvent(10));
            }
            tick.Tick();
            Require(deliberateCalls == 1 && driver.Calls == 2,
                "Festival presentation failure affected execution driving.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(festival);
        }
    }

    private static CharacterLifeRecord TemporalRecord(
        string characterId,
        int maintenanceDay)
    {
        var speciesId = new CharacterSpeciesId("species:b129");
        return new CharacterLifeRecord(
            new CharacterId(characterId), speciesId, 100, 100d, 1,
            new SpeciesLifeHistoryDefinition(
                speciesId, 1, 2, 3, 50, 80f, construct: false),
            requestedAgingCareMode: AgingCareMode.TemporalStasis,
            effectiveAgingCareMode: AgingCareMode.TemporalStasis,
            temporalStasisFacilityId: "building:b129:stasis",
            temporalStasisNextMaintenanceAbsoluteDay: maintenanceDay);
    }

    private static FestivalExecutionPreview FestivalPreview()
    {
        var venue = new SocietyVenueCandidateSnapshot();
        Set(venue, nameof(SocietyVenueCandidateSnapshot.DisplayName), "검증 회관");
        Set(venue, nameof(SocietyVenueCandidateSnapshot.AnchorCenter),
            new Vector2Int(4, 5));
        Set(venue, nameof(SocietyVenueCandidateSnapshot.SpareSeats), 6);
        Set(venue, nameof(SocietyVenueCandidateSnapshot.SpareTables), 2);
        Set(venue, nameof(SocietyVenueCandidateSnapshot.SpareServiceCapacity), 1);
        Set(venue, nameof(SocietyVenueCandidateSnapshot.SpareVacantBeds), 0);
        Set(venue, nameof(SocietyVenueCandidateSnapshot.EventCells),
            new[] { new Vector2Int(4, 5), new Vector2Int(5, 5), new Vector2Int(6, 5) });
        var material = new FestivalMaterialPreview();
        Set(material, nameof(FestivalMaterialPreview.ItemId), "resource:b129");
        Set(material, nameof(FestivalMaterialPreview.Required), 3);
        Set(material, nameof(FestivalMaterialPreview.Available), 2);
        var preview = new FestivalExecutionPreview();
        Set(preview, nameof(FestivalExecutionPreview.OccurrenceId),
            "festival-occurrence:b129:2");
        Set(preview, nameof(FestivalExecutionPreview.FestivalId), "festival:b129");
        Set(preview, nameof(FestivalExecutionPreview.OccurrenceYear), 2);
        Set(preview, nameof(FestivalExecutionPreview.FestivalAbsoluteDay), 12);
        Set(preview, nameof(FestivalExecutionPreview.AnnouncementAbsoluteDay), 10);
        Set(preview, nameof(FestivalExecutionPreview.StartHour), 18);
        Set(preview, nameof(FestivalExecutionPreview.RequiredPreparationWork), 4.5f);
        Set(preview, nameof(FestivalExecutionPreview.PreparationLeadDays), 2);
        Set(preview, nameof(FestivalExecutionPreview.PlannedDurationHours), 3);
        Set(preview, nameof(FestivalExecutionPreview.ParticipantIds),
            new[] { new CharacterId("character:b129:a"), new CharacterId("character:b129:b") });
        Set(preview, nameof(FestivalExecutionPreview.Venue), venue);
        Set(preview, nameof(FestivalExecutionPreview.Materials), new[] { material });
        Set(preview, nameof(FestivalExecutionPreview.CanHost), true);
        return preview;
    }

    private static void Set<T>(T target, string propertyName, object value) where T : class
    {
        PropertyInfo property = typeof(T).GetProperty(
            propertyName, BindingFlags.Instance | BindingFlags.Public);
        if (property == null) throw new InvalidOperationException(
            $"Missing fixture property {typeof(T).Name}.{propertyName}.");
        property.SetValue(target, value);
    }

    private static T Port<T>(
        List<string> unexpected,
        Func<MethodInfo, object[], object> invoke) where T : class
    {
        T value = DispatchProxy.Create<T, StrictPort>();
        ((StrictPort)(object)value).InvokeMethod = invoke
            ?? ((method, _) => Unexpected(unexpected, method));
        return value;
    }

    private static object Unexpected(List<string> calls, MethodInfo method)
    {
        string name = method.DeclaringType?.Name + "." + method.Name;
        calls.Add(name);
        throw new InvalidOperationException("Unexpected fixture call: " + name);
    }

    public class StrictPort : DispatchProxy
    {
        public Func<MethodInfo, object[], object> InvokeMethod;
        protected override object Invoke(MethodInfo method, object[] args) =>
            InvokeMethod(method, args);
    }

    private sealed class MaintenanceOwner : ITemporalStasisMaintenanceService
    {
        public int Calls { get; private set; }
        public void RefreshDailyMaintenance() => Calls++;
    }

    private sealed class MaintenanceRecovery : ITemporalStasisMaintenanceRecovery
    {
        public int Calls { get; private set; }
        public bool TryRecoverPending(out DomainFailure failure)
        {
            Calls++;
            failure = default;
            return true;
        }
    }

    private sealed class CertifiedCommands : ICertifiedSeedCommand
    {
        private readonly List<string> unexpected;
        internal CertifiedCommands(List<string> unexpected) =>
            this.unexpected = unexpected;
        internal List<int> Days { get; } = new();
        public int CompleteDeliveredPlans(int operatingDay)
        {
            Days.Add(operatingDay);
            return 0;
        }
        public bool TryPlan(
            string actionId,
            string cropId,
            string facilityInstanceId,
            out DomainFailure failure)
        {
            unexpected.Add(nameof(ICertifiedSeedCommand) + "." + nameof(TryPlan));
            failure = default;
            throw new InvalidOperationException("Unexpected certified-seed plan command.");
        }
    }

    private sealed class FestivalDriver : IFestivalExecutionDriver
    {
        public int Calls { get; private set; }
        public void Advance() => Calls++;
    }

    private sealed class CertifiedFixture : IDisposable
    {
        private readonly GameObject host;
        private readonly BuildingSO definition;
        internal CertifiedFixture()
        {
            definition = ScriptableObject.CreateInstance<BuildingSO>();
            definition.id = 12901;
            definition.objectName = "육종 온실";
            definition.ConfigureAuthoredContentIdentity(
                "building:qa:b129-certified-seed", 1, "B129 fixture");
            var abilities = new BuildingAbilityCollection();
            abilities.Add(new BuildingProductionWorkstationAbility
            {
                workstationTag = CertifiedSeedFacilityEligibility.WorkstationTag,
                lanePolicy = ProductionWorkstationLanePolicy
                    .ManualWithDetachedBatchProcessors,
                manualWorkLaneCount = 1
            });
            abilities.Add(new BuildingProductionBufferAbility
            {
                defaultBatchCapacity = 2,
                physicalOutputBufferCycleCapacity = 2,
                allowOverflowDump = false
            });
            definition.ReplaceAbilities(abilities);
            host = new GameObject("B129CertifiedSeedFacility");
            Facility = host.AddComponent<BuildableObject>();
            Facility.RestorePersistentIdentity(new BuildingInstanceId("building:b129:0"));
            Set(Facility, nameof(BuildableObject.BuildingData), definition);
            Crops = Enumerable.Range(0, 5).Select(CreateCrop).ToArray();
        }

        internal BuildableObject Facility { get; }
        internal CropDefinitionSO[] Crops { get; }

        private static CropDefinitionSO CreateCrop(int index)
        {
            CropDefinitionSO crop = ScriptableObject.CreateInstance<CropDefinitionSO>();
            crop.Configure(
                $"crop:b129:{index}", $"검증 작물 {index}",
                $"harvest:b129:{index}", string.Empty,
                24f, 1f, 1f, 1f, 1, true, new Vector2(0f, 30f));
            crop.ConfigureEcology(
                $"seed:b129:{index}", null,
                default, default);
            return crop;
        }

        public void Dispose()
        {
            foreach (CropDefinitionSO crop in Crops)
                UnityEngine.Object.DestroyImmediate(crop);
            UnityEngine.Object.DestroyImmediate(host);
            UnityEngine.Object.DestroyImmediate(definition);
        }
    }

    private static void Require(bool condition, string failure)
    {
        if (!condition) throw new InvalidOperationException(failure);
    }
}
#endif
