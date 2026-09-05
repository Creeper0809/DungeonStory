#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
    private const string StandardCapabilityPath =
        "Assets/Scripts/Services/Economy/StandardDefinitionProductionOutputHandler.cs";
    private const string AssemblyBridgePath =
        "Assets/Scripts/Services/Economy/ProductionAssemblyBridgeAdapter.cs";
    private const string BillRuntimePath =
        "Assets/Scripts/Models/Economy/Content/ProductionBillRuntime.cs";
    private const string CapabilitySelectionPath =
        "Assets/Scripts/Models/Economy/Content/ProductionPreparedOutputExecutionPort.cs";
    private const string LegacyOutputExecutionPath =
        "Assets/Scripts/Models/Economy/Content/ProductionOutputExecutionService.cs";
    private const string CapacityProjectorPath =
        "Assets/Scripts/Services/Economy/ProductionOutputBufferCapacityProjector.cs";
    private const string PreparedExecutionAdapterPath =
        "Assets/Scripts/Services/Economy/ProductionPreparedOutputExecutionAdapter.cs";
    private const string DetachedCapacityProjectorPath =
        "Assets/Scripts/Services/Economy/ProductionOutputDestinationDurableSaveProjector.cs";
    private const string DestructiveDrainCapacityValidationPath =
        "Assets/Scripts/Services/Economy/ProductionFacilityDestructiveDrainCrossAggregateSaveValidation.cs";
    private const string WorldSimulationRegistrationPath =
        "Assets/Scripts/Services/Infrastructure/Registration/DungeonWorldSimulationRegistration.cs";

    [MenuItem("DungeonStory/Debug/Items/Validate Prepared Output Legacy Bypasses")]
    public static void RunAll()
    {
        string distribution = Read(DistributionPath);
        RequireOrdered(
            distribution,
            DistributionPath,
            "ProductionPreparedOutputCapabilitySelection",
            ".UsesPreparedOutputMaterializer(recipe, bridge)",
            "TryProgressPreparedOutputRoute(",
            "continue;",
            "bridge.CountBufferedOutput(");

        string standardCapability = Read(StandardCapabilityPath);
        RequireContains(
            standardCapability,
            StandardCapabilityPath,
            "public sealed class StandardDefinitionProductionOutputCapability",
            "IProductionOutputCapability");
        RequireNotContains(
            standardCapability,
            StandardCapabilityPath,
            ":\n    IProductionOutputHandler",
            ":\r\n    IProductionOutputHandler",
            "TryProduceIdempotent(",
            "TryCommitBufferedOutput(");

        string assemblyBridge = Read(AssemblyBridgePath);
        string validation = Slice(
            assemblyBridge,
            AssemblyBridgePath,
            "public bool TryValidateOutputCapability(",
            "public bool TryHandleOutput(");
        RequireContains(
            validation,
            AssemblyBridgePath + ":TryValidateOutputCapability",
            "outputHandlers.TryValidateExact(");
        RequireNotContains(
            validation,
            AssemblyBridgePath + ":TryValidateOutputCapability",
            "TryResolveExact(");

        string billRuntime = Read(BillRuntimePath);
        string ruinedBatch = Slice(
            billRuntime,
            BillRuntimePath,
            "private bool TryConvertRuinedBatch(",
            "private static bool IsCompletedRuinedBatchDispositionValid(");
        RequireContains(
            ruinedBatch,
            BillRuntimePath + ":TryConvertRuinedBatch",
            "UsesPreparedOutput(recipe)",
            "ruined-output-capability-unsupported");
        RequireNotContains(
            ruinedBatch,
            BillRuntimePath + ":TryConvertRuinedBatch",
            ".SpawnOutput(",
            "recipe:silage");

        RequireContains(
            Read(CapabilitySelectionPath),
            CapabilitySelectionPath,
            "ClassifyPhysicalCapabilities(",
            "mixed-prepared-output-capability-route-unsupported");
        RequireContains(
            Read(LegacyOutputExecutionPath),
            LegacyOutputExecutionPath,
            "ClassifyPhysicalCapabilities(",
            "bridge.OutputCapabilityContracts",
            "materialized-output-requires-prepared-batch");

        string preparedAdapter = Read(PreparedExecutionAdapterPath);
        RequireContains(
            preparedAdapter,
            PreparedExecutionAdapterPath,
            "IProductionPreparedOutputMaterializerRegistry",
            "materializers.Create(",
            "materializers.ValidateAndDecode(",
            "materializers.ValidateDescriptor(");
        RequireNotContains(
            preparedAdapter,
            PreparedExecutionAdapterPath,
            "ProductionOutputCapabilityIds.StandardDefinition",
            "ProductionOutputCapabilityIds.DefinitionOnlyCodec");

        RequireRuntimeWriteSurfaceRestrictedToOwners();
        RequireRawCapacityProjectorRestrictedToPreparedOwners();

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
            + "distribution=capability-derived; item-only=excluded; direct-stack=guarded; "
            + "release/remove=guarded; conveyor=guarded; runtime-direct=guarded; "
            + "pickup-precommit=atomic-preflight; standard=descriptor-only; "
            + "mixed-output=preflight-rejected; orphan-write-callers=0; "
            + "raw-capacity-interface=removed; raw-capacity-public-overloads=0; "
            + "raw-capacity-callers=0; concrete-capacity-owners=2.");
    }

    private static void RequireRawCapacityProjectorRestrictedToPreparedOwners()
    {
        string interfaceSource = Slice(
            Read(CapacityProjectorPath),
            CapacityProjectorPath,
            "public interface IProductionOutputBufferCapacityProjector",
            "public sealed class ProductionOutputBufferCapacityProjector");
        RequireNotContains(
            interfaceSource,
            CapacityProjectorPath
                + ":IProductionOutputBufferCapacityProjector",
            "long exactBatchMassGrams");

        string concreteSource = Read(CapacityProjectorPath);
        if (Regex.IsMatch(
                concreteSource,
                @"public\s+ProductionOutputBufferCapacitySourceSnapshot\s+CaptureSource\s*\(\s*(?:ProductionFacilityHandle|ProductionFacilityCapacitySubject)\s+[A-Za-z_][A-Za-z0-9_]*\s*,\s*long\s+",
                RegexOptions.CultureInvariant))
        {
            throw new InvalidOperationException(
                "Concrete capacity projector exposes a raw long CaptureSource overload.");
        }

        string[] rawCallers = Directory
            .EnumerateFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories)
            .Select(path => path.Replace('\\', '/'))
            .Where(path => Regex.IsMatch(
                File.ReadAllText(path),
                @"\.CaptureSource\s*\([^;]{0,320},\s*(?:exactBatchMassGrams\s*:\s*)?(?:[0-9_]+L|[A-Za-z_][A-Za-z0-9_]*(?:BatchMassGrams|batchMass))\s*\)",
                RegexOptions.CultureInvariant))
            .Select(path => "Assets" + path.Substring(
                Application.dataPath.Replace('\\', '/').Length))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        if (rawCallers.Length != 0)
        {
            throw new InvalidOperationException(
                "Raw capacity CaptureSource caller escaped proof/claim authority: "
                + string.Join(",", rawCallers));
        }

        string[] allowed =
        {
            CapacityProjectorPath,
            PreparedExecutionAdapterPath,
            DetachedCapacityProjectorPath,
            DestructiveDrainCapacityValidationPath,
            WorldSimulationRegistrationPath
        };
        string[] unexpected = Directory
            .EnumerateFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories)
            .Select(path => path.Replace('\\', '/'))
            .Where(path => path.IndexOf("/Editor/", StringComparison.Ordinal) < 0)
            .Where(path => Regex.IsMatch(
                File.ReadAllText(path),
                @"(?<![A-Za-z0-9_])ProductionOutputBufferCapacityProjector(?![A-Za-z0-9_])"))
            .Select(path => "Assets" + path.Substring(
                Application.dataPath.Replace('\\', '/').Length))
            .Where(path => !allowed.Contains(path, StringComparer.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        if (unexpected.Length != 0)
        {
            throw new InvalidOperationException(
                "Concrete raw capacity projector escaped its generic prepared owners: "
                + string.Join(",", unexpected));
        }
    }

    private static void RequireRuntimeWriteSurfaceRestrictedToOwners()
    {
        string[] allowed =
        {
            "Assets/Scripts/Models/Economy/Content/ProductionAssemblyBridge.cs",
            AssemblyBridgePath,
            "Assets/Scripts/Services/Economy/ProductionItemGateway.cs"
        };
        string[] tokens =
        {
            "SpawnBufferedOutput(",
            "TryCommitBufferedOutput(",
            "AcknowledgeBufferedOutput(",
            "TryGetBufferedOutputCommitMassGrams("
        };
        foreach (string token in tokens)
        {
            string[] unexpected = Directory
                .EnumerateFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories)
                .Select(path => path.Replace('\\', '/'))
                .Where(path => path.IndexOf("/Editor/", StringComparison.Ordinal) < 0)
                .Where(path => File.ReadAllText(path)
                    .IndexOf(token, StringComparison.Ordinal) >= 0)
                .Select(path => "Assets" + path.Substring(
                    Application.dataPath.Replace('\\', '/').Length))
                .Where(path => !allowed.Contains(path, StringComparer.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            if (unexpected.Length != 0)
            {
                throw new InvalidOperationException(
                    "Prepared-output orphan write surface gained a runtime caller: "
                    + token
                    + ":"
                    + string.Join(",", unexpected));
            }
        }
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

    private static void RequireContains(
        string source,
        string path,
        params string[] required)
    {
        foreach (string token in required ?? Array.Empty<string>())
        {
            if (source.IndexOf(token, StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException(
                    $"Prepared-output bypass manifest token missing: {path}:{token}");
            }
        }
    }

    private static void RequireNotContains(
        string source,
        string path,
        params string[] forbidden)
    {
        foreach (string token in forbidden ?? Array.Empty<string>())
        {
            if (source.IndexOf(token, StringComparison.Ordinal) >= 0)
            {
                throw new InvalidOperationException(
                    $"Prepared-output bypass manifest forbidden token present: {path}:{token}");
            }
        }
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
