#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;
using VContainer.Diagnostics;

// Restore-only composition support for the controlled Informant owner fixture.
// The caller owns the actors, buildings, grid, handler execution and publication.
// This does not compose a full world simulation or start/tick the exterior runtime.
internal static class VisitorReceptionCheckoutCausalTestPorts
{
    internal static ExteriorActivityRuntime CreateExterior(
        Grid grid,
        IReadOnlyList<BuildableObject> buildings,
        IReadOnlyList<CharacterActor> characters,
        IGameClock clock,
        IGameCalendar calendar,
        IRandomStreamProvider randomStreams,
        ExteriorIncidentHandlerRegistry handlers,
        out RestoreWorldCandidateIndex candidates)
    {
        if (grid == null) throw new ArgumentNullException(nameof(grid));
        if (buildings == null) throw new ArgumentNullException(nameof(buildings));
        if (characters == null) throw new ArgumentNullException(nameof(characters));
        if (clock == null) throw new ArgumentNullException(nameof(clock));
        if (calendar == null) throw new ArgumentNullException(nameof(calendar));
        if (randomStreams == null) throw new ArgumentNullException(nameof(randomStreams));
        if (handlers == null) throw new ArgumentNullException(nameof(handlers));

        ResourceGameContentCatalog content = new(new UnityGameContentRootLoader());
        RuntimeBuildingArchetypeCatalog archetypes = new(content);
        IDungeonItemCatalogProvider itemCatalog = new ResourceDungeonItemCatalogProvider(
            new ResourceItemDefinitionCatalog(content));
        IWorldItemStackRuntime itemQueries = VisitorReceptionCheckoutRestoreItemQueries.Create(
            CharacterAiEditorTestDependencies.PhysicalStock,
            itemCatalog);
        UnsupportedExteriorDomains unsupported = new();

        candidates = new RestoreWorldCandidateIndex();
        candidates.SetFacilityCandidate(grid, buildings);
        candidates.SetCharacterCandidate(characters);
        // Wildlife and Society incidents are explicitly absent in this fixture.
        candidates.SetWildlifeCandidate(Array.Empty<WildlifeActor>());

        return new ExteriorActivityRuntime(
            new ExteriorActivityWorldServices(
                new SuppliedGridProvider(grid),
                unsupported,
                // Scene discovery is outside this restore-only composition.
                new WorldSimulationSceneReferences(
                    wildlifeHabitats: Array.Empty<WildlifeHabitatMarker>(),
                    exteriorZones: Array.Empty<ExteriorZoneMarker>()),
                new DetachedZoneInjectionResolver(),
                archetypes,
                candidates,
                candidates,
                itemQueries,
                // There are no acknowledged production-output batches here;
                // this is a prepared empty candidate, not an unavailable query.
                EmptyFacilityBufferAcknowledgedOutputRestoreCandidateQuery.Instance,
                unsupported,
                new PreparedEmptySocietyIncidentOwners()),
            new ExteriorActivityDomainServices(
                unsupported,
                unsupported,
                handlers,
                unsupported,
                unsupported),
            // Production constructor obtains its real "exterior-incidents" stream.
            new ExteriorActivityExecutionServices(clock, calendar, randomStreams));
    }

    internal static NotSupportedException Unsupported(string operation) => new(
        "Visitor reception/checkout restore-only fixture does not support "
        + operation + ".");

    private sealed class SuppliedGridProvider : IGridSystemProvider
    {
        private readonly Grid grid;

        internal SuppliedGridProvider(Grid grid) => this.grid = grid;

        public Grid Grid => grid;
        public GridSystemManager Manager => throw Unsupported(
            nameof(IGridSystemProvider) + "." + nameof(Manager));
        public bool TryGetGrid(out Grid value)
        {
            value = grid;
            return true;
        }
        public bool TryGetManager(out GridSystemManager manager) => throw Unsupported(
            nameof(IGridSystemProvider) + "." + nameof(TryGetManager));
    }

    private sealed class PreparedEmptySocietyIncidentOwners :
        ISocietyIncidentRestoreOwnedCharacterIdQuery
    {
        public bool TryGetOwnedCharacterIds(out IReadOnlyCollection<CharacterId> characterIds)
        {
            // This slice has no Society incident; its visitor is Exterior-owned.
            characterIds = Array.Empty<CharacterId>();
            return true;
        }
    }

    private sealed class DetachedZoneInjectionResolver : IObjectResolver
    {
        public object ApplicationOrigin => throw Unsupported(
            nameof(IObjectResolver) + "." + nameof(ApplicationOrigin));
        public DiagnosticsCollector Diagnostics
        {
            get => throw Unsupported(nameof(IObjectResolver) + "." + nameof(Diagnostics));
            set => throw Unsupported(nameof(IObjectResolver) + "." + nameof(Diagnostics));
        }
        public object Resolve(Type type, object key = null) => throw Unsupported(
            nameof(IObjectResolver) + ".Resolve(Type)");
        public bool TryResolve(Type type, out object resolved, object key = null) =>
            throw Unsupported(nameof(IObjectResolver) + "." + nameof(TryResolve));
        public object Resolve(Registration registration) => throw Unsupported(
            nameof(IObjectResolver) + ".Resolve(Registration)");
        public IScopedObjectResolver CreateScope(Action<IContainerBuilder> installation = null) =>
            throw Unsupported(nameof(IObjectResolver) + "." + nameof(CreateScope));
        public bool TryGetRegistration(Type type, out Registration registration, object key = null) =>
            throw Unsupported(nameof(IObjectResolver) + "." + nameof(TryGetRegistration));
        public void Dispose() => throw Unsupported(nameof(IObjectResolver) + "." + nameof(Dispose));

        public void Inject(object instance)
        {
            if (instance is not ExteriorZoneMarker zone
                || !zone.IsDetachedRestoreCandidate
                || zone.gameObject.activeSelf
                || zone.gameObject.activeInHierarchy)
            {
                throw Unsupported(
                    nameof(IObjectResolver) + ".Inject outside an inactive detached ExteriorZoneMarker");
            }

            // The production coordinator creates this zone and controls its ID.
            CharacterAiEditorTestDependencies.Inject((BuildableObject)zone);
        }
    }

    private sealed class UnsupportedExteriorDomains :
        IWorldDropZoneQuery,
        ICharacterBodyHealthQuery,
        ICharacterMedicalCommand,
        ISurvivalEnvironmentQuery,
        IExperiencePacingRuntime,
        IPhysicalItemExactSourceRestoreAuthorityCommand
    {
        public bool TryGetDeliveryDropoff(out Vector2Int position) => throw Unsupported(
            nameof(IWorldDropZoneQuery) + "." + nameof(TryGetDeliveryDropoff));
        public bool TryGetExpeditionLootDropoff(out Vector2Int position) => throw Unsupported(
            nameof(IWorldDropZoneQuery) + "." + nameof(TryGetExpeditionLootDropoff));
        public bool TryGetVisitorEntryPoint(out WorldGridEntryPoint entryPoint) => throw Unsupported(
            nameof(IWorldDropZoneQuery) + "." + nameof(TryGetVisitorEntryPoint));

        public CharacterVitalsSnapshot GetVitals(CharacterActor actor) => throw Unsupported(
            nameof(ICharacterBodyHealthQuery) + ".GetVitals(actor)");
        public CharacterVitalsSnapshot GetVitals(string characterId) => throw Unsupported(
            nameof(ICharacterBodyHealthQuery) + ".GetVitals(id)");
        public CharacterBodyHealthSnapshot GetSnapshot(CharacterActor actor) => throw Unsupported(
            nameof(ICharacterBodyHealthQuery) + ".GetSnapshot(actor)");
        public CharacterBodyHealthSnapshot GetSnapshot(string characterId) => throw Unsupported(
            nameof(ICharacterBodyHealthQuery) + ".GetSnapshot(id)");
        public float GetTotalBleeding(CharacterActor target) => throw Unsupported(
            nameof(ICharacterBodyHealthQuery) + "." + nameof(GetTotalBleeding));
        public float GetMissingPartHealth(CharacterActor target) => throw Unsupported(
            nameof(ICharacterBodyHealthQuery) + "." + nameof(GetMissingPartHealth));

        public bool TryReserveBestOrder(
            CharacterActor rescuer,
            out CharacterMedicalOrder order,
            out DomainFailure failure) => throw Unsupported(
                nameof(ICharacterMedicalCommand) + "." + nameof(TryReserveBestOrder));
        public bool TryReserveOrderForPatient(
            CharacterActor rescuer,
            CharacterActor patient,
            out CharacterMedicalOrder order,
            out DomainFailure failure) => throw Unsupported(
                nameof(ICharacterMedicalCommand) + "." + nameof(TryReserveOrderForPatient));
        public bool TryRequestTreatment(
            CharacterActor patient,
            out CharacterMedicalOrder order,
            out DomainFailure failure) => throw Unsupported(
                nameof(ICharacterMedicalCommand) + "." + nameof(TryRequestTreatment));
        public bool TryAssignSpecificTreatmentFacility(
            string orderId,
            BuildableObject facility,
            out DomainFailure failure) => throw Unsupported(
                nameof(ICharacterMedicalCommand) + "." + nameof(TryAssignSpecificTreatmentFacility));
        public float AdvanceStabilization(string orderId, CharacterActor rescuer, float work) =>
            throw Unsupported(nameof(ICharacterMedicalCommand) + "." + nameof(AdvanceStabilization));
        public bool TryBeginCarrying(
            string orderId,
            CharacterActor rescuer,
            out DomainFailure failure) => throw Unsupported(
                nameof(ICharacterMedicalCommand) + "." + nameof(TryBeginCarrying));
        public bool TryPlaceAtTreatmentDestination(
            string orderId,
            CharacterActor rescuer,
            out DomainFailure failure) => throw Unsupported(
                nameof(ICharacterMedicalCommand) + "." + nameof(TryPlaceAtTreatmentDestination));
        public float AdvanceTreatment(string orderId, CharacterActor rescuer, float work) =>
            throw Unsupported(nameof(ICharacterMedicalCommand) + "." + nameof(AdvanceTreatment));
        public bool TryReleaseReservation(
            string orderId,
            CharacterActor rescuer,
            CharacterMedicalStatusCode releaseStatus,
            out DomainFailure failure) => throw Unsupported(
                nameof(ICharacterMedicalCommand) + "." + nameof(TryReleaseReservation));
        public void NotifyCharacterDowned(CharacterActor actor) => throw Unsupported(
            nameof(ICharacterMedicalCommand) + "." + nameof(NotifyCharacterDowned));
        public void NotifyCharacterRecovered(CharacterActor actor) => throw Unsupported(
            nameof(ICharacterMedicalCommand) + "." + nameof(NotifyCharacterRecovered));

        public bool HasFuelSupply(BuildableObject building) => throw Unsupported(
            nameof(ISurvivalEnvironmentQuery) + "." + nameof(HasFuelSupply));
        public float GetRemainingFuelGameSeconds(BuildableObject building) => throw Unsupported(
            nameof(ISurvivalEnvironmentQuery) + "." + nameof(GetRemainingFuelGameSeconds));
        public SurvivalEnvironmentSnapshot GetEnvironmentSnapshot() => throw Unsupported(
            nameof(ISurvivalEnvironmentQuery) + "." + nameof(GetEnvironmentSnapshot));

        public int CurrentDay => throw Unsupported(nameof(IExperiencePacingRuntime) + "." + nameof(CurrentDay));
        public bool AllowsRandomInvasion => throw Unsupported(
            nameof(IExperiencePacingRuntime) + "." + nameof(AllowsRandomInvasion));
        public int MaximumConcurrentExternalProblems => throw Unsupported(
            nameof(IExperiencePacingRuntime) + "." + nameof(MaximumConcurrentExternalProblems));
        public bool IsRehearsalActive => throw Unsupported(
            nameof(IExperiencePacingRuntime) + "." + nameof(IsRehearsalActive));
        public int ActiveRehearsalDay => throw Unsupported(
            nameof(IExperiencePacingRuntime) + "." + nameof(ActiveRehearsalDay));
        public void AdvanceToDay(int day) => throw Unsupported(
            nameof(IExperiencePacingRuntime) + "." + nameof(AdvanceToDay));
        public bool TryBeginRehearsal(int day, out RehearsalInvasionProfile profile) => throw Unsupported(
            nameof(IExperiencePacingRuntime) + "." + nameof(TryBeginRehearsal));
        public void ResolveRehearsal() => throw Unsupported(
            nameof(IExperiencePacingRuntime) + "." + nameof(ResolveRehearsal));
        public bool CanStartExteriorIncident(ExteriorIncidentKind kind) => throw Unsupported(
            nameof(IExperiencePacingRuntime) + "." + nameof(CanStartExteriorIncident));
        public void MarkExteriorIncidentStarted(ExteriorIncidentKind kind) => throw Unsupported(
            nameof(IExperiencePacingRuntime) + "." + nameof(MarkExteriorIncidentStarted));
        public DungeonExperiencePacingSaveData Capture() => throw Unsupported(
            nameof(IExperiencePacingRuntime) + "." + nameof(Capture));
        public ExperiencePacingAggregateState PrepareRestoreCandidate(DungeonExperiencePacingSaveData data) =>
            throw Unsupported(nameof(IExperiencePacingRuntime) + "." + nameof(PrepareRestoreCandidate));
        public void PublishRestoreCandidate(ExperiencePacingAggregateState candidate) => throw Unsupported(
            nameof(IExperiencePacingRuntime) + "." + nameof(PublishRestoreCandidate));

        public bool TryReplaceRestoreAuthorities(
            IReadOnlyCollection<string> ownedDomains,
            IReadOnlyList<PhysicalItemExactSourceRestoreDescriptor> retained,
            out string failureReason) => throw Unsupported(
                nameof(IPhysicalItemExactSourceRestoreAuthorityCommand) + "." + nameof(TryReplaceRestoreAuthorities));
    }
}

// Public solely because DispatchProxy emits a derived type. This is not a general
// IWorldItemStackRuntime implementation: only the two restore queries are allowed.
public class VisitorReceptionCheckoutRestoreItemQueries : DispatchProxy
{
    private IStockQuery stock;
    private IDungeonItemCatalogProvider catalog;

    internal static IWorldItemStackRuntime Create(IStockQuery stock, IDungeonItemCatalogProvider catalog)
    {
        if (stock == null) throw new ArgumentNullException(nameof(stock));
        if (catalog == null) throw new ArgumentNullException(nameof(catalog));
        IWorldItemStackRuntime runtime = DispatchProxy.Create<
            IWorldItemStackRuntime,
            VisitorReceptionCheckoutRestoreItemQueries>();
        VisitorReceptionCheckoutRestoreItemQueries queries =
            (VisitorReceptionCheckoutRestoreItemQueries)(object)runtime;
        queries.stock = stock;
        queries.catalog = catalog;
        return runtime;
    }

    protected override object Invoke(MethodInfo targetMethod, object[] arguments)
    {
        if (targetMethod == null) throw new ArgumentNullException(nameof(targetMethod));
        if (targetMethod.GetParameters().Length == 0)
        {
            if (targetMethod.Name == "get_" + nameof(IWorldItemStackRuntime.CatalogProvider)
                && targetMethod.ReturnType == typeof(IDungeonItemCatalogProvider))
                return catalog ?? throw new InvalidOperationException("Restore item queries are not initialized.");
            if (targetMethod.Name == nameof(IWorldItemStackRuntime.GetAllStacks)
                && targetMethod.ReturnType == typeof(IReadOnlyList<WorldItemStackSnapshot>))
                return (stock ?? throw new InvalidOperationException("Restore item queries are not initialized."))
                    .GetAllStacks();
        }

        throw VisitorReceptionCheckoutCausalTestPorts.Unsupported(
            nameof(IWorldItemStackRuntime) + "." + targetMethod.Name);
    }
}
#endif
