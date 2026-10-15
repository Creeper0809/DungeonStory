using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using VContainer;

public sealed class ProductionBillsSaveSection :
    IDungeonSaveSection,
    IDungeonSaveSectionPreflight,
    IDungeonStagedSaveSection,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "economy.production-bills";

    private static readonly string[] Dependencies =
    {
        PhysicalItemsSaveSection.Id,
        ModularFacilityWorldSaveSection.Id
    };

    private readonly IProductionBillDetachedFacilityPersistence persistence;
    private readonly IPhysicalItemRestoreCandidateQuery physicalCandidates;
    private readonly IPhysicalItemRestoreCandidateOutputQuery outputCandidates;
    private readonly IProductionPreparedOutputRestoreJoin preparedOutputJoin;
    private readonly IProductionExactCapabilityOutputRestoreJoin exactOutputJoin;
    private readonly IProductionOutputLifecycleRestoreCandidatePublisher
        lifecycleRestoreCandidates;
    private readonly IRestoreWorldCandidateQuery restoreWorldCandidates;
    private readonly IProductionFacilityHandleQuery facilityHandles;

    public ProductionBillsSaveSection(
        IProductionBillDetachedFacilityPersistence persistence,
        IPhysicalItemRestoreCandidateQuery physicalCandidates,
        IPhysicalItemRestoreCandidateOutputQuery outputCandidates,
        IProductionPreparedOutputRestoreJoin preparedOutputJoin,
        IProductionOutputLifecycleRestoreCandidatePublisher
            lifecycleRestoreCandidates,
        IRestoreWorldCandidateQuery restoreWorldCandidates,
        IProductionFacilityHandleQuery facilityHandles)
        : this(
            persistence,
            physicalCandidates,
            outputCandidates,
            preparedOutputJoin,
            lifecycleRestoreCandidates,
            EmptyProductionExactCapabilityOutputRestoreJoin.Instance,
            restoreWorldCandidates,
            facilityHandles)
    {
    }

    [Inject]
    public ProductionBillsSaveSection(
        IProductionBillDetachedFacilityPersistence persistence,
        IPhysicalItemRestoreCandidateQuery physicalCandidates,
        IPhysicalItemRestoreCandidateOutputQuery outputCandidates,
        IProductionPreparedOutputRestoreJoin preparedOutputJoin,
        IProductionOutputLifecycleRestoreCandidatePublisher
            lifecycleRestoreCandidates,
        IProductionExactCapabilityOutputRestoreJoin exactOutputJoin,
        IRestoreWorldCandidateQuery restoreWorldCandidates,
        IProductionFacilityHandleQuery facilityHandles)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
        this.physicalCandidates = physicalCandidates
            ?? throw new ArgumentNullException(nameof(physicalCandidates));
        this.outputCandidates = outputCandidates
            ?? throw new ArgumentNullException(nameof(outputCandidates));
        this.preparedOutputJoin = preparedOutputJoin
            ?? throw new ArgumentNullException(nameof(preparedOutputJoin));
        this.exactOutputJoin = exactOutputJoin
            ?? throw new ArgumentNullException(nameof(exactOutputJoin));
        this.lifecycleRestoreCandidates = lifecycleRestoreCandidates
            ?? throw new ArgumentNullException(nameof(lifecycleRestoreCandidates));
        this.restoreWorldCandidates = restoreWorldCandidates
            ?? throw new ArgumentNullException(nameof(restoreWorldCandidates));
        this.facilityHandles = facilityHandles
            ?? throw new ArgumentNullException(nameof(facilityHandles));
    }

    public string SectionId => Id;
    public int SectionVersion => DungeonProductionBillSaveData.CurrentVersion;
    public DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.RuntimeState;
    public IReadOnlyList<string> DependsOn => Dependencies;

    public string Capture() => JsonUtility.ToJson(persistence.Capture());

    public void ValidatePayload(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        RequireVersion(sectionVersion);
        // Registry preflight runs before any dependency is staged. Validate
        // this section's own current-format payload here; the Physical ->
        // Production cross-section joins remain fail-loud in StageRestore,
        // after the physical dependency has published its detached candidate.
        DungeonProductionBillSaveData payload = Parse(payloadJson, report);
        _ = persistence.BuildRestore(payload);
    }

    public void Restore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        IDungeonSaveRestoreStage stage = StageRestore(
            payloadJson,
            sectionVersion,
            report);
        if (report.Success)
        {
            stage.Commit(report);
        }
    }

    public IDungeonSaveRestoreStage StageRestore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        RequireVersion(sectionVersion);
        PreparedOutputRestoreStageCandidate candidate =
            BuildCandidate(payloadJson, report);
        return new DungeonDelegateSaveRestoreStage(
            SectionId,
            _ =>
            {
                lifecycleRestoreCandidates.SetProduction(
                    candidate.PreparedOutput.NormalizedPayload);
                persistence.Restore(
                    candidate.Bills,
                    candidate.DetachedFacilities);
                preparedOutputJoin.Acknowledge(candidate.PreparedOutput);
            });
    }

    private void RequireVersion(int sectionVersion)
    {
        if (sectionVersion != SectionVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported {SectionId} section version {sectionVersion}; expected {SectionVersion}.");
        }
    }

    private DungeonProductionBillSaveData Parse(
        string payloadJson,
        DungeonGameRestoreReport report)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            throw new InvalidOperationException(
                $"{SectionId} payload is empty.");
        }
        try
        {
            ValidateCommandObservationsJson(payloadJson);
            DungeonProductionBillSaveData payload =
                JsonUtility.FromJson<DungeonProductionBillSaveData>(payloadJson)
                ?? throw new InvalidOperationException(
                    $"{SectionId} payload deserialized to null.");
            V18WorkProductionCharacterReferenceRestoreNormalizer.Normalize(
                payload,
                (value, path) =>
                    V18TypedCharacterReferenceRestoreNormalizer
                        .RewriteLegacyReference(
                            value,
                            report,
                            SectionId,
                            path));
            return payload;
        }
        catch (Exception exception) when (exception is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"{SectionId} payload JSON is invalid: {exception.Message}",
                exception);
        }
    }

    public static void ValidateCommandObservationsJson(string json)
    {
        var root = JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        RequireCommandField(root, "nextCommandSequence", JTokenType.Integer);
        RequireCommandField(root, "pendingCommandObservations", JTokenType.Array);
        RequireCommandField(root, "bills", JTokenType.Array);
        foreach (var billToken in (JArray)root["bills"])
        {
            if (billToken is not JObject bill) throw new InvalidOperationException("production-bill-save-row-invalid");
            RequireCommandField(bill, "completionObservations", JTokenType.Array);
            foreach (var completionToken in (JArray)bill["completionObservations"])
            {
                if (completionToken is not JObject completion) throw new InvalidOperationException("production-completion-save-row-invalid");
                foreach (string name in new[] { "publicationKey", "publicationId", "publicationFingerprint" }) RequireCommandField(completion, name, JTokenType.String);
                RequireCommandField(completion, "fact", JTokenType.Object);
                var fact = (JObject)completion["fact"];
                foreach (string name in new[] { "billId", "recipeId", "facilityId", "facilityDisplayName", "workerId", "workerDisplayName", "batchCommitId", "outcomeFingerprint", "plannedOutputFingerprint", "destinationId", "wipInputCommitId" }) RequireCommandField(fact, name, JTokenType.String);
                foreach (string name in new[] { "cycleSequence", "absoluteDay", "wipInputQuantity", "wipInputMassGrams", "processCleanWaterMassGrams", "processWastewaterMassGrams", "declaredLossMassGrams", "declaredExternalInputMassGrams" }) RequireCommandField(fact, name, JTokenType.Integer);
                foreach (string name in new[] { "lines", "stacks" }) RequireCommandField(fact, name, JTokenType.Array);
                foreach (var lineToken in (JArray)fact["lines"])
                {
                    if (lineToken is not JObject line) throw new InvalidOperationException("production-completion-line-row-invalid");
                    foreach (string name in new[] { "outputLineId", "itemId", "capabilityFingerprint" }) RequireCommandField(line, name, JTokenType.String);
                    RequireCommandField(line, "commitIds", JTokenType.Array);
                    if (((JArray)line["commitIds"]).Any(value => value.Type != JTokenType.String)) throw new InvalidOperationException("production-completion-commit-id-invalid");
                    foreach (string name in new[] { "quantity", "massGrams" }) RequireCommandField(line, name, JTokenType.Integer);
                }
                foreach (var stackToken in (JArray)fact["stacks"])
                {
                    if (stackToken is not JObject stack) throw new InvalidOperationException("production-completion-stack-row-invalid");
                    foreach (string name in new[] { "stackId", "outputLineId", "itemId", "itemInstanceId", "sourceCommitId", "componentSignature" }) RequireCommandField(stack, name, JTokenType.String);
                    foreach (string name in new[] { "quantity", "massGrams" }) RequireCommandField(stack, name, JTokenType.Integer);
                }
            }
        }
        foreach (var token in (JArray)root["pendingCommandObservations"])
        {
            if (token is not JObject row) throw new InvalidOperationException("production-command-observation-row-invalid");
            foreach (string name in new[] { "publicationKey", "publicationId", "publicationFingerprint" }) RequireCommandField(row, name, JTokenType.String);
            RequireCommandField(row, "fact", JTokenType.Object);
            var fact = (JObject)row["fact"];
            foreach (string name in new[] { "ownerRevision", "kind", "facilityX", "facilityY", "absoluteDay" }) RequireCommandField(fact, name, JTokenType.Integer);
            foreach (string name in new[] { "billId", "recipeId", "recipeDisplayName", "facilityId", "facilityDisplayName" }) RequireCommandField(fact, name, JTokenType.String);
            foreach (string side in new[] { "before", "after" })
            {
                RequireCommandField(fact, side, JTokenType.Object);
                var state = (JObject)fact[side];
                foreach (string name in new[] { "mode", "pendingMode", "remainingCycles", "minimumReserve", "targetStock", "minimumQuality", "blockedCode", "localIndex", "distributionMode" }) RequireCommandField(state, name, JTokenType.Integer);
                foreach (string name in new[] { "exists", "hasPendingMode", "suspended", "sensorInstalled", "sensorAcknowledged" }) RequireCommandField(state, name, JTokenType.Boolean);
                foreach (string name in new[] { "reservedWorkerId", "emergencyWorkerId" }) RequireCommandField(state, name, JTokenType.String);
                RequireCommandField(state, "routes", JTokenType.Array);
                RequireCommandField(state, "workerPolicy", JTokenType.Object);
                foreach (var routeToken in (JArray)state["routes"])
                {
                    if (routeToken is not JObject route) throw new InvalidOperationException("production-command-route-invalid");
                    RequireCommandField(route, "consumerId", JTokenType.String);
                    RequireCommandField(route, "enabled", JTokenType.Boolean);
                    foreach (string name in new[] { "minimumReserve", "targetStock", "priority", "weight" }) RequireCommandField(route, name, JTokenType.Integer);
                    if (route["waitingSeconds"]?.Type is not JTokenType.Float and not JTokenType.Integer)
                        throw new InvalidOperationException("production-command-route-time-invalid");
                }
                var worker = (JObject)state["workerPolicy"];
                foreach (string name in new[] { "mode", "matchMode", "sortMode", "minimumSkillExperience", "minimumCareerRank" }) RequireCommandField(worker, name, JTokenType.Integer);
                RequireCommandField(worker, "minimumSkillId", JTokenType.String);
                foreach (string name in new[] { "specificCharacterIds", "excludedCharacterIds", "requiredTraitIds", "excludedTraitIds" })
                {
                    RequireCommandField(worker, name, JTokenType.Array);
                    if (((JArray)worker[name]).Any(value => value.Type != JTokenType.String))
                        throw new InvalidOperationException("production-command-worker-list-invalid:" + name);
                }
            }
        }
    }

    private static void RequireCommandField(JObject row, string field, JTokenType type)
    { if (row[field]?.Type != type) throw new InvalidOperationException("production-command-save-field-invalid:" + field); }

    private PreparedOutputRestoreStageCandidate BuildCandidate(
        string payloadJson,
        DungeonGameRestoreReport report)
    {
        DungeonProductionBillSaveData payload = Parse(payloadJson, report);
        ValidateStockSensorPhysicalRestoreCandidate(
            payload,
            physicalCandidates);
        ValidateStockSensorRemovalOutputCandidate(payload, outputCandidates);
        ProductionPreparedOutputRestoreJoinPlan prepared =
            preparedOutputJoin.Build(payload);
        exactOutputJoin.Validate(prepared.NormalizedPayload);
        ProductionFacilityHandle[] detachedFacilities =
            CaptureDetachedProductionFacilities(prepared.NormalizedPayload);
        return new PreparedOutputRestoreStageCandidate(
            persistence.BuildRestore(prepared.NormalizedPayload),
            prepared,
            detachedFacilities);
    }

    private ProductionFacilityHandle[] CaptureDetachedProductionFacilities(
        DungeonProductionBillSaveData payload)
    {
        if (!restoreWorldCandidates.TryGetBuildings(
                out IReadOnlyList<BuildableObject> candidateBuildings)
            || candidateBuildings == null)
        {
            throw new InvalidOperationException(
                "Production restore requires the detached facility-world candidate.");
        }

        ProductionFacilityHandle[] facilities = candidateBuildings
            .Where(ProductionFacilityDefinitionIdentity.IsProductionWorkstation)
            .Select(value => facilityHandles.CaptureFacility(value)
                ?? throw new InvalidOperationException(
                    "Production restore projected a null facility handle."))
            .OrderBy(value => value.InstanceId.Value, StringComparer.Ordinal)
            .ToArray();
        if (facilities.Select(value => value.InstanceId.Value)
            .Distinct(StringComparer.Ordinal).Count() != facilities.Length)
        {
            throw new InvalidOperationException(
                "Production restore contains duplicate detached facility identities.");
        }

        HashSet<string> facilityIds = facilities
            .Select(value => value.InstanceId.Value)
            .ToHashSet(StringComparer.Ordinal);
        ProductionBillSaveData orphan = (payload?.bills
                ?? new List<ProductionBillSaveData>())
            .Where(value => value != null)
            .OrderBy(value => value.billId, StringComparer.Ordinal)
            .FirstOrDefault(value => !facilityIds.Contains(
                value.buildingInstanceId));
        if (orphan != null)
        {
            throw new InvalidOperationException(
                $"Production bill '{orphan.billId}' has no detached facility '{orphan.buildingInstanceId}'.");
        }
        return facilities;
    }

    private sealed class PreparedOutputRestoreStageCandidate
    {
        internal PreparedOutputRestoreStageCandidate(
            ProductionBillRestoreCandidate bills,
            ProductionPreparedOutputRestoreJoinPlan preparedOutput,
            IReadOnlyList<ProductionFacilityHandle> detachedFacilities)
        {
            Bills = bills ?? throw new ArgumentNullException(nameof(bills));
            PreparedOutput = preparedOutput
                ?? throw new ArgumentNullException(nameof(preparedOutput));
            DetachedFacilities = (detachedFacilities
                    ?? throw new ArgumentNullException(nameof(detachedFacilities)))
                .ToArray();
        }

        internal ProductionBillRestoreCandidate Bills { get; }
        internal ProductionPreparedOutputRestoreJoinPlan PreparedOutput { get; }
        internal IReadOnlyList<ProductionFacilityHandle> DetachedFacilities
            { get; }
    }

    public static void ValidateStockSensorRemovalOutputCandidate(
        DungeonProductionBillSaveData payload,
        IPhysicalItemRestoreCandidateOutputQuery query)
    {
        if (payload?.pendingStockSensorRemovals == null)
            throw new InvalidOperationException(
                "Production stock-sensor restore has no removal owner collection.");
        if (query == null || !query.IsCandidateAvailable)
            throw new InvalidOperationException(
                "Production stock-sensor removal restore requires the incoming output candidate.");

        foreach (ProductionStockSensorRemovalSaveData owner in
                 payload.pendingStockSensorRemovals)
        {
            string prefix = "physical-source:" + owner.operationId + ":";
            PhysicalItemRestoreCandidateOutputSnapshot[] candidates =
                (query.CommittedOutputs
                    ?? Array.Empty<
                        PhysicalItemRestoreCandidateOutputSnapshot>())
                .Where(output => output != null
                    && output.CommitId.StartsWith(
                        prefix,
                        StringComparison.Ordinal))
                .OrderBy(output => output.CommitId, StringComparer.Ordinal)
                .ThenBy(output => output.StackId, StringComparer.Ordinal)
                .ToArray();
            if (owner.phase == ProductionStockSensorRemovalPhase.Prepared)
            {
                if (candidates.Length != 0)
                    throw new InvalidOperationException(
                        "Prepared stock-sensor removal already has an incoming physical output: "
                        + owner.operationId);
                continue;
            }

            string expectedCommit =
                ProductionStockSensorRuntime.BuildRemovalOutputCommitId(owner);
            if (owner.phase is not
                    ProductionStockSensorRemovalPhase.OutputPublished
                    and not ProductionStockSensorRemovalPhase
                        .OwnerAcknowledgedAwaitingCheckpointGc
                || owner.outputCommitIds == null
                || owner.outputCommitIds.Count != 1
                || !string.Equals(
                    owner.outputCommitIds[0],
                    expectedCommit,
                    StringComparison.Ordinal)
                || !query.TryGetCommittedOutput(
                    expectedCommit,
                    out IReadOnlyList<
                        PhysicalItemRestoreCandidateOutputSnapshot> exact)
                || exact == null
                || exact.Count == 0
                || candidates.Length != exact.Count
                || exact.Any(output => output == null
                    || !string.Equals(
                        output.ItemId,
                        owner.itemId,
                        StringComparison.Ordinal)
                    || output.State != WorldItemStackState.Loose
                    || output.Position.x != owner.outputPositionX
                    || output.Position.y != owner.outputPositionY
                    || !string.IsNullOrEmpty(output.DestinationId))
                || exact.Sum(output => (long)output.Quantity)
                    != owner.outputQuantity
                || exact.Sum(output => output.MassGrams)
                    != owner.outputMassGrams)
            {
                throw new InvalidOperationException(
                    "Published stock-sensor removal has no exact incoming physical output: "
                    + owner.operationId);
            }
        }
    }

    public static void ValidateStockSensorPhysicalRestoreCandidate(
        DungeonProductionBillSaveData payload,
        IPhysicalItemRestoreCandidateQuery query)
    {
        if (payload?.pendingStockSensorInstalls == null)
            throw new InvalidOperationException(
                "Production stock-sensor restore has no physical owner collection.");
        if (query == null || !query.IsCandidateAvailable)
            throw new InvalidOperationException(
                "Production stock-sensor restore requires the incoming item candidate.");
        Dictionary<string, ProductionStockSensorPhysicalCommitSaveData> owners =
            payload.pendingStockSensorInstalls.ToDictionary(
                owner => owner.operationId,
                StringComparer.Ordinal);
        foreach (KeyValuePair<string, ProductionStockSensorPhysicalCommitSaveData>
                 pair in owners)
        {
            if (!query.TryGetPendingBatchDisposition(
                    pair.Key,
                    out PhysicalItemRestoreCandidateDispositionSnapshot receipt)
                || !Matches(pair.Value, receipt))
                throw new InvalidOperationException(
                    "Production stock-sensor owner has no exact incoming Sink receipt: "
                    + pair.Key);
        }
        foreach (PhysicalItemRestoreCandidateDispositionSnapshot receipt in
                 query.PendingBatchDispositions
                 ?? Array.Empty<PhysicalItemRestoreCandidateDispositionSnapshot>())
        {
            if (receipt?.OperationId == null
                || !receipt.OperationId.StartsWith(
                    ProductionStockSensorRuntime.PhysicalOperationPrefix,
                    StringComparison.Ordinal))
                continue;
            if (!owners.TryGetValue(receipt.OperationId, out var owner)
                || !Matches(owner, receipt))
                throw new InvalidOperationException(
                    "Incoming production stock-sensor Sink has no exact owner: "
                    + receipt.OperationId);
        }
    }

    private static bool Matches(
        ProductionStockSensorPhysicalCommitSaveData owner,
        PhysicalItemRestoreCandidateDispositionSnapshot receipt) =>
        owner != null
        && receipt != null
        && receipt.Kind == PhysicalItemDispositionKind.Sink
        && string.Equals(owner.operationId, receipt.OperationId, StringComparison.Ordinal)
        && string.Equals(owner.reasonCode, receipt.ReasonCode, StringComparison.Ordinal)
        && string.Equals(owner.requestFingerprint, receipt.RequestFingerprint, StringComparison.Ordinal)
        && string.Equals(owner.commitId, receipt.CommitId, StringComparison.Ordinal)
        && owner.inputQuantity == receipt.Quantity
        && owner.inputMassGrams == receipt.InputMassGrams
        && receipt.SourceStackIds.SequenceEqual(
            owner.sourceStackIds,
            StringComparer.Ordinal);
}
