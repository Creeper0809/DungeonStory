using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation;

public enum WarehouseMassAdmissionReleaseReason
{
    CancelledBeforePickup = 0,
    PickupFailed = 1,
    DestinationInvalidated = 2,
    LeaseExpired = 3,
    RestoreRollback = 4,
    TransactionRollback = 5
}

public enum WarehouseMassAdmissionTokenStatus
{
    Reserved = 0,
    Committed = 1,
    Released = 2,
    Expired = 3,
    Invalidated = 4
}

public readonly struct WarehouseMassAdmissionRequest
{
    public WarehouseMassAdmissionRequest(
        BuildingInstanceId warehouseId,
        string ownerOperationId,
        ItemDefinitionId itemId,
        string itemInstanceId,
        string lotFingerprint,
        int requestedQuantity,
        long expectedWarehouseCapacityRevision,
        long expectedCatalogRevision,
        long expectedSourceRevision = 0L,
        PhysicalItemMassSubject massSubject = null)
    {
        WarehouseId = warehouseId;
        OwnerOperationId = ownerOperationId ?? string.Empty;
        ItemId = itemId;
        ItemInstanceId = itemInstanceId ?? string.Empty;
        LotFingerprint = lotFingerprint ?? string.Empty;
        RequestedQuantity = requestedQuantity;
        ExpectedWarehouseCapacityRevision = expectedWarehouseCapacityRevision;
        ExpectedCatalogRevision = expectedCatalogRevision;
        ExpectedSourceRevision = expectedSourceRevision;
        MassSubject = massSubject
            ?? PhysicalItemMassSubject.ForDefinition(itemId);
    }

    public BuildingInstanceId WarehouseId { get; }
    public string OwnerOperationId { get; }
    public ItemDefinitionId ItemId { get; }
    public string ItemInstanceId { get; }
    public string LotFingerprint { get; }
    public int RequestedQuantity { get; }
    public long ExpectedWarehouseCapacityRevision { get; }
    public long ExpectedCatalogRevision { get; }
    public long ExpectedSourceRevision { get; }
    public PhysicalItemMassSubject MassSubject { get; }
}

public readonly struct WarehouseMassAdmissionToken
{
    internal WarehouseMassAdmissionToken(
        string tokenId,
        WarehouseMassAdmissionRequest request,
        int acceptedQuantity,
        long reservedMassGrams,
        long warehouseCapacityRevision,
        double expiresAtGameSeconds)
    {
        TokenId = tokenId;
        WarehouseId = request.WarehouseId;
        OwnerOperationId = request.OwnerOperationId;
        ItemId = request.ItemId;
        ItemInstanceId = request.ItemInstanceId;
        LotFingerprint = request.LotFingerprint;
        AcceptedQuantity = acceptedQuantity;
        ReservedMassGrams = reservedMassGrams;
        CatalogRevision = request.ExpectedCatalogRevision;
        SourceRevision = request.ExpectedSourceRevision;
        WarehouseCapacityRevision = warehouseCapacityRevision;
        ExpiresAtGameSeconds = expiresAtGameSeconds;
    }

    public string TokenId { get; }
    public BuildingInstanceId WarehouseId { get; }
    public string OwnerOperationId { get; }
    public ItemDefinitionId ItemId { get; }
    public string ItemInstanceId { get; }
    public string LotFingerprint { get; }
    public int AcceptedQuantity { get; }
    public long ReservedMassGrams { get; }
    public long CatalogRevision { get; }
    public long SourceRevision { get; }
    public long WarehouseCapacityRevision { get; }
    public double ExpiresAtGameSeconds { get; }
}

public readonly struct WarehouseMassAdmissionReceipt
{
    internal WarehouseMassAdmissionReceipt(
        WarehouseMassAdmissionToken token,
        string commitId,
        long resultWarehouseCapacityRevision)
    {
        TokenId = token.TokenId;
        CommitId = commitId;
        WarehouseId = token.WarehouseId;
        OwnerOperationId = token.OwnerOperationId;
        ItemId = token.ItemId;
        LotFingerprint = token.LotFingerprint;
        CommittedQuantity = token.AcceptedQuantity;
        CommittedMassGrams = token.ReservedMassGrams;
        ResultWarehouseCapacityRevision = resultWarehouseCapacityRevision;
    }

    public string TokenId { get; }
    public string CommitId { get; }
    public BuildingInstanceId WarehouseId { get; }
    public string OwnerOperationId { get; }
    public ItemDefinitionId ItemId { get; }
    public string LotFingerprint { get; }
    public int CommittedQuantity { get; }
    public long CommittedMassGrams { get; }
    public long ResultWarehouseCapacityRevision { get; }
}

public readonly struct WarehouseMassAdmissionStatusSnapshot
{
    internal WarehouseMassAdmissionStatusSnapshot(
        WarehouseMassAdmissionToken token,
        WarehouseMassAdmissionTokenStatus status,
        WarehouseMassAdmissionReleaseReason releaseReason)
    {
        Token = token;
        Status = status;
        ReleaseReason = releaseReason;
    }

    public WarehouseMassAdmissionToken Token { get; }
    public WarehouseMassAdmissionTokenStatus Status { get; }
    public WarehouseMassAdmissionReleaseReason ReleaseReason { get; }
}

public interface IWarehouseMassAdmissionService : IWarehouseMassAdmissionLedgerQuery
{
    long CatalogRevision { get; }
    bool HasOwnerOperationHistory(string ownerOperationId);
    long GetDefinitionUnitMassGrams(ItemDefinitionId itemId);
    PhysicalItemMassSubject PrepareMassSubject(
        ItemDefinitionId itemId,
        string itemInstanceId,
        IReadOnlyList<ItemInstanceComponentSaveData> components);

    bool TryReserve(
        WarehouseMassAdmissionRequest request,
        out WarehouseMassAdmissionToken token,
        out DomainFailure failure);

    bool TryRenew(
        string tokenId,
        long expectedWarehouseCapacityRevision,
        out WarehouseMassAdmissionToken renewed,
        out DomainFailure failure);

    bool TryCommit(
        string tokenId,
        string commitId,
        out WarehouseMassAdmissionReceipt receipt,
        out DomainFailure failure);

    bool TryRelease(
        string tokenId,
        WarehouseMassAdmissionReleaseReason reason,
        out DomainFailure failure);

    bool TryGetReceipt(string tokenId, out WarehouseMassAdmissionReceipt receipt);

    bool TryGetStatus(
        string tokenId,
        out WarehouseMassAdmissionStatusSnapshot snapshot);
}

public sealed class WarehouseMassAdmissionService :
    IWarehouseMassAdmissionService,
    IDungeonRestoreTransactionParticipant
{
    private const double DefaultLeaseSeconds = 15d;
    private const double MaximumLeaseSeconds = 45d;
    private const int MaximumTerminalTombstones = 1024;

    private sealed class WarehouseAuthorityState
    {
        internal long Revision = 1L;
        internal long StoredMassRevision;
        internal long StoredMassGrams;
        internal long MaxMassGrams;
        internal bool RestrictsCategory;
        internal StockCategory AcceptedCategory;
    }

    private sealed class TokenState
    {
        internal WarehouseMassAdmissionRequest Request;
        internal WarehouseMassAdmissionToken Token;
        internal long BaselineStoredMassGrams;
        internal int BaselineItemQuantity;
        internal long BaselineStoredMassRevision;
        internal long BaselineMaxMassGrams;
        internal bool BaselineRestrictsCategory;
        internal StockCategory BaselineAcceptedCategory;
        internal double MaximumExpiresAtGameSeconds;
        internal WarehouseMassAdmissionTokenStatus Status;
        internal WarehouseMassAdmissionReleaseReason ReleaseReason;
        internal WarehouseMassAdmissionReceipt Receipt;
    }

    private sealed class RuntimeStateSnapshot
    {
        internal readonly Dictionary<string, TokenState> Tokens =
            new(StringComparer.Ordinal);
        internal readonly Dictionary<string, string> TokenByOperation =
            new(StringComparer.Ordinal);
        internal readonly Dictionary<string, long> ReservedByWarehouse =
            new(StringComparer.Ordinal);
        internal readonly Dictionary<string, WarehouseAuthorityState> Authorities =
            new(StringComparer.Ordinal);
        internal readonly List<string> TerminalTokenIds = new();
        internal long NextTokenSequence;
        internal long Revision;
    }

    private readonly IDungeonItemCatalogProvider catalog;
    private readonly IPhysicalItemMassQuery massQuery;
    private readonly IStockQuery physicalStockQuery;
    private readonly IWarehousePhysicalMassQueryPort physicalMassQuery;
    private readonly IWarehouseWorldQuery worldQuery;
    private readonly IGameClock clock;
    private readonly WorldItemRepository repository;
    private readonly Dictionary<string, TokenState> statesByTokenId =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> tokenIdByOperationId =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> reservedMassByWarehouseId =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, WarehouseAuthorityState> authorityByWarehouseId =
        new(StringComparer.Ordinal);
    private readonly Queue<string> terminalTokenIds = new();
    private long nextTokenSequence = 1L;
    private long revision;

    public bool HasOwnerOperationHistory(string ownerOperationId)
    {
        string canonical = RequireCanonicalOrEmpty(ownerOperationId);
        return canonical.Length > 0
            && tokenIdByOperationId.ContainsKey(canonical);
    }
    private RuntimeStateSnapshot restorePreviousState;
    private bool restoreActive;
    private bool restorePublished;

    public WarehouseMassAdmissionService(
        IDungeonItemCatalogProvider catalog,
        IPhysicalItemMassQuery massQuery,
        IStockQuery physicalStockQuery,
        IWarehouseWorldQuery worldQuery,
        IGameClock clock,
        WorldItemRepository repository)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.massQuery = massQuery ?? throw new ArgumentNullException(nameof(massQuery));
        this.physicalStockQuery = physicalStockQuery
            ?? throw new ArgumentNullException(nameof(physicalStockQuery));
        physicalMassQuery = physicalStockQuery as IWarehousePhysicalMassQueryPort
            ?? throw new ArgumentException(
                "Warehouse admission requires a physical gram index.",
                nameof(physicalStockQuery));
        this.worldQuery = worldQuery ?? throw new ArgumentNullException(nameof(worldQuery));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
    }

    public long Revision => revision;
    public long CatalogRevision => massQuery.AuthorityRevision;
    public string ParticipantId => "215.world.warehouse-mass-admission";

    public long GetDefinitionUnitMassGrams(ItemDefinitionId itemId) =>
        massQuery.GetDefinitionUnitMass(itemId).Value;

    public void BeginRestoreCandidate()
    {
        if (restoreActive)
        {
            throw new InvalidOperationException(
                "Warehouse mass admission restore is already active.");
        }
        restorePreviousState = CaptureRuntimeState();
        restoreActive = true;
        restorePublished = false;
    }

    public void PublishRestoreCandidate()
    {
        if (!restoreActive || restorePublished)
        {
            throw new InvalidOperationException(
                "Warehouse mass admission restore is not ready to publish.");
        }
        ClearRuntimeState();
        restorePublished = true;
    }

    public void RollbackPublishedRestoreCandidate()
    {
        if (!restoreActive || !restorePublished || restorePreviousState == null)
        {
            throw new InvalidOperationException(
                "Warehouse mass admission restore has no published state to roll back.");
        }
        RestoreRuntimeState(restorePreviousState);
        ResetRestoreTransaction();
    }

    public void CompleteRestoreCandidate()
    {
        if (!restoreActive || !restorePublished)
        {
            throw new InvalidOperationException(
                "Warehouse mass admission restore is incomplete.");
        }
        ResetRestoreTransaction();
    }

    public void DiscardRestoreCandidate()
    {
        if (restorePublished && restorePreviousState != null)
        {
            RestoreRuntimeState(restorePreviousState);
        }
        ResetRestoreTransaction();
    }

    public long GetWarehouseCapacityRevision(BuildingInstanceId warehouseId)
    {
        ExpireTokens();
        if (!TrySynchronizeWarehouse(
                warehouseId,
                invalidateReservationsOnChange: true,
                out WarehouseAuthorityState authority,
                out _,
                out DomainFailure failure))
        {
            throw new InvalidOperationException(
                $"Warehouse capacity revision is unavailable: {failure.Code}.");
        }

        return authority.Revision;
    }

    public long GetReservedInboundMassGrams(BuildingInstanceId warehouseId)
    {
        ExpireTokens();
        if (!warehouseId.IsValid)
        {
            throw new ArgumentException(
                "A valid warehouse ID is required.",
                nameof(warehouseId));
        }

        return reservedMassByWarehouseId.TryGetValue(
            warehouseId.Value,
            out long reserved)
                ? reserved
                : 0L;
    }

    public bool TryReserve(
        WarehouseMassAdmissionRequest request,
        out WarehouseMassAdmissionToken token,
        out DomainFailure failure)
    {
        token = default;
        failure = DomainFailure.None;
        ExpireTokens();
        if (!TryValidateRequest(request, out DungeonItemDefinition definition, out failure))
        {
            return false;
        }

        if (tokenIdByOperationId.TryGetValue(
                request.OwnerOperationId,
                out string existingTokenId))
        {
            TokenState existing = statesByTokenId[existingTokenId];
            if (!RequestsMatch(existing.Request, request))
            {
                failure = new DomainFailure(
                    FailureCode.WarehouseMassAdmissionFingerprintMismatch,
                    request.OwnerOperationId,
                    request.LotFingerprint);
                return false;
            }

            if (existing.Status == WarehouseMassAdmissionTokenStatus.Reserved
                || existing.Status == WarehouseMassAdmissionTokenStatus.Committed)
            {
                token = existing.Token;
                return true;
            }

            failure = CreateTerminalFailure(existing);
            return false;
        }

        if (!TrySynchronizeWarehouse(
                request.WarehouseId,
                invalidateReservationsOnChange: true,
                out WarehouseAuthorityState authority,
                out IWarehouseFacility warehouse,
                out failure))
        {
            return false;
        }

        if (request.ExpectedWarehouseCapacityRevision != authority.Revision
            || request.ExpectedCatalogRevision != massQuery.AuthorityRevision)
        {
            failure = new DomainFailure(
                FailureCode.WarehouseMassAdmissionRevisionMismatch,
                request.WarehouseId.Value,
                request.ExpectedWarehouseCapacityRevision.ToString(CultureInfo.InvariantCulture),
                authority.Revision.ToString(CultureInfo.InvariantCulture));
            return false;
        }

        WarehouseInventory inventory = warehouse.Inventory;
        inventory.BindMassAdmissionLedger(this);
        if (!inventory.Accepts(definition.StockCategory))
        {
            failure = new DomainFailure(
                FailureCode.WarehouseMassAdmissionOwnerUnavailable,
                request.WarehouseId.Value,
                StockCategoryPersistenceId.ToId(definition.StockCategory));
            return false;
        }

        long unitMassGrams = massQuery.GetStackUnitMass(
            request.ItemId,
            request.MassSubject).Value;
        long reservedMassGrams = GetReservedInboundMassGrams(request.WarehouseId);
        long remainingMassGrams = Math.Max(
            0L,
            checked(authority.MaxMassGrams
                - authority.StoredMassGrams
                - reservedMassGrams));
        long acceptableByMass = remainingMassGrams / unitMassGrams;
        int acceptedQuantity = (int)Math.Min(
            request.RequestedQuantity,
            Math.Min(int.MaxValue, acceptableByMass));
        if (acceptedQuantity <= 0)
        {
            failure = new DomainFailure(
                FailureCode.WarehouseMassCapacityUnavailable,
                request.WarehouseId.Value,
                request.ItemId.Value,
                remainingMassGrams.ToString(CultureInfo.InvariantCulture));
            return false;
        }

        long admittedMassGrams = checked(unitMassGrams * acceptedQuantity);
        string tokenId = AllocateTokenId();
        double now = Math.Max(0d, clock.Time);
        AddReservedMass(request.WarehouseId, admittedMassGrams);
        AdvanceWarehouseRevision(authority);
        token = new WarehouseMassAdmissionToken(
            tokenId,
            request,
            acceptedQuantity,
            admittedMassGrams,
            authority.Revision,
            now + DefaultLeaseSeconds);
        TokenState state = new()
        {
            Request = request,
            Token = token,
            BaselineStoredMassGrams = authority.StoredMassGrams,
            BaselineItemQuantity = physicalStockQuery.GetWarehouseQuantity(
                request.WarehouseId,
                request.ItemId.Value),
            BaselineStoredMassRevision = authority.StoredMassRevision,
            BaselineMaxMassGrams = authority.MaxMassGrams,
            BaselineRestrictsCategory = authority.RestrictsCategory,
            BaselineAcceptedCategory = authority.AcceptedCategory,
            MaximumExpiresAtGameSeconds = now + MaximumLeaseSeconds,
            Status = WarehouseMassAdmissionTokenStatus.Reserved
        };
        statesByTokenId.Add(tokenId, state);
        tokenIdByOperationId.Add(request.OwnerOperationId, tokenId);
        AdvanceRevision();
        return true;
    }

    public bool TryRenew(
        string tokenId,
        long expectedWarehouseCapacityRevision,
        out WarehouseMassAdmissionToken renewed,
        out DomainFailure failure)
    {
        renewed = default;
        failure = DomainFailure.None;
        ExpireTokens();
        if (!TryGetCanonicalTokenState(tokenId, out TokenState state, out failure))
        {
            return false;
        }

        if (state.Status != WarehouseMassAdmissionTokenStatus.Reserved)
        {
            failure = CreateTerminalFailure(state);
            return false;
        }

        if (!TrySynchronizeWarehouse(
                state.Token.WarehouseId,
                invalidateReservationsOnChange: true,
                out WarehouseAuthorityState authority,
                out _,
                out failure))
        {
            return false;
        }

        if (state.Status != WarehouseMassAdmissionTokenStatus.Reserved)
        {
            failure = CreateTerminalFailure(state);
            return false;
        }

        if (authority.Revision != expectedWarehouseCapacityRevision)
        {
            failure = new DomainFailure(
                FailureCode.WarehouseMassAdmissionRevisionMismatch,
                state.Token.TokenId,
                expectedWarehouseCapacityRevision.ToString(CultureInfo.InvariantCulture),
                authority.Revision.ToString(CultureInfo.InvariantCulture));
            return false;
        }

        double now = Math.Max(0d, clock.Time);
        if (state.Request.OwnerOperationId.StartsWith(
                "haul:",
                StringComparison.Ordinal))
        {
            // Haul admission lives as long as its quantity lease heartbeat.
            // Non-haul ingress retains the bounded absolute lifetime so an
            // abandoned synchronous transaction cannot reserve forever.
            state.MaximumExpiresAtGameSeconds = Math.Max(
                state.MaximumExpiresAtGameSeconds,
                now + MaximumLeaseSeconds);
        }
        double expiresAt = Math.Min(
            state.MaximumExpiresAtGameSeconds,
            now + DefaultLeaseSeconds);
        state.Token = new WarehouseMassAdmissionToken(
            state.Token.TokenId,
            state.Request,
            state.Token.AcceptedQuantity,
            state.Token.ReservedMassGrams,
            authority.Revision,
            expiresAt);
        renewed = state.Token;
        AdvanceRevision();
        return true;
    }

    public bool TryCommit(
        string tokenId,
        string commitId,
        out WarehouseMassAdmissionReceipt receipt,
        out DomainFailure failure)
    {
        receipt = default;
        failure = DomainFailure.None;
        ExpireTokens();
        if (!TryGetCanonicalTokenState(tokenId, out TokenState state, out failure))
        {
            return false;
        }

        string canonicalCommitId = RequireCanonicalOrEmpty(commitId);
        if (state.Status == WarehouseMassAdmissionTokenStatus.Committed)
        {
            if (!string.Equals(
                    state.Receipt.CommitId,
                    canonicalCommitId,
                    StringComparison.Ordinal))
            {
                failure = new DomainFailure(
                    FailureCode.WarehouseMassAdmissionCommitConflict,
                    state.Token.TokenId,
                    canonicalCommitId);
                return false;
            }

            receipt = state.Receipt;
            return true;
        }

        if (state.Status != WarehouseMassAdmissionTokenStatus.Reserved)
        {
            failure = CreateTerminalFailure(state);
            return false;
        }

        if (canonicalCommitId.Length == 0)
        {
            failure = new DomainFailure(
                FailureCode.WarehouseMassAdmissionRequestInvalid,
                state.Token.TokenId,
                "commit-id-missing");
            return false;
        }

        if (!TryResolveWarehouse(state.Token.WarehouseId, out IWarehouseFacility warehouse)
            || warehouse.Inventory == null)
        {
            InvalidateToken(state, WarehouseMassAdmissionReleaseReason.DestinationInvalidated);
            failure = new DomainFailure(
                FailureCode.WarehouseMassAdmissionOwnerUnavailable,
                state.Token.WarehouseId.Value,
                string.Empty);
            return false;
        }

        WarehouseInventory inventory = warehouse.Inventory;
        long storedMassAfter = physicalMassQuery.GetWarehouseStoredMassGrams(
            state.Token.WarehouseId);
        long storedMassRevisionAfter = physicalMassQuery.GetWarehouseStoredMassRevision(
            state.Token.WarehouseId);
        int itemQuantityAfter = physicalStockQuery.GetWarehouseQuantity(
            state.Token.WarehouseId,
            state.Token.ItemId.Value);
        long expectedStoredMass = checked(
            state.BaselineStoredMassGrams + state.Token.ReservedMassGrams);
        int expectedItemQuantity = checked(
            state.BaselineItemQuantity + state.Token.AcceptedQuantity);
        bool structuralAuthorityMatches = inventory.MaxMassGrams
                == state.BaselineMaxMassGrams
            && inventory.RestrictsCategory == state.BaselineRestrictsCategory
            && inventory.AcceptedCategory == state.BaselineAcceptedCategory
            && massQuery.AuthorityRevision == state.Token.CatalogRevision;
        if (!structuralAuthorityMatches
            || storedMassAfter != expectedStoredMass
            || itemQuantityAfter != expectedItemQuantity
            || storedMassRevisionAfter <= state.BaselineStoredMassRevision)
        {
            InvalidateToken(state, WarehouseMassAdmissionReleaseReason.TransactionRollback);
            failure = new DomainFailure(
                FailureCode.WarehouseMassAdmissionRevisionMismatch,
                state.Token.TokenId,
                expectedStoredMass.ToString(CultureInfo.InvariantCulture),
                storedMassAfter.ToString(CultureInfo.InvariantCulture));
            return false;
        }

        WarehouseAuthorityState authority = GetOrCreateAuthority(
            state.Token.WarehouseId,
            inventory,
            storedMassAfter,
            storedMassRevisionAfter);
        RemoveReservedMass(
            state.Token.WarehouseId,
            state.Token.ReservedMassGrams);
        authority.StoredMassGrams = storedMassAfter;
        authority.StoredMassRevision = storedMassRevisionAfter;
        authority.MaxMassGrams = inventory.MaxMassGrams;
        authority.RestrictsCategory = inventory.RestrictsCategory;
        authority.AcceptedCategory = inventory.AcceptedCategory;
        AdvanceWarehouseRevision(authority);
        state.Status = WarehouseMassAdmissionTokenStatus.Committed;
        state.Receipt = new WarehouseMassAdmissionReceipt(
            state.Token,
            canonicalCommitId,
            authority.Revision);
        receipt = state.Receipt;
        AdjustOtherActiveBaselinesAfterCommit(state);
        RememberTerminal(state.Token.TokenId);
        AdvanceRevision();
        return true;
    }

    public bool TryRelease(
        string tokenId,
        WarehouseMassAdmissionReleaseReason reason,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        ExpireTokens();
        return TryReleaseCore(
            tokenId,
            reason,
            string.Empty,
            out failure);
    }

    private bool TryReleaseCore(
        string tokenId,
        WarehouseMassAdmissionReleaseReason reason,
        string authorityReleasePlanFingerprint,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (!TryGetCanonicalTokenState(tokenId, out TokenState state, out failure))
        {
            return false;
        }

        if (repository.TryGetActiveCapacityRoutingAuthorityReleaseForAdmission(
                state.Token.TokenId,
                out ProductionCapacityRoutingActorAuthorityReleaseSaveData
                    activeRelease)
            && !string.Equals(
                activeRelease.planFingerprint,
                authorityReleasePlanFingerprint,
                StringComparison.Ordinal))
        {
            failure = new DomainFailure(
                FailureCode.ItemReservationOperationConflict);
            return false;
        }

        if (state.Status == WarehouseMassAdmissionTokenStatus.Released
            && state.ReleaseReason == reason)
        {
            return true;
        }

        if (state.Status != WarehouseMassAdmissionTokenStatus.Reserved)
        {
            failure = CreateTerminalFailure(state);
            return false;
        }

        TransitionToTerminal(
            state,
            WarehouseMassAdmissionTokenStatus.Released,
            reason);
        return true;
    }

    internal ExactAuthorityReleaseStatus TryReleaseExactOwnedSet(
        string ownerOperationId,
        IReadOnlyList<string> expectedTokenIds,
        WarehouseMassAdmissionReleaseReason reason,
        string authorityReleasePlanFingerprint,
        out string failureReason)
    {
        failureReason = string.Empty;
        string owner = ownerOperationId ?? string.Empty;
        string[] expected = (expectedTokenIds ?? Array.Empty<string>()).ToArray();
        if (owner.Length == 0
            || !string.Equals(owner, owner.Trim(), StringComparison.Ordinal)
            || expected.Any(value => string.IsNullOrEmpty(value)
                || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            || expected.Distinct(StringComparer.Ordinal).Count() != expected.Length
            || !expected.SequenceEqual(
                expected.OrderBy(value => value, StringComparer.Ordinal),
                StringComparer.Ordinal))
        {
            failureReason =
                "capacity-routing-exact-admission-release-plan-conflict";
            return ExactAuthorityReleaseStatus.Conflict;
        }
        if (!repository.TryGetActiveCapacityRoutingAuthorityRelease(
                owner,
                out ProductionCapacityRoutingActorAuthorityReleaseSaveData plan)
            || !string.Equals(
                plan.planFingerprint,
                authorityReleasePlanFingerprint,
                StringComparison.Ordinal))
        {
            failureReason =
                "capacity-routing-exact-admission-release-plan-conflict";
            return ExactAuthorityReleaseStatus.Conflict;
        }
        ProductionCapacityRoutingOperationAuthorityRowSaveData row =
            plan.operations.FirstOrDefault(candidate => candidate != null
                && string.Equals(
                    candidate.operationId,
                    owner,
                    StringComparison.Ordinal));
        if (row == null
            || !row.warehouseAdmissionTokenIds.SequenceEqual(
                expected,
                StringComparer.Ordinal))
        {
            failureReason =
                "capacity-routing-exact-admission-release-plan-conflict";
            return ExactAuthorityReleaseStatus.Conflict;
        }
        if (expected.Length == 0)
            return ExactAuthorityReleaseStatus.Replay;

        bool applied = false;
        foreach (string tokenId in expected)
        {
            if (!statesByTokenId.TryGetValue(tokenId, out TokenState state))
            {
                failureReason =
                    "capacity-routing-exact-admission-release-missing:"
                    + tokenId;
                return ExactAuthorityReleaseStatus.Conflict;
            }
            if (state.Status == WarehouseMassAdmissionTokenStatus.Released
                && state.ReleaseReason == reason)
            {
                continue;
            }
            if (state.Status != WarehouseMassAdmissionTokenStatus.Reserved)
            {
                failureReason =
                    "capacity-routing-exact-admission-release-terminal-conflict:"
                    + tokenId;
                return ExactAuthorityReleaseStatus.Conflict;
            }
            if (!TryReleaseCore(
                    tokenId,
                    reason,
                    authorityReleasePlanFingerprint,
                    out DomainFailure releaseFailure))
            {
                failureReason =
                    "capacity-routing-exact-admission-release-failed:"
                    + tokenId + ":" + releaseFailure.Code;
                return ExactAuthorityReleaseStatus.Conflict;
            }
            applied = true;
        }
        return applied
            ? ExactAuthorityReleaseStatus.Applied
            : ExactAuthorityReleaseStatus.Replay;
    }

    public bool TryGetReceipt(
        string tokenId,
        out WarehouseMassAdmissionReceipt receipt)
    {
        receipt = default;
        string canonicalTokenId = RequireCanonicalOrEmpty(tokenId);
        if (!statesByTokenId.TryGetValue(canonicalTokenId, out TokenState state)
            || state.Status != WarehouseMassAdmissionTokenStatus.Committed)
        {
            return false;
        }

        receipt = state.Receipt;
        return true;
    }

    public bool TryGetStatus(
        string tokenId,
        out WarehouseMassAdmissionStatusSnapshot snapshot)
    {
        snapshot = default;
        string canonicalTokenId = RequireCanonicalOrEmpty(tokenId);
        if (!statesByTokenId.TryGetValue(canonicalTokenId, out TokenState state))
        {
            return false;
        }

        snapshot = new WarehouseMassAdmissionStatusSnapshot(
            state.Token,
            state.Status,
            state.ReleaseReason);
        return true;
    }

    private bool TryValidateRequest(
        WarehouseMassAdmissionRequest request,
        out DungeonItemDefinition definition,
        out DomainFailure failure)
    {
        definition = null;
        failure = DomainFailure.None;
        if (!request.WarehouseId.IsValid
            || !IsCanonical(request.OwnerOperationId)
            || !request.ItemId.IsValid
            || !IsCanonical(request.ItemId.Value)
            || !IsCanonical(request.LotFingerprint)
            || request.RequestedQuantity <= 0
            || request.ExpectedWarehouseCapacityRevision <= 0L
            || request.ExpectedCatalogRevision != massQuery.AuthorityRevision
            || request.ExpectedSourceRevision < 0L
            || !request.MassSubject.ItemId.IsValid
            || !request.MassSubject.ItemId.Equals(request.ItemId)
            || !catalog.TryGetDefinition(request.ItemId.Value, out definition))
        {
            failure = new DomainFailure(
                FailureCode.WarehouseMassAdmissionRequestInvalid,
                request.OwnerOperationId,
                request.ItemId.Value);
            return false;
        }

        bool statefulLot = definition.MaxStack <= 1;
        bool instanceIdentityValid = statefulLot
            ? request.RequestedQuantity == 1
                && IsCanonical(request.ItemInstanceId)
            : request.ItemInstanceId.Length == 0;
        if (!instanceIdentityValid)
        {
            failure = new DomainFailure(
                FailureCode.WarehouseMassAdmissionRequestInvalid,
                request.OwnerOperationId,
                statefulLot
                    ? "stateful-lot-instance-required"
                    : "stackable-lot-instance-forbidden");
            return false;
        }

        if (PhysicalItemIds.TryGetEquipmentDefinitionId(
                request.ItemId.Value,
                out _)
            && (request.MassSubject.Kind
                    != PhysicalItemMassSubjectKind.CombatEquipment
                || !string.Equals(
                    request.MassSubject.ItemInstanceId,
                    request.ItemInstanceId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    request.MassSubject.ComponentFingerprint,
                    request.LotFingerprint,
                    StringComparison.Ordinal)))
        {
            failure = new DomainFailure(
                FailureCode.WarehouseMassAdmissionRequestInvalid,
                request.OwnerOperationId,
                "combat-equipment-mass-subject-required");
            return false;
        }

        return true;
    }

    private bool TrySynchronizeWarehouse(
        BuildingInstanceId warehouseId,
        bool invalidateReservationsOnChange,
        out WarehouseAuthorityState authority,
        out IWarehouseFacility warehouse,
        out DomainFailure failure)
    {
        authority = null;
        warehouse = null;
        failure = DomainFailure.None;
        if (!warehouseId.IsValid || !TryResolveWarehouse(warehouseId, out warehouse))
        {
            InvalidateActiveTokensForWarehouse(
                warehouseId,
                WarehouseMassAdmissionReleaseReason.DestinationInvalidated);
            failure = new DomainFailure(
                FailureCode.WarehouseMassAdmissionOwnerUnavailable,
                warehouseId.Value,
                string.Empty);
            return false;
        }

        WarehouseInventory inventory = warehouse.Inventory;
        long storedMass = physicalMassQuery.GetWarehouseStoredMassGrams(warehouseId);
        long storedMassRevision = physicalMassQuery.GetWarehouseStoredMassRevision(
            warehouseId);
        if (!authorityByWarehouseId.TryGetValue(warehouseId.Value, out authority))
        {
            authority = GetOrCreateAuthority(
                warehouseId,
                inventory,
                storedMass,
                storedMassRevision);
            return true;
        }

        bool changed = authority.StoredMassRevision != storedMassRevision
            || authority.StoredMassGrams != storedMass
            || authority.MaxMassGrams != inventory.MaxMassGrams
            || authority.RestrictsCategory != inventory.RestrictsCategory
            || authority.AcceptedCategory != inventory.AcceptedCategory;
        if (!changed)
        {
            return true;
        }

        authority.StoredMassRevision = storedMassRevision;
        authority.StoredMassGrams = storedMass;
        authority.MaxMassGrams = inventory.MaxMassGrams;
        authority.RestrictsCategory = inventory.RestrictsCategory;
        authority.AcceptedCategory = inventory.AcceptedCategory;
        AdvanceWarehouseRevision(authority);
        if (invalidateReservationsOnChange)
        {
            InvalidateActiveTokensForWarehouse(
                warehouseId,
                WarehouseMassAdmissionReleaseReason.DestinationInvalidated);
        }
        return true;
    }

    private WarehouseAuthorityState GetOrCreateAuthority(
        BuildingInstanceId warehouseId,
        WarehouseInventory inventory,
        long storedMass,
        long storedMassRevision)
    {
        if (authorityByWarehouseId.TryGetValue(
                warehouseId.Value,
                out WarehouseAuthorityState existing))
        {
            return existing;
        }

        WarehouseAuthorityState created = new()
        {
            StoredMassRevision = storedMassRevision,
            StoredMassGrams = storedMass,
            MaxMassGrams = inventory.MaxMassGrams,
            RestrictsCategory = inventory.RestrictsCategory,
            AcceptedCategory = inventory.AcceptedCategory
        };
        authorityByWarehouseId.Add(warehouseId.Value, created);
        AdvanceRevision();
        return created;
    }

    private bool TryResolveWarehouse(
        BuildingInstanceId warehouseId,
        out IWarehouseFacility warehouse)
    {
        warehouse = null;
        int matches = 0;
        IReadOnlyList<IWarehouseFacility> warehouses = worldQuery.Warehouses;
        for (int index = 0; index < warehouses.Count; index++)
        {
            IWarehouseFacility candidate = warehouses[index];
            if (candidate == null
                || candidate.Inventory == null
                || !candidate.HasWarehouseInventory
                || !candidate.Inventory.HasMassCapacityAuthority
                || !candidate.PersistentInstanceId.Equals(warehouseId))
            {
                continue;
            }

            warehouse = candidate;
            matches++;
        }
        return matches == 1;
    }

    private void ExpireTokens()
    {
        double now = Math.Max(0d, clock.Time);
        List<TokenState> expired = null;
        foreach (TokenState state in statesByTokenId.Values)
        {
            if (state.Status != WarehouseMassAdmissionTokenStatus.Reserved
                || state.Token.ExpiresAtGameSeconds > now
                || repository
                    .TryGetActiveCapacityRoutingAuthorityReleaseForAdmission(
                        state.Token.TokenId,
                        out _))
            {
                continue;
            }

            expired ??= new List<TokenState>();
            expired.Add(state);
        }

        if (expired == null)
        {
            return;
        }

        for (int index = 0; index < expired.Count; index++)
        {
            TransitionToTerminal(
                expired[index],
                WarehouseMassAdmissionTokenStatus.Expired,
                WarehouseMassAdmissionReleaseReason.LeaseExpired);
        }
    }

    private void InvalidateToken(
        TokenState state,
        WarehouseMassAdmissionReleaseReason reason)
    {
        if (state.Status == WarehouseMassAdmissionTokenStatus.Reserved)
        {
            if (repository.TryGetActiveCapacityRoutingAuthorityReleaseForAdmission(
                    state.Token.TokenId,
                    out _))
            {
                return;
            }
            TransitionToTerminal(
                state,
                WarehouseMassAdmissionTokenStatus.Invalidated,
                reason);
        }
    }

    private void InvalidateActiveTokensForWarehouse(
        BuildingInstanceId warehouseId,
        WarehouseMassAdmissionReleaseReason reason)
    {
        if (!warehouseId.IsValid)
        {
            return;
        }

        List<TokenState> invalidated = null;
        foreach (TokenState state in statesByTokenId.Values)
        {
            if (state.Status != WarehouseMassAdmissionTokenStatus.Reserved
                || !state.Token.WarehouseId.Equals(warehouseId))
            {
                continue;
            }

            invalidated ??= new List<TokenState>();
            invalidated.Add(state);
        }

        if (invalidated == null)
        {
            return;
        }

        for (int index = 0; index < invalidated.Count; index++)
        {
            TransitionToTerminal(
                invalidated[index],
                WarehouseMassAdmissionTokenStatus.Invalidated,
                reason);
        }
    }

    private void TransitionToTerminal(
        TokenState state,
        WarehouseMassAdmissionTokenStatus status,
        WarehouseMassAdmissionReleaseReason reason)
    {
        if (state.Status != WarehouseMassAdmissionTokenStatus.Reserved)
        {
            return;
        }

        RemoveReservedMass(
            state.Token.WarehouseId,
            state.Token.ReservedMassGrams);
        state.Status = status;
        state.ReleaseReason = reason;
        if (authorityByWarehouseId.TryGetValue(
                state.Token.WarehouseId.Value,
                out WarehouseAuthorityState authority))
        {
            AdvanceWarehouseRevision(authority);
        }
        RememberTerminal(state.Token.TokenId);
        AdvanceRevision();
    }

    private void AdjustOtherActiveBaselinesAfterCommit(TokenState committed)
    {
        foreach (TokenState state in statesByTokenId.Values)
        {
            if (ReferenceEquals(state, committed)
                || state.Status != WarehouseMassAdmissionTokenStatus.Reserved
                || !state.Token.WarehouseId.Equals(committed.Token.WarehouseId))
            {
                continue;
            }

            state.BaselineStoredMassGrams = checked(
                state.BaselineStoredMassGrams + committed.Token.ReservedMassGrams);
            state.BaselineStoredMassRevision = physicalMassQuery
                .GetWarehouseStoredMassRevision(committed.Token.WarehouseId);
            if (state.Token.ItemId.Equals(committed.Token.ItemId))
            {
                state.BaselineItemQuantity = checked(
                    state.BaselineItemQuantity + committed.Token.AcceptedQuantity);
            }
        }
    }

    private void RememberTerminal(string tokenId)
    {
        terminalTokenIds.Enqueue(tokenId);
        while (terminalTokenIds.Count > MaximumTerminalTombstones)
        {
            string prunedTokenId = terminalTokenIds.Dequeue();
            if (!statesByTokenId.TryGetValue(prunedTokenId, out TokenState pruned)
                || pruned.Status == WarehouseMassAdmissionTokenStatus.Reserved
                || repository
                    .TryGetActiveCapacityRoutingAuthorityReleaseForAdmission(
                        prunedTokenId,
                        out _))
            {
                continue;
            }

            statesByTokenId.Remove(prunedTokenId);
            if (tokenIdByOperationId.TryGetValue(
                    pruned.Token.OwnerOperationId,
                    out string mappedTokenId)
                && string.Equals(
                    mappedTokenId,
                    prunedTokenId,
                    StringComparison.Ordinal))
            {
                tokenIdByOperationId.Remove(pruned.Token.OwnerOperationId);
            }
        }
    }

    private void AddReservedMass(BuildingInstanceId warehouseId, long massGrams)
    {
        reservedMassByWarehouseId.TryGetValue(warehouseId.Value, out long current);
        reservedMassByWarehouseId[warehouseId.Value] = checked(current + massGrams);
    }

    private void RemoveReservedMass(BuildingInstanceId warehouseId, long massGrams)
    {
        if (!reservedMassByWarehouseId.TryGetValue(
                warehouseId.Value,
                out long current)
            || current < massGrams)
        {
            throw new InvalidOperationException(
                $"Warehouse '{warehouseId.Value}' reserved-mass ledger underflow.");
        }

        long next = current - massGrams;
        if (next == 0L)
        {
            reservedMassByWarehouseId.Remove(warehouseId.Value);
        }
        else
        {
            reservedMassByWarehouseId[warehouseId.Value] = next;
        }
    }

    private bool TryGetCanonicalTokenState(
        string tokenId,
        out TokenState state,
        out DomainFailure failure)
    {
        state = null;
        string canonicalTokenId = RequireCanonicalOrEmpty(tokenId);
        if (canonicalTokenId.Length == 0
            || !statesByTokenId.TryGetValue(canonicalTokenId, out state))
        {
            failure = new DomainFailure(
                FailureCode.WarehouseMassAdmissionTokenMissing,
                canonicalTokenId);
            return false;
        }

        failure = DomainFailure.None;
        return true;
    }

    private static DomainFailure CreateTerminalFailure(TokenState state)
    {
        FailureCode code = state.Status == WarehouseMassAdmissionTokenStatus.Expired
            ? FailureCode.WarehouseMassAdmissionTokenExpired
            : FailureCode.WarehouseMassAdmissionTokenTerminal;
        return new DomainFailure(
            code,
            state.Token.TokenId,
            state.Status.ToString());
    }

    private string AllocateTokenId()
    {
        long sequence = nextTokenSequence;
        nextTokenSequence = checked(sequence + 1L);
        return $"warehouse-mass:{sequence:D16}";
    }

    private void AdvanceWarehouseRevision(WarehouseAuthorityState authority)
    {
        authority.Revision = checked(authority.Revision + 1L);
    }

    private void AdvanceRevision()
    {
        revision = checked(revision + 1L);
    }

    private RuntimeStateSnapshot CaptureRuntimeState()
    {
        RuntimeStateSnapshot snapshot = new()
        {
            NextTokenSequence = nextTokenSequence,
            Revision = revision
        };
        foreach (KeyValuePair<string, TokenState> pair in statesByTokenId)
        {
            TokenState source = pair.Value;
            snapshot.Tokens.Add(pair.Key, new TokenState
            {
                Request = source.Request,
                Token = source.Token,
                BaselineStoredMassGrams = source.BaselineStoredMassGrams,
                BaselineItemQuantity = source.BaselineItemQuantity,
                BaselineStoredMassRevision = source.BaselineStoredMassRevision,
                BaselineMaxMassGrams = source.BaselineMaxMassGrams,
                BaselineRestrictsCategory = source.BaselineRestrictsCategory,
                BaselineAcceptedCategory = source.BaselineAcceptedCategory,
                MaximumExpiresAtGameSeconds = source.MaximumExpiresAtGameSeconds,
                Status = source.Status,
                ReleaseReason = source.ReleaseReason,
                Receipt = source.Receipt
            });
        }
        foreach (KeyValuePair<string, string> pair in tokenIdByOperationId)
            snapshot.TokenByOperation.Add(pair.Key, pair.Value);
        foreach (KeyValuePair<string, long> pair in reservedMassByWarehouseId)
            snapshot.ReservedByWarehouse.Add(pair.Key, pair.Value);
        foreach (KeyValuePair<string, WarehouseAuthorityState> pair in
                 authorityByWarehouseId)
        {
            WarehouseAuthorityState source = pair.Value;
            snapshot.Authorities.Add(pair.Key, new WarehouseAuthorityState
            {
                Revision = source.Revision,
                StoredMassRevision = source.StoredMassRevision,
                StoredMassGrams = source.StoredMassGrams,
                MaxMassGrams = source.MaxMassGrams,
                RestrictsCategory = source.RestrictsCategory,
                AcceptedCategory = source.AcceptedCategory
            });
        }
        snapshot.TerminalTokenIds.AddRange(terminalTokenIds);
        return snapshot;
    }

    private void RestoreRuntimeState(RuntimeStateSnapshot snapshot)
    {
        ClearRuntimeState();
        foreach (KeyValuePair<string, TokenState> pair in snapshot.Tokens)
            statesByTokenId.Add(pair.Key, pair.Value);
        foreach (KeyValuePair<string, string> pair in snapshot.TokenByOperation)
            tokenIdByOperationId.Add(pair.Key, pair.Value);
        foreach (KeyValuePair<string, long> pair in snapshot.ReservedByWarehouse)
            reservedMassByWarehouseId.Add(pair.Key, pair.Value);
        foreach (KeyValuePair<string, WarehouseAuthorityState> pair in
                 snapshot.Authorities)
        {
            authorityByWarehouseId.Add(pair.Key, pair.Value);
        }
        foreach (string tokenId in snapshot.TerminalTokenIds)
            terminalTokenIds.Enqueue(tokenId);
        nextTokenSequence = snapshot.NextTokenSequence;
        revision = snapshot.Revision;
    }

    private void ClearRuntimeState()
    {
        statesByTokenId.Clear();
        tokenIdByOperationId.Clear();
        reservedMassByWarehouseId.Clear();
        authorityByWarehouseId.Clear();
        terminalTokenIds.Clear();
        nextTokenSequence = 1L;
        AdvanceRevision();
    }

    private void ResetRestoreTransaction()
    {
        restorePreviousState = null;
        restoreActive = false;
        restorePublished = false;
    }

    private static bool RequestsMatch(
        WarehouseMassAdmissionRequest left,
        WarehouseMassAdmissionRequest right) =>
        left.WarehouseId.Equals(right.WarehouseId)
        && string.Equals(left.OwnerOperationId, right.OwnerOperationId, StringComparison.Ordinal)
        && left.ItemId.Equals(right.ItemId)
        && string.Equals(left.ItemInstanceId, right.ItemInstanceId, StringComparison.Ordinal)
        && string.Equals(left.LotFingerprint, right.LotFingerprint, StringComparison.Ordinal)
        && left.MassSubject.Kind == right.MassSubject.Kind
        && string.Equals(
            left.MassSubject.ItemInstanceId,
            right.MassSubject.ItemInstanceId,
            StringComparison.Ordinal)
        && string.Equals(
            left.MassSubject.ComponentFingerprint,
            right.MassSubject.ComponentFingerprint,
            StringComparison.Ordinal)
        && left.RequestedQuantity == right.RequestedQuantity
        && left.ExpectedWarehouseCapacityRevision
            == right.ExpectedWarehouseCapacityRevision
        && left.ExpectedCatalogRevision == right.ExpectedCatalogRevision
        && left.ExpectedSourceRevision == right.ExpectedSourceRevision;

    private static bool IsCanonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static string RequireCanonicalOrEmpty(string value)
    {
        string candidate = value ?? string.Empty;
        return IsCanonical(candidate) ? candidate : string.Empty;
    }

    public PhysicalItemMassSubject PrepareMassSubject(
        ItemDefinitionId itemId,
        string itemInstanceId,
        IReadOnlyList<ItemInstanceComponentSaveData> components) =>
        PhysicalItemMassSubjectAdapter.Create(
            massQuery,
            itemId,
            itemInstanceId,
            components);
}
