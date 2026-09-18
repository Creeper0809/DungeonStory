using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Environment;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public sealed class EnvironmentalFireWorldAdapter :
    IEnvironmentalFireTargetQuery,
    IEnvironmentalFireDamageCommand,
    IEnvironmentalFireExposureTargetQuery,
    IEnvironmentalFireFuelLossSink,
    IEnvironmentalFireSuppressionAccessQuery,
    IEnvironmentalFireElectricalSafetyQuery,
    IEnvironmentalFireWaterSink
{
    private const float Epsilon = 0.0001f;
    public const string CleanWaterItemId = "resource:clean-water";
    public const string WaterSinkReasonCode =
        "environmental-fire-water-suppression";
    public const string FuelLossSinkReasonCode =
        "environmental-fire-fuel-loss";

    private sealed class DamageCommit
    {
        public string Fingerprint;
        public EnvironmentalFireDamageResult Result;
    }

    private readonly IBuildingWorldQuery buildings;
    private readonly ICharacterWorldQuery characters;
    private readonly IGridSystemProvider gridProvider;
    private readonly IBuildingStructuralIntegrityRuntime structuralIntegrity;
    private readonly IWorldHazardZoneQuery hazards;
    private readonly IPowerInfrastructureQuery power;
    private readonly IItemQuantityReservationService reservations;
    private readonly IWorldItemStackRuntime items;
    private readonly IReservedPhysicalItemBatchDispositionService reservedSink;
    private readonly IPhysicalItemBatchDispositionService dispositions;
    private readonly IOutcomeAwareReservedPhysicalItemBatchDispositionService
        outcomeAwareReservedSink;
    private readonly IOutcomeAwarePhysicalItemBatchDispositionService
        outcomeAwareDispositions;
    private readonly IEnvironmentGameplayOutcomeCommitter environmentOutcomes;
    private readonly IGameplayOutcomeDiagnosticsQuery outcomeDiagnostics;
    private readonly IGameCalendar calendar;
    private readonly ICharacterEnvironmentProtectionResolver protection;
    private readonly Dictionary<string, DamageCommit> damageCommits =
        new(StringComparer.Ordinal);

    public EnvironmentalFireWorldAdapter(
        IBuildingWorldQuery buildings,
        ICharacterWorldQuery characters,
        IGridSystemProvider gridProvider,
        IBuildingStructuralIntegrityRuntime structuralIntegrity,
        IWorldHazardZoneQuery hazards,
        IPowerInfrastructureQuery power,
        IItemQuantityReservationService reservations,
        IWorldItemStackRuntime items,
        IReservedPhysicalItemBatchDispositionService reservedSink,
        IPhysicalItemBatchDispositionService dispositions,
        IOutcomeAwareReservedPhysicalItemBatchDispositionService
            outcomeAwareReservedSink,
        IOutcomeAwarePhysicalItemBatchDispositionService
            outcomeAwareDispositions,
        IEnvironmentGameplayOutcomeCommitter environmentOutcomes,
        IGameplayOutcomeDiagnosticsQuery outcomeDiagnostics,
        IGameCalendar calendar,
        ICharacterEnvironmentProtectionResolver protection = null)
    {
        this.buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
        this.characters = characters ?? throw new ArgumentNullException(nameof(characters));
        this.gridProvider = gridProvider ?? throw new ArgumentNullException(nameof(gridProvider));
        this.structuralIntegrity = structuralIntegrity
            ?? throw new ArgumentNullException(nameof(structuralIntegrity));
        this.hazards = hazards ?? throw new ArgumentNullException(nameof(hazards));
        this.power = power ?? throw new ArgumentNullException(nameof(power));
        this.reservations = reservations
            ?? throw new ArgumentNullException(nameof(reservations));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.reservedSink = reservedSink
            ?? throw new ArgumentNullException(nameof(reservedSink));
        this.dispositions = dispositions
            ?? throw new ArgumentNullException(nameof(dispositions));
        this.outcomeAwareReservedSink = outcomeAwareReservedSink
            ?? throw new ArgumentNullException(nameof(outcomeAwareReservedSink));
        this.outcomeAwareDispositions = outcomeAwareDispositions
            ?? throw new ArgumentNullException(nameof(outcomeAwareDispositions));
        this.environmentOutcomes = environmentOutcomes
            ?? throw new ArgumentNullException(nameof(environmentOutcomes));
        this.outcomeDiagnostics = outcomeDiagnostics
            ?? throw new ArgumentNullException(nameof(outcomeDiagnostics));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.protection = protection;
    }

    public bool TryGetTarget(
        EnvironmentalFireTargetRef target,
        out EnvironmentalFireTargetSnapshot snapshot)
    {
        snapshot = default;
        if (target.Kind == EnvironmentalFireTargetKind.Character)
        {
            if (!TryResolveCharacter(target.TargetId, out CharacterActor actor))
                return false;
            Vector2Int position = actor.GetNowXY();
            snapshot = new EnvironmentalFireTargetSnapshot(
                target,
                position,
                !actor.IsDead,
                false,
                false,
                null,
                EnvironmentalFireIgnitionSources.None);
            return true;
        }
        if (target.Kind == EnvironmentalFireTargetKind.ItemStack)
        {
            WorldItemStackSnapshot stack = (items.GetAllStacks()
                    ?? Array.Empty<WorldItemStackSnapshot>())
                .SingleOrDefault(value => value != null
                    && string.Equals(
                        value.StackId,
                        target.TargetId,
                        StringComparison.Ordinal));
            if (stack == null)
                return false;
            snapshot = new EnvironmentalFireTargetSnapshot(
                target,
                stack.Position,
                stack.Quantity > 0,
                false,
                false,
                null,
                EnvironmentalFireIgnitionSources.None);
            return true;
        }

        if (target.Kind != EnvironmentalFireTargetKind.Building
            || !TryResolveBuilding(target.TargetId, out BuildableObject building))
            return false;

        BuildingEnvironmentalFireAbility ability = building.BuildingData?
            .GetAbility<BuildingEnvironmentalFireAbility>();
        EnvironmentalFireProfile profile = ability?.CreateProfileOrThrow();
        bool exists = building != null && !building.isDestroy;
        bool combustible = exists
            && profile != null
            && structuralIntegrity.TryGet(building, out _);
        bool hasElectricalHazard = power.TryGetNode(building, out _);
        snapshot = new EnvironmentalFireTargetSnapshot(
            target,
            building.centerPos,
            exists,
            combustible,
            hasElectricalHazard,
            profile,
            ability?.acceptedSources
                ?? EnvironmentalFireIgnitionSources.None);
        return true;
    }

    public IReadOnlyList<EnvironmentalFireTargetSnapshot> GetAdjacentTargets(
        EnvironmentalFireTargetRef target)
    {
        if (!TryResolveBuilding(target.TargetId, out BuildableObject source)
            || source.isDestroy
            || source.Grid == null)
        {
            return Array.Empty<EnvironmentalFireTargetSnapshot>();
        }

        var directSameFloorCells = new HashSet<Vector2Int>();
        foreach (Vector2Int cell in LiveFootprint(source))
        {
            directSameFloorCells.Add(cell + Vector2Int.left);
            directSameFloorCells.Add(cell + Vector2Int.right);
        }

        var result = new List<EnvironmentalFireTargetSnapshot>();
        foreach (BuildableObject candidate in buildings.Buildings
                     .Where(value => value != null
                         && !ReferenceEquals(value, source)
                         && !value.isDestroy
                         && ReferenceEquals(value.Grid, source.Grid)
                         && value.PersistentInstanceId.IsValid)
                     .OrderBy(
                         value => value.PersistentInstanceId.Value,
                         StringComparer.Ordinal))
        {
            BuildingEnvironmentalFireAbility ability = candidate.BuildingData?
                .GetAbility<BuildingEnvironmentalFireAbility>();
            if (ability == null
                || !ability.Accepts(EnvironmentalFireIgnitionKind.Spread)
                || !LiveFootprint(candidate).Any(cell =>
                    directSameFloorCells.Contains(cell)
                    && HasUnblockedDirectContact(source, candidate, cell)))
            {
                continue;
            }

            var candidateRef = new EnvironmentalFireTargetRef(
                EnvironmentalFireTargetKind.Building,
                candidate.PersistentInstanceId.Value);
            if (TryGetTarget(candidateRef, out EnvironmentalFireTargetSnapshot found))
                result.Add(found);
        }

        return result;
    }

    public IReadOnlyList<EnvironmentalFireTargetRef> GetExposedCharacters(
        EnvironmentalFireTargetRef source,
        Vector2Int position)
    {
        if (source.Kind != EnvironmentalFireTargetKind.Building
            || !TryResolveBuilding(source.TargetId, out BuildableObject building)
            || building.isDestroy
            || building.Grid == null
            || !building.Grid.IsValidGridPos(position))
        {
            return Array.Empty<EnvironmentalFireTargetRef>();
        }

        return (characters.Characters ?? Array.Empty<CharacterActor>())
            .Where(actor => actor != null
                && !actor.IsDead
                && actor.GetNowXY() == position
                && CharacterPersistentIdentity.TryGet(actor, out _))
            .Select(actor => new EnvironmentalFireTargetRef(
                EnvironmentalFireTargetKind.Character,
                CharacterPersistentIdentity.Require(actor).Value))
            .Distinct()
            .OrderBy(value => value.TargetId, StringComparer.Ordinal)
            .ToArray();
    }

    public bool TryApply(
        EnvironmentalFireDamageCommand command,
        out EnvironmentalFireDamageResult result)
    {
        string operationId = command.OperationId?.Trim() ?? string.Empty;
        string fingerprint = CreateDamageFingerprint(command);
        if (damageCommits.TryGetValue(operationId, out DamageCommit known))
        {
            if (!string.Equals(known.Fingerprint, fingerprint, StringComparison.Ordinal))
            {
                result = new EnvironmentalFireDamageResult(
                    false,
                    0f,
                    true,
                    "environmental-fire-damage-operation-conflict");
                return false;
            }

            result = known.Result;
            return result.Committed;
        }

        if (operationId.Length == 0
            || !IsFinite(command.Intensity)
            || command.Intensity <= 0f
            || !IsFinite(command.RequestedDamage)
            || command.RequestedDamage <= 0f)
        {
            result = new EnvironmentalFireDamageResult(
                false,
                0f,
                false,
                "environmental-fire-damage-invalid-target-or-amount");
            return false;
        }

        if (command.Target.Kind == EnvironmentalFireTargetKind.Character)
        {
            return TryApplyCharacterDamage(
                command,
                operationId,
                fingerprint,
                out result);
        }
        if (command.Target.Kind != EnvironmentalFireTargetKind.Building
            || !TryResolveBuilding(
                command.Target.TargetId,
                out BuildableObject building))
        {
            result = new EnvironmentalFireDamageResult(
                false,
                0f,
                false,
                "environmental-fire-damage-invalid-target-or-amount");
            return false;
        }

        BuildingStructuralDamageResult applied =
            structuralIntegrity.ApplyDamage(building, command.RequestedDamage);
        if (!applied.Applied)
        {
            result = new EnvironmentalFireDamageResult(
                false,
                0f,
                !building.isDestroy,
                string.IsNullOrWhiteSpace(applied.FailureReason)
                    ? "environmental-fire-structural-damage-rejected"
                    : applied.FailureReason);
            return false;
        }

        result = new EnvironmentalFireDamageResult(
            true,
            applied.Damage,
            !applied.Destroyed && !building.isDestroy);
        damageCommits.Add(
            operationId,
            new DamageCommit { Fingerprint = fingerprint, Result = result });
        return true;
    }

    public bool TryCommitPending(
        EnvironmentalFireFuelLossRequest request,
        string operationId,
        out EnvironmentalFireFuelLossReceipt receipt,
        out string failureReason)
    {
        receipt = default;
        string operationKey = operationId?.Trim() ?? string.Empty;
        if (request?.IsValid != true || operationKey.Length == 0)
        {
            failureReason = "environmental-fire-fuel-loss-invalid-request";
            return false;
        }

        if (dispositions.TryGetPending(
                operationKey,
                out PhysicalItemBatchDispositionReceipt pending))
        {
            if (!MatchesPhysicalFuelLoss(pending, request, operationKey))
            {
                failureReason = "environmental-fire-fuel-loss-operation-conflict";
                return false;
            }

            receipt = CreateFuelLossReceipt(request.Target, pending);
            failureReason = string.Empty;
            return true;
        }

        WorldItemStackSnapshot source = (items.GetAllStacks()
                ?? Array.Empty<WorldItemStackSnapshot>())
            .SingleOrDefault(value => value != null
                && string.Equals(
                    value.StackId,
                    request.Target.TargetId,
                    StringComparison.Ordinal));
        if (source == null
            || source.State != WorldItemStackState.Stored
            || source.ReservedQuantity != 0
            || source.Quantity != request.Quantity)
        {
            failureReason =
                "environmental-fire-fuel-loss-exact-unreserved-stored-lot-unavailable";
            return false;
        }

        var participant = new EnvironmentalFireFuelOutcomeParticipant(
            environmentOutcomes,
            outcomeDiagnostics,
            calendar,
            request,
            new CoreGridCell(source.Position.x, source.Position.y));
        if (!outcomeAwareDispositions.TryCommitPending(
                new[]
                {
                    new PhysicalItemTransformInput(
                        request.Target.TargetId,
                        request.Quantity)
                },
                PhysicalItemDispositionKind.Sink,
                operationKey,
                FuelLossSinkReasonCode,
                participant,
                out PhysicalItemBatchDispositionReceipt physical,
                out failureReason)
            || !MatchesPhysicalFuelLoss(physical, request, operationKey))
        {
            receipt = default;
            if (string.IsNullOrWhiteSpace(failureReason))
                failureReason = "environmental-fire-fuel-loss-receipt-mismatch";
            return false;
        }

        receipt = CreateFuelLossReceipt(request.Target, physical);
        failureReason = string.Empty;
        return true;
    }

    bool IEnvironmentalFireFuelLossSink.TryAcknowledge(
        string commitId,
        out string failureReason) =>
        dispositions.Acknowledge(commitId, out failureReason);

    public bool CanSuppress(
        string workerId,
        Vector2Int standPosition,
        EnvironmentalFireTargetRef target,
        out string failureReason)
    {
        string canonicalWorkerId = workerId?.Trim() ?? string.Empty;
        CharacterActor worker = characters.Characters.FirstOrDefault(candidate =>
            candidate != null
            && !candidate.IsDead
            && candidate.IsBuildingInteractionAvailable
            && string.Equals(
                candidate.Identity?.PersistentId,
                canonicalWorkerId,
                StringComparison.Ordinal));
        if (worker == null)
        {
            failureReason = "environmental-fire-suppression-worker-unavailable";
            return false;
        }
        if (!TryResolveBuilding(target.TargetId, out BuildableObject building)
            || !gridProvider.TryGetGrid(out Grid grid)
            || !grid.IsValidGridPos(standPosition)
            || !grid.IsWalkable(standPosition)
            || grid.GetXY(worker.transform.position) != standPosition)
        {
            failureReason = "environmental-fire-suppression-worker-not-at-stand";
            return false;
        }
        if (!building.IsWorkAccessGridPosition(grid, standPosition))
        {
            failureReason = "environmental-fire-suppression-not-at-authored-access";
            return false;
        }

        WorldHazardSnapshot standHazard = hazards.GetHazard(
            new CharacterId(canonicalWorkerId),
            standPosition);
        if (standHazard.Level == WorldHazardLevel.Forbidden)
        {
            failureReason = "environmental-fire-suppression-stand-forbidden";
            return false;
        }

        failureReason = string.Empty;
        return true;
    }

    public bool TryGetSafety(
        EnvironmentalFireTargetRef target,
        out EnvironmentalFireElectricalSafetySnapshot snapshot)
    {
        snapshot = default;
        if (target.Kind != EnvironmentalFireTargetKind.Building
            || !TryResolveBuilding(target.TargetId, out BuildableObject building)
            || !power.TryGetNode(building, out PowerNodeSnapshot node))
        {
            return false;
        }

        PowerNetworkSnapshot network = power.Networks.FirstOrDefault(candidate =>
            candidate != null
            && string.Equals(
                candidate.NetworkId,
                node.NetworkId,
                StringComparison.Ordinal));
        if (network == null)
            return false;

        bool hasContact = node.ConnectionEnabled
            && !node.BreakerTripped
            && !network.Tripped;
        snapshot = new EnvironmentalFireElectricalSafetySnapshot(
            target,
            hasContact,
            node.ConnectionEnabled,
            node.BreakerTripped || network.Tripped,
            hasContact
                && (!IsFiniteNonNegative(network.AvailableSourcePerSecond)
                    || network.AvailableSourcePerSecond > 0.001f),
            !IsFiniteNonNegative(network.StoredPower)
                || network.StoredPower > 0.001f);
        return true;
    }

    public bool TryCommitReservedWaterPending(
        string leaseId,
        int quantity,
        string operationId,
        string workerId,
        string fireId,
        Vector2Int standPosition,
        Vector2Int targetPosition,
        out EnvironmentalFireWaterReceipt receipt,
        out string failureReason)
    {
        receipt = default;
        string leaseKey = leaseId?.Trim() ?? string.Empty;
        string operationKey = operationId?.Trim() ?? string.Empty;
        string workerKey = workerId?.Trim() ?? string.Empty;
        string fireKey = fireId?.Trim() ?? string.Empty;
        string expectedDestinationId = fireKey.Length > 0
            ? EnvironmentalFireWaterDestinationIdentity.Create(fireKey)
            : string.Empty;
        ItemQuantityLease lease = null;
        DomainFailure leaseFailure = DomainFailure.None;

        if (TryGetPending(operationKey, leaseKey, out receipt))
        {
            if (receipt.Quantity == quantity)
            {
                failureReason = string.Empty;
                return true;
            }
            receipt = default;
            failureReason = "environmental-fire-water-operation-conflict";
            return false;
        }

        if (quantity <= 0
            || leaseKey.Length == 0
            || operationKey.Length == 0
            || workerKey.Length == 0
            || expectedDestinationId.Length == 0
            || !reservations.Revalidate(
                leaseKey,
                out lease,
                out leaseFailure)
            || lease == null
            || lease.remainingQuantity < quantity
            || !string.Equals(
                lease.ownerOperationId,
                operationKey,
                StringComparison.Ordinal)
            || !string.Equals(
                lease.ownerCharacterId,
                workerKey,
                StringComparison.Ordinal)
            || !string.Equals(
                lease.aggregationCohortId,
                expectedDestinationId,
                StringComparison.Ordinal))
        {
            failureReason = "environmental-fire-water-lease-invalid:"
                + (leaseFailure.IsFailure
                    ? leaseFailure.Code.ToString()
                    : operationKey);
            return false;
        }

        Dictionary<string, WorldItemStackSnapshot> stacksById = items
            .GetAllStacks()
            .Where(value => value != null
                && !string.IsNullOrWhiteSpace(value.StackId))
            .ToDictionary(value => value.StackId, StringComparer.Ordinal);
        int remaining = quantity;
        foreach (ItemLeaseSlice slice in lease.slices
                     .Where(value => value != null && value.quantity > 0)
                     .OrderBy(value => value.stackId, StringComparer.Ordinal))
        {
            if (remaining <= 0)
                break;
            if (!stacksById.TryGetValue(
                    slice.stackId,
                    out WorldItemStackSnapshot stack)
                || stack == null
                || !string.Equals(
                    stack.ItemId,
                    CleanWaterItemId,
                    StringComparison.Ordinal)
                || !IsFiniteNonNegative(stack.Contamination)
                || stack.Contamination > 0.01f + Epsilon
                || !string.Equals(
                    stack.DestinationId,
                    expectedDestinationId,
                    StringComparison.Ordinal)
                || stack.State != WorldItemStackState.FacilityBuffer
                || stack.Position != targetPosition)
            {
                failureReason =
                    "environmental-fire-water-must-be-exact-clean-buffered-stock";
                return false;
            }
            remaining -= Math.Min(remaining, slice.quantity);
        }
        if (remaining > 0)
        {
            failureReason = "environmental-fire-water-quantity-unavailable";
            return false;
        }

        if (!TryResolveCharacter(workerKey, out CharacterActor worker)
            || string.IsNullOrWhiteSpace(worker.BuildingDisplayName))
        {
            failureReason =
                "environmental-fire-water-worker-display-snapshot-missing";
            return false;
        }
        var participant = new EnvironmentalFireWaterOutcomeParticipant(
            environmentOutcomes,
            outcomeDiagnostics,
            calendar,
            leaseKey,
            new CharacterId(workerKey),
            worker.BuildingDisplayName,
            new CoreGridCell(targetPosition.x, targetPosition.y));
        if (!outcomeAwareReservedSink.TryCommitReservedSinkPending(
                leaseKey,
                quantity,
                operationKey,
                WaterSinkReasonCode,
                participant,
                out PhysicalItemBatchDispositionReceipt physical,
                out failureReason)
            || !MatchesPhysicalReceipt(physical, operationKey, quantity))
        {
            receipt = default;
            if (string.IsNullOrWhiteSpace(failureReason))
                failureReason = "environmental-fire-water-receipt-mismatch";
            return false;
        }

        receipt = new EnvironmentalFireWaterReceipt(
            operationKey,
            leaseKey,
            quantity,
            physical.CommitId);
        return true;
    }

    public bool TryGetPending(
        string operationId,
        string leaseId,
        out EnvironmentalFireWaterReceipt receipt)
    {
        receipt = default;
        string operationKey = operationId?.Trim() ?? string.Empty;
        string leaseKey = leaseId?.Trim() ?? string.Empty;
        if (operationKey.Length == 0
            || leaseKey.Length == 0
            || !dispositions.TryGetPending(
                operationKey,
                out PhysicalItemBatchDispositionReceipt physical)
            || !MatchesPhysicalReceipt(
                physical,
                operationKey,
                physical.Quantity))
        {
            return false;
        }

        receipt = new EnvironmentalFireWaterReceipt(
            operationKey,
            leaseKey,
            physical.Quantity,
            physical.CommitId);
        return receipt.IsCommitted;
    }

    public bool TryAcknowledge(string commitId, out string failureReason) =>
        dispositions.Acknowledge(commitId, out failureReason);

    private bool TryResolveBuilding(
        string targetId,
        out BuildableObject building)
    {
        string canonical = targetId?.Trim() ?? string.Empty;
        building = buildings.Buildings.FirstOrDefault(candidate =>
            candidate != null
            && candidate.PersistentInstanceId.IsValid
            && string.Equals(
                candidate.PersistentInstanceId.Value,
                canonical,
                StringComparison.Ordinal));
        return building != null;
    }

    private bool TryResolveCharacter(
        string targetId,
        out CharacterActor actor)
    {
        string canonical = targetId?.Trim() ?? string.Empty;
        actor = (characters.Characters ?? Array.Empty<CharacterActor>())
            .FirstOrDefault(candidate =>
                CharacterPersistentIdentity.TryGet(candidate, out CharacterId id)
                && string.Equals(id.Value, canonical, StringComparison.Ordinal));
        return actor != null;
    }

    private bool TryApplyCharacterDamage(
        EnvironmentalFireDamageCommand command,
        string operationId,
        string fingerprint,
        out EnvironmentalFireDamageResult result)
    {
        if (protection == null
            || !TryResolveCharacter(command.Target.TargetId, out CharacterActor actor)
            || actor.IsDead)
        {
            result = new EnvironmentalFireDamageResult(
                false,
                0f,
                false,
                "environmental-fire-body-damage-target-unavailable");
            return false;
        }

        ThermalProtectionProfile resolved = protection.Resolve(actor);
        if (resolved == null
            || !IsFinite(resolved.heatExposureMultiplier)
            || resolved.heatExposureMultiplier <= 0f)
        {
            result = new EnvironmentalFireDamageResult(
                false,
                0f,
                true,
                "environmental-fire-body-damage-protection-invalid");
            return false;
        }

        float multiplier = Mathf.Clamp(
            resolved.heatExposureMultiplier,
            0.05f,
            2f);
        float requestedDamage = command.RequestedDamage * multiplier;
        if (!IsFinite(requestedDamage) || requestedDamage <= 0f)
        {
            result = new EnvironmentalFireDamageResult(
                false,
                0f,
                true,
                "environmental-fire-body-damage-amount-invalid");
            return false;
        }

        float healthBefore = actor.CurrentHealth;
        actor.ApplyDamage(
            requestedDamage,
            string.Concat("environmental-fire:", command.FireId));
        float appliedDamage = Math.Max(0f, healthBefore - actor.CurrentHealth);
        result = new EnvironmentalFireDamageResult(
            true,
            appliedDamage,
            !actor.IsDead);
        damageCommits.Add(
            operationId,
            new DamageCommit { Fingerprint = fingerprint, Result = result });
        return true;
    }

    private static IEnumerable<Vector2Int> LiveFootprint(
        BuildableObject building)
    {
        if (building?.Grid == null || building.isDestroy)
            yield break;

        IReadOnlyList<Vector2Int> authored = building.buildPoses != null
            && building.buildPoses.Count > 0
                ? building.buildPoses
                : (IReadOnlyList<Vector2Int>)building.BuildingData?
                    .GetGridPosList(building.centerPos)
                    ?? Array.Empty<Vector2Int>();
        foreach (Vector2Int cell in authored.Distinct())
        {
            if (building.Grid.IsValidGridPos(cell)
                && building.Grid.GetGridCell(cell)?.ContainsOccupant(building)
                    == true)
            {
                yield return cell;
            }
        }
    }

    private static bool HasUnblockedDirectContact(
        BuildableObject source,
        BuildableObject candidate,
        Vector2Int candidateCell)
    {
        foreach (Vector2Int sourceCell in LiveFootprint(source))
        {
            if (candidateCell != sourceCell + Vector2Int.left
                && candidateCell != sourceCell + Vector2Int.right)
            {
                continue;
            }

            if (!HasBlockingBoundary(
                    source.Grid.GetGridCell(sourceCell),
                    source,
                    candidate)
                && !HasBlockingBoundary(
                    source.Grid.GetGridCell(candidateCell),
                    source,
                    candidate))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasBlockingBoundary(
        GridCell cell,
        BuildableObject source,
        BuildableObject candidate) =>
        cell?.GetAllOccupants()
            .OfType<BuildableObject>()
            .Any(value => value != null
                && !ReferenceEquals(value, source)
                && !ReferenceEquals(value, candidate)
                && !value.isDestroy
                && IsClosedBoundary(value))
            == true;

    private static bool IsClosedBoundary(BuildableObject value)
    {
        bool isDoor = value is Door || value.BuildingData?.IsDoor == true;
        if (isDoor)
            return value is not Door door || !door.IsOpen;
        return value.BuildingData?.IsStructuralWall == true;
    }

    private static EnvironmentalFireFuelLossReceipt CreateFuelLossReceipt(
        EnvironmentalFireTargetRef target,
        PhysicalItemBatchDispositionReceipt physical) =>
        new(
            target,
            physical.OperationId,
            physical.Quantity,
            physical.InputMassGrams,
            physical.CommitId);

    private static bool MatchesPhysicalFuelLoss(
        PhysicalItemBatchDispositionReceipt receipt,
        EnvironmentalFireFuelLossRequest request,
        string operationId) =>
        receipt.IsCommitted
        && receipt.Kind == PhysicalItemDispositionKind.Sink
        && receipt.Quantity == request.Quantity
        && receipt.InputMassGrams > 0L
        && receipt.SourceStackIds != null
        && receipt.SourceStackIds.Count == 1
        && string.Equals(
            receipt.SourceStackIds[0],
            request.Target.TargetId,
            StringComparison.Ordinal)
        && string.Equals(
            receipt.OperationId,
            operationId,
            StringComparison.Ordinal)
        && string.Equals(
            receipt.ReasonCode,
            FuelLossSinkReasonCode,
            StringComparison.Ordinal);

    private static bool MatchesPhysicalReceipt(
        PhysicalItemBatchDispositionReceipt receipt,
        string operationId,
        int quantity) =>
        receipt.IsCommitted
        && receipt.Kind == PhysicalItemDispositionKind.Sink
        && receipt.Quantity == quantity
        && string.Equals(
            receipt.OperationId,
            operationId,
            StringComparison.Ordinal)
        && string.Equals(
            receipt.ReasonCode,
            WaterSinkReasonCode,
            StringComparison.Ordinal);

    private static string CreateDamageFingerprint(
        EnvironmentalFireDamageCommand command) => string.Join(
        "|",
        command.FireId,
        (int)command.Target.Kind,
        command.Target.TargetId,
        command.Position.x,
        command.Position.y,
        command.Intensity.ToString("R", CultureInfo.InvariantCulture),
        command.RequestedDamage.ToString("R", CultureInfo.InvariantCulture));

    private static bool IsFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);

    private static bool IsFiniteNonNegative(float value) =>
        IsFinite(value) && value >= 0f;
}

public sealed class ElectricalEnvironmentalFireProducer : ITickable
{
    public const string RiskKeyPrefix =
        "environment:fire:electrical-ignition:";
    private const float Epsilon = 0.001f;
    private readonly IGameClock clock;
    private readonly IGameCalendar calendar;
    private readonly IPowerInfrastructureQuery power;
    private readonly IBuildingWorldQuery buildings;
    private readonly IEnvironmentalFireCommand fire;

    public ElectricalEnvironmentalFireProducer(
        IGameClock clock,
        IGameCalendar calendar,
        IPowerInfrastructureQuery power,
        IBuildingWorldQuery buildings,
        IEnvironmentalFireCommand fire)
    {
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.power = power ?? throw new ArgumentNullException(nameof(power));
        this.buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
        this.fire = fire ?? throw new ArgumentNullException(nameof(fire));
    }

    public void Tick()
    {
        if (clock.IsPaused || !calendar.IsRunning)
            return;
        float delta = clock.DeltaTime;
        if (!IsFiniteNonNegative(delta)
            || calendar.Day < 1
            || !IsFiniteNonNegative(calendar.ElapsedSeconds))
        {
            throw new InvalidOperationException(
                "Electrical-fire scheduling requires finite nonnegative game time.");
        }
        if (delta <= 0f)
            return;

        double absoluteNow = Math.Max(
            0d,
            (calendar.Day - 1d) * GameSimulationTimeRules.SecondsPerDay
                + calendar.ElapsedSeconds);
        double absoluteBefore = Math.Max(0d, absoluteNow - delta);
        Dictionary<string, BuildableObject> liveById = buildings.Buildings
            .Where(value => value != null
                && !value.isDestroy
                && value.PersistentInstanceId.IsValid)
            .ToDictionary(
                value => value.PersistentInstanceId.Value,
                StringComparer.Ordinal);

        foreach (PowerNetworkSnapshot network in power.Networks
                     .Where(value => value != null)
                     .OrderBy(value => value.NetworkId, StringComparer.Ordinal))
        {
            if (network.Tripped
                || !IsFiniteNonNegative(network.AvailableSourcePerSecond)
                || !IsFiniteNonNegative(network.DemandPerSecond)
                || network.AvailableSourcePerSecond <= Epsilon
                || network.DemandPerSecond
                    <= network.AvailableSourcePerSecond + Epsilon)
            {
                continue;
            }

            (PowerNodeSnapshot Node, BuildableObject Building,
                BuildingEnvironmentalFireAbility Ability,
                long FirstWindow, long LastWindow) chosen = default;
            bool hasChosen = false;
            foreach (PowerNodeSnapshot node in network.Nodes
                         .Where(value => value != null
                             && value.ConnectionEnabled
                             && !value.BreakerTripped)
                         .OrderBy(value => value.BuildingId.Value, StringComparer.Ordinal))
            {
                if (!liveById.TryGetValue(
                        node.BuildingId.Value,
                        out BuildableObject building))
                {
                    continue;
                }
                BuildingEnvironmentalFireAbility ability = building.BuildingData?
                    .GetAbility<BuildingEnvironmentalFireAbility>();
                if (ability == null
                    || !ability.Accepts(
                        EnvironmentalFireIgnitionKind.ElectricalFault))
                {
                    continue;
                }
                ability.CreateProfileOrThrow();
                if (!IsFiniteNonNegative(node.Heat)
                    || !IsFiniteNonNegative(node.Fault)
                    || node.Heat + Epsilon < ability.electricalIgnitionHeat
                    || node.Fault + Epsilon < ability.electricalIgnitionFault)
                {
                    continue;
                }

                double windowSeconds = ability.electricalIgnitionWindowSeconds;
                long lastWindow = (long)Math.Floor(absoluteNow / windowSeconds);
                long previousWindow =
                    (long)Math.Floor(absoluteBefore / windowSeconds);
                if (lastWindow <= previousWindow)
                    continue;

                chosen = (
                    node,
                    building,
                    ability,
                    previousWindow + 1L,
                    lastWindow);
                hasChosen = true;
                break;
            }

            if (!hasChosen)
                continue;

            for (long window = chosen.FirstWindow;
                 window <= chosen.LastWindow;
                 window++)
            {
                TryIgniteWindow(network, chosen.Building, chosen.Ability, window);
                if (window == long.MaxValue)
                    break;
            }
        }
    }

    private void TryIgniteWindow(
        PowerNetworkSnapshot network,
        BuildableObject building,
        BuildingEnvironmentalFireAbility ability,
        long window)
    {
        string windowToken = ability.electricalIgnitionWindowSeconds
            .ToString("R", CultureInfo.InvariantCulture);
        string causeId = string.Concat(
            "environmental-fire-electrical:",
            network.NetworkId,
            ":",
            windowToken,
            ":",
            window);
        float riskSample = (PersistentEntityId.GetStableHash32(
                RiskKeyPrefix + causeId) & 0x00ffffffu)
            / 16777216f;
        if (riskSample >= ability.electricalIgnitionChancePerWindow)
            return;

        string evidenceId = string.Concat(
            "power-overload-window:",
            network.NetworkId,
            ":heat>=",
            ability.electricalIgnitionHeat.ToString(
                "R",
                CultureInfo.InvariantCulture),
            ":fault>=",
            ability.electricalIgnitionFault.ToString(
                "R",
                CultureInfo.InvariantCulture),
            ":chance=",
            ability.electricalIgnitionChancePerWindow.ToString(
                "R",
                CultureInfo.InvariantCulture),
            ":sample=",
            riskSample.ToString("R", CultureInfo.InvariantCulture));
        EnvironmentalFireIgnitionResult result = fire.TryIgnite(
            new EnvironmentalFireIgnitionRequest(
                causeId,
                EnvironmentalFireIgnitionKind.ElectricalFault,
                network.NetworkId,
                new EnvironmentalFireTargetRef(
                    EnvironmentalFireTargetKind.Building,
                    building.PersistentInstanceId.Value),
                ability.electricalIgnitionIntensity,
                evidenceId));
        if (result.Disposition ==
            EnvironmentalFireIgnitionDisposition.CauseConflict)
        {
            throw new InvalidOperationException(
                $"Electrical fire window '{causeId}' produced conflicting evidence.");
        }
    }

    private static bool IsFiniteNonNegative(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
}

public sealed class EnvironmentalFireApplicationAdapter : ITickable
{
    private const string OverlayId = "environmental-fire:active";
    private const float RecoveryInterval = 1f;
    private readonly IGameClock clock;
    private readonly EnvironmentalFireRuntime fire;
    private readonly IWorldHazardOverlayCommand hazards;
    private readonly HashSet<Vector2Int> publishedCells = new();
    private float recoveryAccumulator = RecoveryInterval;

    public EnvironmentalFireApplicationAdapter(
        IGameClock clock,
        EnvironmentalFireRuntime fire,
        IWorldHazardOverlayCommand hazards)
    {
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.fire = fire ?? throw new ArgumentNullException(nameof(fire));
        this.hazards = hazards ?? throw new ArgumentNullException(nameof(hazards));
    }

    public void Tick()
    {
        float delta = !clock.IsPaused && clock.DeltaTime > 0f
            ? clock.DeltaTime
            : 0f;
        if (delta > 0f)
            fire.Advance(delta);

        recoveryAccumulator += delta;
        if (recoveryAccumulator >= RecoveryInterval)
        {
            recoveryAccumulator = 0f;
            fire.ResumePendingSuppressions();
        }

        HashSet<Vector2Int> next = new HashSet<Vector2Int>(fire.GetActiveFireCells());
        if (next.SetEquals(publishedCells))
            return;

        publishedCells.Clear();
        publishedCells.UnionWith(next);
        if (next.Count == 0)
            hazards.RemoveOverlay(OverlayId);
        else
            hazards.ReplaceOverlay(OverlayId, WorldHazardFlags.Fire, next);
    }
}
