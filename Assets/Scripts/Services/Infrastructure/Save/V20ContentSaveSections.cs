using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Infrastructure;

public sealed class CharacterNarrativeSaveSection :
    DungeonStrictJsonSaveSection<CharacterNarrativeWorldSaveData, CharacterNarrativeAggregateState>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "characters.narrative";
    private readonly ICharacterNarrativePersistence persistence;
    public CharacterNarrativeSaveSection(ICharacterNarrativePersistence persistence) =>
        this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
    public override string SectionId => Id;
    public override int SectionVersion => CharacterNarrativeWorldSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn => new[]
    {
        CharacterLifeSaveSection.Id,
        KinshipHouseholdSaveSection.Id,
        CharacterCareerSaveSection.Id
    };
    protected override void ValidateRawPayload(string payloadJson) =>
        RequireTopLevelArrayFields(
            payloadJson,
            "characters",
            "identityStates",
            "workCompletionDeliveries");
    protected override CharacterNarrativeWorldSaveData CapturePayload() => persistence.Capture();
    protected override CharacterNarrativeAggregateState BuildRestoreCandidate(CharacterNarrativeWorldSaveData payload) => persistence.PrepareRestore(payload);
    protected override void PublishRestoreCandidate(CharacterNarrativeAggregateState candidate) => persistence.PublishRestore(candidate);
}

public sealed class SeasonalWorldEventsSaveSection :
    DungeonStrictJsonSaveSection<SeasonalEventWorldSaveData, SeasonalEventAggregateState>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "world.seasonal-events";
    private readonly IV20CampaignPersistence persistence;
    private readonly IRestoreWorldCandidateQuery restoreWorldCandidates;
    public SeasonalWorldEventsSaveSection(
        IV20CampaignPersistence persistence,
        IRestoreWorldCandidateQuery restoreWorldCandidates)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
        this.restoreWorldCandidates = restoreWorldCandidates
            ?? throw new ArgumentNullException(nameof(restoreWorldCandidates));
    }
    public override string SectionId => Id;
    public override int SectionVersion => SeasonalEventWorldSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.RuntimeState;
    public override IReadOnlyList<string> DependsOn => new[]
    {
        CalendarClimateSaveSection.Id,
        CropEcologySaveSection.Id,
        WildlifeSaveSection.Id,
        ModularFacilityWorldSaveSection.Id
    };
    protected override SeasonalEventWorldSaveData CapturePayload() => persistence.CaptureSeasonal();
    protected override SeasonalEventAggregateState BuildRestoreCandidate(
        SeasonalEventWorldSaveData payload)
    {
        bool requiresWildlifeJoin = (payload?.activeEvents
                ?? new List<V20ActiveEventSaveData>())
            .Any(value => value?.seasonalWildlifeArrival?.configured == true
                && (value.seasonalWildlifeArrival.members?.Count ?? 0) > 0);
        if (requiresWildlifeJoin)
        {
            if (!restoreWorldCandidates.TryGetWildlife(
                    out IReadOnlyList<WildlifeActor> wildlife))
                throw new InvalidOperationException(
                    "Seasonal wildlife restore requires the incoming wildlife candidate.");
            SeasonalWildlifeArrivalRules.ValidateRestoreJoin(
                payload,
                wildlife);
        }
        return persistence.PrepareSeasonalRestore(payload);
    }
    protected override void PublishRestoreCandidate(
        SeasonalEventAggregateState candidate) =>
        persistence.PublishSeasonalRestore(candidate);
}

public interface ISocietyIncidentRestoreOwnedCharacterIdQuery
{
    bool TryGetOwnedCharacterIds(
        out IReadOnlyCollection<CharacterId> characterIds);
}

public sealed class SocietyEventsRestoreCandidate :
    IDungeonDiscardableRestoreCandidate
{
    private Action discardProjection;

    public SocietyEventsRestoreCandidate(
        SocietyEventAggregateState society,
        IReadOnlyList<EconomyProjectInputOwnerDescriptor> inputOwners,
        IReadOnlyCollection<CharacterId> observedIncidentOwnedCharacterIds,
        Action discardProjection)
    {
        Society = society ?? throw new ArgumentNullException(nameof(society));
        InputOwners = inputOwners
            ?? throw new ArgumentNullException(nameof(inputOwners));
        ObservedIncidentOwnedCharacterIds =
            observedIncidentOwnedCharacterIds
            ?? throw new ArgumentNullException(
                nameof(observedIncidentOwnedCharacterIds));
        this.discardProjection = discardProjection
            ?? throw new ArgumentNullException(nameof(discardProjection));
    }

    public SocietyEventAggregateState Society { get; }
    public IReadOnlyList<EconomyProjectInputOwnerDescriptor> InputOwners { get; }
    public IReadOnlyCollection<CharacterId> ObservedIncidentOwnedCharacterIds
    {
        get;
    }

    public void Discard() => ReleaseProjection();

    internal void ReleaseProjection()
    {
        Action release = discardProjection;
        discardProjection = null;
        release?.Invoke();
    }
}

public sealed class SocietyEventsSaveSection :
    DungeonStrictJsonSaveSection<
        SocietyEventWorldSaveData,
        SocietyEventsRestoreCandidate>,
    IDungeonRollbackFreeSaveSection,
    ISocietyIncidentRestoreOwnedCharacterIdQuery
{
    public const string Id = "society.events";
    private readonly IV20CampaignPersistence persistence;
    private readonly V20StoryContentCatalog catalog;
    private readonly IPhysicalItemRestoreCandidateQuery physicalCandidates;
    private readonly IEconomyProjectInputOwnerRestoreRuntime inputOwners;
    private readonly IRestoreWorldCandidateQuery restoreWorldCandidates;
    private IReadOnlyCollection<CharacterId>
        preparedObservedIncidentOwnedCharacterIds;
    public SocietyEventsSaveSection(
        IV20CampaignPersistence persistence,
        V20StoryContentCatalog catalog,
        IPhysicalItemRestoreCandidateQuery physicalCandidates,
        IEconomyProjectInputOwnerRestoreRuntime inputOwners,
        IRestoreWorldCandidateQuery restoreWorldCandidates)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.physicalCandidates = physicalCandidates
            ?? throw new ArgumentNullException(nameof(physicalCandidates));
        this.inputOwners = inputOwners
            ?? throw new ArgumentNullException(nameof(inputOwners));
        this.restoreWorldCandidates = restoreWorldCandidates
            ?? throw new ArgumentNullException(nameof(restoreWorldCandidates));
    }
    public override string SectionId => Id;
    public override int SectionVersion => SocietyEventWorldSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn => new[]
    {
        CharacterNarrativeSaveSection.Id,
        CharacterWorldSaveSection.Id,
        SeasonalWorldEventsSaveSection.Id,
        PhysicalItemsSaveSection.Id,
        PopulationHealthSaveSection.Id,
        CharacterConsumablesSaveSection.Id,
        CharacterMedicalSaveSection.Id,
        ModularFacilityWorldSaveSection.Id
    };
    protected override SocietyEventWorldSaveData CapturePayload() => persistence.CaptureSociety();
    protected override SocietyEventsRestoreCandidate BuildRestoreCandidate(
        SocietyEventWorldSaveData payload)
    {
        if (preparedObservedIncidentOwnedCharacterIds != null)
        {
            throw new InvalidOperationException(
                "A Society incident restore-owner projection is already prepared.");
        }

        ValidatePhysicalRestoreJoin(payload, physicalCandidates);
        SocietyEventAggregateState society = persistence.PrepareSociety(payload);
        ValidateObservedIncidentCharacterRestoreJoin(
            payload,
            restoreWorldCandidates);
        IReadOnlyList<EconomyProjectInputOwnerDescriptor> descriptors =
            BuildInputOwnerDescriptors(payload);
        if (!inputOwners.TryValidateForRestore(
                EconomyProjectInputOwnerAuthority.GuestRequestDomain,
                descriptors,
                out string ownerFailure))
        {
            throw new InvalidOperationException(
                "Guest-request input-owner restore join failed: "
                + ownerFailure);
        }

        CharacterId[] observedIncidentOwnedCharacterIds =
            payload.activeEvents
                .Where(value => value?.hasObservedMealIncident == true)
                .Select(value => new CharacterId(
                    value.observedMealIncident.targetCharacterId))
                .OrderBy(value => value.Value, StringComparer.Ordinal)
                .ToArray();
        IReadOnlyCollection<CharacterId> ownerProjection =
            Array.AsReadOnly(observedIncidentOwnedCharacterIds);
        SocietyEventsRestoreCandidate candidate = new(
            society,
            descriptors,
            ownerProjection,
            () => ClearPreparedObservedIncidentOwnerProjection(
                ownerProjection));
        preparedObservedIncidentOwnedCharacterIds = ownerProjection;
        return candidate;
    }

    public bool TryGetOwnedCharacterIds(
        out IReadOnlyCollection<CharacterId> characterIds)
    {
        characterIds = preparedObservedIncidentOwnedCharacterIds;
        return characterIds != null;
    }

    public static void ValidateObservedIncidentCharacterRestoreJoin(
        SocietyEventWorldSaveData payload,
        IRestoreWorldCandidateQuery query)
    {
        if (payload?.activeEvents == null
            || query == null
            || !query.TryGetCharacters(
                out IReadOnlyList<CharacterActor> candidates)
            || candidates == null)
        {
            throw new InvalidOperationException(
                "Observed society-incident restore requires society and character candidates.");
        }

        Dictionary<CharacterId, string> occurrenceByTarget = new();
        foreach (V20ActiveEventSaveData occurrence in payload.activeEvents
                     .Where(value => value?.hasObservedMealIncident == true)
                     .OrderBy(value => value.instanceId, StringComparer.Ordinal))
        {
            ObservedMealIncidentEvidenceSaveData evidence =
                occurrence.observedMealIncident;
            CharacterId targetId = new(evidence.targetCharacterId);
            if (!targetId.IsValid
                || !string.Equals(
                    evidence.targetCharacterId,
                    targetId.Value,
                    StringComparison.Ordinal)
                || !occurrenceByTarget.TryAdd(
                    targetId,
                    occurrence.instanceId))
            {
                throw new InvalidOperationException(
                    $"Observed society incident '{occurrence.instanceId}' operation '{evidence.operationId}' has an invalid or multiply-owned Customer '{evidence.targetCharacterId ?? string.Empty}'.");
            }

            CharacterActor[] matches = candidates
                .Where(actor => actor != null
                    && CharacterPersistentIdentity.TryGet(
                        actor,
                        out CharacterId candidateId)
                    && candidateId.Equals(targetId))
                .ToArray();
            if (matches.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Observed society incident '{occurrence.instanceId}' operation '{evidence.operationId}' requires exactly one restored Customer '{targetId.Value}', but found {matches.Length}.");
            }

            CharacterActor target = matches[0];
            if (target.Identity?.Data == null
                || target.IsDead
                || target.CurrentLifecycleState
                    == CharacterLifecycleState.Despawned)
            {
                throw new InvalidOperationException(
                    $"Observed society incident '{occurrence.instanceId}' operation '{evidence.operationId}' references an invalid, dead, or despawned restored character '{targetId.Value}'.");
            }
        }
    }

    protected override void ValidateParsedPayload(
        SocietyEventWorldSaveData payload)
    {
        _ = persistence.PrepareSociety(payload)
            ?? throw new InvalidOperationException(
                "Society event restore candidate builder returned null.");
    }

    protected override void PublishRestoreCandidate(
        SocietyEventsRestoreCandidate candidate)
    {
        SocietyEventsRestoreCandidate required = candidate
            ?? throw new ArgumentNullException(nameof(candidate));
        try
        {
            if (!inputOwners.TryReplaceForRestore(
                    EconomyProjectInputOwnerAuthority.GuestRequestDomain,
                    required.InputOwners,
                    out string ownerFailure))
            {
                throw new InvalidOperationException(
                    "Guest-request input-owner restore publication failed: "
                    + ownerFailure);
            }
            persistence.PublishSociety(required.Society);
        }
        finally
        {
            required.ReleaseProjection();
        }
    }

    private void ClearPreparedObservedIncidentOwnerProjection(
        IReadOnlyCollection<CharacterId> expected)
    {
        if (preparedObservedIncidentOwnedCharacterIds == null)
        {
            return;
        }
        if (!ReferenceEquals(
                preparedObservedIncidentOwnedCharacterIds,
                expected))
        {
            throw new InvalidOperationException(
                "Society incident restore-owner projection ownership mismatch.");
        }
        preparedObservedIncidentOwnedCharacterIds = null;
    }

    private IReadOnlyList<EconomyProjectInputOwnerDescriptor>
        BuildInputOwnerDescriptors(SocietyEventWorldSaveData payload) =>
        (payload?.activeEvents ?? new List<V20ActiveEventSaveData>())
            .Where(value => value?.guestDelivery?.inputOwnerActive == true)
            .OrderBy(
                value => value.guestDelivery.destinationId,
                StringComparer.Ordinal)
            .Select(value =>
            {
                GuestRequestDefinitionSO definition = catalog.GuestRequests
                    .Single(request => string.Equals(
                        request.StableId,
                        value.definitionId,
                        StringComparison.Ordinal));
                GuestRequestDeliverySaveData delivery = value.guestDelivery;
                return new EconomyProjectInputOwnerDescriptor(
                    EconomyProjectInputOwnerAuthority.GuestRequestDomain,
                    value.instanceId,
                    delivery.destinationId,
                    new UnityEngine.Vector2Int(
                        delivery.destinationX,
                        delivery.destinationY),
                    FacilityBufferDestinationAnchorKind.LiveFacility,
                    delivery.venueFacilityInstanceId,
                    GuestRequestDeliveryMaterialRules
                        .BuildMaterialRequirements(definition),
                    delivery.inputCapacityGrams,
                    delivery.inputMassAuthorityRevision,
                    delivery.inputCapacityFingerprint);
            })
            .ToArray();

    public static void ValidatePhysicalRestoreJoin(
        SocietyEventWorldSaveData payload,
        IPhysicalItemRestoreCandidateQuery query)
    {
        if (payload?.activeEvents == null
            || query == null
            || !query.IsCandidateAvailable)
        {
            throw new InvalidOperationException(
                "Guest-request restore requires society and physical candidates.");
        }
        Dictionary<string, GuestRequestDeliverySaveData> owners =
            payload.activeEvents
                .Where(value => value?.guestDelivery != null
                    && GuestRequestDeliveryOutbox.IsCanonicalReceipt(
                        value.guestDelivery))
                .ToDictionary(
                    value => value.guestDelivery.operationId,
                    value => value.guestDelivery,
                    StringComparer.Ordinal);
        foreach (KeyValuePair<string, GuestRequestDeliverySaveData> owner in
                 owners)
        {
            if (!query.TryGetPendingBatchDisposition(
                    owner.Key,
                    out PhysicalItemRestoreCandidateDispositionSnapshot receipt)
                || !Matches(owner.Value, receipt))
            {
                throw new InvalidOperationException(
                    $"Guest-request delivery '{owner.Key}' has no exact incoming physical Transfer receipt.");
            }
        }
        foreach (PhysicalItemRestoreCandidateDispositionSnapshot receipt in
                 query.PendingBatchDispositions)
        {
            if (receipt?.OperationId == null
                || !GuestRequestDeliveryOutbox.IsOwnedOperationId(
                    receipt.OperationId))
            {
                continue;
            }
            if (!owners.TryGetValue(
                    receipt.OperationId,
                    out GuestRequestDeliverySaveData owner)
                || !Matches(owner, receipt))
            {
                throw new InvalidOperationException(
                    $"Incoming guest-request Transfer '{receipt.OperationId}' has no exact society owner.");
            }
        }
    }

    private static bool Matches(
        GuestRequestDeliverySaveData owner,
        PhysicalItemRestoreCandidateDispositionSnapshot receipt) =>
        owner != null
        && receipt != null
        && receipt.Kind == PhysicalItemDispositionKind.Transfer
        && string.Equals(receipt.OperationId, owner.operationId,
            StringComparison.Ordinal)
        && string.Equals(receipt.ReasonCode,
            GuestRequestDeliveryOutbox.TransferReason,
            StringComparison.Ordinal)
        && string.Equals(receipt.CommitId, owner.commitId,
            StringComparison.Ordinal)
        && string.Equals(
            receipt.RequestFingerprint,
            owner.requestFingerprint,
            StringComparison.Ordinal)
        && receipt.Quantity == owner.committedQuantity
        && receipt.InputMassGrams == owner.committedMassGrams
        && (receipt.SourceStackIds ?? Array.Empty<string>())
            .OrderBy(value => value, StringComparer.Ordinal)
            .SequenceEqual(owner.sourceStackIds, StringComparer.Ordinal);
}

public sealed class FactionCampaignRestoreCandidate
{
    public FactionCampaignRestoreCandidate(
        FactionCampaignAggregateState campaign,
        IReadOnlyList<EconomyProjectInputOwnerDescriptor> inputOwners)
    {
        Campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
        InputOwners = inputOwners
            ?? throw new ArgumentNullException(nameof(inputOwners));
    }

    public FactionCampaignAggregateState Campaign { get; }
    public IReadOnlyList<EconomyProjectInputOwnerDescriptor> InputOwners { get; }
}

public sealed class SocietyObservedIncidentResponseCrossAggregateSaveValidation :
    IDungeonSavePreflightValidator,
    IDungeonSaveRegistryPreflightValidator,
    IDungeonCapturedSavePreflightValidator
{
    private const string ReplacementOperationPrefix =
        "consumable-operation:wim040-response:";

    private readonly IBuildingDefinitionLookup buildingDefinitions;

    public SocietyObservedIncidentResponseCrossAggregateSaveValidation(
        IBuildingDefinitionLookup buildingDefinitions)
    {
        this.buildingDefinitions = buildingDefinitions
            ?? throw new ArgumentNullException(nameof(buildingDefinitions));
    }

    public void Validate(
        DungeonGameSaveData saveData,
        DungeonGameRestoreReport report)
    {
        if (report == null)
            throw new ArgumentNullException(nameof(report));
        try
        {
            if (saveData?.sections == null)
            {
                throw new InvalidOperationException(
                    "Captured save section collection is missing.");
            }
            Dictionary<string, DungeonSaveSectionEnvelope> envelopes = new(
                StringComparer.Ordinal);
            foreach (DungeonSaveSectionEnvelope envelope in saveData.sections)
            {
                if (envelope == null
                    || string.IsNullOrEmpty(envelope.sectionId)
                    || !envelopes.TryAdd(envelope.sectionId, envelope))
                {
                    throw new InvalidOperationException(
                        "Captured save contains a null, empty, or duplicate section identity.");
                }
            }
            ValidateEnvelopes(envelopes);
        }
        catch (Exception exception)
        {
            report.AddError(
                "Society incident-response cross-aggregate preflight failed: "
                + exception.Message);
        }
    }

    public void Validate(
        IReadOnlyDictionary<string, DungeonSaveSectionEnvelope> envelopes,
        DungeonGameRestoreReport report)
    {
        if (envelopes == null)
            throw new ArgumentNullException(nameof(envelopes));
        if (report == null)
            throw new ArgumentNullException(nameof(report));
        try
        {
            ValidateEnvelopes(envelopes);
        }
        catch (Exception exception)
        {
            report.AddError(
                "Society incident-response registry preflight failed: "
                + exception.Message);
        }
    }

    private void ValidateEnvelopes(
        IReadOnlyDictionary<string, DungeonSaveSectionEnvelope> envelopes)
    {
        SocietyEventWorldSaveData society = ParseRequired<
            SocietyEventWorldSaveData>(
            envelopes,
            SocietyEventsSaveSection.Id,
            SocietyEventWorldSaveData.CurrentVersion);
        DungeonCharacterConsumablesSaveData consumables = ParseRequired<
            DungeonCharacterConsumablesSaveData>(
            envelopes,
            CharacterConsumablesSaveSection.Id,
            DungeonCharacterConsumablesSaveData.CurrentVersion);
        DungeonCharacterMedicalSaveData medical = ParseRequired<
            DungeonCharacterMedicalSaveData>(
            envelopes,
            CharacterMedicalSaveSection.Id,
            DungeonCharacterMedicalSaveData.CurrentVersion);
        DungeonCharacterWorldSaveData characters = ParseRequired<
            DungeonCharacterWorldSaveData>(
            envelopes,
            CharacterWorldSaveSection.Id,
            CharacterWorldSaveSection.CurrentVersion);
        ModularFacilityWorldSaveData facilities = ParseRequired<
            ModularFacilityWorldSaveData>(
            envelopes,
            ModularFacilityWorldSaveSection.Id,
            ModularFacilityWorldSaveSection.CurrentSectionVersion);

        if (society.version != SocietyEventWorldSaveData.CurrentVersion
            || consumables.version
                != DungeonCharacterConsumablesSaveData.CurrentVersion
            || medical.version != DungeonCharacterMedicalSaveData.CurrentVersion
            || facilities.version != ModularFacilityWorldSaveService.CurrentVersion)
        {
            throw new InvalidOperationException(
                "Incident-response joins require exact current payload versions.");
        }
        ValidatePayloads(
            society,
            consumables,
            medical,
            characters,
            facilities);
    }

    private void ValidatePayloads(
        SocietyEventWorldSaveData society,
        DungeonCharacterConsumablesSaveData consumables,
        DungeonCharacterMedicalSaveData medical,
        DungeonCharacterWorldSaveData characters,
        ModularFacilityWorldSaveData facilities)
    {
        if (society?.activeEvents == null
            || society.recentResolvedEvents == null
            || consumables?.activeMealPlans == null
            || consumables.completedOperations == null
            || medical?.orders == null
            || characters?.actors == null
            || facilities?.buildings == null
            || facilities.gridCells == null)
        {
            throw new InvalidOperationException(
                "Incident-response join collections are missing.");
        }

        V20ActiveEventSaveData[] allResponseOwners = society.activeEvents
            .Concat(society.recentResolvedEvents)
            .Where(value => value?.hasObservedMealIncident == true
                && value.observedIncidentResponse != null
                && value.observedIncidentResponse.phase
                    != ObservedIncidentResponsePhase.None)
            .ToArray();
        Dictionary<string, V20ActiveEventSaveData> ownersByOperation = new(
            StringComparer.Ordinal);
        foreach (V20ActiveEventSaveData owner in allResponseOwners)
        {
            string operationId = owner.observedIncidentResponse.operationId;
            if (string.IsNullOrEmpty(operationId)
                || !ownersByOperation.TryAdd(operationId, owner))
            {
                throw new InvalidOperationException(
                    "Incident-response operation ownership is empty or duplicated.");
            }
        }

        HashSet<string> activeMedicalOperations = new(StringComparer.Ordinal);
        Dictionary<string, V20ActiveEventSaveData> activeOwnersByOperation =
            new(StringComparer.Ordinal);
        foreach (V20ActiveEventSaveData occurrence in society.activeEvents
                     .Where(value => value?.hasObservedMealIncident == true))
        {
            ObservedIncidentResponseSaveData response =
                occurrence.observedIncidentResponse;
            if (response == null
                || response.phase == ObservedIncidentResponsePhase.None)
            {
                continue;
            }
            if (response.phase is not (
                ObservedIncidentResponsePhase.Accepted
                or ObservedIncidentResponsePhase.ExternalOperationStarted
                or ObservedIncidentResponsePhase.ReceiptCommitted))
            {
                throw new InvalidOperationException(
                    $"Active incident response '{occurrence.instanceId}' has terminal phase '{response.phase}'.");
            }

            DungeonCharacterSaveData target = RequireTarget(
                occurrence,
                characters);
            RequireResponseIdentity(occurrence);
            if (!activeOwnersByOperation.TryAdd(
                    response.operationId,
                    occurrence))
            {
                throw new InvalidOperationException(
                    $"Active incident response operation '{response.operationId}' is multiply owned.");
            }
            if (string.Equals(response.choiceId, "replace", StringComparison.Ordinal))
            {
                ValidateReplacementResponse(
                    occurrence,
                    response,
                    target,
                    consumables,
                    facilities);
                continue;
            }
            if (string.Equals(
                    response.choiceId,
                    "emergency-care",
                    StringComparison.Ordinal))
            {
                ValidateEmergencyCareResponse(
                    occurrence,
                    response,
                    target,
                    medical,
                    facilities,
                    activeMedicalOperations);
                continue;
            }
            if (string.Equals(response.choiceId, "transfer", StringComparison.Ordinal))
            {
                ValidateTransferResponse(
                    occurrence,
                    response,
                    target,
                    medical,
                    facilities,
                    activeMedicalOperations);
                continue;
            }
            throw new InvalidOperationException(
                $"Incident response '{occurrence.instanceId}' has unsupported choice '{response.choiceId}'.");
        }

        ValidateReplacementOrphans(
            activeOwnersByOperation,
            consumables,
            facilities);
        ValidateMedicalAndMovementProvenanceOrphans(
            activeOwnersByOperation,
            medical,
            characters);
    }

    private static void RequireResponseIdentity(
        V20ActiveEventSaveData occurrence)
    {
        ObservedIncidentResponseSaveData response =
            occurrence.observedIncidentResponse;
        string expected = string.Equals(
                response.choiceId,
                "replace",
                StringComparison.Ordinal)
            ? ReplacementOperationPrefix + occurrence.instanceId
            : $"society-incident-response:{occurrence.instanceId}:"
                + response.choiceId;
        bool kindMatches = string.Equals(
                    response.choiceId,
                    "replace",
                    StringComparison.Ordinal)
                && occurrence.observedMealIncident.incidentKind
                    == ServiceIncidentKind.ForbiddenMeal
            || (response.choiceId is "emergency-care" or "transfer")
                && occurrence.observedMealIncident.incidentKind
                    == ServiceIncidentKind.MedicalCollapse;
        if (!kindMatches
            || !string.Equals(
                occurrence.selectedChoiceId,
                response.choiceId,
                StringComparison.Ordinal)
            || !string.Equals(
                response.operationId,
                expected,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Incident response '{occurrence.instanceId}' has foreign choice or operation identity.");
        }
    }

    private static DungeonCharacterSaveData RequireTarget(
        V20ActiveEventSaveData occurrence,
        DungeonCharacterWorldSaveData characters)
    {
        string targetId = occurrence.observedMealIncident?.targetCharacterId
            ?? string.Empty;
        DungeonCharacterSaveData[] matches = characters.actors
            .Where(value => value != null
                && string.Equals(
                    value.persistentId,
                    targetId,
                    StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1
            || matches[0].characterType != CharacterType.Customer
            || matches[0].lifecycleState
                == CharacterLifecycleState.Despawned)
        {
            throw new InvalidOperationException(
                $"Incident response '{occurrence.instanceId}' has no exact living Customer target '{targetId}'.");
        }
        return matches[0];
    }

    private void ValidateReplacementResponse(
        V20ActiveEventSaveData occurrence,
        ObservedIncidentResponseSaveData response,
        DungeonCharacterSaveData target,
        DungeonCharacterConsumablesSaveData consumables,
        ModularFacilityWorldSaveData facilities)
    {
        string facilityId = occurrence.observedMealIncident.facilityInstanceId;
        RequireFacility(
            facilityId,
            facilities,
            FacilityRole.Meal,
            requireIsolationRecovery: false);
        CharacterMealPlanSaveData[] active = consumables.activeMealPlans
            .Where(value => value != null
                && string.Equals(
                    value.planId,
                    response.operationId,
                    StringComparison.Ordinal))
            .ToArray();
        CharacterConsumableOperationState[] completed =
            consumables.completedOperations
                .Where(value => value != null
                    && string.Equals(
                        value.operationId,
                        response.operationId,
                        StringComparison.Ordinal))
                .ToArray();
        if (active.Length + completed.Length > 1)
        {
            throw new InvalidOperationException(
                $"Replacement response '{occurrence.instanceId}' has duplicate external aggregate state.");
        }
        if (active.Length == 1)
        {
            RequireReplacementPlan(
                occurrence,
                target,
                facilityId,
                active[0]);
        }
        if (completed.Length == 1)
        {
            RequireReplacementCompletion(
                occurrence,
                response,
                target,
                facilityId,
                completed[0],
                requireSuccessfulMeal:
                    response.phase != ObservedIncidentResponsePhase.Failed
                    && response.phase
                        != ObservedIncidentResponsePhase.Cancelled);
        }

        switch (response.phase)
        {
            case ObservedIncidentResponsePhase.Accepted:
                if (active.Length + completed.Length != 0
                    || !string.IsNullOrEmpty(response.externalOperationId)
                    || !string.IsNullOrEmpty(response.receiptId))
                {
                    throw new InvalidOperationException(
                        $"Accepted replacement response '{occurrence.instanceId}' already owns an external identity.");
                }
                break;
            case ObservedIncidentResponsePhase.ExternalOperationStarted:
                if (active.Length + completed.Length != 1
                    || !string.Equals(
                        response.externalOperationId,
                        response.operationId,
                        StringComparison.Ordinal)
                    || !string.IsNullOrEmpty(response.receiptId))
                {
                    throw new InvalidOperationException(
                        $"Started replacement response '{occurrence.instanceId}' has no exact meal operation.");
                }
                break;
            case ObservedIncidentResponsePhase.ReceiptCommitted:
                if (active.Length != 0
                    || completed.Length != 1
                    || !string.Equals(
                        response.externalOperationId,
                        response.operationId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        response.receiptId,
                        $"physical-meal-consumed:{response.operationId}:"
                            + completed[0].itemStackId,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Replacement response '{occurrence.instanceId}' has no exact consumption receipt.");
                }
                break;
        }
    }

    private static void RequireReplacementPlan(
        V20ActiveEventSaveData occurrence,
        DungeonCharacterSaveData target,
        string facilityId,
        CharacterMealPlanSaveData plan)
    {
        if (!string.Equals(
                plan.characterId,
                target.persistentId,
                StringComparison.Ordinal)
            || !string.Equals(
                plan.facilityInstanceId,
                facilityId,
                StringComparison.Ordinal)
            || !string.Equals(
                plan.planId,
                occurrence.observedIncidentResponse.operationId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Replacement response '{occurrence.instanceId}' has a foreign active meal plan.");
        }
    }

    private static void RequireReplacementCompletion(
        V20ActiveEventSaveData occurrence,
        ObservedIncidentResponseSaveData response,
        DungeonCharacterSaveData target,
        string facilityId,
        CharacterConsumableOperationState completed,
        bool requireSuccessfulMeal)
    {
        if (!completed.meal
            || completed.detox
            || !string.Equals(
                completed.operationId,
                response.operationId,
                StringComparison.Ordinal)
            || !string.Equals(
                completed.characterId,
                target.persistentId,
                StringComparison.Ordinal)
            || !string.Equals(
                completed.facilityInstanceId,
                facilityId,
                StringComparison.Ordinal)
            || !new ItemStackId(completed.itemStackId).IsValid
            || requireSuccessfulMeal
                && (completed.policyViolation || completed.contaminated))
        {
            throw new InvalidOperationException(
                $"Replacement response '{occurrence.instanceId}' has a foreign or invalid completed meal operation.");
        }
    }

    private void ValidateEmergencyCareResponse(
        V20ActiveEventSaveData occurrence,
        ObservedIncidentResponseSaveData response,
        DungeonCharacterSaveData target,
        DungeonCharacterMedicalSaveData medical,
        ModularFacilityWorldSaveData facilities,
        ISet<string> activeMedicalOperations)
    {
        if (response.phase == ObservedIncidentResponsePhase.Accepted)
        {
            if (!string.IsNullOrEmpty(response.externalOperationId)
                || !string.IsNullOrEmpty(response.receiptId))
            {
                throw new InvalidOperationException(
                    $"Accepted emergency response '{occurrence.instanceId}' already owns an external operation.");
            }
            return;
        }
        CharacterMedicalOrder order = RequireMedicalOrder(
            occurrence,
            response,
            target,
            medical,
            activeMedicalOperations);
        if (!string.IsNullOrEmpty(order.treatmentFacilityId))
        {
            RequireFacility(
                order.treatmentFacilityId,
                facilities,
                FacilityRole.Medical,
                requireIsolationRecovery: false);
        }
        bool receipt = response.phase
            == ObservedIncidentResponsePhase.ReceiptCommitted;
        if (receipt != !string.IsNullOrEmpty(response.receiptId)
            || receipt != !string.IsNullOrEmpty(
                order.societyResponseReceiptId)
            || receipt && (!order.stabilized
                || !string.Equals(
                    order.societyResponseReceiptId,
                    response.receiptId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    response.receiptId,
                    $"medical-stabilized:{order.orderId}",
                    StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"Emergency response '{occurrence.instanceId}' has no exact stabilization receipt.");
        }
    }

    private void ValidateTransferResponse(
        V20ActiveEventSaveData occurrence,
        ObservedIncidentResponseSaveData response,
        DungeonCharacterSaveData target,
        DungeonCharacterMedicalSaveData medical,
        ModularFacilityWorldSaveData facilities,
        ISet<string> activeMedicalOperations)
    {
        if (response.phase == ObservedIncidentResponsePhase.Accepted)
        {
            if (!string.IsNullOrEmpty(response.externalOperationId)
                || !string.IsNullOrEmpty(response.receiptId))
            {
                throw new InvalidOperationException(
                    $"Accepted transfer response '{occurrence.instanceId}' already owns an external operation.");
            }
            return;
        }
        if (!V20CampaignRuntime
                .TryClassifyObservedIncidentTransferExternalOperationId(
                    occurrence,
                    response.externalOperationId,
                    out bool medicalOrder))
        {
            throw new InvalidOperationException(
                $"Transfer response '{occurrence.instanceId}' has an invalid external operation.");
        }
        if (medicalOrder)
        {
            CharacterMedicalOrder order = RequireMedicalOrder(
                occurrence,
                response,
                target,
                medical,
                activeMedicalOperations);
            ModularFacilityBuildingSaveData facility = RequireFacility(
                order.treatmentFacilityId,
                facilities,
                FacilityRole.Medical,
                requireIsolationRecovery: true);
            bool receipt = response.phase
                == ObservedIncidentResponsePhase.ReceiptCommitted;
            if (receipt != !string.IsNullOrEmpty(response.receiptId)
                || receipt != !string.IsNullOrEmpty(
                    order.societyResponseReceiptId)
                || receipt && (!order.stabilized
                    || order.carried
                    || order.state is not (
                        CharacterMedicalOrderState.Treating
                        or CharacterMedicalOrderState.Recovering
                        or CharacterMedicalOrderState.Completed)
                    || order.patientX != facility.centerX
                    || order.patientY != facility.centerY
                    || order.bedX != facility.centerX
                    || order.bedY != facility.centerY
                    || target.gridX != facility.centerX
                    || target.gridY != facility.centerY
                    || !string.Equals(
                        order.societyResponseReceiptId,
                        response.receiptId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        response.receiptId,
                        V20CampaignRuntime
                            .FormatObservedIncidentTransferReceiptId(
                                response.externalOperationId,
                                new CharacterId(target.persistentId)),
                        StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"Medical transfer response '{occurrence.instanceId}' has no exact placement receipt.");
            }
            return;
        }

        if (target.lifecycleState != CharacterLifecycleState.Active
            || !V20CampaignRuntime.TryParseObservedIncidentTransferOperationId(
                response.operationId,
                response.externalOperationId,
                out BuildingInstanceId facilityId,
                out CoreGridCell destination))
        {
            throw new InvalidOperationException(
                $"Movement transfer response '{occurrence.instanceId}' has a foreign target or operation.");
        }
        bool savedMovementReceipt = !string.IsNullOrEmpty(
            target.societyResponseMovementReceiptId);
        if (!string.Equals(
                target.societyResponseMovementOperationId,
                response.operationId,
                StringComparison.Ordinal)
            || !string.Equals(
                target.societyResponseMovementExternalOperationId,
                response.externalOperationId,
                StringComparison.Ordinal)
            || !string.Equals(
                target.societyResponseMovementFacilityId,
                facilityId.Value,
                StringComparison.Ordinal)
            || target.societyResponseMovementDestinationX != destination.X
            || target.societyResponseMovementDestinationY != destination.Y
            || savedMovementReceipt
                != (response.phase
                    == ObservedIncidentResponsePhase.ReceiptCommitted)
            || savedMovementReceipt
                && !string.Equals(
                    target.societyResponseMovementReceiptId,
                    response.receiptId,
                    StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Movement transfer response '{occurrence.instanceId}' has mismatched persisted movement provenance.");
        }
        ModularFacilityBuildingSaveData movementFacility = RequireFacility(
            facilityId.Value,
            facilities,
            FacilityRole.Medical,
            requireIsolationRecovery: true);
        RequireMovementDestination(
            occurrence,
            destination,
            movementFacility,
            facilities);
        bool movementReceipt = response.phase
            == ObservedIncidentResponsePhase.ReceiptCommitted;
        if (movementReceipt != !string.IsNullOrEmpty(response.receiptId)
            || movementReceipt && (target.gridX != destination.X
                || target.gridY != destination.Y
                || !string.Equals(
                    response.receiptId,
                    V20CampaignRuntime.FormatObservedIncidentTransferReceiptId(
                        response.externalOperationId,
                        new CharacterId(target.persistentId)),
                    StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"Movement transfer response '{occurrence.instanceId}' has no exact arrival receipt.");
        }
    }

    private static CharacterMedicalOrder RequireMedicalOrder(
        V20ActiveEventSaveData occurrence,
        ObservedIncidentResponseSaveData response,
        DungeonCharacterSaveData target,
        DungeonCharacterMedicalSaveData medical,
        ISet<string> activeMedicalOperations)
    {
        CharacterMedicalOrder[] matches = medical.orders
            .Where(value => value != null
                && string.Equals(
                    value.orderId,
                    response.externalOperationId,
                    StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1
            || !string.Equals(
                matches[0].patientId,
                target.persistentId,
                StringComparison.Ordinal)
            || !string.Equals(
                matches[0].societyResponseOperationId,
                response.operationId,
                StringComparison.Ordinal)
            || matches[0].state == CharacterMedicalOrderState.Cancelled
            || !activeMedicalOperations.Add(response.externalOperationId))
        {
            throw new InvalidOperationException(
                $"Incident response '{occurrence.instanceId}' has an orphan, foreign, or multiply-owned medical order.");
        }
        return matches[0];
    }

    private static void ValidateMedicalAndMovementProvenanceOrphans(
        IReadOnlyDictionary<string, V20ActiveEventSaveData> activeOwners,
        DungeonCharacterMedicalSaveData medical,
        DungeonCharacterWorldSaveData characters)
    {
        foreach (CharacterMedicalOrder order in medical.orders.Where(value =>
                     value != null
                     && !string.IsNullOrEmpty(
                         value.societyResponseOperationId)))
        {
            if (!activeOwners.TryGetValue(
                    order.societyResponseOperationId,
                    out V20ActiveEventSaveData owner)
                || !string.Equals(
                    owner.observedIncidentResponse.externalOperationId,
                    order.orderId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    owner.observedMealIncident.targetCharacterId,
                    order.patientId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Medical order '{order.orderId}' has orphan or foreign Society response provenance.");
            }
        }
        foreach (DungeonCharacterSaveData actor in characters.actors.Where(value =>
                     value != null
                     && !string.IsNullOrEmpty(
                         value.societyResponseMovementOperationId)))
        {
            if (!activeOwners.TryGetValue(
                    actor.societyResponseMovementOperationId,
                    out V20ActiveEventSaveData owner)
                || !string.Equals(
                    owner.observedIncidentResponse.choiceId,
                    "transfer",
                    StringComparison.Ordinal)
                || !string.Equals(
                    owner.observedIncidentResponse.externalOperationId,
                    actor.societyResponseMovementExternalOperationId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    owner.observedMealIncident.targetCharacterId,
                    actor.persistentId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Character '{actor.persistentId}' has orphan or foreign Society movement provenance.");
            }
        }
    }

    private void ValidateReplacementOrphans(
        IReadOnlyDictionary<string, V20ActiveEventSaveData> ownersByOperation,
        DungeonCharacterConsumablesSaveData consumables,
        ModularFacilityWorldSaveData facilities)
    {
        foreach (CharacterMealPlanSaveData plan in consumables.activeMealPlans
                     .Where(value => value != null
                         && value.planId != null
                         && value.planId.StartsWith(
                             ReplacementOperationPrefix,
                             StringComparison.Ordinal)))
        {
            if (!ownersByOperation.TryGetValue(plan.planId, out V20ActiveEventSaveData owner)
                || owner.observedIncidentResponse.phase
                    != ObservedIncidentResponsePhase.ExternalOperationStarted)
            {
                throw new InvalidOperationException(
                    $"Replacement meal plan '{plan.planId}' has no active Society response owner.");
            }
            DungeonCharacterSaveData target = new()
            {
                persistentId = owner.observedMealIncident.targetCharacterId
            };
            RequireReplacementPlan(
                owner,
                target,
                owner.observedMealIncident.facilityInstanceId,
                plan);
        }

        foreach (CharacterConsumableOperationState completed in
                 consumables.completedOperations.Where(value => value != null
                     && value.operationId != null
                     && value.operationId.StartsWith(
                         ReplacementOperationPrefix,
                         StringComparison.Ordinal)))
        {
            if (!ownersByOperation.TryGetValue(
                    completed.operationId,
                    out V20ActiveEventSaveData owner))
            {
                // Completed consumable operations are retained longer than the
                // bounded Society resolved-event history. Only live response
                // ownership can be joined exactly here.
                continue;
            }
            DungeonCharacterSaveData target = new()
            {
                persistentId = owner.observedMealIncident.targetCharacterId
            };
            RequireReplacementCompletion(
                owner,
                owner.observedIncidentResponse,
                target,
                owner.observedMealIncident.facilityInstanceId,
                completed,
                requireSuccessfulMeal:
                    owner.observedIncidentResponse.phase is
                        ObservedIncidentResponsePhase
                            .ExternalOperationStarted
                        or ObservedIncidentResponsePhase.ReceiptCommitted
                        or ObservedIncidentResponsePhase.EffectsPublished);
            RequireFacility(
                completed.facilityInstanceId,
                facilities,
                FacilityRole.Meal,
                requireIsolationRecovery: false);
        }
    }

    private ModularFacilityBuildingSaveData RequireFacility(
        string facilityId,
        ModularFacilityWorldSaveData facilities,
        FacilityRole requiredRole,
        bool requireIsolationRecovery)
    {
        ModularFacilityBuildingSaveData[] matches = facilities.buildings
            .Where(value => value != null
                && string.Equals(
                    value.persistentInstanceId,
                    facilityId,
                    StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1 || matches[0].relocationPacked)
        {
            throw new InvalidOperationException(
                $"Incident response requires exactly one available facility '{facilityId}'.");
        }
        BuildingSO definition = buildingDefinitions.GetBuilding(
            matches[0].buildingId);
        bool roleValid = requiredRole == FacilityRole.None
            || definition?.Facility?.SupportsRole(requiredRole) == true;
        bool isolationValid = !requireIsolationRecovery
            || definition != null
                && definition.Abilities
                    .OfType<ISurgicalFacilityAbility>()
                    .Any(value => (value.FacilityTags
                            & SurgeryFacilityTag.IsolationRecovery)
                        == SurgeryFacilityTag.IsolationRecovery);
        if (!roleValid || !isolationValid)
        {
            throw new InvalidOperationException(
                $"Incident response facility '{facilityId}' lacks its exact capability.");
        }
        return matches[0];
    }

    private void RequireMovementDestination(
        V20ActiveEventSaveData occurrence,
        CoreGridCell destination,
        ModularFacilityBuildingSaveData facility,
        ModularFacilityWorldSaveData facilities)
    {
        if (destination.X < 0
            || destination.Y < 0
            || destination.X >= facilities.gridWidth
            || destination.Y >= facilities.gridHeight)
        {
            throw new InvalidOperationException(
                $"Movement transfer '{occurrence.instanceId}' has an out-of-bounds destination.");
        }
        ModularFacilityGridCellSaveData[] cells = facilities.gridCells
            .Where(value => value != null
                && value.x == destination.X
                && value.y == destination.Y)
            .ToArray();
        BuildingSO definition = buildingDefinitions.GetBuilding(
            facility.buildingId);
        IReadOnlyList<UnityEngine.Vector2Int> footprint =
            definition.GetGridPosList(new UnityEngine.Vector2Int(
                facility.centerX,
                facility.centerY));
        bool access = BuildingWorkAccessRules.EnumerateCandidates(
                footprint,
                definition.IsGridMovement)
            .Contains(new UnityEngine.Vector2Int(
                destination.X,
                destination.Y));
        if (cells.Length != 1
            || !GridCellAreaRules.IsWalkableArea(cells[0].areaType)
            || cells[0].terrainType == GridCellTerrainType.DeepWater
            || !access)
        {
            throw new InvalidOperationException(
                $"Movement transfer '{occurrence.instanceId}' destination is not the exact facility access cell.");
        }
    }

    private static TPayload ParseRequired<TPayload>(
        IReadOnlyDictionary<string, DungeonSaveSectionEnvelope> envelopes,
        string sectionId,
        int currentVersion)
        where TPayload : class
    {
        if (!envelopes.TryGetValue(
                sectionId,
                out DungeonSaveSectionEnvelope envelope)
            || envelope == null
            || !string.Equals(
                envelope.sectionId,
                sectionId,
                StringComparison.Ordinal)
            || envelope.sectionVersion != currentVersion
            || string.IsNullOrWhiteSpace(envelope.payloadJson))
        {
            throw new InvalidOperationException(
                "Required current save section is missing or mismatched: "
                + sectionId);
        }
        return UnityEngine.JsonUtility.FromJson<TPayload>(envelope.payloadJson)
            ?? throw new InvalidOperationException(
                "Save section payload deserialized to null: " + sectionId);
    }
}

public sealed class FactionCampaignSaveSection :
    DungeonStrictJsonSaveSection<
        FactionCampaignWorldSaveData,
        FactionCampaignRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "factions.campaign";
    private readonly IV20CampaignPersistence persistence;
    private readonly V20StoryContentCatalog catalog;
    private readonly IPhysicalItemRestoreCandidateQuery physicalCandidates;
    private readonly IEconomyProjectInputOwnerRestoreRuntime inputOwners;
    public FactionCampaignSaveSection(
        IV20CampaignPersistence persistence,
        V20StoryContentCatalog catalog,
        IPhysicalItemRestoreCandidateQuery physicalCandidates,
        IEconomyProjectInputOwnerRestoreRuntime inputOwners)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.physicalCandidates = physicalCandidates
            ?? throw new ArgumentNullException(nameof(physicalCandidates));
        this.inputOwners = inputOwners
            ?? throw new ArgumentNullException(nameof(inputOwners));
    }
    public override string SectionId => Id;
    public override int SectionVersion => FactionCampaignWorldSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn => new[]
    {
        FactionSaveSection.Id,
        SeasonalWorldEventsSaveSection.Id,
        SocietyEventsSaveSection.Id,
        OffenseAggregateSaveSection.Id,
        PhysicalItemsSaveSection.Id
    };
    protected override FactionCampaignWorldSaveData CapturePayload() => persistence.CaptureFactions();
    protected override FactionCampaignRestoreCandidate BuildRestoreCandidate(
        FactionCampaignWorldSaveData payload)
    {
        ValidatePhysicalRestoreJoin(payload, physicalCandidates);
        FactionCampaignAggregateState campaign =
            persistence.PrepareFactionsRestore(payload);
        IReadOnlyList<EconomyProjectInputOwnerDescriptor> descriptors =
            BuildInputOwnerDescriptors(payload);
        if (!inputOwners.TryValidateForRestore(
                EconomyProjectInputOwnerAuthority.FactionContractDomain,
                descriptors,
                out string ownerFailure))
        {
            throw new InvalidOperationException(
                "Faction-contract input-owner restore join failed: "
                + ownerFailure);
        }
        return new FactionCampaignRestoreCandidate(campaign, descriptors);
    }

    protected override void ValidateParsedPayload(
        FactionCampaignWorldSaveData payload)
    {
        _ = persistence.PrepareFactionsPreflight(payload)
            ?? throw new InvalidOperationException(
                "Faction campaign restore candidate builder returned null.");
    }

    protected override void PublishRestoreCandidate(
        FactionCampaignRestoreCandidate candidate)
    {
        FactionCampaignRestoreCandidate required = candidate
            ?? throw new ArgumentNullException(nameof(candidate));
        if (!inputOwners.TryReplaceForRestore(
                EconomyProjectInputOwnerAuthority.FactionContractDomain,
                required.InputOwners,
                out string ownerFailure))
        {
            throw new InvalidOperationException(
                "Faction-contract input-owner restore publication failed: "
                + ownerFailure);
        }
        persistence.PublishFactions(required.Campaign);
    }

    private IReadOnlyList<EconomyProjectInputOwnerDescriptor>
        BuildInputOwnerDescriptors(FactionCampaignWorldSaveData payload) =>
        (payload?.factions ?? new List<FactionCampaignStateSaveData>())
            .Where(value => value != null
                && value.activeContractInputOwnerActive)
            .OrderBy(
                value => value.activeContractDestinationId,
                StringComparer.Ordinal)
            .Select(value => new EconomyProjectInputOwnerDescriptor(
                EconomyProjectInputOwnerAuthority.FactionContractDomain,
                value.activeContractDestinationOwnerId,
                value.activeContractDestinationId,
                new UnityEngine.Vector2Int(
                    value.activeContractDestinationX,
                    value.activeContractDestinationY),
                FacilityBufferDestinationAnchorKind.ReservedTarget,
                string.Empty,
                BuildMaterialRequirements(value),
                value.activeContractInputCapacityGrams,
                value.activeContractInputMassAuthorityRevision,
                value.activeContractInputCapacityFingerprint))
            .ToArray();

    private IReadOnlyDictionary<string, int> BuildMaterialRequirements(
        FactionCampaignStateSaveData state)
    {
        if (!FactionContractDeliveryOutbox.TryGetContractIdFromOwnerId(
                state.activeContractDestinationOwnerId,
                out string contractId))
            throw new InvalidOperationException(
                "Faction-contract input owner has invalid provenance.");
        FactionContractDefinitionSO contract = catalog.Contracts.Single(value =>
            string.Equals(
                value.StableId,
                contractId,
                StringComparison.Ordinal)
            && string.Equals(
                value.factionId,
                state.factionId,
                StringComparison.Ordinal));
        return FactionContractMaterialRules.BuildMaterialRequirements(contract);
    }

    public static void ValidatePhysicalRestoreJoin(
        FactionCampaignWorldSaveData payload,
        IPhysicalItemRestoreCandidateQuery query)
    {
        if (payload?.factions == null
            || query == null
            || !query.IsCandidateAvailable)
        {
            throw new InvalidOperationException(
                "Faction-contract restore requires campaign and physical candidates.");
        }
        Dictionary<string, FactionCampaignStateSaveData> owners = payload.factions
            .Where(FactionContractDeliveryOutbox.HasPending)
            .ToDictionary(
                value => value.deliveryOperationId,
                StringComparer.Ordinal);
        foreach (KeyValuePair<string, FactionCampaignStateSaveData> owner in owners)
        {
            if (!query.TryGetPendingBatchDisposition(
                    owner.Key,
                    out PhysicalItemRestoreCandidateDispositionSnapshot receipt)
                || !Matches(owner.Value, receipt))
            {
                throw new InvalidOperationException(
                    $"Faction-contract delivery '{owner.Key}' has no exact incoming physical Transfer receipt.");
            }
        }
        foreach (PhysicalItemRestoreCandidateDispositionSnapshot receipt in
                 query.PendingBatchDispositions)
        {
            if (receipt?.OperationId == null
                || !FactionContractDeliveryOutbox.IsOwnedOperationId(
                    receipt.OperationId))
                continue;
            if (!owners.TryGetValue(
                    receipt.OperationId,
                    out FactionCampaignStateSaveData owner)
                || !Matches(owner, receipt))
            {
                throw new InvalidOperationException(
                    $"Incoming faction-contract Transfer '{receipt.OperationId}' has no exact campaign owner.");
            }
        }
    }

    private static bool Matches(
        FactionCampaignStateSaveData owner,
        PhysicalItemRestoreCandidateDispositionSnapshot receipt) =>
        owner != null
        && receipt != null
        && receipt.Kind == PhysicalItemDispositionKind.Transfer
        && string.Equals(
            receipt.OperationId,
            owner.deliveryOperationId,
            StringComparison.Ordinal)
        && string.Equals(
            receipt.ReasonCode,
            FactionContractDeliveryOutbox.TransferReason,
            StringComparison.Ordinal)
        && string.Equals(
            receipt.CommitId,
            owner.deliveryCommitId,
            StringComparison.Ordinal)
        && receipt.Quantity == owner.deliveryQuantity
        && receipt.InputMassGrams == owner.deliveryMassGrams
        && (receipt.SourceStackIds ?? Array.Empty<string>())
            .SequenceEqual(
                owner.deliverySourceStackIds ?? new List<string>(),
                StringComparer.Ordinal);
}

public sealed class RunMilestonesSaveSection :
    DungeonStrictJsonSaveSection<RunMilestoneWorldSaveData, RunMilestoneAggregateState>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "run.milestones";
    private readonly IV20CampaignPersistence persistence;
    private readonly IPhysicalItemRestoreCandidateQuery physicalCandidates;
    public RunMilestonesSaveSection(
        IV20CampaignPersistence persistence,
        IPhysicalItemRestoreCandidateQuery physicalCandidates)
    {
        this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        this.physicalCandidates = physicalCandidates ?? throw new ArgumentNullException(nameof(physicalCandidates));
    }
    public override string SectionId => Id;
    public override int SectionVersion => RunMilestoneWorldSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.Presentation;
    public override IReadOnlyList<string> DependsOn => new[]
    {
        PhysicalItemsSaveSection.Id,
        BlueprintResearchSaveSection.Id,
        ProductionBillsSaveSection.Id,
        OffenseAggregateSaveSection.Id,
        FactionCampaignSaveSection.Id,
        CharacterCareerSaveSection.Id
    };
    protected override void ValidateRawPayload(string payloadJson) =>
        RequireTopLevelArrayFields(
            payloadJson,
            "committedChoices",
            "endlessCrisisAxes");
    protected override RunMilestoneWorldSaveData CapturePayload() => persistence.CaptureMilestones();
    protected override RunMilestoneAggregateState BuildRestoreCandidate(RunMilestoneWorldSaveData payload)
    {
        ValidateAccordSignalPhysicalJoin(payload, physicalCandidates);
        return persistence.PrepareMilestones(payload);
    }

    protected override void ValidateParsedPayload(
        RunMilestoneWorldSaveData payload)
    {
        _ = persistence.PrepareMilestones(payload)
            ?? throw new InvalidOperationException(
                "Run milestone restore candidate builder returned null.");
    }

    public static void ValidateAccordSignalPhysicalJoin(
        RunMilestoneWorldSaveData payload,
        IPhysicalItemRestoreCandidateQuery query)
    {
        const string prefix = "accord-signal-support:";
        const string reason = "alliance-signal-kit-consumed";
        if (payload == null || query == null || !query.IsCandidateAvailable)
            throw new InvalidOperationException("Run milestone restore requires the incoming physical candidate.");
        bool hasOwner = !string.IsNullOrEmpty(payload.pendingAccordSignalOperationId);
        if (hasOwner)
        {
            if (!query.TryGetPendingBatchDisposition(payload.pendingAccordSignalOperationId, out PhysicalItemRestoreCandidateDispositionSnapshot receipt)
                || receipt.Kind != PhysicalItemDispositionKind.Sink
                || !string.Equals(receipt.ReasonCode, reason, StringComparison.Ordinal)
                || !string.Equals(receipt.CommitId, payload.pendingAccordSignalCommitId, StringComparison.Ordinal)
                || receipt.Quantity != 1
                || receipt.InputMassGrams != payload.pendingAccordSignalMassGrams
                || receipt.SourceStackIds.Count != 1
                || !string.Equals(receipt.SourceStackIds[0], payload.pendingAccordSignalSourceStackId, StringComparison.Ordinal))
                throw new InvalidOperationException("Pending accord signal has no exact incoming physical Sink receipt.");
        }
        foreach (PhysicalItemRestoreCandidateDispositionSnapshot receipt in query.PendingBatchDispositions)
        {
            if (receipt?.OperationId == null || !receipt.OperationId.StartsWith(prefix, StringComparison.Ordinal)) continue;
            if (!hasOwner || !string.Equals(receipt.OperationId, payload.pendingAccordSignalOperationId, StringComparison.Ordinal))
                throw new InvalidOperationException($"Incoming accord signal Sink '{receipt.OperationId}' has no milestone owner.");
        }
    }
    protected override void PublishRestoreCandidate(RunMilestoneAggregateState candidate) => persistence.PublishMilestones(candidate);
}
