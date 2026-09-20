#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DungeonStory.Content.CoreSession;
using DungeonStory.ServiceRooms;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class MigratedProducerEconomyServiceEntryE2EDebugScenarios
{
    public static IReadOnlyList<MigratedProducerOutcomeKind> CoveredKinds { get; } =
        new[]
        {
            MigratedProducerOutcomeKind.FacilityVisitEvent,
            MigratedProducerOutcomeKind.MetaUpgradePurchasedEvent,
            MigratedProducerOutcomeKind.PhysicalItemTransformReceipt,
            MigratedProducerOutcomeKind.RegularCustomerRecruitResult,
            MigratedProducerOutcomeKind.RegularCustomerVisitResult,
            MigratedProducerOutcomeKind.ServiceModeChangeResult
        };

    public static bool RunAll(bool throwOnFailure = true)
    {
        try
        {
            VerifyFacilityVisitEntry();
            VerifyPhysicalItemTransformEntry();
            VerifyServiceModeChangeEntry();
            VerifyMetaUpgradePurchaseEntry();
            VerifyRegularCustomerVisitEntry();
            VerifyRegularCustomerRecruitEntry();
            return true;
        }
        catch
        {
            if (throwOnFailure)
                throw;
            return false;
        }
    }

    private static void VerifyMetaUpgradePurchaseEntry()
    {
        const MigratedProducerOutcomeKind Kind =
            MigratedProducerOutcomeKind.MetaUpgradePurchasedEvent;

        using (var scenario = new MetaPurchaseScenario(Kind))
        {
            Require(
                scenario.Purchase()
                && scenario.Level == 1
                && scenario.Currency == 90,
                "The meta-upgrade production entry did not mutate its owner.");
            _ = MigratedProducerOutcomeE2EAssertions.RequireExact(
                scenario.Outcomes,
                Kind,
                scenario.Probe);
        }

        using (var scenario = new MetaPurchaseScenario(
                   Kind,
                   MigratedProducerOutcomeFaultMode.RejectCommit))
        {
            Require(
                !scenario.Purchase()
                && scenario.Level == 0
                && scenario.Currency == 100
                && RejectionReachedCommit(scenario.Probe, Kind),
                "A rejected meta-upgrade purchase left currency or level mutation.");
        }

        using (var scenario = new MetaPurchaseScenario(
                   Kind,
                   deliveryFault: true))
        {
            Require(
                scenario.Purchase()
                && scenario.Level == 1
                && scenario.Currency == 90,
                "A durably pending meta-upgrade purchase was rolled back.");
            int level = scenario.Level;
            int currency = scenario.Currency;
            MigratedProducerOutcomeE2EAssertions.RequirePendingThenRetry(
                scenario.Outcomes,
                Kind,
                scenario.Probe,
                scenario.DeliveryFault);
            Require(
                scenario.Level == level && scenario.Currency == currency,
                "Delivery retry duplicated the meta-upgrade purchase.");
        }
    }

    private static void VerifyPhysicalItemTransformEntry()
    {
        const MigratedProducerOutcomeKind Kind =
            MigratedProducerOutcomeKind.PhysicalItemTransformReceipt;

        using (var scenario = new PhysicalTransformScenario(Kind))
        {
            Require(
                scenario.Transform(out PhysicalItemTransformFailureCode code)
                && code == PhysicalItemTransformFailureCode.None
                && scenario.SourceQuantity == 2
                && scenario.OutputQuantity == 1,
                "The physical-transform production entry did not mutate its owner.");
            _ = MigratedProducerOutcomeE2EAssertions.RequireExact(
                scenario.Outcomes,
                Kind,
                scenario.Probe);
        }

        using (var scenario = new PhysicalTransformScenario(
                   Kind,
                   MigratedProducerOutcomeFaultMode.RejectCommit))
        {
            Require(
                !scenario.Transform(out PhysicalItemTransformFailureCode code)
                && code == PhysicalItemTransformFailureCode.OutcomeCommitFailed
                && scenario.SourceQuantity == 3
                && scenario.OutputQuantity == 0
                && RejectionReachedCommit(scenario.Probe, Kind),
                "A rejected physical transform left source/output mutation.");
        }

        using (var scenario = new PhysicalTransformScenario(
                   Kind,
                   deliveryFault: true))
        {
            Require(
                scenario.Transform(out _)
                && scenario.SourceQuantity == 2
                && scenario.OutputQuantity == 1,
                "A durably pending physical transform was rolled back.");
            int source = scenario.SourceQuantity;
            int output = scenario.OutputQuantity;
            MigratedProducerOutcomeE2EAssertions.RequirePendingThenRetry(
                scenario.Outcomes,
                Kind,
                scenario.Probe,
                scenario.DeliveryFault);
            Require(
                scenario.SourceQuantity == source
                && scenario.OutputQuantity == output,
                "Delivery retry duplicated the physical transform.");
        }
    }

    private static void VerifyServiceModeChangeEntry()
    {
        const MigratedProducerOutcomeKind Kind =
            MigratedProducerOutcomeKind.ServiceModeChangeResult;

        using (var scenario = new ServiceModeScenario(Kind))
        {
            ServiceModeChangeResult result = scenario.SwitchToDirect();
            Require(
                result.Succeeded
                && scenario.Mode == ServiceOperationMode.Direct,
                "The service-mode production entry did not mutate its owner.");
            _ = MigratedProducerOutcomeE2EAssertions.RequireExact(
                scenario.Outcomes,
                Kind,
                scenario.Probe);
        }

        using (var scenario = new ServiceModeScenario(
                   Kind,
                   MigratedProducerOutcomeFaultMode.RejectCommit))
        {
            ServiceModeChangeResult result = scenario.SwitchToDirect();
            Require(
                !result.Succeeded
                && scenario.Mode == ServiceOperationMode.Managed
                && scenario.Outcomes.Transaction.Capture().owners.Count == 0
                && RejectionReachedCommit(scenario.Probe, Kind),
                "A rejected service-mode change left a partial mode mutation.");
        }

        using (var scenario = new ServiceModeScenario(
                   Kind,
                   deliveryFault: true))
        {
            ServiceModeChangeResult result = scenario.SwitchToDirect();
            Require(
                result.Succeeded
                && scenario.Mode == ServiceOperationMode.Direct,
                "A durably pending service-mode change was rolled back.");
            ServiceOperationMode committed = scenario.Mode;
            MigratedProducerOutcomeE2EAssertions.RequirePendingThenRetry(
                scenario.Outcomes,
                Kind,
                scenario.Probe,
                scenario.DeliveryFault);
            Require(
                scenario.Mode == committed,
                "Delivery retry duplicated or reversed the service-mode mutation.");
        }
    }

    private static void VerifyFacilityVisitEntry()
    {
        const MigratedProducerOutcomeKind Kind =
            MigratedProducerOutcomeKind.FacilityVisitEvent;

        using (var scenario = new FacilityVisitScenario(Kind))
        {
            scenario.CommitVisit();
            Require(
                scenario.CompletedUses == 1,
                "The facility-visit production entry did not mutate its owner.");
            _ = MigratedProducerOutcomeE2EAssertions.RequireExact(
                scenario.Outcomes,
                Kind,
                scenario.Probe);
        }

        using (var scenario = new FacilityVisitScenario(
                   Kind,
                   MigratedProducerOutcomeFaultMode.RejectCommit))
        {
            RequireThrows<InvalidOperationException>(scenario.CommitVisit);
            Require(
                scenario.CompletedUses == 0
                && scenario.Outcomes.Transaction.Capture().owners.Count == 0
                && RejectionReachedCommit(scenario.Probe, Kind),
                "A rejected facility visit left a partial use mutation.");
        }

        using (var scenario = new FacilityVisitScenario(
                   Kind,
                   deliveryFault: true))
        {
            scenario.CommitVisit();
            int committedUses = scenario.CompletedUses;
            Require(
                committedUses == 1,
                "A durably pending facility visit was rolled back.");
            MigratedProducerOutcomeE2EAssertions.RequirePendingThenRetry(
                scenario.Outcomes,
                Kind,
                scenario.Probe,
                scenario.DeliveryFault);
            Require(
                scenario.CompletedUses == committedUses,
                "Delivery retry duplicated the facility visit mutation.");
        }
    }

    private static void VerifyRegularCustomerVisitEntry()
    {
        const MigratedProducerOutcomeKind Kind =
            MigratedProducerOutcomeKind.RegularCustomerVisitResult;

        using (var scenario = new RegularCustomerScenario(Kind))
        {
            scenario.Visit();
            Require(
                scenario.VisitCount == 1,
                "The regular-customer production visit did not mutate its owner.");
            _ = MigratedProducerOutcomeE2EAssertions.RequireExact(
                scenario.Outcomes,
                Kind,
                scenario.Probe);
        }

        using (var scenario = new RegularCustomerScenario(
                   Kind,
                   MigratedProducerOutcomeFaultMode.RejectCommit))
        {
            scenario.Visit();
            Require(
                scenario.VisitCount == 0
                && scenario.Outcomes.Transaction.Capture().owners.Count == 0
                && RejectionReachedCommit(scenario.Probe, Kind),
                "A rejected regular-customer visit left a partial mutation.");
        }

        using (var scenario = new RegularCustomerScenario(
                   Kind,
                   deliveryFault: true))
        {
            scenario.Visit();
            int committedVisits = scenario.VisitCount;
            Require(
                committedVisits == 1,
                "A durably pending regular-customer visit was rolled back.");
            MigratedProducerOutcomeE2EAssertions.RequirePendingThenRetry(
                scenario.Outcomes,
                Kind,
                scenario.Probe,
                scenario.DeliveryFault);
            Require(
                scenario.VisitCount == committedVisits,
                "Delivery retry duplicated the regular-customer visit mutation.");
        }
    }

    private static void VerifyRegularCustomerRecruitEntry()
    {
        const MigratedProducerOutcomeKind Kind =
            MigratedProducerOutcomeKind.RegularCustomerRecruitResult;

        using (var scenario = new RegularCustomerScenario(Kind))
        {
            scenario.MakeRecruitCandidate();
            scenario.Probe.ClearObservations();
            Require(
                scenario.Recruit(),
                "The regular-customer production recruit entry failed.");
            Require(
                scenario.IsRecruited,
                "The regular-customer recruit owner did not advance.");
            _ = MigratedProducerOutcomeE2EAssertions.RequireExact(
                scenario.Outcomes,
                Kind,
                scenario.Probe);
        }

        using (var scenario = new RegularCustomerScenario(
                   Kind,
                   MigratedProducerOutcomeFaultMode.RejectCommit))
        {
            scenario.MakeRecruitCandidate();
            scenario.Probe.ClearObservations();
            Require(
                !scenario.Recruit()
                && !scenario.IsRecruited
                && scenario.Actor.characterType == CharacterType.Customer
                && RejectionReachedCommit(scenario.Probe, Kind),
                "A rejected regular-customer recruit left owner or actor mutation.");
        }

        using (var scenario = new RegularCustomerScenario(
                   Kind,
                   deliveryFault: true))
        {
            scenario.MakeRecruitCandidate();
            scenario.Probe.ClearObservations();
            Require(
                scenario.Recruit() && scenario.IsRecruited,
                "A durably pending regular-customer recruit was rolled back.");
            CharacterType committedType = scenario.Actor.characterType;
            MigratedProducerOutcomeE2EAssertions.RequirePendingThenRetry(
                scenario.Outcomes,
                Kind,
                scenario.Probe,
                scenario.DeliveryFault);
            Require(
                scenario.IsRecruited
                && scenario.Actor.characterType == committedType,
                "Delivery retry duplicated or reversed regular-customer recruitment.");
        }
    }

    private sealed class RegularCustomerScenario : IDisposable
    {
        private readonly GameObject runtimeObject;
        private readonly BuildableObject facility;

        internal RegularCustomerScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode faultMode =
                MigratedProducerOutcomeFaultMode.None,
            bool deliveryFault = false)
        {
            DeliveryFault = new MigratedProducerOutcomeDeliveryFault
            {
                TargetKind = target,
                Enabled = deliveryFault
            };
            Outcomes = new MigratedProducerOutcomeEditorFixture(
                "run:migrated-entry-regular-" + Guid.NewGuid().ToString("N"),
                deliveryFault: DeliveryFault);
            Probe = new MigratedProducerOutcomeProbeTransaction(
                Outcomes.Transaction,
                Outcomes.Recorder)
            {
                TargetKind = target,
                FaultMode = faultMode
            };

            runtimeObject = new GameObject("Migrated Regular Customer E2E");
            Runtime = runtimeObject.AddComponent<RegularCustomerRuntime>();
            Runtime.ConstructRecruitmentRuntime(
                new RecruitActivation(),
                new DungeonStory.Foundation.GameEventBus(),
                new FixedGameSessionStateProvider(20),
                Probe);
            Actor = CreateCustomerForEntryFixture();
            facility = CreateFacilityForEntryFixture();
            CustomerId = RegularCustomerService.GetCustomerId(Actor);
        }

        internal MigratedProducerOutcomeEditorFixture Outcomes { get; }
        internal MigratedProducerOutcomeProbeTransaction Probe { get; }
        internal MigratedProducerOutcomeDeliveryFault DeliveryFault { get; }
        internal RegularCustomerRuntime Runtime { get; }
        internal CharacterActor Actor { get; }
        internal string CustomerId { get; }
        internal int VisitCount => Runtime.State.TryGetRecord(
                CustomerId,
                out RegularCustomerRecord record)
            ? record.VisitCount
            : 0;
        internal bool IsRecruited => Runtime.State.IsRecruited(CustomerId);

        internal void Visit() => Runtime.OnTriggerEvent(
            new FacilityVisitEvent(CharacterActor.From(Actor), facility));

        internal void MakeRecruitCandidate()
        {
            for (int index = 0; index < 4; index++)
                Visit();
            Require(
                Runtime.State.TryGetRecord(
                    CustomerId,
                    out RegularCustomerRecord record)
                && record.IsRecruitCandidate,
                "The production visit path did not create a recruit candidate.");
        }

        internal bool Recruit() => Runtime.TryRecruit(CustomerId, out _);

        public void Dispose()
        {
            if (facility?.BuildingData != null)
                Object.DestroyImmediate(facility.BuildingData);
            if (facility != null)
                Object.DestroyImmediate(facility.gameObject);
            if (Actor?.data != null)
                Object.DestroyImmediate(Actor.data);
            if (Actor != null)
                Object.DestroyImmediate(Actor.gameObject);
            if (runtimeObject != null)
                Object.DestroyImmediate(runtimeObject);
        }

        internal static CharacterActor CreateCustomerForEntryFixture()
        {
            GameObject gameObject = new("Migrated Regular Customer");
            gameObject.AddComponent<SpriteRenderer>();
            CharacterAiEditorTestDependencies.EnsureCharacterProgression(
                gameObject);
            CharacterActor actor = gameObject.AddComponent<CharacterActor>();
            CharacterSO data =
                CharacterAiEditorTestDependencies.CreateCharacterFixtureData(
                    CharacterType.Customer,
                    "원장 검증 손님",
                    "Slime");
            CharacterAiEditorTestDependencies.Inject(gameObject);
            actor.Initialization(data);
            actor.characterType = CharacterType.Customer;
            actor.Identity.SetPersistentId(
                "character:world:migrated-e2e:regular-customer");
            actor.stats ??= new Dictionary<CharacterCondition, float>();
            actor.stats[CharacterCondition.HUNGER] = 100f;
            actor.stats[CharacterCondition.SLEEP] = 100f;
            actor.stats[CharacterCondition.FUN] = 100f;
            actor.stats[CharacterCondition.MOOD] = 90f;
            return actor;
        }

        internal static BuildableObject CreateFacilityForEntryFixture()
        {
            GameObject gameObject = new("Migrated Regular Customer Facility");
            BuildableObject facility = gameObject.AddComponent<BuildableObject>();
            facility.ConstructPersistentIdentity(new GuidPersistentIdGenerator());
            BuildingSO data = ScriptableObject.CreateInstance<BuildingSO>();
            data.id = 9910;
            data.objectName = "원장 검증 시설";
            data.width = 1;
            data.height = 1;
            data.category = BuildingCategory.Shop;
            data.Facility = new FacilityData
            {
                roles = FacilityRole.Meal,
                capacity = 1
            };
            data.Facility.SetSupportedWorkTypeIds(
                new[] { BuiltInWorkTypeIds.Operate });
            CharacterAiEditorTestDependencies.Inject(facility);
            facility.Initialization(data, Vector2Int.zero);
            return facility;
        }
    }

    private sealed class FacilityVisitScenario : IDisposable
    {
        private readonly CharacterActor visitor;
        private readonly BuildableObject facility;
        private readonly BuildingVisitEventPublisher publisher;

        internal FacilityVisitScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode faultMode =
                MigratedProducerOutcomeFaultMode.None,
            bool deliveryFault = false)
        {
            DeliveryFault = new MigratedProducerOutcomeDeliveryFault
            {
                TargetKind = target,
                Enabled = deliveryFault
            };
            Outcomes = new MigratedProducerOutcomeEditorFixture(
                "run:migrated-entry-facility-visit-"
                + Guid.NewGuid().ToString("N"),
                deliveryFault: DeliveryFault);
            Probe = new MigratedProducerOutcomeProbeTransaction(
                Outcomes.Transaction,
                Outcomes.Recorder)
            {
                TargetKind = target,
                FaultMode = faultMode
            };
            publisher = new BuildingVisitEventPublisher(
                new DungeonStory.Foundation.GameEventBus(),
                new FixedGameSessionStateProvider(7),
                Probe);
            visitor = RegularCustomerScenario.CreateCustomerForEntryFixture();
            facility = RegularCustomerScenario.CreateFacilityForEntryFixture();
        }

        internal MigratedProducerOutcomeEditorFixture Outcomes { get; }
        internal MigratedProducerOutcomeProbeTransaction Probe { get; }
        internal MigratedProducerOutcomeDeliveryFault DeliveryFault { get; }
        internal int CompletedUses => facility.FacilityState.completedUses;

        internal void CommitVisit() => publisher.CommitVisit(
            CharacterActor.From(visitor),
            facility);

        public void Dispose()
        {
            if (facility?.BuildingData != null)
                Object.DestroyImmediate(facility.BuildingData);
            if (facility != null)
                Object.DestroyImmediate(facility.gameObject);
            if (visitor?.data != null)
                Object.DestroyImmediate(visitor.data);
            if (visitor != null)
                Object.DestroyImmediate(visitor.gameObject);
        }
    }

    private sealed class ServiceModeScenario : IDisposable
    {
        private readonly ServiceSessionRuntime runtime;
        private readonly BuildableObject hub;
        private readonly BuildingSO hubData;

        internal ServiceModeScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode faultMode =
                MigratedProducerOutcomeFaultMode.None,
            bool deliveryFault = false)
        {
            DeliveryFault = new MigratedProducerOutcomeDeliveryFault
            {
                TargetKind = target,
                Enabled = deliveryFault
            };
            Outcomes = new MigratedProducerOutcomeEditorFixture(
                "run:migrated-entry-service-mode-"
                + Guid.NewGuid().ToString("N"),
                deliveryFault: DeliveryFault);
            Probe = new MigratedProducerOutcomeProbeTransaction(
                Outcomes.Transaction,
                Outcomes.Recorder)
            {
                TargetKind = target,
                FaultMode = faultMode
            };

            CoreSessionRulesSO rulesAsset =
                AssetDatabase.LoadAssetAtPath<CoreSessionRulesSO>(
                    "Assets/Resources/SO/Content/CoreSessionRules.asset")
                ?? throw new InvalidOperationException(
                    "Authored core-session rules are missing.");
            var store = new DungeonRuntimeAggregateRootStore();
            var restoreCandidates = new RestoreWorldCandidateIndex();
            runtime = new ServiceSessionRuntime(
                DefaultProxy.Create<IBuildingWorldQuery>(),
                DefaultProxy.Create<IServiceRoomLinkRuntime>(),
                DefaultProxy.Create<IServiceProcessCatalog>(),
                new ServiceSessionRuntime.Dependencies(
                    new DungeonStory.Foundation.UnityGameClock(),
                    DefaultProxy.Create<IGameMoneyAccount>(),
                    DefaultProxy.Create<IPowerInfrastructureQuery>(),
                    DefaultProxy.Create<IServiceRoomResearchQuery>(),
                    new FixedRulesProvider(rulesAsset.CreateRuntimeDefinition()),
                    new FixedGameSessionStateProvider(9),
                    Probe),
                store,
                restoreCandidates,
                DefaultProxy.Create<
                    ICharacterWorldPersistenceIdentityQuery>());

            BuildingSO source = AssetDatabase.LoadAssetAtPath<BuildingSO>(
                "Assets/Resources/SO/Building/Modular/D04_배식카운터.asset")
                ?? throw new InvalidOperationException(
                    "The Direct service-hub fixture asset is missing.");
            hubData = Object.Instantiate(source);
            GameObject gameObject = new("Migrated Service Mode Hub");
            hub = gameObject.AddComponent<BuildableObject>();
            hub.ConstructPersistentIdentity(new GuidPersistentIdGenerator());
            CharacterAiEditorTestDependencies.Inject(hub);
            hub.Initialization(hubData, Vector2Int.zero);
            restoreCandidates.SetFacilityCandidate(
                new Grid(1, 1),
                new[] { hub });
            restoreCandidates.SetCharacterCandidate(
                Array.Empty<CharacterActor>());
            string hubId = hub.RequirePersistentInstanceId().Value;
            var save = new ServiceRoomsSaveData();
            save.hubs.Add(new ServiceHubModeSaveData
            {
                hubId = hubId,
                mode = ServiceOperationMode.Managed
            });
            runtime.PublishRestoreCandidate(
                runtime.PrepareRestoreCandidate(save));
            Require(
                Mode == ServiceOperationMode.Managed,
                "The service-mode fixture did not restore its pre-command owner state.");
        }

        internal MigratedProducerOutcomeEditorFixture Outcomes { get; }
        internal MigratedProducerOutcomeProbeTransaction Probe { get; }
        internal MigratedProducerOutcomeDeliveryFault DeliveryFault { get; }
        internal ServiceOperationMode Mode =>
            runtime.GetHubSnapshot(hub).Mode;

        internal ServiceModeChangeResult SwitchToDirect() =>
            runtime.SwitchToDirect(hub);

        public void Dispose()
        {
            runtime?.Dispose();
            if (hub != null)
                Object.DestroyImmediate(hub.gameObject);
            if (hubData != null)
                Object.DestroyImmediate(hubData);
        }
    }

    private sealed class PhysicalTransformScenario : IDisposable
    {
        private const string SourceItemId = "material:lumber";
        private readonly WorldItemRepository repository;
        private readonly PhysicalStockQuery stockQuery;
        private readonly PhysicalItemTransformService service;
        private readonly string sourceStackId;
        private readonly string outputItemId;
        private readonly Vector2Int position = new(47, 9);

        internal PhysicalTransformScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode faultMode =
                MigratedProducerOutcomeFaultMode.None,
            bool deliveryFault = false)
        {
            DeliveryFault = new MigratedProducerOutcomeDeliveryFault
            {
                TargetKind = target,
                Enabled = deliveryFault
            };
            Outcomes = new MigratedProducerOutcomeEditorFixture(
                "run:migrated-entry-physical-transform-"
                + Guid.NewGuid().ToString("N"),
                deliveryFault: DeliveryFault);
            Probe = new MigratedProducerOutcomeProbeTransaction(
                Outcomes.Transaction,
                Outcomes.Recorder)
            {
                TargetKind = target,
                FaultMode = faultMode
            };
            repository = new WorldItemRepository(
                new GuidPersistentIdGenerator(),
                new DungeonRuntimeAggregateRootStore());
            IDungeonItemCatalogProvider catalog =
                EditorItemCatalogFactory.Create();
            var definitions = new GenericDefinitionPhysicalItemMassProjector(
                catalog);
            IPhysicalItemMassQuery massQuery = new PhysicalItemMassQuery(
                new IPhysicalItemMassProjector[] { definitions });
            stockQuery = new PhysicalStockQuery(repository, catalog, massQuery);
            long sourceMass = massQuery.GetDefinitionUnitMass(
                (ItemDefinitionId)SourceItemId).Value;
            outputItemId = catalog.All
                .Where(definition => definition != null
                    && definition.MaxStack > 1
                    && !string.Equals(
                        definition.ItemId,
                        SourceItemId,
                        StringComparison.Ordinal)
                    && !PhysicalItemIds.TryGetEquipmentDefinitionId(
                        definition.ItemId,
                        out _)
                    && !PhysicalItemIds.IsEquipmentModule(definition.ItemId)
                    && massQuery.GetDefinitionUnitMass(
                        (ItemDefinitionId)definition.ItemId).Value <= sourceMass)
                .OrderBy(definition => definition.ItemId, StringComparer.Ordinal)
                .Select(definition => definition.ItemId)
                .FirstOrDefault()
                ?? throw new InvalidOperationException(
                    "No bounded physical-transform output fixture exists.");
            IItemMarkerPresenter markers =
                EditorNullItemMarkerPresenter.Instance;
            service = new PhysicalItemTransformService(
                repository,
                new WorldItemSpawner(catalog, repository, markers),
                massQuery,
                catalog,
                markers,
                new FixedGameSessionStateProvider(11),
                Probe);
            sourceStackId = WorldItemRepositoryEditorAccess.AddStack(
                repository,
                SourceItemId,
                3,
                WorldItemStackState.Loose,
                position: position);
        }

        internal MigratedProducerOutcomeEditorFixture Outcomes { get; }
        internal MigratedProducerOutcomeProbeTransaction Probe { get; }
        internal MigratedProducerOutcomeDeliveryFault DeliveryFault { get; }
        internal int SourceQuantity => stockQuery.GetAllStacks()
            .Where(record => record != null
                && string.Equals(
                    record.StackId,
                    sourceStackId,
                    StringComparison.Ordinal))
            .Sum(record => record.Quantity);
        internal int OutputQuantity => stockQuery.GetAllStacks()
            .Where(record => record != null
                && string.Equals(
                    record.ItemId,
                    outputItemId,
                    StringComparison.Ordinal))
            .Sum(record => record.Quantity);

        internal bool Transform(out PhysicalItemTransformFailureCode code) =>
            service.TryTransformQuantity(
                sourceStackId,
                1,
                new[]
                {
                    new PhysicalItemTransformOutput(
                        outputItemId,
                        1,
                        position)
                },
                "migrated-e2e:physical-transform",
                "migrated-e2e",
                out _,
                out code,
                out _);

        public void Dispose()
        {
        }
    }

    private sealed class MetaPurchaseScenario : IDisposable
    {
        private const string UpgradeId = "meta:migrated-e2e:upgrade";
        private readonly GameObject runtimeObject;
        private readonly MetaProgressionRuntime runtime;

        internal MetaPurchaseScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode faultMode =
                MigratedProducerOutcomeFaultMode.None,
            bool deliveryFault = false)
        {
            DeliveryFault = new MigratedProducerOutcomeDeliveryFault
            {
                TargetKind = target,
                Enabled = deliveryFault
            };
            Outcomes = new MigratedProducerOutcomeEditorFixture(
                "run:migrated-entry-meta-purchase-"
                + Guid.NewGuid().ToString("N"),
                deliveryFault: DeliveryFault);
            Probe = new MigratedProducerOutcomeProbeTransaction(
                Outcomes.Transaction,
                Outcomes.Recorder)
            {
                TargetKind = target,
                FaultMode = faultMode
            };
            var catalog = new SingleMetaCatalog(new MetaUpgradeDefinition(
                UpgradeId,
                MetaProgressionBranch.OperationKnowledge,
                "원장 검증 강화",
                "실제 구매 진입 검증",
                10,
                1,
                Array.Empty<IMetaUpgradeEffect>()));
            runtimeObject = new GameObject("Migrated Meta Purchase E2E");
            runtime = runtimeObject.AddComponent<MetaProgressionRuntime>();
            runtime.Construct(
                new MetaRunResultBuilder(),
                new MetaApplicationPort(),
                new DungeonStory.Foundation.UnityGameClock(),
                catalog,
                new DungeonRuntimeAggregateRootStore());
            runtime.ConstructOutcomeTransactions(
                new MetaUpgradePurchaseOutcomeTransaction(Probe));
            runtime.State.AddCurrency(100);
        }

        internal MigratedProducerOutcomeEditorFixture Outcomes { get; }
        internal MigratedProducerOutcomeProbeTransaction Probe { get; }
        internal MigratedProducerOutcomeDeliveryFault DeliveryFault { get; }
        internal int Level => runtime.State.GetUpgradeLevel(UpgradeId);
        internal int Currency => runtime.State.AvailableCurrency;

        internal bool Purchase() =>
            runtime.TryPurchaseUpgrade(UpgradeId, out _);

        public void Dispose()
        {
            if (runtimeObject != null)
                Object.DestroyImmediate(runtimeObject);
        }
    }

    private sealed class SingleMetaCatalog : IMetaUpgradeDefinitionCatalog
    {
        private readonly MetaUpgradeDefinition definition;

        internal SingleMetaCatalog(MetaUpgradeDefinition definition) =>
            this.definition = definition
                ?? throw new ArgumentNullException(nameof(definition));

        public IReadOnlyCollection<MetaUpgradeDefinition> All =>
            new[] { definition };

        public MetaUpgradeDefinition Get(string id) =>
            string.Equals(definition.id, id, StringComparison.Ordinal)
                ? definition
                : null;

        public MetaUpgradeDefinition Require(string id) =>
            Get(id) ?? throw new KeyNotFoundException(id);
    }

    private sealed class MetaApplicationPort : IMetaRuntimeApplicationPort
    {
        public void Bind(IMetaRuntimeEventSink runtime)
        {
        }

        public void Unbind(IMetaRuntimeEventSink runtime)
        {
        }

        public MetaRunEnvironmentSnapshot CaptureRunEnvironment() =>
            new(1f, DungeonDifficulty.Normal,
                DungeonSurvivalPressure.Standard);

        public void PublishUpgradePurchased(
            MetaUpgradePurchasedEvent purchasedEvent,
            string message)
        {
        }

        public void PublishRunResult(RunResultReadyEvent readyEvent)
        {
        }

        public void ShowRunResult(RunResultSnapshot result)
        {
        }
    }

    private sealed class FixedRulesProvider : ICoreSessionRulesProvider
    {
        internal FixedRulesProvider(CoreSessionRulesDefinition rules) =>
            CoreSessionRules = rules
                ?? throw new ArgumentNullException(nameof(rules));

        public CoreSessionRulesDefinition CoreSessionRules { get; }
    }

    public class DefaultProxy : DispatchProxy
    {
        public DefaultProxy()
        {
        }

        internal static TContract Create<TContract>()
            where TContract : class =>
            DispatchProxy.Create<TContract, DefaultProxy>();

        protected override object Invoke(
            MethodInfo targetMethod,
            object[] arguments)
        {
            ParameterInfo[] parameters = targetMethod.GetParameters();
            for (int index = 0; index < parameters.Length; index++)
            {
                Type parameterType = parameters[index].ParameterType;
                if (!parameters[index].IsOut && !parameterType.IsByRef)
                    continue;
                Type valueType = parameterType.IsByRef
                    ? parameterType.GetElementType()
                    : parameterType;
                arguments[index] = valueType != null && valueType.IsValueType
                    ? Activator.CreateInstance(valueType)
                    : null;
            }
            Type returnType = targetMethod.ReturnType;
            if (returnType == typeof(void))
                return null;
            if (returnType.IsArray)
                return Array.CreateInstance(returnType.GetElementType(), 0);
            if (returnType.IsGenericType)
            {
                Type definition = returnType.GetGenericTypeDefinition();
                if (definition == typeof(IReadOnlyList<>)
                    || definition == typeof(IReadOnlyCollection<>)
                    || definition == typeof(IEnumerable<>))
                {
                    return Array.CreateInstance(
                        returnType.GetGenericArguments()[0],
                        0);
                }
            }
            return returnType.IsValueType
                ? Activator.CreateInstance(returnType)
                : null;
        }
    }

    private sealed class RecruitActivation : IRecruitedCharacterActivationService
    {
        public bool TryValidateActivation(
            RegularCustomerRecord record,
            out string message)
        {
            message = record?.ActiveActor == null ? "actor missing" : string.Empty;
            return record?.ActiveActor != null;
        }

        public bool TryActivate(
            RegularCustomerRecord record,
            out CharacterActor actor,
            out string message)
        {
            actor = record?.ActiveActor;
            if (actor != null)
            {
                actor.characterType = CharacterType.NPC;
                actor.SetLifecycleState(CharacterLifecycleState.Active);
                if (actor.GetComponent<AbilityWork>() == null)
                    actor.gameObject.AddComponent<AbilityWork>();
                actor.RefreshAbilityCache();
            }
            message = actor == null ? "actor missing" : "activated";
            return actor != null;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static bool RejectionReachedCommit(
        MigratedProducerOutcomeProbeTransaction probe,
        MigratedProducerOutcomeKind kind) =>
        probe.ReservationCount(kind) > 0
        && probe.CommitAttemptCount(kind) > 0
        && probe.CommitCount(kind) == 0;

    private static void RequireThrows<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        throw new InvalidOperationException(
            "The production entry did not surface the injected "
            + typeof(TException).Name + ".");
    }
}
#endif
