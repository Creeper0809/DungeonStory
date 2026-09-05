using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DungeonStory.Foundation;
using UnityEngine;

public readonly struct CaptivityInteractionMaterialMassDefinition
{
    public CaptivityInteractionMaterialMassDefinition(
        ItemDefinitionId itemId,
        StockCategory category,
        PhysicalMassGrams unitMass)
    {
        if (!itemId.IsValid || unitMass.Value <= 0L)
            throw new ArgumentException("A positive exact item mass is required.");
        ItemId = itemId;
        Category = category;
        UnitMass = unitMass;
    }

    public ItemDefinitionId ItemId { get; }
    public StockCategory Category { get; }
    public PhysicalMassGrams UnitMass { get; }
}

public interface ICaptivityInteractionMaterialMassCatalog
{
    long AuthorityRevision { get; }
    IReadOnlyList<CaptivityInteractionMaterialMassDefinition> CaptureAll();
}

public sealed class CaptivityInteractionMaterialMassCatalog :
    ICaptivityInteractionMaterialMassCatalog
{
    private readonly IItemDefinitionCatalog definitions;
    private readonly IPhysicalItemMassQuery mass;

    public CaptivityInteractionMaterialMassCatalog(
        IItemDefinitionCatalog definitions,
        IPhysicalItemMassQuery mass)
    {
        this.definitions = definitions
            ?? throw new ArgumentNullException(nameof(definitions));
        this.mass = mass ?? throw new ArgumentNullException(nameof(mass));
    }

    public long AuthorityRevision => mass.AuthorityRevision;

    public IReadOnlyList<CaptivityInteractionMaterialMassDefinition>
        CaptureAll() => definitions.All
        .Where(value => value != null && value.StableId.IsValid)
        .OrderBy(value => value.ItemId, StringComparer.Ordinal)
        .Select(value => new CaptivityInteractionMaterialMassDefinition(
            value.StableId,
            value.StockCategory,
            mass.GetDefinitionUnitMass(value.StableId)))
        .ToArray();
}

public sealed class CaptivityInteractionMaterialProjection
{
    internal CaptivityInteractionMaterialProjection(
        string captiveId,
        string interactionId,
        BuildingInstanceId facilityId,
        Vector2Int position,
        long massAuthorityRevision,
        long capacityGrams,
        string destinationId,
        string ownerOperationId,
        string fingerprint)
    {
        CaptiveId = captiveId;
        InteractionId = interactionId;
        FacilityId = facilityId;
        Position = position;
        MassAuthorityRevision = massAuthorityRevision;
        CapacityGrams = capacityGrams;
        DestinationId = destinationId;
        OwnerOperationId = ownerOperationId;
        Fingerprint = fingerprint;
    }

    public string CaptiveId { get; }
    public string InteractionId { get; }
    public BuildingInstanceId FacilityId { get; }
    public Vector2Int Position { get; }
    public long MassAuthorityRevision { get; }
    public long CapacityGrams { get; }
    public string DestinationId { get; }
    public string OwnerOperationId { get; }
    public string Fingerprint { get; }
}

public static class CaptivityInteractionMaterialAuthority
{
    public const string OwnerDomain = "captivity.interaction";
    public const long CapacitySchemaRevision = 1L;
    public const string SinkReasonCode = "captivity-interaction-material-use";
    private const string DestinationPrefix =
        "facility-input:exact:captivity.interaction:v1:";
    private const string CommittedPrefix =
        "captivity-interaction-material-commit:v1:";

    public static bool TryProject(
        CaptiveState state,
        ICaptivityInteractionHandler handler,
        BuildingInstanceId facilityId,
        Vector2Int position,
        ICaptivityInteractionMaterialMassCatalog catalog,
        out CaptivityInteractionMaterialProjection projection,
        out string failureReason)
    {
        projection = null;
        failureReason = string.Empty;
        if (state == null
            || handler == null
            || catalog == null
            || !IsCanonical(state.captiveId)
            || !IsCanonical(handler.InteractionId)
            || !facilityId.IsValid)
        {
            failureReason = "captivity-interaction-material-owner-invalid";
            return false;
        }

        KeyValuePair<StockCategory, int>[] requirements =
            (handler.MaterialRequirements
                ?? new Dictionary<StockCategory, int>())
            .Where(value => value.Value > 0)
            .OrderBy(value => (int)value.Key)
            .ToArray();
        if (requirements.Length == 0)
        {
            failureReason = "captivity-interaction-material-requirement-empty";
            return false;
        }

        CaptivityInteractionMaterialMassDefinition[] definitions =
            (catalog.CaptureAll()
                ?? Array.Empty<CaptivityInteractionMaterialMassDefinition>())
            .OrderBy(value => value.ItemId.Value, StringComparer.Ordinal)
            .ToArray();
        long capacity = 0L;
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("captivity-interaction-material-capacity-v1");
        digest.Append(state.captiveId);
        digest.Append(handler.InteractionId);
        digest.Append(facilityId.Value);
        digest.Append(position.x);
        digest.Append(position.y);
        digest.Append(catalog.AuthorityRevision);
        try
        {
            foreach (KeyValuePair<StockCategory, int> requirement in requirements)
            {
                CaptivityInteractionMaterialMassDefinition[] eligible = definitions
                    .Where(value => value.Category == requirement.Key)
                    .ToArray();
                if (eligible.Length == 0 || eligible.Any(value =>
                        value.UnitMass.Value <= 0L))
                {
                    failureReason =
                        "captivity-interaction-material-category-unmapped:"
                        + requirement.Key;
                    return false;
                }
                long maximumUnitMass = eligible.Max(value => value.UnitMass.Value);
                capacity = checked(capacity
                    + checked(maximumUnitMass * requirement.Value));
                digest.Append((int)requirement.Key);
                digest.Append(requirement.Value);
                digest.Append(maximumUnitMass);
                foreach (CaptivityInteractionMaterialMassDefinition item in eligible)
                {
                    digest.Append(item.ItemId.Value);
                    digest.Append(item.UnitMass.Value);
                }
            }
        }
        catch (OverflowException)
        {
            failureReason = "captivity-interaction-material-capacity-overflow";
            return false;
        }
        if (capacity <= 0L)
        {
            failureReason = "captivity-interaction-material-capacity-not-positive";
            return false;
        }

        string fingerprint = digest.ComputeSha256();
        string ownerOperationId = "captivity-interaction:"
            + Encode(state.captiveId) + ":" + Encode(handler.InteractionId);
        string destinationId = DestinationPrefix
            + Encode(state.captiveId) + ":"
            + Encode(handler.InteractionId) + ":"
            + Encode(facilityId.Value) + ":"
            + position.x.ToString(CultureInfo.InvariantCulture) + ":"
            + position.y.ToString(CultureInfo.InvariantCulture) + ":"
            + catalog.AuthorityRevision.ToString(CultureInfo.InvariantCulture) + ":"
            + capacity.ToString(CultureInfo.InvariantCulture) + ":"
            + fingerprint;
        projection = new CaptivityInteractionMaterialProjection(
            state.captiveId,
            handler.InteractionId,
            facilityId,
            position,
            catalog.AuthorityRevision,
            capacity,
            destinationId,
            ownerOperationId,
            fingerprint);
        return true;
    }

    public static string FormatSinkOperationId(
        CaptivityInteractionMaterialProjection projection,
        IReadOnlyList<PhysicalItemTransformInput> inputs)
    {
        if (projection == null || inputs == null || inputs.Count == 0)
            throw new ArgumentException("An exact interaction input vector is required.");
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("captivity-interaction-material-sink-v1");
        digest.Append(projection.DestinationId);
        foreach (PhysicalItemTransformInput input in inputs
                     .OrderBy(value => value.StackId, StringComparer.Ordinal))
        {
            digest.Append(input.StackId);
            digest.Append(input.Quantity);
        }
        return "captivity-interaction-sink:" + digest.ComputeSha256();
    }

    public static string FormatCommittedToken(
        string destinationId,
        PhysicalItemBatchDispositionReceipt receipt) => CommittedPrefix
        + Encode(destinationId) + ":"
        + Encode(receipt.OperationId) + ":"
        + Encode(receipt.CommitId) + ":"
        + receipt.Quantity.ToString(CultureInfo.InvariantCulture) + ":"
        + receipt.InputMassGrams.ToString(CultureInfo.InvariantCulture);

    public static bool TryParseCommittedToken(
        string value,
        out string destinationId,
        out string operationId,
        out string commitId,
        out int quantity,
        out long massGrams)
    {
        destinationId = operationId = commitId = string.Empty;
        quantity = 0;
        massGrams = 0L;
        if (value == null || !value.StartsWith(CommittedPrefix,
                StringComparison.Ordinal))
            return false;
        string[] parts = value.Substring(CommittedPrefix.Length).Split(':');
        return parts.Length == 5
            && TryDecode(parts[0], out destinationId)
            && TryDecode(parts[1], out operationId)
            && TryDecode(parts[2], out commitId)
            && int.TryParse(parts[3], NumberStyles.None,
                CultureInfo.InvariantCulture, out quantity)
            && long.TryParse(parts[4], NumberStyles.None,
                CultureInfo.InvariantCulture, out massGrams)
            && quantity > 0
            && massGrams > 0L
            && string.Equals(
                commitId,
                $"physical-batch-disposition:{(int)PhysicalItemDispositionKind.Sink}:"
                + operationId + ":" + quantity.ToString(CultureInfo.InvariantCulture)
                + ":" + massGrams.ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal);
    }

    private static string Encode(string value)
    {
        string encoded = Convert.ToBase64String(
                Encoding.UTF8.GetBytes(value ?? string.Empty))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        return encoded.Length == 0 ? "_" : encoded;
    }

    private static bool TryDecode(string value, out string decoded)
    {
        decoded = string.Empty;
        if (value == "_")
            return false;
        string padded = value.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - padded.Length % 4) % 4);
        try
        {
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            return IsCanonical(decoded);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool IsCanonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

public interface ICaptivityInteractionMaterialRuntime
{
    bool TryOpenAndRequest(
        CaptiveState state,
        ICaptivityInteractionHandler handler,
        BuildableObject facility,
        out string destinationId,
        out string failureReason);

    bool IsReady(
        CaptiveState state,
        ICaptivityInteractionHandler handler,
        out string failureReason);

    bool TryCommitSink(
        CaptiveState state,
        ICaptivityInteractionHandler handler,
        out string failureReason);

    bool TryClose(
        CaptiveState state,
        string reasonCode,
        out string failureReason);
}

public sealed class CaptivityInteractionMaterialRuntime :
    ICaptivityInteractionMaterialRuntime
{
    private readonly IWorldItemStackRuntime items;
    private readonly ICaptivityInteractionMaterialMassCatalog catalog;
    private readonly IFacilityBufferDestinationClaimAuthorityQuery claims;
    private readonly IFacilityBufferMassCapacityAuthorityQuery capacities;
    private readonly IFacilityBufferDestinationLifecycleCommand lifecycle;
    private readonly IFacilityBufferDestinationReleaseService releases;
    private readonly IPhysicalItemBatchDispositionService dispositions;

    public CaptivityInteractionMaterialRuntime(
        IWorldItemStackRuntime items,
        ICaptivityInteractionMaterialMassCatalog catalog,
        IFacilityBufferDestinationClaimAuthorityQuery claims,
        IFacilityBufferMassCapacityAuthorityQuery capacities,
        IFacilityBufferDestinationLifecycleCommand lifecycle,
        IFacilityBufferDestinationReleaseService releases,
        IPhysicalItemBatchDispositionService dispositions)
    {
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.claims = claims ?? throw new ArgumentNullException(nameof(claims));
        this.capacities = capacities ?? throw new ArgumentNullException(nameof(capacities));
        this.lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        this.releases = releases ?? throw new ArgumentNullException(nameof(releases));
        this.dispositions = dispositions
            ?? throw new ArgumentNullException(nameof(dispositions));
    }

    public bool TryOpenAndRequest(
        CaptiveState state,
        ICaptivityInteractionHandler handler,
        BuildableObject facility,
        out string destinationId,
        out string failureReason)
    {
        destinationId = string.Empty;
        failureReason = string.Empty;
        if (facility == null
            || !CaptivityInteractionMaterialAuthority.TryProject(
                state,
                handler,
                facility.RequirePersistentInstanceId(),
                facility.centerPos,
                catalog,
                out CaptivityInteractionMaterialProjection projection,
                out failureReason)
            || !TryAdd(projection, out failureReason))
        {
            return false;
        }

        destinationId = projection.DestinationId;
        foreach (KeyValuePair<StockCategory, int> requirement in
                 handler.MaterialRequirements
                     .Where(value => value.Value > 0)
                     .OrderBy(value => (int)value.Key))
        {
            if (items.TryRequestFacilityDelivery(
                    requirement.Key,
                    requirement.Value,
                    projection.Position,
                    projection.DestinationId,
                    out int requested,
                    out string deliveryFailure)
                && requested == requirement.Value)
            {
                continue;
            }
            if (!TryCloseProjection(
                    projection,
                    "captivity-interaction-request-rollback",
                    out string closeFailure))
            {
                throw new InvalidOperationException(
                    "Interaction delivery failed and exact authority rollback failed: "
                    + closeFailure);
            }
            destinationId = string.Empty;
            failureReason = string.IsNullOrWhiteSpace(deliveryFailure)
                ? "captivity-interaction-material-delivery-incomplete:"
                    + requirement.Key
                : deliveryFailure;
            return false;
        }
        return true;
    }

    public bool IsReady(
        CaptiveState state,
        ICaptivityInteractionHandler handler,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (state == null || handler == null)
        {
            failureReason = "captivity-interaction-material-readiness-invalid";
            return false;
        }
        if (state.interactionMaterialsConsumed)
        {
            return CaptivityInteractionMaterialAuthority.TryParseCommittedToken(
                state.interactionMaterialDestinationId,
                out _, out _, out _, out _, out _);
        }
        foreach (KeyValuePair<StockCategory, int> requirement in
                 handler.MaterialRequirements
                     .Where(value => value.Value > 0)
                     .OrderBy(value => (int)value.Key))
        {
            int delivered = items.GetAllStacks()
                .Where(value => value != null
                    && value.State == WorldItemStackState.FacilityBuffer
                    && string.Equals(value.DestinationId,
                        state.interactionMaterialDestinationId,
                        StringComparison.Ordinal)
                    && value.StockCategory == requirement.Key)
                .Sum(value => value.AvailableQuantity);
            if (delivered < requirement.Value)
            {
                failureReason = "Interaction input pending: "
                    + requirement.Key + " " + delivered + "/"
                    + requirement.Value + ".";
                return false;
            }
        }
        return true;
    }

    public bool TryCommitSink(
        CaptiveState state,
        ICaptivityInteractionHandler handler,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (state == null || handler == null)
        {
            failureReason = "captivity-interaction-material-sink-invalid";
            return false;
        }
        if (state.interactionMaterialsConsumed)
            return TryFinalizeCommitted(state, out failureReason);

        if (!TryResolveProjection(
                state,
                handler,
                state.interactionMaterialDestinationId,
                out CaptivityInteractionMaterialProjection projection,
                out failureReason)
            || !TryFindPair(projection, out failureReason)
            || !TrySelectExactInputs(
                projection.DestinationId,
                handler.MaterialRequirements,
                out PhysicalItemTransformInput[] inputs,
                out failureReason))
        {
            return false;
        }
        string operationId = CaptivityInteractionMaterialAuthority
            .FormatSinkOperationId(projection, inputs);
        if (!dispositions.TryCommitPending(
                inputs,
                PhysicalItemDispositionKind.Sink,
                operationId,
                CaptivityInteractionMaterialAuthority.SinkReasonCode,
                out PhysicalItemBatchDispositionReceipt receipt,
                out failureReason))
        {
            return false;
        }

        state.interactionMaterialDestinationId =
            CaptivityInteractionMaterialAuthority.FormatCommittedToken(
                projection.DestinationId,
                receipt);
        state.interactionMaterialsConsumed = true;
        return TryFinalizeCommitted(state, out failureReason);
    }

    public bool TryClose(
        CaptiveState state,
        string reasonCode,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (state == null || string.IsNullOrWhiteSpace(reasonCode))
            return true;
        if (state.interactionMaterialsConsumed)
            return TryFinalizeCommitted(state, out failureReason);
        if (!TryParseProjection(
                state.interactionMaterialDestinationId,
                out CaptivityInteractionMaterialProjection projection,
                out failureReason))
        {
            return false;
        }
        return TryCloseProjection(projection, reasonCode, out failureReason);
    }

    internal bool TryReplace(
        IReadOnlyList<CaptiveState> captiveStates,
        CaptivityInteractionRegistry interactions,
        IReadOnlyList<BuildableObject> facilities,
        out string failureReason)
    {
        failureReason = string.Empty;
        Dictionary<string, BuildableObject> live = (facilities
                ?? Array.Empty<BuildableObject>())
            .Where(value => value != null && !value.isDestroy)
            .ToDictionary(
                value => value.RequirePersistentInstanceId().Value,
                StringComparer.Ordinal);
        List<FacilityBufferDestinationClaim> desiredClaims = new();
        List<FacilityBufferCapacityProfile> desiredProfiles = new();
        foreach (CaptiveState state in (captiveStates ?? Array.Empty<CaptiveState>())
                     .Where(value => value?.status == CaptivityStatus.Interaction)
                     .OrderBy(value => value.captiveId, StringComparer.Ordinal))
        {
            string destination = state.interactionMaterialDestinationId;
            if (state.interactionMaterialsConsumed)
            {
                if (!CaptivityInteractionMaterialAuthority.TryParseCommittedToken(
                        destination,
                        out destination,
                        out _, out _, out _, out _))
                {
                    failureReason =
                        "captivity-interaction-material-restore-commit-invalid:"
                        + state.captiveId;
                    return false;
                }
                continue;
            }
            if (!interactions.TryGet(state.currentInteractionId, out var handler)
                || !TryParseProjection(destination, out var stored,
                    out failureReason)
                || !live.TryGetValue(stored.FacilityId.Value, out var facility)
                || !CaptivityInteractionMaterialAuthority.TryProject(
                    state,
                    handler,
                    stored.FacilityId,
                    facility.centerPos,
                    catalog,
                    out var expected,
                    out failureReason)
                || !string.Equals(expected.DestinationId, destination,
                    StringComparison.Ordinal))
            {
                failureReason = string.IsNullOrEmpty(failureReason)
                    ? "captivity-interaction-material-restore-projection-drift:"
                        + state.captiveId
                    : failureReason;
                return false;
            }
            desiredClaims.Add(CreateClaim(expected));
            desiredProfiles.Add(CreateProfile(expected));
        }
        return lifecycle.TryReplaceOwnedAuthorities(
            CaptivityInteractionMaterialAuthority.OwnerDomain,
            desiredClaims,
            desiredProfiles,
            out failureReason);
    }

    private bool TryFinalizeCommitted(
        CaptiveState state,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!CaptivityInteractionMaterialAuthority.TryParseCommittedToken(
                state.interactionMaterialDestinationId,
                out string destination,
                out string operationId,
                out string commitId,
                out int quantity,
                out long massGrams)
            || !TryParseProjection(destination, out var projection,
                out failureReason)
            || !TryCloseProjection(
                projection,
                "captivity-interaction-input-committed",
                out failureReason))
        {
            return false;
        }
        if (dispositions.TryGetPending(operationId, out var pending)
            && (pending.Kind != PhysicalItemDispositionKind.Sink
                || pending.Quantity != quantity
                || pending.InputMassGrams != massGrams
                || !string.Equals(pending.CommitId, commitId,
                    StringComparison.Ordinal)
                || !string.Equals(pending.ReasonCode,
                    CaptivityInteractionMaterialAuthority.SinkReasonCode,
                    StringComparison.Ordinal)))
        {
            failureReason =
                "captivity-interaction-material-pending-receipt-mismatch";
            return false;
        }
        return dispositions.Acknowledge(commitId, out failureReason);
    }

    private bool TryResolveProjection(
        CaptiveState state,
        ICaptivityInteractionHandler handler,
        string destinationId,
        out CaptivityInteractionMaterialProjection projection,
        out string failureReason)
    {
        projection = null;
        if (!TryParseProjection(destinationId, out var stored, out failureReason)
            || !CaptivityInteractionMaterialAuthority.TryProject(
                state,
                handler,
                stored.FacilityId,
                stored.Position,
                catalog,
                out projection,
                out failureReason)
            || !string.Equals(projection.DestinationId, destinationId,
                StringComparison.Ordinal))
        {
            projection = null;
            failureReason = string.IsNullOrEmpty(failureReason)
                ? "captivity-interaction-material-projection-drift"
                : failureReason;
            return false;
        }
        return true;
    }

    private static bool TryParseProjection(
        string destinationId,
        out CaptivityInteractionMaterialProjection projection,
        out string failureReason)
    {
        projection = null;
        failureReason = string.Empty;
        const string prefix = "facility-input:exact:captivity.interaction:v1:";
        if (destinationId == null
            || !destinationId.StartsWith(prefix, StringComparison.Ordinal))
        {
            failureReason = "captivity-interaction-material-destination-invalid";
            return false;
        }
        string[] parts = destinationId.Substring(prefix.Length).Split(':');
        if (parts.Length != 8
            || !TryDecode(parts[0], out string captiveId)
            || !TryDecode(parts[1], out string interactionId)
            || !TryDecode(parts[2], out string facilityValue)
            || !int.TryParse(parts[3], NumberStyles.Integer,
                CultureInfo.InvariantCulture, out int x)
            || !int.TryParse(parts[4], NumberStyles.Integer,
                CultureInfo.InvariantCulture, out int y)
            || !long.TryParse(parts[5], NumberStyles.None,
                CultureInfo.InvariantCulture, out long revision)
            || !long.TryParse(parts[6], NumberStyles.None,
                CultureInfo.InvariantCulture, out long capacity)
            || revision <= 0L
            || capacity <= 0L
            || parts[7].Length != 64
            || !((BuildingInstanceId)facilityValue).IsValid)
        {
            failureReason = "captivity-interaction-material-destination-malformed";
            return false;
        }
        projection = new CaptivityInteractionMaterialProjection(
            captiveId,
            interactionId,
            (BuildingInstanceId)facilityValue,
            new Vector2Int(x, y),
            revision,
            capacity,
            destinationId,
            "captivity-interaction:" + Encode(captiveId) + ":"
                + Encode(interactionId),
            parts[7]);
        return true;
    }

    private bool TrySelectExactInputs(
        string destinationId,
        IReadOnlyDictionary<StockCategory, int> requirements,
        out PhysicalItemTransformInput[] inputs,
        out string failureReason)
    {
        List<PhysicalItemTransformInput> selected = new();
        WorldItemStackSnapshot[] available = items.GetAllStacks()
            .Where(value => value != null
                && value.State == WorldItemStackState.FacilityBuffer
                && string.Equals(value.DestinationId, destinationId,
                    StringComparison.Ordinal)
                && value.AvailableQuantity > 0)
            .OrderBy(value => value.StackId, StringComparer.Ordinal)
            .ToArray();
        foreach (KeyValuePair<StockCategory, int> requirement in requirements
                     .Where(value => value.Value > 0)
                     .OrderBy(value => (int)value.Key))
        {
            int remaining = requirement.Value;
            foreach (WorldItemStackSnapshot stack in available
                         .Where(value => value.StockCategory == requirement.Key))
            {
                if (remaining <= 0)
                    break;
                int take = Math.Min(remaining, stack.AvailableQuantity);
                selected.Add(new PhysicalItemTransformInput(stack.StackId, take));
                remaining -= take;
            }
            if (remaining != 0)
            {
                inputs = Array.Empty<PhysicalItemTransformInput>();
                failureReason = "captivity-interaction-material-exact-input-missing:"
                    + requirement.Key;
                return false;
            }
        }
        inputs = selected
            .GroupBy(value => value.StackId, StringComparer.Ordinal)
            .Select(group => new PhysicalItemTransformInput(
                group.Key,
                group.Sum(value => value.Quantity)))
            .OrderBy(value => value.StackId, StringComparer.Ordinal)
            .ToArray();
        failureReason = string.Empty;
        return inputs.Length > 0;
    }

    private bool TryAdd(
        CaptivityInteractionMaterialProjection projection,
        out string failureReason)
    {
        if (!TryCapturePairs(
                out List<FacilityBufferDestinationClaim> ownedClaims,
                out List<FacilityBufferCapacityProfile> ownedProfiles,
                out failureReason))
            return false;
        if (ownedClaims.Any(value => string.Equals(value.DestinationId,
                projection.DestinationId, StringComparison.Ordinal)))
        {
            failureReason =
                "captivity-interaction-material-destination-duplicate";
            return false;
        }
        ownedClaims.Add(CreateClaim(projection));
        ownedProfiles.Add(CreateProfile(projection));
        return lifecycle.TryReplaceOwnedAuthorities(
            CaptivityInteractionMaterialAuthority.OwnerDomain,
            ownedClaims,
            ownedProfiles,
            out failureReason);
    }

    private bool TryCloseProjection(
        CaptivityInteractionMaterialProjection projection,
        string reasonCode,
        out string failureReason)
    {
        if (!releases.TryReleaseAtOwnerPosition(
                projection.DestinationId,
                projection.Position,
                reasonCode,
                out _,
                out failureReason)
            || !TryCapturePairs(
                out List<FacilityBufferDestinationClaim> ownedClaims,
                out List<FacilityBufferCapacityProfile> ownedProfiles,
                out failureReason))
        {
            return false;
        }
        FacilityBufferDestinationClaim claim = ownedClaims.SingleOrDefault(
            value => string.Equals(value.DestinationId,
                projection.DestinationId, StringComparison.Ordinal));
        FacilityBufferCapacityProfile profile = ownedProfiles.SingleOrDefault(
            value => string.Equals(value.DestinationId,
                projection.DestinationId, StringComparison.Ordinal));
        if (claim == null && profile == null)
            return true;
        if (claim == null || profile == null || !PairMatches(
                projection, claim, profile))
        {
            failureReason =
                "captivity-interaction-material-close-pair-invalid";
            return false;
        }
        ownedClaims.Remove(claim);
        ownedProfiles.Remove(profile);
        return lifecycle.TryReplaceOwnedAuthorities(
            CaptivityInteractionMaterialAuthority.OwnerDomain,
            ownedClaims,
            ownedProfiles,
            out failureReason);
    }

    private bool TryFindPair(
        CaptivityInteractionMaterialProjection projection,
        out string failureReason)
    {
        var matchingClaims = claims.CaptureAuthorityClaims()
            .Where(value => value != null && string.Equals(value.DestinationId,
                projection.DestinationId, StringComparison.Ordinal)).ToArray();
        var matchingProfiles = capacities.CaptureAuthorityProfiles()
            .Where(value => value != null && string.Equals(value.DestinationId,
                projection.DestinationId, StringComparison.Ordinal)).ToArray();
        bool valid = matchingClaims.Length == 1
            && matchingProfiles.Length == 1
            && PairMatches(projection, matchingClaims[0], matchingProfiles[0]);
        failureReason = valid ? string.Empty
            : "captivity-interaction-material-authority-pair-mismatch";
        return valid;
    }

    private bool TryCapturePairs(
        out List<FacilityBufferDestinationClaim> ownedClaims,
        out List<FacilityBufferCapacityProfile> ownedProfiles,
        out string failureReason)
    {
        ownedClaims = claims.CaptureAuthorityClaims()
            .Where(value => value != null && string.Equals(value.OwnerDomain,
                CaptivityInteractionMaterialAuthority.OwnerDomain,
                StringComparison.Ordinal))
            .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
            .ToList();
        ownedProfiles = capacities.CaptureAuthorityProfiles()
            .Where(value => value != null && string.Equals(value.OwnerDomain,
                CaptivityInteractionMaterialAuthority.OwnerDomain,
                StringComparison.Ordinal))
            .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
            .ToList();
        bool valid = ownedClaims.Count == ownedProfiles.Count
            && ownedClaims.Select(value => value.DestinationId)
                .SequenceEqual(ownedProfiles.Select(value => value.DestinationId),
                    StringComparer.Ordinal);
        failureReason = valid ? string.Empty
            : "captivity-interaction-material-owner-set-mismatch";
        return valid;
    }

    private static FacilityBufferDestinationClaim CreateClaim(
        CaptivityInteractionMaterialProjection projection) => new(
        projection.DestinationId,
        projection.Position,
        CaptivityInteractionMaterialAuthority.OwnerDomain,
        projection.OwnerOperationId,
        projection.FacilityId.Value,
        FacilityBufferDestinationAnchorKind.LiveFacility,
        FacilityBufferDestinationAdmissionPolicy.ExactGramRequired);

    private static FacilityBufferCapacityProfile CreateProfile(
        CaptivityInteractionMaterialProjection projection) => new(
        projection.DestinationId,
        projection.Position,
        CaptivityInteractionMaterialAuthority.OwnerDomain,
        projection.OwnerOperationId,
        projection.FacilityId.Value,
        new PhysicalMassGrams(projection.CapacityGrams),
        CaptivityInteractionMaterialAuthority.CapacitySchemaRevision);

    private static bool PairMatches(
        CaptivityInteractionMaterialProjection projection,
        FacilityBufferDestinationClaim claim,
        FacilityBufferCapacityProfile profile) => claim != null
        && profile != null
        && claim.DropPosition == projection.Position
        && profile.DropPosition == projection.Position
        && string.Equals(claim.OwnerDomain,
            CaptivityInteractionMaterialAuthority.OwnerDomain,
            StringComparison.Ordinal)
        && string.Equals(profile.OwnerDomain, claim.OwnerDomain,
            StringComparison.Ordinal)
        && string.Equals(claim.OwnerOperationId, projection.OwnerOperationId,
            StringComparison.Ordinal)
        && string.Equals(profile.OwnerOperationId, claim.OwnerOperationId,
            StringComparison.Ordinal)
        && string.Equals(claim.OwnerFacilityId, projection.FacilityId.Value,
            StringComparison.Ordinal)
        && string.Equals(profile.OwnerFacilityId, claim.OwnerFacilityId,
            StringComparison.Ordinal)
        && claim.AnchorKind == FacilityBufferDestinationAnchorKind.LiveFacility
        && claim.AdmissionPolicy
            == FacilityBufferDestinationAdmissionPolicy.ExactGramRequired
        && profile.MaxMassGrams == projection.CapacityGrams
        && profile.CapacityRevision
            == CaptivityInteractionMaterialAuthority.CapacitySchemaRevision;

    private static string Encode(string value) => Convert.ToBase64String(
            Encoding.UTF8.GetBytes(value ?? string.Empty))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static bool TryDecode(string value, out string decoded)
    {
        decoded = string.Empty;
        try
        {
            string padded = value.Replace('-', '+').Replace('_', '/');
            padded += new string('=', (4 - padded.Length % 4) % 4);
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            return !string.IsNullOrWhiteSpace(decoded)
                && string.Equals(decoded, decoded.Trim(), StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
