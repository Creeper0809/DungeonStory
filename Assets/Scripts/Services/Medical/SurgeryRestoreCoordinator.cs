using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public sealed class SurgeryRestoreCoordinator :
    IDungeonRestoreTransactionParticipant
{
    private const string RestoreParticipantId = "525.world.surgery";

    private readonly SurgeryContentServices content;
    private readonly SurgeryWorldServices world;
    private readonly SurgeryResourceServices resources;
    private readonly SurgeryAggregateStateStore stateStore;
    private readonly DungeonRuntimeAggregateRootStore rootStore;
    private readonly IGridSystemProvider gridProvider;
    private readonly SurgeryRestoreProjection projection;
    private List<SurgeryOrder> previousOrders = new();
    private SurgeryRestorePublication activePublication;
    private bool restoreTransactionActive;
    private bool restoreCandidatePrepared;

    public SurgeryRestoreCoordinator(
        SurgeryContentServices content,
        SurgeryWorldServices world,
        SurgeryResourceServices resources,
        SurgeryAggregateStateStore stateStore,
        DungeonRuntimeAggregateRootStore rootStore,
        IGridSystemProvider gridProvider)
    {
        this.content = content ?? throw new ArgumentNullException(nameof(content));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.resources = resources ?? throw new ArgumentNullException(nameof(resources));
        this.stateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
        this.rootStore = rootStore ?? throw new ArgumentNullException(nameof(rootStore));
        this.gridProvider = gridProvider
            ?? throw new ArgumentNullException(nameof(gridProvider));
        projection = new SurgeryRestoreProjection(this.world, this.stateStore);
    }

    public string ParticipantId => RestoreParticipantId;

    public SurgeryRestoreCandidate PrepareRestore(
        DungeonSurgerySaveData saveData)
    {
        DungeonGameRestoreReport report = new();
        SurgerySaveValidation.Validate(
            saveData,
            content.Procedures,
            content.AnatomyProfiles,
            report);
        if (!report.Success)
        {
            throw new InvalidOperationException(
                "Surgery restore candidate is invalid: "
                + string.Join(" | ", report.Errors));
        }

        SurgeryAggregateState state = SurgerySaveValidation.CreateState(saveData);
        ValidateWorldReferences(state, report);
        if (!report.Success)
        {
            throw new InvalidOperationException(
                "Surgery world references are invalid: "
                + string.Join(" | ", report.Errors));
        }

        return new SurgeryRestoreCandidate(state);
    }

    public void PublishRestore(SurgeryRestoreCandidate candidate)
    {
        if (candidate == null)
        {
            throw new ArgumentNullException(nameof(candidate));
        }
        if (!restoreTransactionActive || !rootStore.IsRestoreStaging)
        {
            throw new InvalidOperationException(
                "Surgery restore requires the V18 save registry transaction boundary.");
        }
        if (restoreCandidatePrepared)
        {
            throw new InvalidOperationException(
                "A surgery restore candidate was staged more than once.");
        }

        stateStore.Replace(candidate.State);
        restoreCandidatePrepared = true;
    }

    public void BeginRestoreCandidate()
    {
        if (restoreTransactionActive)
        {
            throw new InvalidOperationException(
                "A surgery restore candidate is already active.");
        }

        // Begin runs before aggregate staging. Keep the actual live order
        // objects so active transports can remain attached to them until the
        // whole restore transaction completes.
        previousOrders = stateStore.State.Orders.ToList();
        activePublication = null;
        restoreTransactionActive = true;
        restoreCandidatePrepared = false;
    }

    public void PublishRestoreCandidate()
    {
        if (!restoreTransactionActive || !restoreCandidatePrepared)
        {
            throw new InvalidOperationException(
                "No surgery restore candidate is ready to publish.");
        }

        SurgeryRestorePublication publication =
            projection.PreparePublication(previousOrders);
        activePublication = publication;
        try
        {
            projection.Publish(publication);
            restoreCandidatePrepared = false;
        }
        catch
        {
            try
            {
                projection.Rollback(publication);
            }
            finally
            {
                ResetTransactionState();
            }

            throw;
        }
    }

    public void RollbackPublishedRestoreCandidate()
    {
        if (activePublication == null)
        {
            ResetTransactionState();
            return;
        }

        try
        {
            projection.Rollback(activePublication);
        }
        finally
        {
            ResetTransactionState();
        }
    }

    public void CompleteRestoreCandidate()
    {
        try
        {
            if (activePublication != null)
            {
                projection.Complete(activePublication);
            }
        }
        catch (Exception exception)
        {
            // Completion is the irreversible retirement phase and the save
            // protocol requires it to be non-failing. Individual transport
            // commands are already guarded by the projection; retain this
            // final boundary so a Unity callback cannot poison the committed
            // aggregate root.
            Debug.LogException(exception);
        }
        finally
        {
            ResetTransactionState();
        }
    }

    public void DiscardRestoreCandidate()
    {
        if (activePublication != null)
        {
            try
            {
                projection.Rollback(activePublication);
            }
            finally
            {
                ResetTransactionState();
            }
            return;
        }

        ResetTransactionState();
    }

    private void ResetTransactionState()
    {
        activePublication = null;
        previousOrders = new List<SurgeryOrder>();
        restoreCandidatePrepared = false;
        restoreTransactionActive = false;
    }

    private void ValidateWorldReferences(
        SurgeryAggregateState candidate,
        DungeonGameRestoreReport report)
    {
        if (!gridProvider.TryGetGrid(out Grid grid) || grid == null)
        {
            report.AddError("Surgery restore requires an active grid.");
            return;
        }

        Dictionary<string, CharacterActor> characters = world.Characters.Characters
            .Where(actor => actor != null
                && CharacterPersistentIdentity.TryGet(actor, out _))
            .GroupBy(
                actor => CharacterPersistentIdentity.Require(actor).Value,
                StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.Ordinal);
        Dictionary<string, WildlifeActor> wildlife = world.Wildlife.Wildlife
            .Where(actor => actor != null
                && !string.IsNullOrWhiteSpace(actor.WildlifeId))
            .GroupBy(actor => actor.WildlifeId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.Ordinal);
        Dictionary<string, BuildableObject> buildings = world.Buildings.Buildings
            .Where(building => building != null
                && !building.isDestroy
                && building.PersistentInstanceId.IsValid)
            .Select(building => new
            {
                Id = content.Facilities.GetFacilityId(building),
                Building = building
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .GroupBy(item => item.Id, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.First().Building,
                StringComparer.Ordinal);
        Dictionary<string, WorldItemStackSnapshot> stacks = resources.Items
            .GetAllStacks()
            .Where(stack => stack != null
                && !string.IsNullOrWhiteSpace(stack.StackId))
            .GroupBy(stack => stack.StackId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.Ordinal);

        ValidateOrders(
            candidate,
            grid,
            characters,
            wildlife,
            buildings,
            stacks,
            report);
        ValidateParts(candidate, characters, wildlife, buildings, stacks, report);
        ValidateStorageAndCorpses(candidate, buildings, stacks, report);
        ValidatePolicies(candidate, characters, wildlife, report);
        ValidateWildlifeAnatomy(candidate, wildlife, report);
    }

    private void ValidateOrders(
        SurgeryAggregateState candidate,
        Grid grid,
        IReadOnlyDictionary<string, CharacterActor> characters,
        IReadOnlyDictionary<string, WildlifeActor> wildlife,
        IReadOnlyDictionary<string, BuildableObject> buildings,
        IReadOnlyDictionary<string, WorldItemStackSnapshot> stacks,
        DungeonGameRestoreReport report)
    {
        HashSet<string> activeSubjects = new(StringComparer.Ordinal);
        foreach (SurgeryOrder order in candidate.Orders.Where(
                     order => order != null && order.IsActive))
        {
            content.Procedures.TryGet(
                order.procedureId,
                out SurgicalProcedureSO procedure);
            if (!buildings.TryGetValue(order.facilityId, out BuildableObject facility))
            {
                report.AddError(
                    $"Active surgery order '{order.orderId}' references missing facility '{order.facilityId}'.");
            }
            else if (procedure != null)
            {
                SurgicalFacilitySnapshot snapshot = content.Facilities.Evaluate(
                    facility,
                    procedure.RequiredFacilityTags);
                if (!snapshot.IsAvailable)
                {
                    report.AddError(
                        $"Active surgery order '{order.orderId}' facility is unavailable: {snapshot.BlockFailure.Code}");
                }
            }

            if (!activeSubjects.Add(order.subject.subjectId))
            {
                report.AddError(
                    $"Surgery subject '{order.subject.subjectId}' has multiple active orders.");
            }
            ValidateSubjectReference(
                order,
                procedure,
                characters,
                wildlife,
                stacks,
                report);
            ValidateDoctorReference(order, characters, report);
            foreach (SurgicalMaterialRequirement material in order.materials)
            {
                if (!resources.Items.CatalogProvider.TryGetDefinition(
                        material.itemId,
                        out _))
                {
                    report.AddError(
                        $"Surgery order '{order.orderId}' references unknown material '{material.itemId}'.");
                }
            }
            if (!grid.IsValidGridPos(
                    new Vector2Int(order.admissionX, order.admissionY))
                || !grid.IsValidGridPos(
                    new Vector2Int(order.patientOriginX, order.patientOriginY)))
            {
                report.AddError(
                    $"Surgery order '{order.orderId}' has an invalid saved patient position.");
            }
            if ((order.materialsRequested || order.materialsConsumed)
                && string.IsNullOrWhiteSpace(order.materialDestinationId))
            {
                report.AddError(
                    $"Surgery order '{order.orderId}' has material state without destination.");
            }
        }
    }

    private static void ValidateDoctorReference(
        SurgeryOrder order,
        IReadOnlyDictionary<string, CharacterActor> characters,
        DungeonGameRestoreReport report)
    {
        foreach (string doctorId in new[]
                 {
                     order.preferredDoctorId,
                     order.doctorId
                 }.Where(id => !string.IsNullOrWhiteSpace(id)))
        {
            if (!characters.TryGetValue(doctorId, out CharacterActor doctor)
                || doctor == null
                || doctor.IsDead)
            {
                report.AddError(
                    $"Surgery order '{order.orderId}' references unavailable doctor '{doctorId}'.");
            }
        }
    }

    private static void ValidateSubjectReference(
        SurgeryOrder order,
        SurgicalProcedureSO procedure,
        IReadOnlyDictionary<string, CharacterActor> characters,
        IReadOnlyDictionary<string, WildlifeActor> wildlife,
        IReadOnlyDictionary<string, WorldItemStackSnapshot> stacks,
        DungeonGameRestoreReport report)
    {
        bool available = order.subject.kind switch
        {
            SurgicalSubjectKind.Character =>
                characters.TryGetValue(order.subject.subjectId, out CharacterActor character)
                && character != null
                && !character.IsDead,
            SurgicalSubjectKind.Wildlife =>
                wildlife.TryGetValue(order.subject.subjectId, out WildlifeActor animal)
                && animal != null
                && animal.IsAlive,
            SurgicalSubjectKind.HumanoidCorpse or SurgicalSubjectKind.WildlifeCorpse =>
                stacks.ContainsKey(order.subject.subjectId),
            _ => false
        };
        if (!available)
        {
            report.AddError(
                $"Active surgery order '{order.orderId}' references unavailable subject '{order.subject.subjectId}'.");
        }
        if (procedure == null)
        {
            return;
        }
        bool allowed = order.subject.kind switch
        {
            SurgicalSubjectKind.Character => procedure.AllowsLivingSubject,
            SurgicalSubjectKind.Wildlife =>
                procedure.AllowsLivingSubject && procedure.AllowsWildlife,
            SurgicalSubjectKind.HumanoidCorpse => procedure.AllowsCorpseSubject,
            SurgicalSubjectKind.WildlifeCorpse =>
                procedure.AllowsCorpseSubject && procedure.AllowsWildlife,
            _ => false
        };
        if (!allowed)
        {
            report.AddError(
                $"Surgery order '{order.orderId}' subject kind is not allowed by procedure '{procedure.ProcedureId}'.");
        }
    }

    private static void ValidateParts(
        SurgeryAggregateState candidate,
        IReadOnlyDictionary<string, CharacterActor> characters,
        IReadOnlyDictionary<string, WildlifeActor> wildlife,
        IReadOnlyDictionary<string, BuildableObject> buildings,
        IReadOnlyDictionary<string, WorldItemStackSnapshot> stacks,
        DungeonGameRestoreReport report)
    {
        foreach (SurgicalPartInstance part in candidate.Parts)
        {
            if (!string.IsNullOrEmpty(part.worldStackId)
                && !stacks.ContainsKey(part.worldStackId))
            {
                report.AddError(
                    $"Surgical part '{part.partInstanceId}' references missing stack '{part.worldStackId}'.");
            }
            if (!string.IsNullOrEmpty(part.storedFacilityId)
                && (!buildings.TryGetValue(
                        part.storedFacilityId,
                        out BuildableObject storage)
                    || storage.BuildingData?.GetAbility<BuildingOrganStorageAbility>() == null))
            {
                report.AddError(
                    $"Surgical part '{part.partInstanceId}' references invalid storage '{part.storedFacilityId}'.");
            }
            if (part.installed
                && !characters.ContainsKey(part.installedSubjectId)
                && !wildlife.ContainsKey(part.installedSubjectId))
            {
                report.AddError(
                    $"Installed surgical part '{part.partInstanceId}' references missing subject '{part.installedSubjectId}'.");
            }
        }
    }

    private static void ValidateStorageAndCorpses(
        SurgeryAggregateState candidate,
        IReadOnlyDictionary<string, BuildableObject> buildings,
        IReadOnlyDictionary<string, WorldItemStackSnapshot> stacks,
        DungeonGameRestoreReport report)
    {
        foreach (SurgicalOrganStorageState storage in candidate.OrganStorage.Values)
        {
            if (!buildings.TryGetValue(storage.facilityId, out BuildableObject building)
                || building.BuildingData?.GetAbility<BuildingOrganStorageAbility>() == null)
            {
                report.AddError(
                    $"Organ storage state references invalid facility '{storage.facilityId}'.");
            }
        }
        foreach (string stackId in candidate.CorpseFreshness.Keys)
        {
            if (!stacks.ContainsKey(stackId))
            {
                report.AddError(
                    $"Corpse freshness references missing stack '{stackId}'.");
            }
        }
    }

    private static void ValidatePolicies(
        SurgeryAggregateState candidate,
        IReadOnlyDictionary<string, CharacterActor> characters,
        IReadOnlyDictionary<string, WildlifeActor> wildlife,
        DungeonGameRestoreReport report)
    {
        foreach (string subjectId in candidate.Policies.Keys)
        {
            if (!characters.ContainsKey(subjectId) && !wildlife.ContainsKey(subjectId))
            {
                report.AddError(
                    $"Surgery policy references missing subject '{subjectId}'.");
            }
        }
    }

    private void ValidateWildlifeAnatomy(
        SurgeryAggregateState candidate,
        IReadOnlyDictionary<string, WildlifeActor> wildlife,
        DungeonGameRestoreReport report)
    {
        foreach (WildlifeAnatomyState state in candidate.WildlifeAnatomy.Values)
        {
            if (!wildlife.TryGetValue(state.wildlifeId, out WildlifeActor actor)
                || actor == null
                || !actor.IsAlive)
            {
                report.AddError(
                    $"Wildlife anatomy references unavailable wildlife '{state.wildlifeId}'.");
                continue;
            }
            AnatomyProfileDefinition expected =
                content.AnatomyProfiles.GetForSpecies(actor.SpeciesId);
            if (!string.Equals(
                    expected.ProfileId,
                    state.profileId,
                    StringComparison.Ordinal))
            {
                report.AddError(
                    $"Wildlife anatomy '{state.wildlifeId}' profile '{state.profileId}' does not match species '{actor.SpeciesId}'.");
            }
        }
    }
}

internal sealed class SurgeryRestoreProjection
{
    private readonly ICharacterWorldQuery characters;
    private readonly ISurgicalPatientTransportRuntime patientTransport;
    private readonly SurgeryAggregateStateStore stateStore;

    internal SurgeryRestoreProjection(
        SurgeryWorldServices world,
        SurgeryAggregateStateStore stateStore)
        : this(
            (world ?? throw new ArgumentNullException(nameof(world))).Characters,
            world.PatientTransport,
            stateStore)
    {
    }

    internal SurgeryRestoreProjection(
        ICharacterWorldQuery characters,
        ISurgicalPatientTransportRuntime patientTransport,
        SurgeryAggregateStateStore stateStore)
    {
        this.characters = characters
            ?? throw new ArgumentNullException(nameof(characters));
        this.patientTransport = patientTransport
            ?? throw new ArgumentNullException(nameof(patientTransport));
        this.stateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
    }

    internal SurgeryRestorePublication PreparePublication(
        IReadOnlyList<SurgeryOrder> previousOrders)
    {
        List<SurgeryOrder> candidateOrders = stateStore.State.Orders.ToList();
        Dictionary<CharacterActor, SurgeryPatientProjectionSnapshot> patients =
            new();
        foreach (string patientId in (previousOrders
                     ?? Array.Empty<SurgeryOrder>())
                 .Concat(candidateOrders)
                 .Where(order => order?.IsActive == true
                     && order.subject?.kind == SurgicalSubjectKind.Character)
                 .Select(order => order.subject.subjectId)
                 .Where(id => !string.IsNullOrWhiteSpace(id))
                 .Distinct(StringComparer.Ordinal))
        {
            CharacterActor patient = SurgicalSubjectResolver.FindCharacter(
                characters,
                patientId);
            if (patient != null && !patients.ContainsKey(patient))
            {
                patients.Add(
                    patient,
                    new SurgeryPatientProjectionSnapshot(patient));
            }
        }

        return new SurgeryRestorePublication(
            previousOrders ?? Array.Empty<SurgeryOrder>(),
            candidateOrders,
            patients.Values.ToArray());
    }

    internal void Publish(SurgeryRestorePublication publication)
    {
        if (publication == null)
        {
            throw new ArgumentNullException(nameof(publication));
        }

        // The aggregate candidate already owns the new orders. Keep every
        // live-world projection untouched until completion: even unpausing a
        // character triggers an AI replan that can release reservations and
        // therefore cannot be restored exactly after a later participant
        // fails.
    }

    internal void Rollback(SurgeryRestorePublication publication)
    {
        if (publication == null)
        {
            return;
        }

        foreach (SurgeryPatientProjectionSnapshot patient in
                 publication.PatientSnapshots)
        {
            patient.Restore();
        }
    }

    internal void Complete(SurgeryRestorePublication publication)
    {
        if (publication == null)
        {
            return;
        }

        HashSet<string> newlyAdmittedCharacters = new(
            publication.CandidateOrders
                .Where(order => order?.IsActive == true
                    && order.patientAdmitted
                    && order.subject?.kind == SurgicalSubjectKind.Character)
                .Select(order => order.subject.subjectId),
            StringComparer.Ordinal);

        foreach (SurgeryOrder oldOrder in publication.PreviousOrders.Where(
                     order => order?.IsActive == true))
        {
            TryCompleteCommand(
                () => patientTransport.CancelTransport(oldOrder));
            if (oldOrder.subject?.kind != SurgicalSubjectKind.Character
                || newlyAdmittedCharacters.Contains(oldOrder.subject.subjectId))
            {
                continue;
            }
            CharacterActor oldPatient = SurgicalSubjectResolver.FindCharacter(
                characters,
                oldOrder.subject.subjectId);
            if (oldPatient == null)
            {
                continue;
            }
            if (!oldOrder.subjectAiWasPaused)
            {
                oldPatient.SetAiPaused(false);
            }
            oldPatient.Brain?.SetActionPhase(
                SurgeryStatusCode.ProcedurePaused.ToString(),
                null);
        }

        foreach (SurgeryOrder order in publication.CandidateOrders.Where(
                     order => order?.IsActive == true))
        {
            if (order.subject?.kind == SurgicalSubjectKind.Character
                && order.patientAdmitted)
            {
                CharacterActor patient = SurgicalSubjectResolver.FindCharacter(
                    characters,
                    order.subject.subjectId);
                patient?.SetAiPaused(true);
                patient?.Brain?.SetActionPhase(
                    order.statusData.code.ToString(),
                    null);
            }
            else if (order.subject?.kind == SurgicalSubjectKind.Wildlife
                && order.patientReturnRequested)
            {
                TryCompleteCommand(
                    () => patientTransport.RequestWildlifeReturn(order));
            }
        }
    }

    private static void TryCompleteCommand(Action command)
    {
        try
        {
            command();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }
}

internal sealed class SurgeryRestorePublication
{
    internal SurgeryRestorePublication(
        IReadOnlyList<SurgeryOrder> previousOrders,
        IReadOnlyList<SurgeryOrder> candidateOrders,
        IReadOnlyList<SurgeryPatientProjectionSnapshot> patientSnapshots)
    {
        PreviousOrders = previousOrders
            ?? throw new ArgumentNullException(nameof(previousOrders));
        CandidateOrders = candidateOrders
            ?? throw new ArgumentNullException(nameof(candidateOrders));
        PatientSnapshots = patientSnapshots
            ?? throw new ArgumentNullException(nameof(patientSnapshots));
    }

    internal IReadOnlyList<SurgeryOrder> PreviousOrders { get; }
    internal IReadOnlyList<SurgeryOrder> CandidateOrders { get; }
    internal IReadOnlyList<SurgeryPatientProjectionSnapshot> PatientSnapshots
    {
        get;
    }
}

internal sealed class SurgeryPatientProjectionSnapshot
{
    private readonly CharacterActor actor;
    private readonly CharacterLifecycleState lifecycleState;
    private readonly bool aiPaused;
    private readonly CharacterDecisionState decisionState;
    private readonly AIBrain brain;
    private readonly AIAction bestAction;
    private readonly bool isExecuted;
    private readonly bool isBestActionEnd;
    private readonly string actionPhase;
    private readonly string actionPhaseDetail;

    internal SurgeryPatientProjectionSnapshot(CharacterActor actor)
    {
        this.actor = actor ?? throw new ArgumentNullException(nameof(actor));
        lifecycleState = actor.CurrentLifecycleState;
        aiPaused = actor.IsAiPaused();
        decisionState = actor.State;
        brain = actor.Brain;
        bestAction = brain?.bestAction;
        isExecuted = brain?.isExecuted == true;
        isBestActionEnd = brain?.isBestActionEnd == true;
        actionPhase = brain?.CurrentActionPhase ?? string.Empty;
        actionPhaseDetail = brain?.CurrentActionPhaseDetail ?? string.Empty;
    }

    internal void Restore()
    {
        if (actor == null)
        {
            return;
        }

        if (actor.CurrentLifecycleState != lifecycleState)
        {
            actor.SetLifecycleState(lifecycleState);
        }
        if (actor.IsAiPaused() != aiPaused)
        {
            actor.SetAiPaused(aiPaused);
        }
        if (actor.State != decisionState)
        {
            actor.state = decisionState;
        }
        if (brain == null)
        {
            return;
        }

        if (!ReferenceEquals(brain.bestAction, bestAction))
        {
            brain.bestAction = bestAction;
        }
        if (brain.isExecuted != isExecuted)
        {
            brain.isExecuted = isExecuted;
        }
        if (brain.isBestActionEnd != isBestActionEnd)
        {
            brain.isBestActionEnd = isBestActionEnd;
        }
        if (!string.Equals(
                brain.CurrentActionPhase,
                actionPhase,
                StringComparison.Ordinal)
            || !string.Equals(
                brain.CurrentActionPhaseDetail,
                actionPhaseDetail,
                StringComparison.Ordinal))
        {
            brain.SetActionPhase(actionPhase, detail: actionPhaseDetail);
        }
    }
}

#if UNITY_EDITOR
public static class SurgeryRestoreFaultScenarios
{
    private enum LateFailureCheckpoint
    {
        ImmediatelyAfterSurgery = 0,
        AfterFirstLateParticipant = 1,
        BeforeAggregatePublication = 2
    }

    public static bool Run()
    {
        GameObject oldPatientObject = null;
        GameObject candidatePatientObject = null;
        DungeonRuntimeAggregateRootStore rootStore = new();
        try
        {
            CharacterActor oldPatient = CreatePatient(
                "surgery-old-patient",
                "character:surgery-old");
            oldPatientObject = oldPatient.gameObject;
            CharacterActor candidatePatient = CreatePatient(
                "surgery-candidate-patient",
                "character:surgery-candidate");
            candidatePatientObject = candidatePatient.gameObject;

            SurgeryOrder previousOrder = CharacterOrder(
                "surgery:previous",
                oldPatient.Identity.PersistentId,
                admitted: true,
                SurgeryStatusCode.ProcedureInProgress);
            previousOrder.patientTransportInProgress = true;
            previousOrder.patientTransporterId = "character:old-carrier";
            previousOrder.subjectAiWasPaused = false;

            SurgeryOrder candidateCharacterOrder = CharacterOrder(
                "surgery:candidate-character",
                candidatePatient.Identity.PersistentId,
                admitted: true,
                SurgeryStatusCode.SuturingInProgress);
            SurgeryOrder candidateWildlifeReturn = new()
            {
                orderId = "surgery:candidate-wildlife-return",
                procedureId = "procedure:test",
                subject = new SurgicalSubjectRef
                {
                    kind = SurgicalSubjectKind.Wildlife,
                    subjectId = "wildlife:surgery-candidate"
                },
                state = SurgeryOrderState.Recovering,
                patientAdmitted = true,
                patientReturnRequested = true,
                patientTransportInProgress = false,
                statusData = new SurgeryStatusData
                {
                    code = SurgeryStatusCode.WildlifePatientReturning
                }
            };

            SurgeryAggregateState previousState = new();
            previousState.Orders.Add(previousOrder);
            SurgeryAggregateStateStore stateStore = new(rootStore);
            stateStore.Replace(previousState);
            TestCharacterWorld characters = new(oldPatient, candidatePatient);
            TestPatientTransport transport = new(previousOrder);
            SurgeryRestoreProjection projection = new(
                characters,
                transport,
                stateStore);

            SetPriorCandidatePatientState(candidatePatient);
            AIAction expectedBestAction = candidatePatient.Brain.bestAction;
            int expectedBrainDebugVersion = candidatePatient.Brain.DebugVersion;

            foreach (LateFailureCheckpoint checkpoint in
                     Enum.GetValues(typeof(LateFailureCheckpoint)))
            {
                SurgeryAggregateState candidateState = new();
                candidateState.Orders.Add(candidateCharacterOrder);
                candidateState.Orders.Add(candidateWildlifeReturn);
                stateStore.Replace(candidateState);
                SurgeryRestorePublication publication =
                    projection.PreparePublication(
                        new[] { previousOrder });

                bool injectedFailureObserved = false;
                try
                {
                    projection.Publish(publication);
                    if (!transport.ContainsActive(previousOrder)
                        || transport.Cancelled.Count != 0
                        || transport.RequestedReturns.Count != 0
                        || candidatePatient.CurrentLifecycleState
                            != CharacterLifecycleState.OnExpedition
                        || candidatePatient.IsAiPaused()
                        || candidatePatient.State
                            != CharacterDecisionState.EXECUTE
                        || !ReferenceEquals(
                            candidatePatient.Brain.bestAction,
                            expectedBestAction)
                        || !candidatePatient.Brain.isExecuted
                        || candidatePatient.Brain.isBestActionEnd
                        || candidatePatient.Brain.CurrentActionPhase
                            != "prior-surgery-phase"
                        || candidatePatient.Brain.CurrentActionPhaseDetail
                            != "prior-surgery-detail"
                        || candidatePatient.Brain.DebugVersion
                            != expectedBrainDebugVersion)
                    {
                        return false;
                    }
                    AdvanceLaterParticipants(checkpoint);
                }
                catch (InjectedLateParticipantFailure)
                {
                    injectedFailureObserved = true;
                    projection.Rollback(publication);
                }
                finally
                {
                    stateStore.Replace(previousState);
                }

                if (!injectedFailureObserved
                    || !transport.ContainsActive(previousOrder)
                    || transport.Cancelled.Count != 0
                    || transport.RequestedReturns.Count != 0
                    || previousOrder.patientTransporterId
                        != "character:old-carrier"
                    || !previousOrder.patientTransportInProgress
                    || !candidateWildlifeReturn.patientReturnRequested
                    || candidateWildlifeReturn.patientTransportInProgress
                    || candidatePatient.CurrentLifecycleState
                        != CharacterLifecycleState.OnExpedition
                    || candidatePatient.IsAiPaused()
                    || candidatePatient.State != CharacterDecisionState.EXECUTE
                    || !ReferenceEquals(
                        candidatePatient.Brain.bestAction,
                        expectedBestAction)
                    || !candidatePatient.Brain.isExecuted
                    || candidatePatient.Brain.isBestActionEnd
                    || candidatePatient.Brain.CurrentActionPhase
                        != "prior-surgery-phase"
                    || candidatePatient.Brain.CurrentActionPhaseDetail
                        != "prior-surgery-detail"
                    || candidatePatient.Brain.DebugVersion
                        != expectedBrainDebugVersion)
                {
                    return false;
                }
            }

            SurgeryAggregateState completingState = new();
            completingState.Orders.Add(candidateCharacterOrder);
            completingState.Orders.Add(candidateWildlifeReturn);
            stateStore.Replace(completingState);
            SurgeryRestorePublication completingPublication =
                projection.PreparePublication(new[] { previousOrder });
            projection.Publish(completingPublication);
            if (!transport.ContainsActive(previousOrder)
                || transport.Cancelled.Count != 0
                || transport.RequestedReturns.Count != 0)
            {
                return false;
            }

            projection.Complete(completingPublication);
            return !transport.ContainsActive(previousOrder)
                && transport.Cancelled.Count == 1
                && ReferenceEquals(transport.Cancelled[0], previousOrder)
                && transport.RequestedReturns.Count == 1
                && ReferenceEquals(
                    transport.RequestedReturns[0],
                    candidateWildlifeReturn)
                && !oldPatient.IsAiPaused()
                && oldPatient.Brain.CurrentActionPhase
                    == SurgeryStatusCode.ProcedurePaused.ToString()
                && candidatePatient.IsAiPaused()
                && candidatePatient.Brain.CurrentActionPhase
                    == SurgeryStatusCode.SuturingInProgress.ToString();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(oldPatientObject);
            UnityEngine.Object.DestroyImmediate(candidatePatientObject);
        }
    }

    private static SurgeryOrder CharacterOrder(
        string orderId,
        string patientId,
        bool admitted,
        SurgeryStatusCode status)
    {
        return new SurgeryOrder
        {
            orderId = orderId,
            procedureId = "procedure:test",
            subject = new SurgicalSubjectRef
            {
                kind = SurgicalSubjectKind.Character,
                subjectId = patientId
            },
            state = SurgeryOrderState.Procedure,
            patientAdmitted = admitted,
            statusData = new SurgeryStatusData { code = status }
        };
    }

    private static CharacterActor CreatePatient(string name, string id)
    {
        GameObject patientObject = new(name);
        patientObject.SetActive(false);
        CharacterActor actor = patientObject.AddComponent<CharacterActor>();
        patientObject.AddComponent<AIBrain>();
        actor.EnsureRuntimeState();
        actor.Identity.SetPersistentId(id);
        return actor;
    }

    private static void SetPriorCandidatePatientState(CharacterActor patient)
    {
        patient.SetLifecycleState(CharacterLifecycleState.OnExpedition);
        patient.SetAiPaused(false);
        patient.state = CharacterDecisionState.EXECUTE;
        patient.Brain.bestAction = new AIAction();
        patient.Brain.isExecuted = true;
        patient.Brain.isBestActionEnd = false;
        patient.Brain.SetActionPhase(
            "prior-surgery-phase",
            detail: "prior-surgery-detail");
    }

    private static void AdvanceLaterParticipants(LateFailureCheckpoint checkpoint)
    {
        int completedLaterParticipants = checkpoint switch
        {
            LateFailureCheckpoint.ImmediatelyAfterSurgery => 0,
            LateFailureCheckpoint.AfterFirstLateParticipant => 1,
            LateFailureCheckpoint.BeforeAggregatePublication => 2,
            _ => throw new ArgumentOutOfRangeException(nameof(checkpoint))
        };
        for (int index = 0; index < completedLaterParticipants; index++)
        {
            // Represents a later participant whose reversible publish has
            // completed. Surgery must remain rollback-capable regardless of
            // how many later publishers ran before the failure.
        }

        throw new InjectedLateParticipantFailure();
    }

    private sealed class InjectedLateParticipantFailure : Exception
    {
    }

    private sealed class TestCharacterWorld : ICharacterWorldQuery
    {
        internal TestCharacterWorld(params CharacterActor[] characters)
        {
            Characters = characters;
        }

        public int CharacterVersion => 1;
        public IReadOnlyList<CharacterActor> Characters { get; }
    }

    private sealed class TestPatientTransport : ISurgicalPatientTransportRuntime
    {
        private readonly HashSet<SurgeryOrder> active = new();

        internal TestPatientTransport(SurgeryOrder activeOrder)
        {
            active.Add(activeOrder);
        }

        internal List<SurgeryOrder> Cancelled { get; } = new();
        internal List<SurgeryOrder> RequestedReturns { get; } = new();

        internal bool ContainsActive(SurgeryOrder order) => active.Contains(order);

        public bool EnsureWildlifeAdmission(
            SurgeryOrder order,
            WildlifeActor patient,
            Vector2Int destination,
            out SurgeryStatusData status)
        {
            status = new SurgeryStatusData();
            return false;
        }

        public void RequestWildlifeReturn(SurgeryOrder order)
        {
            RequestedReturns.Add(order);
        }

        public void CancelTransport(SurgeryOrder order)
        {
            Cancelled.Add(order);
            active.Remove(order);
        }

        public bool TryGetTransport(
            string orderId,
            CharacterActor carrier,
            out WildlifeActor patient,
            out Vector2Int destination,
            out bool returning,
            out DomainFailure failure)
        {
            patient = null;
            destination = default;
            returning = false;
            failure = DomainFailure.None;
            return false;
        }

        public IDisposable BeginTransportPass(
            CharacterActor carrier,
            string orderId) => EmptyDisposable.Instance;

        public bool TryBeginCarry(
            string orderId,
            CharacterActor carrier,
            out DomainFailure failure)
        {
            failure = DomainFailure.None;
            return false;
        }

        public bool TryCompleteCarry(
            string orderId,
            CharacterActor carrier,
            out DomainFailure failure)
        {
            failure = DomainFailure.None;
            return false;
        }

        public void FailCarry(string orderId, CharacterActor carrier)
        {
        }
    }

    private sealed class EmptyDisposable : IDisposable
    {
        internal static readonly EmptyDisposable Instance = new();
        public void Dispose()
        {
        }
    }
}
#endif
