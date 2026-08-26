#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class PreparedOutputLegacyBypassStaticDiagnostics
{
    private const string DistributionPath =
        "Assets/Scripts/Models/Economy/Content/ProductionDistributionRuntime.cs";
    private const string TransferPath =
        "Assets/Scripts/Services/Items/ItemTransferService.cs";
    private const string WarehousePath =
        "Assets/Scripts/Services/Items/WorldItemWarehouseService.cs";
    private const string RuntimePath =
        "Assets/Scripts/Services/Items/WorldItemStackRuntime.cs";

    [MenuItem("DungeonStory/Debug/Items/Validate Prepared Output Legacy Bypasses")]
    public static void RunAll()
    {
        string distribution = Read(DistributionPath);
        RequireOrdered(
            distribution,
            DistributionPath,
            "ProductionPreparedOutputMigrationScope.Contains(bill.recipeId)",
            "continue;",
            "bridge.CountBufferedOutput(");

        string transfers = Read(TransferPath);
        RequireMethodGuard(
            transfers,
            TransferPath,
            "public bool TryRouteFacilityOutput(",
            "public void PrioritizeDestination(",
            "FacilityOutputExactRouteCustodyCodec.HasAnyCustody(",
            "source.quantity -= moved");
        RequireMethodGuard(
            transfers,
            TransferPath,
            "public int ReleaseDestination(",
            "public int RemoveDestination(",
            "FacilityOutputExactRouteCustodyCodec.HasAnyCustody(",
            "target.state = state");
        RequireMethodGuard(
            transfers,
            TransferPath,
            "public int RemoveDestination(",
            "public bool TryBeginTransit(",
            "FacilityOutputExactRouteCustodyCodec.HasAnyCustody(",
            "repository.Remove(target)");
        RequireMethodGuard(
            transfers,
            TransferPath,
            "public bool TryBeginTransit(",
            "public bool TryInspectStackForTransit(",
            "FacilityOutputExactRouteCustodyCodec.HasAnyCustody(",
            "record.state = WorldItemStackState.InTransit");
        RequireMethodContains(
            transfers,
            TransferPath,
            "public void CopyLoadableTransitStackIds(",
            "public bool TryGetTransitStack(",
            "!FacilityOutputExactRouteCustodyCodec.HasAnyCustody(");
        RequireOrdered(
            Slice(
                transfers,
                TransferPath,
                "public bool TryPickupReservedStackQuantity(",
                "public bool TryDepositCarriedItems("),
            TransferPath + ":TryPickupReservedStackQuantity",
            "ValidatePreparedOutputPickupBoundary(",
            "TryWithdrawOutboundStoredStock(",
            "TryExtractReservedQuantity(",
            "TryAddLeasedPartialStack(");

        string warehouses = Read(WarehousePath);
        RequireMethodContains(
            warehouses,
            WarehousePath,
            "private bool TryRetargetDeliveryAtomically(",
            "private bool TryCommitDeliveryRetargetPlan(",
            "!FacilityOutputExactRouteCustodyCodec.HasAnyCustody(");
        RequireMethodGuard(
            warehouses,
            WarehousePath,
            "private bool TryCommitDeliveryRetargetPlan(",
            "public bool TryRequestCategoryDelivery(",
            "FacilityOutputExactRouteCustodyCodec.HasAnyCustody(",
            "slice.Source.quantity -= slice.Quantity");
        RequireMethodGuard(
            warehouses,
            WarehousePath,
            "public bool TryRequestStackDelivery(",
            "public void NormalizeStorageIds(",
            "FacilityOutputExactRouteCustodyCodec.HasAnyCustody(",
            "source.quantity -= moved");
        RequireMethodContains(
            warehouses,
            WarehousePath,
            "private int CountLooseAvailable(",
            "private int CountUnassignedStored(",
            "!FacilityOutputExactRouteCustodyCodec.HasAnyCustody(");
        RequireMethodContains(
            warehouses,
            WarehousePath,
            "private int CountUnassignedStored(",
            "private int AddStoredItems(",
            "!FacilityOutputExactRouteCustodyCodec.HasAnyCustody(");

        string runtime = Read(RuntimePath);
        RequireMethodGuard(
            runtime,
            RuntimePath,
            "public bool TryRouteStackToDestination(",
            "public bool TryAbsorbUniqueItemStack(",
            "FacilityOutputExactRouteCustodyCodec.HasAnyCustody(",
            "record.destinationId = canonicalDestination");
        RequireMethodGuard(
            runtime,
            RuntimePath,
            "public int RemoveStacksByStateAndDestination(",
            "public int ReleaseStacksByDestination(",
            "FacilityOutputExactRouteCustodyCodec.HasAnyCustody(",
            "RemoveRecord(target)");
        RequireMethodGuard(
            runtime,
            RuntimePath,
            "public int ReleaseStacksByDestination(",
            "private int Spawn(",
            "FacilityOutputExactRouteCustodyCodec.HasAnyCustody(",
            "target.destinationId = string.Empty");

        Debug.Log(
            "Prepared-output legacy bypass manifest passed: "
            + "distribution=exact-scope; item-only=excluded; direct-stack=guarded; "
            + "release/remove=guarded; conveyor=guarded; runtime-direct=guarded; "
            + "pickup-precommit=atomic-preflight.");
    }

    private static string Read(string path)
    {
        if (!File.Exists(path))
            throw new InvalidOperationException("Missing manifest source: " + path);
        return File.ReadAllText(path);
    }

    private static void RequireMethodContains(
        string source,
        string path,
        string start,
        string end,
        string required)
    {
        string body = Slice(source, path, start, end);
        if (body.IndexOf(required, StringComparison.Ordinal) < 0)
            throw new InvalidOperationException(
                $"Prepared-output bypass guard missing: {path}:{start}:{required}");
    }

    private static void RequireMethodGuard(
        string source,
        string path,
        string start,
        string end,
        string guard,
        string mutation)
    {
        string body = Slice(source, path, start, end);
        RequireOrdered(body, path + ":" + start, guard, mutation);
    }

    private static string Slice(
        string source,
        string path,
        string start,
        string end)
    {
        int first = source.IndexOf(start, StringComparison.Ordinal);
        int last = source.IndexOf(end, first + Math.Max(1, start.Length),
            StringComparison.Ordinal);
        if (first < 0 || last <= first)
            throw new InvalidOperationException(
                $"Prepared-output bypass manifest boundary missing: {path}:{start}->{end}");
        return source.Substring(first, last - first);
    }

    private static void RequireOrdered(
        string source,
        string path,
        params string[] tokens)
    {
        int cursor = -1;
        foreach (string token in tokens ?? Array.Empty<string>())
        {
            int found = source.IndexOf(token, cursor + 1, StringComparison.Ordinal);
            if (found < 0)
                throw new InvalidOperationException(
                    $"Prepared-output bypass manifest token missing or reordered: {path}:{token}");
            cursor = found;
        }
    }
}
#endif
