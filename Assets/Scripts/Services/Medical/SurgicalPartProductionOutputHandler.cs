using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public sealed class SurgicalPartProductionOutputHandler :
    IProductionOutputHandler,
    IIdempotentProductionOutputHandler
{
    public static readonly string ProstheticArmOutputId =
        SurgeryItemDefinitions.GetProstheticItemId("arm:left");
    public static readonly string ProstheticLegOutputId =
        SurgeryItemDefinitions.GetProstheticItemId("leg:left");
    public static readonly string ArtificialEyeOutputId =
        SurgeryItemDefinitions.GetProstheticItemId("eye:left");

    private const string PublicationOperationPrefix =
        "surgical-part-output-publication:";
    private const string OutputLinePrefix = "surgical-part-output-line:";

    private readonly ISurgicalPartPreparedOutputRuntime preparedParts;
    private readonly IItemDefinitionCatalog itemCatalog;
    private readonly IProductionAssemblyBridge bridge;
    private readonly IProductionOutputDestinationAuthorityRuntime destinations;
    private readonly ISurgicalPartOutputAdmissionPort admission;
    private readonly ISurgicalPartOutputPublicationPort publication;

    public SurgicalPartProductionOutputHandler(
        ISurgicalPartRuntime parts,
        IItemDefinitionCatalog itemCatalog,
        IProductionAssemblyBridge bridge,
        IProductionOutputDestinationAuthorityRuntime destinations,
        IFacilityBufferMassAdmissionService admission,
        IFacilityBufferPlannedOutputPublicationService publication)
    {
        preparedParts = parts as ISurgicalPartPreparedOutputRuntime
            ?? throw new ArgumentException(
                "Surgical-part runtime does not implement prepared-output custody.",
                nameof(parts));
        this.itemCatalog = itemCatalog
            ?? throw new ArgumentNullException(nameof(itemCatalog));
        this.bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        this.destinations = destinations
            ?? throw new ArgumentNullException(nameof(destinations));
        this.admission = new SurgicalPartOutputAdmissionPort(
            admission ?? throw new ArgumentNullException(nameof(admission)));
        this.publication = new SurgicalPartOutputPublicationPort(
            publication ?? throw new ArgumentNullException(nameof(publication)));
    }

    internal SurgicalPartProductionOutputHandler(
        ISurgicalPartPreparedOutputRuntime preparedParts,
        ISurgicalPartOutputAdmissionPort admission,
        ISurgicalPartOutputPublicationPort publication)
    {
        this.preparedParts = preparedParts
            ?? throw new ArgumentNullException(nameof(preparedParts));
        this.admission = admission ?? throw new ArgumentNullException(nameof(admission));
        this.publication = publication
            ?? throw new ArgumentNullException(nameof(publication));
    }

    public bool CanHandle(string itemId) =>
        string.Equals(itemId, ProstheticArmOutputId, StringComparison.Ordinal)
        || string.Equals(itemId, ProstheticLegOutputId, StringComparison.Ordinal)
        || string.Equals(itemId, ArtificialEyeOutputId, StringComparison.Ordinal);

    public bool TryProduce(
        ProductionOutputContext context,
        out string failureReason)
    {
        bool succeeded = TryProduceIdempotent(context, out DomainFailure failure);
        failureReason = succeeded ? string.Empty : failure.Code.ToString();
        return succeeded;
    }

    public bool TryProduceIdempotent(
        ProductionOutputContext context,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (!CanHandle(context.ItemId)
            || context.Amount != 1
            || context.Facility == null
            || !IsCanonicalRequired(context.CommitId))
        {
            failure = new DomainFailure(FailureCode.SurgeryPartUnavailable);
            return false;
        }

        ResolveDefinition(context.ItemId, out string nodeId, out SurgicalPartKind kind);
        string displayName = itemCatalog
            .GetRequired(new ItemDefinitionId(context.ItemId))
            .DisplayName;
        if (!preparedParts.TryPrepareCraftedOutput(
                context.ItemId,
                nodeId,
                displayName,
                kind,
                context.WorkerQuality,
                context.CommitId,
                out SurgicalPartPreparedOutput prepared,
                out failure))
        {
            return false;
        }
        if (prepared.IsReplay)
        {
            return preparedParts.TryValidateCommittedCraftedOutput(
                context.CommitId,
                requireAcknowledged: false,
                out _,
                out failure);
        }

        ProductionFacilityHandle facility;
        try
        {
            facility = bridge.CaptureFacility(context.Facility);
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException)
        {
            failure = Fail("facility-capture-failed", exception.Message);
            return false;
        }
        if (!destinations.TryValidate(
                facility,
                out FacilityBufferCapacityProfile profile,
                out string destinationFailure))
        {
            failure = Fail("output-destination-unavailable", destinationFailure);
            return false;
        }

        return TryPublishPreparedOutput(
            prepared,
            profile,
            facility.Position,
            out failure);
    }

    internal bool TryPublishPreparedOutputForEditorTest(
        SurgicalPartPreparedOutput prepared,
        FacilityBufferCapacityProfile profile,
        Vector2Int position,
        out DomainFailure failure) => TryPublishPreparedOutput(
        prepared,
        profile,
        position,
        out failure);

    private bool TryPublishPreparedOutput(
        SurgicalPartPreparedOutput prepared,
        FacilityBufferCapacityProfile profile,
        Vector2Int position,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (prepared == null || profile == null || prepared.IsReplay)
        {
            failure = Fail("prepared-output-request-invalid");
            return false;
        }
        ItemInstanceComponentSaveData component =
            SurgicalPartPreparedOutputComponentCodec.Create(prepared);
        string componentFingerprint = component.ToCanonicalString();
        SurgicalPartOutputCapacitySource capacitySource =
            SurgicalPartOutputCapacitySource.Capture(
                prepared,
                profile,
                position,
                componentFingerprint);
        FacilityBufferPlannedOutputRequest request = new(
            PublicationOperationPrefix + prepared.CommitId,
            prepared.CommitId,
            SurgicalPartPreparedOutputComponentCodec.Hash(componentFingerprint),
            profile.DestinationId,
            position,
            profile.OwnerDomain,
            profile.OwnerOperationId,
            profile.OwnerFacilityId,
            profile.CapacityRevision,
            new[]
            {
                new FacilityBufferPlannedOutputSlice(
                    OutputLinePrefix + prepared.ItemId,
                    PhysicalItemMassSubject.ForDefinition(
                        new ItemDefinitionId(prepared.ItemId)),
                    1,
                    new[] { component },
                    componentFingerprint)
            },
            capacitySource.Digest,
            capacitySource.RequiredMinimumCapacityGrams);
        if (!admission.TryReserve(
                request,
                out FacilityBufferPlannedOutputToken token,
                out FacilityBufferMassAdmissionFailureCode admissionCode,
                out string admissionFailure))
        {
            failure = new DomainFailure(
                admissionCode == FacilityBufferMassAdmissionFailureCode.CapacityUnavailable
                    ? FailureCode.ProductionOutputSpaceUnavailable
                    : FailureCode.ProductionOutputUnavailable,
                prepared.CommitId,
                admissionFailure);
            return false;
        }

        if (!publication.TryPublish(
                token,
                out FacilityBufferPlannedOutputPublicationReceipt published,
                out _,
                out string publicationFailure))
        {
            admission.TryRelease(token, out _, out _);
            failure = Fail("publication-failed", publicationFailure);
            return false;
        }

        if (!preparedParts.TryCommitCraftedOutput(prepared, published, out failure))
        {
            RollbackUncommitted(token, published, prepared, out string rollbackFailure);
            if (rollbackFailure.Length > 0)
                failure = Fail("runtime-join-rollback-failed", rollbackFailure);
            return false;
        }

        if (!admission.TryCommit(
                token,
                published,
                out FacilityBufferPlannedOutputReceipt committed,
                out _,
                out string commitFailure)
            || committed.CommittedMassGrams != token.ReservedMassGrams)
        {
            RollbackUncommitted(token, published, prepared, out string rollbackFailure);
            failure = Fail(
                "admission-commit-failed",
                commitFailure + (rollbackFailure.Length == 0
                    ? string.Empty
                    : $";rollback={rollbackFailure}"));
            return false;
        }
        return true;
    }

    public bool TryAcknowledge(string commitId, out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (!preparedParts.TryValidateCommittedCraftedOutput(
                commitId,
                requireAcknowledged: false,
                out SurgicalPartPublishedOutputSnapshot joined,
                out failure))
        {
            return false;
        }
        if (joined.Acknowledged)
            return true;

        if (!publication.TryCapturePending(
                commitId,
                out FacilityBufferPlannedOutputRestoreBatchSnapshot pending,
                out _,
                out string captureFailure)
            || pending.Stacks.Count != 1
            || !string.Equals(
                pending.Stacks[0].StackId,
                joined.StackId,
                StringComparison.Ordinal)
            || pending.Stacks[0].MassGrams != joined.MassGrams)
        {
            failure = Fail("acknowledgement-join-failed", captureFailure);
            return false;
        }
        if (!publication.TryAcknowledge(
                pending,
                out _,
                out string acknowledgementFailure))
        {
            failure = Fail("acknowledgement-failed", acknowledgementFailure);
            return false;
        }
        return preparedParts.TryValidateCommittedCraftedOutput(
            commitId,
            requireAcknowledged: true,
            out _,
            out failure);
    }

    public bool TryGetCommittedMassGrams(
        string commitId,
        out long massGrams,
        out DomainFailure failure)
    {
        massGrams = 0L;
        if (!preparedParts.TryValidateCommittedCraftedOutput(
                commitId,
                requireAcknowledged: false,
                out SurgicalPartPublishedOutputSnapshot joined,
                out failure))
        {
            return false;
        }
        massGrams = joined.MassGrams;
        return massGrams > 0L;
    }

    private void RollbackUncommitted(
        FacilityBufferPlannedOutputToken token,
        FacilityBufferPlannedOutputPublicationReceipt published,
        SurgicalPartPreparedOutput prepared,
        out string failureReason)
    {
        List<string> failures = new();
        if (!preparedParts.TryRollbackCraftedOutput(prepared, published, out string runtimeFailure))
            failures.Add(runtimeFailure);
        if (!publication.TryRollback(published, out _, out string publicationFailure))
            failures.Add(publicationFailure);
        if (!admission.TryRelease(token, out _, out string admissionFailure))
        {
            failures.Add(admissionFailure);
        }
        failureReason = string.Join(";", failures.Where(value => value.Length > 0));
    }

    private static DomainFailure Fail(string detail, string reason = "") =>
        new(FailureCode.ProductionOutputUnavailable, reason ?? string.Empty, detail);

    private static bool IsCanonicalRequired(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static void ResolveDefinition(
        string itemId,
        out string nodeId,
        out SurgicalPartKind kind)
    {
        kind = SurgicalPartKind.Prosthetic;
        if (string.Equals(itemId, ProstheticLegOutputId, StringComparison.Ordinal))
        {
            nodeId = "leg:left";
            return;
        }
        if (string.Equals(itemId, ArtificialEyeOutputId, StringComparison.Ordinal))
        {
            nodeId = "eye:left";
            kind = SurgicalPartKind.Implant;
            return;
        }
        nodeId = "arm:left";
    }
}

internal readonly struct SurgicalPartOutputCapacitySource
{
    internal const string SchemaToken =
        "surgical-part-output-capacity-source@1";

    private SurgicalPartOutputCapacitySource(
        string digest,
        long requiredMinimumCapacityGrams)
    {
        Digest = digest;
        RequiredMinimumCapacityGrams = requiredMinimumCapacityGrams;
    }

    internal string Digest { get; }
    internal long RequiredMinimumCapacityGrams { get; }

    internal static SurgicalPartOutputCapacitySource Capture(
        SurgicalPartPreparedOutput prepared,
        FacilityBufferCapacityProfile profile,
        Vector2Int position,
        string componentFingerprint)
    {
        if (prepared == null
            || profile == null
            || position != profile.DropPosition
            || string.IsNullOrWhiteSpace(componentFingerprint)
            || profile.MaxMassGrams <= 0L)
        {
            throw new InvalidOperationException(
                "Surgical-part capacity source is incomplete.");
        }

        CanonicalSemanticDigestBuilder canonical = new();
        canonical.Append(SchemaToken);
        canonical.Append(prepared.ItemId);
        canonical.Append(componentFingerprint);
        canonical.Append(profile.DestinationId);
        canonical.Append(profile.DropPosition.x);
        canonical.Append(profile.DropPosition.y);
        canonical.Append(profile.OwnerDomain);
        canonical.Append(profile.OwnerOperationId);
        canonical.Append(profile.OwnerFacilityId);
        canonical.Append(profile.CapacityRevision);
        canonical.Append(profile.MaxMassGrams);
        return new SurgicalPartOutputCapacitySource(
            canonical.ComputeSha256(),
            profile.MaxMassGrams);
    }
}

internal interface ISurgicalPartOutputAdmissionPort
{
    bool TryReserve(
        FacilityBufferPlannedOutputRequest request,
        out FacilityBufferPlannedOutputToken token,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason);
    bool TryCommit(
        FacilityBufferPlannedOutputToken token,
        FacilityBufferPlannedOutputPublicationReceipt publication,
        out FacilityBufferPlannedOutputReceipt receipt,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason);
    bool TryRelease(
        FacilityBufferPlannedOutputToken token,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason);
}

internal interface ISurgicalPartOutputPublicationPort
{
    bool TryPublish(
        FacilityBufferPlannedOutputToken token,
        out FacilityBufferPlannedOutputPublicationReceipt receipt,
        out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
        out string failureReason);
    bool TryRollback(
        FacilityBufferPlannedOutputPublicationReceipt receipt,
        out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
        out string failureReason);
    bool TryCapturePending(
        string batchCommitId,
        out FacilityBufferPlannedOutputRestoreBatchSnapshot candidate,
        out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
        out string failureReason);
    bool TryAcknowledge(
        FacilityBufferPlannedOutputRestoreBatchSnapshot candidate,
        out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
        out string failureReason);
}

internal sealed class SurgicalPartOutputAdmissionPort :
    ISurgicalPartOutputAdmissionPort
{
    private readonly IFacilityBufferMassAdmissionService inner;

    internal SurgicalPartOutputAdmissionPort(
        IFacilityBufferMassAdmissionService inner) =>
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));

    public bool TryReserve(
        FacilityBufferPlannedOutputRequest request,
        out FacilityBufferPlannedOutputToken token,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason) => inner.TryReservePlannedOutput(
        request,
        out token,
        out failureCode,
        out failureReason);

    public bool TryCommit(
        FacilityBufferPlannedOutputToken token,
        FacilityBufferPlannedOutputPublicationReceipt publication,
        out FacilityBufferPlannedOutputReceipt receipt,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason) => inner.TryCommitPlannedOutput(
        token,
        publication,
        out receipt,
        out failureCode,
        out failureReason);

    public bool TryRelease(
        FacilityBufferPlannedOutputToken token,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason) => inner.TryReleasePlannedOutput(
        token,
        FacilityBufferMassAdmissionReleaseReason.TransactionRollback,
        out failureCode,
        out failureReason);
}

internal sealed class SurgicalPartOutputPublicationPort :
    ISurgicalPartOutputPublicationPort
{
    private readonly IFacilityBufferPlannedOutputPublicationService inner;

    internal SurgicalPartOutputPublicationPort(
        IFacilityBufferPlannedOutputPublicationService inner) =>
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));

    public bool TryPublish(
        FacilityBufferPlannedOutputToken token,
        out FacilityBufferPlannedOutputPublicationReceipt receipt,
        out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
        out string failureReason) => inner.TryPublishFullBatch(
        token,
        out receipt,
        out failureCode,
        out failureReason);

    public bool TryRollback(
        FacilityBufferPlannedOutputPublicationReceipt receipt,
        out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
        out string failureReason) => inner.TryRollbackPublishedBatch(
        receipt,
        out failureCode,
        out failureReason);

    public bool TryCapturePending(
        string batchCommitId,
        out FacilityBufferPlannedOutputRestoreBatchSnapshot candidate,
        out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
        out string failureReason) => inner.TryCapturePendingBatch(
        batchCommitId,
        out candidate,
        out failureCode,
        out failureReason);

    public bool TryAcknowledge(
        FacilityBufferPlannedOutputRestoreBatchSnapshot candidate,
        out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
        out string failureReason) => inner.TryAcknowledgeRestoreCandidate(
        candidate,
        out failureCode,
        out failureReason);
}

internal sealed class SurgicalPartPreparedOutput
{
    internal string ItemId { get; set; }
    internal string PartInstanceId { get; set; }
    internal string NodeId { get; set; }
    internal string DisplayName { get; set; }
    internal SurgicalPartKind Kind { get; set; }
    internal float Quality { get; set; }
    internal string CommitId { get; set; }
    internal int ExpectedSequence { get; set; }
    internal bool IsReplay { get; set; }
}

internal readonly struct SurgicalPartPublishedOutputSnapshot
{
    internal SurgicalPartPublishedOutputSnapshot(
        string stackId,
        string itemInstanceId,
        long massGrams,
        bool acknowledged)
    {
        StackId = stackId;
        ItemInstanceId = itemInstanceId;
        MassGrams = massGrams;
        Acknowledged = acknowledged;
    }

    internal string StackId { get; }
    internal string ItemInstanceId { get; }
    internal long MassGrams { get; }
    internal bool Acknowledged { get; }
}

internal interface ISurgicalPartPreparedOutputRuntime
{
    bool TryPrepareCraftedOutput(
        string itemId,
        string nodeId,
        string displayName,
        SurgicalPartKind kind,
        float quality,
        string commitId,
        out SurgicalPartPreparedOutput prepared,
        out DomainFailure failure);
    bool TryCommitCraftedOutput(
        SurgicalPartPreparedOutput prepared,
        FacilityBufferPlannedOutputPublicationReceipt published,
        out DomainFailure failure);
    bool TryRollbackCraftedOutput(
        SurgicalPartPreparedOutput prepared,
        FacilityBufferPlannedOutputPublicationReceipt published,
        out string failureReason);
    bool TryValidateCommittedCraftedOutput(
        string commitId,
        bool requireAcknowledged,
        out SurgicalPartPublishedOutputSnapshot joined,
        out DomainFailure failure);
}

internal static class SurgicalPartPreparedOutputComponentCodec
{
    internal const string ComponentTypeId = "medical:surgical-part-output";
    private const string PartIdKey = "part-instance-id";
    private const string NodeIdKey = "node-id";
    private const string KindKey = "kind";
    private const string QualityKey = "quality";
    private const string CommitIdKey = "production-commit-id";

    internal static ItemInstanceComponentSaveData Create(
        SurgicalPartPreparedOutput prepared) => new()
    {
        componentTypeId = ComponentTypeId,
        schemaVersion = 1,
        affectsStacking = true,
        values = new List<ItemStateValueSaveData>
        {
            String(PartIdKey, prepared.PartInstanceId),
            String(NodeIdKey, prepared.NodeId),
            Integer(KindKey, (int)prepared.Kind),
            Decimal(QualityKey, prepared.Quality),
            String(CommitIdKey, prepared.CommitId)
        }
    };

    internal static bool TryRead(
        IEnumerable<ItemInstanceComponentSaveData> components,
        out string partInstanceId,
        out string nodeId,
        out SurgicalPartKind kind,
        out float quality,
        out string commitId)
    {
        partInstanceId = string.Empty;
        nodeId = string.Empty;
        kind = default;
        quality = 0f;
        commitId = string.Empty;
        ItemInstanceComponentSaveData[] matches = (components
                ?? Array.Empty<ItemInstanceComponentSaveData>())
            .Where(value => value != null
                && string.Equals(value.componentTypeId, ComponentTypeId, StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1
            || matches[0].schemaVersion != 1
            || !matches[0].affectsStacking)
        {
            return false;
        }
        IReadOnlyList<ItemStateValueSaveData> values = matches[0].values
            ?? new List<ItemStateValueSaveData>();
        if (!TryString(values, PartIdKey, out partInstanceId)
            || !TryString(values, NodeIdKey, out nodeId)
            || !TryInteger(values, KindKey, out long kindValue)
            || kindValue < int.MinValue
            || kindValue > int.MaxValue
            || !Enum.IsDefined(typeof(SurgicalPartKind), (int)kindValue)
            || !TryDecimal(values, QualityKey, out double qualityValue)
            || qualityValue < 0.1d
            || qualityValue > 1.75d
            || !TryString(values, CommitIdKey, out commitId))
        {
            return false;
        }
        kind = (SurgicalPartKind)kindValue;
        quality = (float)qualityValue;
        return true;
    }

    internal static string Hash(string canonical)
    {
        using SHA256 sha = SHA256.Create();
        byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical ?? string.Empty));
        StringBuilder text = new(bytes.Length * 2);
        foreach (byte value in bytes)
            text.Append(value.ToString("x2", CultureInfo.InvariantCulture));
        return text.ToString();
    }

    private static ItemStateValueSaveData String(string key, string value) => new()
    {
        key = key,
        kind = ItemStateValueKind.String,
        stringValue = value ?? string.Empty
    };

    private static ItemStateValueSaveData Integer(string key, long value) => new()
    {
        key = key,
        kind = ItemStateValueKind.Integer,
        integerValue = value
    };

    private static ItemStateValueSaveData Decimal(string key, double value) => new()
    {
        key = key,
        kind = ItemStateValueKind.Decimal,
        decimalValue = value
    };

    private static bool TryString(
        IEnumerable<ItemStateValueSaveData> values,
        string key,
        out string result)
    {
        ItemStateValueSaveData[] found = values.Where(value => value != null
                && value.kind == ItemStateValueKind.String
                && string.Equals(value.key, key, StringComparison.Ordinal))
            .ToArray();
        result = found.Length == 1 ? found[0].stringValue ?? string.Empty : string.Empty;
        return found.Length == 1 && IsCanonicalRequired(result);
    }

    private static bool TryInteger(
        IEnumerable<ItemStateValueSaveData> values,
        string key,
        out long result)
    {
        ItemStateValueSaveData[] found = values.Where(value => value != null
                && value.kind == ItemStateValueKind.Integer
                && string.Equals(value.key, key, StringComparison.Ordinal))
            .ToArray();
        result = found.Length == 1 ? found[0].integerValue : 0L;
        return found.Length == 1;
    }

    private static bool TryDecimal(
        IEnumerable<ItemStateValueSaveData> values,
        string key,
        out double result)
    {
        ItemStateValueSaveData[] found = values.Where(value => value != null
                && value.kind == ItemStateValueKind.Decimal
                && string.Equals(value.key, key, StringComparison.Ordinal))
            .ToArray();
        result = found.Length == 1 ? found[0].decimalValue : 0d;
        return found.Length == 1 && !double.IsNaN(result) && !double.IsInfinity(result);
    }

    private static bool IsCanonicalRequired(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}
