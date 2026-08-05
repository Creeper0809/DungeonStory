using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

internal enum CharacterMedicalPublicationFaultPoint
{
    PreviousRegistrationsRemoved,
    CandidateRegistrationsAdopted,
    RuntimeMapsAdopted,
    PatientPhasesAdopted,
    OrderViewAdopted
}

internal sealed class CharacterMedicalPublicationApplier
{
    private readonly Dictionary<string, CharacterMedicalDownedRegistration>
        liveDownedRegistrations;
    private readonly IDictionary<string, Transform> carriedPatientParents;
    private readonly IDictionary<string, string> treatmentFacilityReservations;
    private readonly Action resetOrdersView;
    private readonly Action<CharacterMedicalOrderViewSnapshot> restoreOrdersView;

    internal CharacterMedicalPublicationApplier(
        Dictionary<string, CharacterMedicalDownedRegistration>
            liveDownedRegistrations,
        IDictionary<string, Transform> carriedPatientParents,
        IDictionary<string, string> treatmentFacilityReservations,
        Action resetOrdersView,
        Action<CharacterMedicalOrderViewSnapshot> restoreOrdersView)
    {
        this.liveDownedRegistrations = liveDownedRegistrations
            ?? throw new ArgumentNullException(nameof(liveDownedRegistrations));
        this.carriedPatientParents = carriedPatientParents
            ?? throw new ArgumentNullException(nameof(carriedPatientParents));
        this.treatmentFacilityReservations = treatmentFacilityReservations
            ?? throw new ArgumentNullException(
                nameof(treatmentFacilityReservations));
        this.resetOrdersView = resetOrdersView
            ?? throw new ArgumentNullException(nameof(resetOrdersView));
        this.restoreOrdersView = restoreOrdersView
            ?? throw new ArgumentNullException(nameof(restoreOrdersView));
    }

    internal void Apply(CharacterMedicalPublication publication)
    {
        Apply(publication, _ => { });
    }

    internal void Apply(
        CharacterMedicalPublication publication,
        Action<CharacterMedicalPublicationFaultPoint> afterStep)
    {
        if (publication == null)
        {
            throw new ArgumentNullException(nameof(publication));
        }
        afterStep ??= _ => { };

        DiscardRegistrations(liveDownedRegistrations.Values);
        liveDownedRegistrations.Clear();
        afterStep(CharacterMedicalPublicationFaultPoint.PreviousRegistrationsRemoved);

        RegisterSource(
            publication.Candidate.DownedRegistrations,
            "Validated downed-patient projection");
        afterStep(CharacterMedicalPublicationFaultPoint.CandidateRegistrationsAdopted);

        carriedPatientParents.Clear();
        treatmentFacilityReservations.Clear();
        foreach (CharacterMedicalOrder order in publication.Candidate.State.Orders
                     .Where(order => order.IsActive
                         && !string.IsNullOrWhiteSpace(
                             order.treatmentFacilityId)))
        {
            treatmentFacilityReservations[order.treatmentFacilityId] =
                order.patientId;
        }
        afterStep(CharacterMedicalPublicationFaultPoint.RuntimeMapsAdopted);

        foreach (CharacterActor patient in publication.Candidate.DownedPatients)
        {
            patient?.SetLifecycleState(CharacterLifecycleState.Downed);
        }
        afterStep(CharacterMedicalPublicationFaultPoint.PatientPhasesAdopted);

        resetOrdersView();
        afterStep(CharacterMedicalPublicationFaultPoint.OrderViewAdopted);
    }

    internal void Rollback(CharacterMedicalPublication publication)
    {
        if (publication == null)
        {
            return;
        }

        DiscardRegistrations(liveDownedRegistrations.Values);
        liveDownedRegistrations.Clear();
        RegisterSource(
            publication.PreviousDownedRegistrations,
            "Previous downed-patient projection");

        ReplaceDictionary(
            carriedPatientParents,
            publication.PreviousCarriedPatientParents);
        ReplaceDictionary(
            treatmentFacilityReservations,
            publication.PreviousTreatmentFacilityReservations);

        foreach (CharacterMedicalPatientPhaseSnapshot patientPhase in
                 publication.PreviousPatientPhases)
        {
            patientPhase.Restore();
        }

        restoreOrdersView(publication.PreviousOrderView);
    }

    private void RegisterSource(
        IReadOnlyDictionary<string, CharacterMedicalDownedRegistration> source,
        string projectionLabel)
    {
        foreach (KeyValuePair<string, CharacterMedicalDownedRegistration> pair
                 in source)
        {
            CharacterMedicalDownedRegistration registration = pair.Value;
            if (!registration.Grid.RegisterOccupant(
                    registration.Occupant,
                    GridLayer.DownedCharacter,
                    new[] { registration.Position },
                    connectPositions: false))
            {
                throw new InvalidOperationException(
                    $"{projectionLabel} '{pair.Key}' could not be published.");
            }
            liveDownedRegistrations.Add(pair.Key, registration);
        }
    }

    private static void ReplaceDictionary<TValue>(
        IDictionary<string, TValue> destination,
        IReadOnlyDictionary<string, TValue> source)
    {
        destination.Clear();
        foreach (KeyValuePair<string, TValue> pair in source)
        {
            destination.Add(pair.Key, pair.Value);
        }
    }

    private static void DiscardRegistrations(
        IEnumerable<CharacterMedicalDownedRegistration> registrations)
    {
        foreach (CharacterMedicalDownedRegistration registration in
                 registrations
                 ?? Enumerable.Empty<CharacterMedicalDownedRegistration>())
        {
            registration?.Grid.RemoveOccupant(
                GridLayer.DownedCharacter,
                new[] { registration.Position },
                disconnectPositions: false);
        }
    }
}

internal sealed class CharacterMedicalRestoreServices
{
    internal CharacterMedicalRestoreServices(
        ICharacterBodyHealthQuery bodyHealthQuery,
        ICharacterAiWorldRegistry worldRegistry,
        IResourceEconomyContentCatalog resourceCatalog,
        DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        BodyHealthQuery = bodyHealthQuery
            ?? throw new ArgumentNullException(nameof(bodyHealthQuery));
        WorldRegistry = worldRegistry
            ?? throw new ArgumentNullException(nameof(worldRegistry));
        ResourceCatalog = resourceCatalog
            ?? throw new ArgumentNullException(nameof(resourceCatalog));
        AggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
    }

    internal ICharacterBodyHealthQuery BodyHealthQuery { get; }
    internal ICharacterAiWorldRegistry WorldRegistry { get; }
    internal IResourceEconomyContentCatalog ResourceCatalog { get; }
    internal DungeonRuntimeAggregateRootStore AggregateRootStore { get; }
}

internal sealed class CharacterMedicalProjectionContext
{
    internal CharacterMedicalProjectionContext(
        Dictionary<string, CharacterMedicalDownedRegistration>
            liveDownedRegistrations,
        IDictionary<string, Transform> carriedPatientParents,
        IDictionary<string, string> treatmentFacilityReservations,
        Action resetOrdersView,
        Func<CharacterMedicalOrderViewSnapshot> captureOrdersView,
        Action<CharacterMedicalOrderViewSnapshot> restoreOrdersView)
    {
        LiveDownedRegistrations = liveDownedRegistrations
            ?? throw new ArgumentNullException(nameof(liveDownedRegistrations));
        CarriedPatientParents = carriedPatientParents
            ?? throw new ArgumentNullException(nameof(carriedPatientParents));
        TreatmentFacilityReservations = treatmentFacilityReservations
            ?? throw new ArgumentNullException(
                nameof(treatmentFacilityReservations));
        ResetOrdersView = resetOrdersView
            ?? throw new ArgumentNullException(nameof(resetOrdersView));
        CaptureOrdersView = captureOrdersView
            ?? throw new ArgumentNullException(nameof(captureOrdersView));
        RestoreOrdersView = restoreOrdersView
            ?? throw new ArgumentNullException(nameof(restoreOrdersView));
    }

    internal Dictionary<string, CharacterMedicalDownedRegistration>
        LiveDownedRegistrations { get; }
    internal IDictionary<string, Transform> CarriedPatientParents { get; }
    internal IDictionary<string, string> TreatmentFacilityReservations { get; }
    internal Action ResetOrdersView { get; }
    internal Func<CharacterMedicalOrderViewSnapshot> CaptureOrdersView { get; }
    internal Action<CharacterMedicalOrderViewSnapshot> RestoreOrdersView { get; }
}

internal sealed class CharacterMedicalRestoreCoordinator
{
    private const string RestoreParticipantId = "350.world.medical";

    private readonly ICharacterBodyHealthQuery bodyHealthQuery;
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly IResourceEconomyContentCatalog resourceCatalog;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private readonly Dictionary<string, CharacterMedicalDownedRegistration>
        liveDownedRegistrations;
    private readonly IDictionary<string, Transform> carriedPatientParents;
    private readonly IDictionary<string, string> treatmentFacilityReservations;
    private readonly Action resetOrdersView;
    private readonly Func<CharacterMedicalOrderViewSnapshot> captureOrdersView;
    private readonly Action<CharacterMedicalOrderViewSnapshot> restoreOrdersView;
    private readonly CharacterMedicalPublicationApplier publicationApplier;
    private bool restoreTransactionActive;
    private CharacterMedicalRestoreCandidate restoreCandidate;
    private CharacterMedicalOrderViewSnapshot transactionOrderView;
    private CharacterMedicalPublication activePublication;

    internal CharacterMedicalRestoreCoordinator(
        CharacterMedicalRestoreServices services,
        CharacterMedicalProjectionContext projection)
    {
        services = services
            ?? throw new ArgumentNullException(nameof(services));
        projection = projection
            ?? throw new ArgumentNullException(nameof(projection));
        bodyHealthQuery = services.BodyHealthQuery;
        worldRegistry = services.WorldRegistry;
        resourceCatalog = services.ResourceCatalog;
        aggregateRootStore = services.AggregateRootStore;
        liveDownedRegistrations = projection.LiveDownedRegistrations;
        carriedPatientParents = projection.CarriedPatientParents;
        treatmentFacilityReservations =
            projection.TreatmentFacilityReservations;
        resetOrdersView = projection.ResetOrdersView;
        captureOrdersView = projection.CaptureOrdersView;
        restoreOrdersView = projection.RestoreOrdersView;
        publicationApplier = new CharacterMedicalPublicationApplier(
            this.liveDownedRegistrations,
            this.carriedPatientParents,
            this.treatmentFacilityReservations,
            this.resetOrdersView,
            this.restoreOrdersView);
    }

    internal string ParticipantId => RestoreParticipantId;

    internal CharacterMedicalRestoreCandidate PrepareRestore(
        DungeonCharacterMedicalSaveData saveData)
    {
        DungeonGameRestoreReport report = new();
        CharacterMedicalSaveValidation.Validate(
            saveData,
            report,
            resourceCatalog);
        if (!report.Success)
        {
            throw new InvalidOperationException(
                "Character-medical restore candidate is invalid: "
                + string.Join(" | ", report.Errors));
        }
        if (!worldRegistry.TryGetGrid(out Grid candidateGrid)
            || candidateGrid == null)
        {
            throw new InvalidOperationException(
                "Character medical restore requires a facility Grid candidate.");
        }

        CharacterMedicalAggregateState restored =
            CharacterMedicalSaveValidation.CreateState(saveData);
        CharacterMedicalRestoreCandidate candidate = new(restored);
        ValidateWorldReferencesAndPrepareProjection(
            restored,
            candidateGrid,
            candidate,
            report);
        if (!report.Success)
        {
            throw new InvalidOperationException(
                "Character-medical world references are invalid: "
                + string.Join(" | ", report.Errors));
        }

        return candidate;
    }

    internal void PublishRestore(CharacterMedicalRestoreCandidate candidate)
    {
        if (candidate == null)
        {
            throw new ArgumentNullException(nameof(candidate));
        }
        if (!restoreTransactionActive || !aggregateRootStore.IsRestoreStaging)
        {
            throw new InvalidOperationException(
                "Character medical restore requires the V18 save registry transaction boundary.");
        }
        if (restoreCandidate != null)
        {
            throw new InvalidOperationException(
                "A character medical restore candidate was staged more than once.");
        }

        aggregateRootStore.Replace(candidate.State);
        restoreCandidate = candidate;
    }

    internal void BeginRestoreCandidate()
    {
        if (restoreTransactionActive || activePublication != null)
        {
            throw new InvalidOperationException(
                "A character medical restore candidate is already active.");
        }

        restoreTransactionActive = true;
        restoreCandidate = null;
        transactionOrderView = captureOrdersView();
    }

    internal void PublishRestoreCandidate()
    {
        if (!restoreTransactionActive || restoreCandidate == null)
        {
            throw new InvalidOperationException(
                "No character medical restore candidate is ready to publish.");
        }

        CharacterMedicalPublication publication = new(
            restoreCandidate,
            liveDownedRegistrations,
            carriedPatientParents,
            treatmentFacilityReservations,
            transactionOrderView ?? captureOrdersView());
        activePublication = publication;
        try
        {
            publicationApplier.Apply(publication);
        }
        catch
        {
            try
            {
                publicationApplier.Rollback(publication);
            }
            finally
            {
                ResetTransactionState();
            }
            throw;
        }
    }

    internal void RollbackPublishedRestoreCandidate()
    {
        if (!restoreTransactionActive || activePublication == null)
        {
            DiscardRestoreCandidate();
            return;
        }

        CharacterMedicalPublication publication = activePublication;
        try
        {
            publicationApplier.Rollback(publication);
        }
        finally
        {
            ResetTransactionState();
        }
    }

    internal void CompleteRestoreCandidate()
    {
        ResetTransactionState();
    }

    internal void DiscardRestoreCandidate()
    {
        if (activePublication != null)
        {
            RollbackPublishedRestoreCandidate();
            return;
        }

        if (transactionOrderView != null)
        {
            restoreOrdersView(transactionOrderView);
        }
        ResetTransactionState();
    }

    private void ResetTransactionState()
    {
        activePublication = null;
        transactionOrderView = null;
        restoreCandidate = null;
        restoreTransactionActive = false;
    }

    private void ValidateWorldReferencesAndPrepareProjection(
        CharacterMedicalAggregateState restored,
        Grid candidateGrid,
        CharacterMedicalRestoreCandidate candidate,
        DungeonGameRestoreReport report)
    {
        Dictionary<string, BuildableObject> facilities = worldRegistry.Buildings
            .Where(building => building != null
                && !building.isDestroy
                && building.PersistentInstanceId.IsValid)
            .ToDictionary(
                building => building.PersistentInstanceId.Value,
                StringComparer.Ordinal);
        HashSet<string> reservedFacilities = new(StringComparer.Ordinal);
        foreach (CharacterMedicalOrder order in restored.Orders)
        {
            if (!order.IsActive)
            {
                continue;
            }

            CharacterActor patient = FindCharacter(order.patientId);
            if (patient == null || patient.IsDead)
            {
                report.AddError(
                    $"Active medical order '{order.orderId}' references missing patient '{order.patientId}'.");
                continue;
            }
            if (!string.IsNullOrWhiteSpace(order.rescuerId))
            {
                CharacterActor rescuer = FindCharacter(order.rescuerId);
                if (rescuer == null || rescuer.IsDead)
                {
                    report.AddError(
                        $"Active medical order '{order.orderId}' references missing rescuer '{order.rescuerId}'.");
                }
            }
            if (!string.IsNullOrWhiteSpace(order.treatmentFacilityId)
                && (!facilities.ContainsKey(order.treatmentFacilityId)
                    || !reservedFacilities.Add(
                        order.treatmentFacilityId)))
            {
                report.AddError(
                    $"Active medical order '{order.orderId}' has a missing or multiply reserved treatment facility '{order.treatmentFacilityId}'.");
            }

            CharacterBodyHealthSnapshot health =
                bodyHealthQuery.GetSnapshot(patient);
            if (!health.Downed)
            {
                if (order.carried)
                {
                    report.AddError(
                        $"Medical order '{order.orderId}' carries a patient who is not downed.");
                }
                continue;
            }

            order.carried = false;
            order.rescuerId = string.Empty;
            order.state = order.stabilized
                ? CharacterMedicalOrderState.AwaitingRescue
                : CharacterMedicalOrderState.AwaitingStabilization;
            if (!TryPrepareDownedRegistration(
                    candidateGrid,
                    patient,
                    candidate.DownedRegistrations,
                    out DomainFailure failure))
            {
                report.AddError(
                    $"Medical order '{order.orderId}' could not project its downed patient: {failure.Code}");
                continue;
            }


            candidate.DownedPatients.Add(patient);
        }
    }

    private CharacterActor FindCharacter(string persistentId)
    {
        if (string.IsNullOrWhiteSpace(persistentId))
        {
            return null;
        }

        return worldRegistry.Characters.FirstOrDefault(actor =>
            actor != null
            && string.Equals(
                GetPersistentId(actor),
                persistentId,
                StringComparison.Ordinal));
    }

    private static bool TryPrepareDownedRegistration(
        Grid grid,
        CharacterActor actor,
        IDictionary<string, CharacterMedicalDownedRegistration> destination,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        string id = GetPersistentId(actor);
        Vector2Int position = grid.GetXY(actor.transform.position);
        if (!grid.IsValidGridPos(position)
            || destination.ContainsKey(id)
            || destination.Values.Any(registration =>
                registration.Position == position))
        {
            failure = new DomainFailure(
                FailureCode.CharacterMedicalProjectionPositionInvalid,
                id,
                position.x.ToString(CultureInfo.InvariantCulture),
                position.y.ToString(CultureInfo.InvariantCulture));
            return false;
        }

        DownedCharacterGridOccupant occupant =
            new DownedCharacterGridOccupant(actor);
        GridCell cell = grid.GetGridCell(position);
        IGridOccupant existing = cell?.GetOccupant(GridLayer.DownedCharacter);
        bool occupiedByExistingDowned =
            existing is DownedCharacterGridOccupant;
        if (cell == null
            || !occupiedByExistingDowned
                && !cell.CanOccupy(GridLayer.DownedCharacter))
        {
            failure = new DomainFailure(
                FailureCode.CharacterMedicalProjectionGridOccupied,
                position.x.ToString(CultureInfo.InvariantCulture),
                position.y.ToString(CultureInfo.InvariantCulture));
            return false;
        }

        destination.Add(
            id,
            new CharacterMedicalDownedRegistration(
                grid,
                position,
                occupant));
        return true;
    }

    private static string GetPersistentId(CharacterActor actor)
    {
        return actor != null
            ? CharacterPersistentIdentity.Require(actor).Value
            : string.Empty;
    }
}

#if UNITY_EDITOR
public static class CharacterMedicalRestoreFaultScenarios
{
    public static bool Run()
    {
        Grid grid = new Grid(2, 1);
        GameObject previousPatientObject = CreatePatient("Previous Medical Patient");
        GameObject candidatePatientObject = CreatePatient("Candidate Medical Patient");
        GameObject previousParentObject = new GameObject("Previous Carry Parent");
        try
        {
            CharacterActor previousPatient =
                previousPatientObject.GetComponent<CharacterActor>();
            CharacterActor candidatePatient =
                candidatePatientObject.GetComponent<CharacterActor>();
            Vector2Int previousPosition = new Vector2Int(0, 0);
            Vector2Int candidatePosition = new Vector2Int(1, 0);
            DownedCharacterGridOccupant previousOccupant =
                new DownedCharacterGridOccupant(previousPatient);
            DownedCharacterGridOccupant candidateOccupant =
                new DownedCharacterGridOccupant(candidatePatient);
            if (!grid.RegisterOccupant(
                    previousOccupant,
                    GridLayer.DownedCharacter,
                    new[] { previousPosition },
                    connectPositions: false))
            {
                return false;
            }

            CharacterMedicalDownedRegistration previousRegistration = new(
                grid,
                previousPosition,
                previousOccupant);
            CharacterMedicalDownedRegistration candidateRegistration = new(
                grid,
                candidatePosition,
                candidateOccupant);
            Dictionary<string, CharacterMedicalDownedRegistration> live =
                new(StringComparer.Ordinal)
                {
                    ["previous"] = previousRegistration
                };
            Dictionary<string, Transform> carried = new(StringComparer.Ordinal)
            {
                ["previous"] = previousParentObject.transform
            };
            Dictionary<string, string> reservations = new(StringComparer.Ordinal)
            {
                ["facility:previous"] = "previous"
            };
            List<CharacterMedicalOrder> previousOrders = new()
            {
                new CharacterMedicalOrder
                {
                    orderId = "medical:previous",
                    patientId = "previous",
                    state = CharacterMedicalOrderState.AwaitingRescue
                }
            };
            IReadOnlyList<CharacterMedicalOrder> previousView =
                previousOrders.AsReadOnly();
            List<CharacterMedicalOrder> currentSource = previousOrders;
            IReadOnlyList<CharacterMedicalOrder> currentView = previousView;

            CharacterMedicalAggregateState candidateState = new();
            candidateState.Orders.Add(new CharacterMedicalOrder
            {
                orderId = "medical:candidate",
                patientId = "candidate",
                treatmentFacilityId = "facility:candidate",
                state = CharacterMedicalOrderState.AwaitingRescue
            });
            CharacterMedicalRestoreCandidate candidate = new(candidateState);
            candidate.DownedRegistrations.Add("candidate", candidateRegistration);
            candidate.DownedPatients.Add(candidatePatient);

            candidatePatient.state = CharacterDecisionState.EXECUTE;
            candidatePatient.SetLifecycleState(CharacterLifecycleState.OnExpedition);
            candidatePatient.SetAiPaused(true);
            AIAction previousBestAction = new AIAction();
            candidatePatient.Brain.bestAction = previousBestAction;
            candidatePatient.Brain.isExecuted = true;
            candidatePatient.Brain.isBestActionEnd = false;
            candidatePatient.Brain.SetActionPhase(
                "previous-phase",
                detail: "previous-detail");

            CharacterMedicalPublication publication = new(
                candidate,
                live,
                carried,
                reservations,
                new CharacterMedicalOrderViewSnapshot(
                    previousView,
                    previousOrders));
            CharacterMedicalPublicationApplier applier = new(
                live,
                carried,
                reservations,
                () =>
                {
                    currentView = null;
                    currentSource = null;
                },
                snapshot =>
                {
                    currentView = snapshot.View;
                    currentSource = snapshot.Source;
                });

            foreach (CharacterMedicalPublicationFaultPoint faultPoint in
                     Enum.GetValues(typeof(CharacterMedicalPublicationFaultPoint)))
            {
                bool faultObserved = false;
                try
                {
                    applier.Apply(
                        publication,
                        reached =>
                        {
                            if (reached == faultPoint)
                            {
                                throw new CharacterMedicalInjectedFaultException();
                            }
                        });
                }
                catch (CharacterMedicalInjectedFaultException)
                {
                    faultObserved = true;
                }

                applier.Rollback(publication);
                if (!faultObserved
                    || live.Count != 1
                    || !ReferenceEquals(live["previous"], previousRegistration)
                    || !ReferenceEquals(
                        grid.GetGridCell(previousPosition)
                            .GetOccupant(GridLayer.DownedCharacter),
                        previousOccupant)
                    || grid.GetGridCell(candidatePosition)
                        .GetOccupant(GridLayer.DownedCharacter) != null
                    || carried.Count != 1
                    || !ReferenceEquals(
                        carried["previous"],
                        previousParentObject.transform)
                    || reservations.Count != 1
                    || reservations["facility:previous"] != "previous"
                    || !ReferenceEquals(currentView, previousView)
                    || !ReferenceEquals(currentSource, previousOrders)
                    || candidatePatient.CurrentLifecycleState
                        != CharacterLifecycleState.OnExpedition
                    || !candidatePatient.IsAiPaused()
                    || candidatePatient.State != CharacterDecisionState.EXECUTE
                    || !ReferenceEquals(
                        candidatePatient.Brain.bestAction,
                        previousBestAction)
                    || !candidatePatient.Brain.isExecuted
                    || candidatePatient.Brain.isBestActionEnd
                    || candidatePatient.Brain.CurrentActionPhase
                        != "previous-phase"
                    || candidatePatient.Brain.CurrentActionPhaseDetail
                        != "previous-detail")
                {
                    return false;
                }
            }

            return true;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(previousPatientObject);
            UnityEngine.Object.DestroyImmediate(candidatePatientObject);
            UnityEngine.Object.DestroyImmediate(previousParentObject);
        }
    }

    private static GameObject CreatePatient(string name)
    {
        GameObject patientObject = new GameObject(name);
        patientObject.SetActive(false);
        CharacterActor actor = patientObject.AddComponent<CharacterActor>();
        patientObject.AddComponent<AIBrain>();
        actor.EnsureRuntimeState();
        return patientObject;
    }

    private sealed class CharacterMedicalInjectedFaultException : Exception
    {
    }
}
#endif
