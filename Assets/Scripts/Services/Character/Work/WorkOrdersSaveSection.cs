using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

public sealed class WorkOrdersSaveSection :
    DungeonStrictJsonSaveSection<
        DungeonWorkOrderSaveData,
        WorkOrderRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "work.orders";

    private static readonly string[] Dependencies =
    {
        ModularFacilityWorldSaveSection.Id,
        PhysicalItemsSaveSection.Id
    };
    private readonly IWorkOrderRuntime runtime;
    private readonly IPhysicalItemRestoreCandidateQuery physicalCandidates;
    private readonly IPhysicalItemRestoreCandidateOutputQuery outputCandidates;

    public WorkOrdersSaveSection(
        IWorkOrderRuntime runtime,
        IPhysicalItemRestoreCandidateQuery physicalCandidates,
        IPhysicalItemRestoreCandidateOutputQuery outputCandidates)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        this.physicalCandidates = physicalCandidates
            ?? throw new ArgumentNullException(nameof(physicalCandidates));
        this.outputCandidates = outputCandidates
            ?? throw new ArgumentNullException(nameof(outputCandidates));
    }

    public override string SectionId => Id;
    public override int SectionVersion => DungeonWorkOrderSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.RuntimeState;
    public override IReadOnlyList<string> DependsOn => Dependencies;

    protected override DungeonWorkOrderSaveData CapturePayload()
    {
        return runtime.Capture();
    }

    protected override void ValidateRawPayload(string payloadJson) => ValidateAccidentFields(payloadJson);

    internal static void ValidateAccidentFields(string json)
    {
        var root = JObject.Parse(json);
        if (root["version"]?.Type != JTokenType.Integer
            || root.Value<int>("version") != DungeonWorkOrderSaveData.CurrentVersion
            || root["pendingAccidents"] is not JArray rows)
            throw new InvalidOperationException("work-accident-current-save-fields-missing");
        foreach (var token in rows)
        {
            if (token is not JObject row || row["fact"] is not JObject fact)
                throw new InvalidOperationException("work-accident-saved-fact-missing");
            Require(row, JTokenType.String, "publicationKey rollbackFailure");
            Require(row, JTokenType.Boolean, "observationCompleted identityNotificationAttempted rollbackBlocked");
            Require(fact, JTokenType.String,
                "operationId workerId workerDisplayName facilityId facilityDisplayName workTypeId anatomyNodeId observedCause fireTargetDisplayName");
            Require(fact, JTokenType.Float, "appliedDamage accidentMultiplier fireIntensity");
            Require(fact, JTokenType.Integer, "absoluteDay x y commandOrigin");
            Require(fact, JTokenType.Boolean, "fireEligible");
        }
        if (root["repairOutcomes"] is not JArray repairs)
            throw new InvalidOperationException("repair-work-current-save-fields-missing");
        foreach (var token in repairs)
        {
            if (token is not JObject repair)
                throw new InvalidOperationException("repair-work-saved-owner-missing");
            Require(repair, JTokenType.String, "publicationKey completionPublicationKey");
            Require(repair, JTokenType.Boolean, "publicationConfirmed");
        }
        if (root["orders"] is not JArray orders)
            throw new InvalidOperationException("construction-current-save-orders-missing");
        foreach (var token in orders)
        {
            if (token is not JObject order || order["terminalFact"] is not JObject fact)
                throw new InvalidOperationException("construction-saved-fact-missing");
            Require(order, JTokenType.String, "terminalPublicationKey");
            Require(order, JTokenType.Boolean, "terminalFactCaptured");
            Require(fact, JTokenType.String,
                "workOrderId placementOperationId constructionSitePersistentId buildingDefinitionId buildingDisplayName finalFacilityId finalFacilityDefinitionId replacedFacilityId replacedFacilityDefinitionId materialInputOperationId restitutionOperationId");
            Require(fact, JTokenType.Integer, "terminalKind targetBuildingId absoluteDay gridX gridY qualityTier");
            Require(fact, JTokenType.Float, "requiredWork completedWork");
            Require(fact, JTokenType.Array, "supportBuildingIds contributions itemMaterials");
            foreach (var entry in (JArray)fact["contributions"])
            {
                if (entry is not JObject contribution)
                    throw new InvalidOperationException("construction-saved-contribution-invalid");
                Require(contribution, JTokenType.String, "characterId");
                Require(contribution, JTokenType.Float, "contributedWork relevantSkill");
            }
            foreach (var entry in (JArray)fact["itemMaterials"])
            {
                if (entry is not JObject material)
                    throw new InvalidOperationException("construction-saved-material-invalid");
                Require(material, JTokenType.String, "itemId");
                Require(material, JTokenType.Integer, "required delivered");
            }
        }
        static void Require(JObject row, JTokenType type, string names)
        {
            foreach (string name in names.Split(' '))
                if (row[name]?.Type != type && !(type == JTokenType.Float && row[name]?.Type == JTokenType.Integer))
                    throw new InvalidOperationException("work-accident-save-field-missing:" + name);
        }
    }

    protected override void NormalizeRestorePayload(
        DungeonWorkOrderSaveData payload,
        DungeonGameRestoreReport report) =>
        V18WorkProductionCharacterReferenceRestoreNormalizer.Normalize(
            payload,
            (value, path) => NormalizeV18CharacterReference(value, report, path));

    protected override void ValidateParsedPayload(
        DungeonWorkOrderSaveData payload)
    {
        runtime.ValidateRestorePayload(payload);
    }

    protected override WorkOrderRestoreCandidate BuildRestoreCandidate(
        DungeonWorkOrderSaveData payload)
    {
        ValidatePhysicalRestoreCandidate(payload, physicalCandidates);
        ValidateRestitutionOutputCandidate(payload, outputCandidates);
        return runtime.PrepareRestoreCandidate(payload);
    }

    public static void ValidateRestitutionOutputCandidate(
        DungeonWorkOrderSaveData payload,
        IPhysicalItemRestoreCandidateOutputQuery query)
    {
        WorkOrderSaveData[] pending = (payload?.orders
                ?? new List<WorkOrderSaveData>())
            .Where(order => order?.materialTransfer?.phase ==
                WorkOrderMaterialTransferPhase.RestitutionPending)
            .OrderBy(order => order.workOrderId, StringComparer.Ordinal)
            .ToArray();
        if (query == null || !query.IsCandidateAvailable)
        {
            if (pending.Length == 0)
            {
                return;
            }
            throw new InvalidOperationException(
                "Work-order restitution restore requires the incoming output candidate.");
        }

        foreach (WorkOrderSaveData order in pending)
        {
            WorkOrderMaterialTransferSaveData owner = order.materialTransfer;
            string prefix = "physical-source:"
                + owner.restitutionOperationId + ":";
            PhysicalItemRestoreCandidateOutputSnapshot[] outputs =
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
            Dictionary<string, int> requirements = order.itemMaterials
                .ToDictionary(
                    value => value.itemId,
                    value => value.required,
                    StringComparer.Ordinal);
            long restoredMass = 0L;
            foreach (IGrouping<string,
                         PhysicalItemRestoreCandidateOutputSnapshot> group in
                     outputs.GroupBy(
                         output => output.CommitId,
                         StringComparer.Ordinal))
            {
                PhysicalItemRestoreCandidateOutputSnapshot first = group.First();
                long quantity = group.Sum(value => (long)value.Quantity);
                long mass = group.Sum(value => value.MassGrams);
                if (!requirements.TryGetValue(first.ItemId, out int required)
                    || group.Any(value => !string.Equals(
                            value.ItemId,
                            first.ItemId,
                            StringComparison.Ordinal)
                        || value.State != WorldItemStackState.Loose
                        || value.Position.x != order.gridX
                        || value.Position.y != order.gridY
                        || !string.IsNullOrEmpty(value.DestinationId))
                    || quantity != required
                    || mass <= 0L
                    || !string.Equals(
                        group.Key,
                        prefix + first.ItemId + ":" + required + ":" + mass,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Work-order restitution has an invalid incoming physical output: "
                        + owner.restitutionOperationId);
                }
                requirements.Remove(first.ItemId);
                restoredMass = checked(restoredMass + mass);
            }
            if (restoredMass > owner.inputMassGrams
                || requirements.Count == 0
                    && restoredMass != owner.inputMassGrams)
            {
                throw new InvalidOperationException(
                    "Work-order restitution output mass does not match its input custody: "
                    + owner.restitutionOperationId);
            }
        }
    }

    protected override void PublishRestoreCandidate(
        WorkOrderRestoreCandidate candidate)
    {
        runtime.PublishRestoreCandidate(candidate);
    }

    public static void ValidatePhysicalRestoreCandidate(
        DungeonWorkOrderSaveData payload,
        IPhysicalItemRestoreCandidateQuery query)
    {
        WorkOrderMaterialTransferSaveData[] pendingOwners = (payload?.orders
                ?? new List<WorkOrderSaveData>())
            .Where(order => order?.materialTransfer?.phase is
                WorkOrderMaterialTransferPhase.InputCommitted
                or WorkOrderMaterialTransferPhase.CustodyPublished)
            .Select(order => order.materialTransfer)
            .OrderBy(owner => owner.operationId, StringComparer.Ordinal)
            .ToArray();
        if (query == null || !query.IsCandidateAvailable)
        {
            if (pendingOwners.Length == 0)
            {
                return;
            }
            throw new InvalidOperationException(
                "Work-order material restore requires the incoming item candidate.");
        }

        Dictionary<string, WorkOrderMaterialTransferSaveData> owners =
            pendingOwners.ToDictionary(
                owner => owner.operationId,
                StringComparer.Ordinal);
        foreach (KeyValuePair<string, WorkOrderMaterialTransferSaveData> pair in
                 owners)
        {
            if (!query.TryGetPendingBatchDisposition(
                    pair.Key,
                    out PhysicalItemRestoreCandidateDispositionSnapshot receipt)
                || !Matches(pair.Value, receipt))
            {
                throw new InvalidOperationException(
                    "Work-order material owner has no exact incoming Transfer receipt: "
                    + pair.Key);
            }
        }

        foreach (PhysicalItemRestoreCandidateDispositionSnapshot receipt in
                 query.PendingBatchDispositions
                 ?? Array.Empty<PhysicalItemRestoreCandidateDispositionSnapshot>())
        {
            if (receipt?.OperationId == null
                || !receipt.OperationId.StartsWith(
                    WorkOrderMaterialOutbox.OperationPrefix,
                    StringComparison.Ordinal))
            {
                continue;
            }
            if (!owners.TryGetValue(receipt.OperationId, out var owner)
                || !Matches(owner, receipt))
            {
                throw new InvalidOperationException(
                    "Incoming work-order material Transfer has no exact domain owner: "
                    + receipt.OperationId);
            }
        }
    }

    private static bool Matches(
        WorkOrderMaterialTransferSaveData owner,
        PhysicalItemRestoreCandidateDispositionSnapshot receipt) =>
        owner != null
        && receipt != null
        && receipt.Kind == PhysicalItemDispositionKind.Transfer
        && string.Equals(
            owner.operationId,
            receipt.OperationId,
            StringComparison.Ordinal)
        && string.Equals(
            owner.reasonCode,
            receipt.ReasonCode,
            StringComparison.Ordinal)
        && string.Equals(
            owner.requestFingerprint,
            receipt.RequestFingerprint,
            StringComparison.Ordinal)
        && string.Equals(
            owner.commitId,
            receipt.CommitId,
            StringComparison.Ordinal)
        && owner.inputQuantity == receipt.Quantity
        && owner.inputMassGrams == receipt.InputMassGrams
        && receipt.SourceStackIds.SequenceEqual(
            owner.sources.Select(source => source.stackId)
                .OrderBy(value => value, StringComparer.Ordinal),
            StringComparer.Ordinal);
}
