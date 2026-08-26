using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class
    ProductionFacilityDestructiveDrainParticipantRegistryDebugScenarios
{
    [MenuItem(
        "DungeonStory/Debug/Economy/Run Production Facility Destructive Drain Participant Registry Contracts")]
    public static void RunAll()
    {
        IProductionFacilityDestructiveDrainParticipant[] canonical =
            CreateRequiredParticipants();
        ProductionFacilityDestructiveDrainParticipantRegistry forward =
            new(canonical);
        ProductionFacilityDestructiveDrainParticipantRegistry reversed =
            new(canonical.Reverse());

        string[] expectedExecution =
        {
            ProductionFacilityDestructiveDrainParticipantIds.ApparelWorkOrders,
            ProductionFacilityDestructiveDrainParticipantIds
                .CombatEquipmentCrafting,
            ProductionFacilityDestructiveDrainParticipantIds
                .GenericProductionBills,
            ProductionFacilityDestructiveDrainParticipantIds
                .CapacityRoutingOutbox,
            ProductionFacilityDestructiveDrainParticipantIds
                .PhysicalCustodyCarryRecovery
        };
        Require(
            forward.ExecutionOrder.Select(value => value.ParticipantId)
                .SequenceEqual(expectedExecution, StringComparer.Ordinal),
            "destructive-drain participant execution order is not the canonical DAG order");
        Require(
            string.Equals(
                forward.RegistryFingerprint,
                reversed.RegistryFingerprint,
                StringComparison.Ordinal)
            && string.Equals(
                forward.RegistryFingerprint,
                ProductionFacilityDestructiveDrainParticipantRegistry
                    .ExpectedRegistryFingerprint,
                StringComparison.Ordinal),
            "destructive-drain registry fingerprint drifted or depended on DI order");

        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        CultureInfo previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            CultureInfo.CurrentUICulture =
                CultureInfo.GetCultureInfo("tr-TR");
            ProductionFacilityDestructiveDrainParticipantRegistry culture =
                new(CreateRequiredParticipants().Reverse());
            Require(
                string.Equals(
                    culture.RegistryFingerprint,
                    ProductionFacilityDestructiveDrainParticipantRegistry
                        .ExpectedRegistryFingerprint,
                    StringComparison.Ordinal),
                "destructive-drain registry fingerprint depended on culture");
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }

        RequireThrows(
            () => new ProductionFacilityDestructiveDrainParticipantRegistry(
                canonical.Take(4)),
            "a missing required destructive-drain participant was accepted");
        RequireThrows(
            () => new ProductionFacilityDestructiveDrainParticipantRegistry(
                canonical.Concat(new[]
                {
                    new FakeParticipant("unexpected-participant", 1)
                })),
            "an extra destructive-drain participant was accepted");
        RequireThrows(
            () => new ProductionFacilityDestructiveDrainParticipantRegistry(
                canonical.Concat(new[] { canonical[0] })),
            "a duplicate destructive-drain participant was accepted");

        IProductionFacilityDestructiveDrainParticipant[] versionDrift =
            CreateRequiredParticipants();
        versionDrift[0] = new FakeParticipant(
            versionDrift[0].ParticipantId,
            2,
            versionDrift[0].DependsOnParticipantIds);
        RequireThrows(
            () => new ProductionFacilityDestructiveDrainParticipantRegistry(
                versionDrift),
            "a destructive-drain participant contract-version drift was accepted");

        IProductionFacilityDestructiveDrainParticipant[] edgeDrift =
            CreateRequiredParticipants();
        edgeDrift[3] = new FakeParticipant(
            ProductionFacilityDestructiveDrainParticipantIds
                .GenericProductionBills,
            1,
            new[]
            {
                ProductionFacilityDestructiveDrainParticipantIds
                    .CombatEquipmentCrafting
            });
        RequireThrows(
            () => new ProductionFacilityDestructiveDrainParticipantRegistry(
                edgeDrift),
            "a destructive-drain participant dependency drift was accepted");

        IProductionFacilityDestructiveDrainParticipant[] unknownDependency =
            CreateRequiredParticipants();
        unknownDependency[0] = new FakeParticipant(
            unknownDependency[0].ParticipantId,
            1,
            new[] { "unknown-participant" });
        RequireThrows(
            () => new ProductionFacilityDestructiveDrainParticipantRegistry(
                unknownDependency),
            "an unknown destructive-drain dependency was accepted");

        IProductionFacilityDestructiveDrainParticipant[] selfDependency =
            CreateRequiredParticipants();
        selfDependency[0] = new FakeParticipant(
            selfDependency[0].ParticipantId,
            1,
            new[] { selfDependency[0].ParticipantId });
        RequireThrows(
            () => new ProductionFacilityDestructiveDrainParticipantRegistry(
                selfDependency),
            "a self-dependent destructive-drain participant was accepted");

        IProductionFacilityDestructiveDrainParticipant[] duplicateDependency =
            CreateRequiredParticipants();
        duplicateDependency[1] = new FakeParticipant(
            duplicateDependency[1].ParticipantId,
            1,
            new[]
            {
                ProductionFacilityDestructiveDrainParticipantIds
                    .ApparelWorkOrders,
                ProductionFacilityDestructiveDrainParticipantIds
                    .ApparelWorkOrders
            });
        RequireThrows(
            () => new ProductionFacilityDestructiveDrainParticipantRegistry(
                duplicateDependency),
            "a duplicate destructive-drain dependency was accepted");

        IProductionFacilityDestructiveDrainParticipant[] cycle =
            CreateRequiredParticipants();
        cycle[0] = new FakeParticipant(
            ProductionFacilityDestructiveDrainParticipantIds.ApparelWorkOrders,
            1,
            new[]
            {
                ProductionFacilityDestructiveDrainParticipantIds
                    .PhysicalCustodyCarryRecovery
            });
        RequireThrows(
            () => new ProductionFacilityDestructiveDrainParticipantRegistry(
                cycle),
            "a destructive-drain participant dependency cycle was accepted");

        Debug.Log(
            "Production facility destructive-drain participant registry contracts passed."
            + " fingerprint=" + forward.RegistryFingerprint);
    }

    public static ProductionFacilityDestructiveDrainParticipantRegistry
        CreateRegistry() => new(CreateRequiredParticipants());

    public static List<ProductionFacilityDestructiveDrainParticipantSaveData>
        CreateSaveParticipants(
            ProductionFacilityDestructiveDrainOperationId operationId,
            string genericOwnerStableId = null)
    {
        List<ProductionFacilityDestructiveDrainParticipantSaveData> result =
            new();
        foreach (IProductionFacilityDestructiveDrainParticipant participant in
                 CreateRequiredParticipants()
                     .OrderBy(value => value.ParticipantId, StringComparer.Ordinal))
        {
            string contribution =
                ProductionFacilityDestructiveDrainCanonical.ComputeFingerprint(
                    "qa:contribution:" + participant.ParticipantId);
            ProductionFacilityDestructiveDrainParticipantSaveData row = new()
            {
                participantId = participant.ParticipantId,
                contractVersion = participant.ContractVersion,
                preparedContributionFingerprint = contribution,
                expectedCurrentContributionFingerprint = contribution,
                planFingerprint =
                    ProductionFacilityDestructiveDrainCanonical
                        .ComputeFingerprint(
                            "qa:plan:" + participant.ParticipantId),
                owners = new List<
                    ProductionFacilityDestructiveDrainOwnerSaveData>()
            };
            if (string.Equals(
                    participant.ParticipantId,
                    ProductionFacilityDestructiveDrainParticipantIds
                        .GenericProductionBills,
                    StringComparison.Ordinal)
                && !string.IsNullOrEmpty(genericOwnerStableId))
            {
                row.owners.Add(new ProductionFacilityDestructiveDrainOwnerSaveData
                {
                    ownerStableId = genericOwnerStableId,
                    disposition =
                        ProductionFacilityDestructiveDrainDisposition.Terminalize,
                    targetDestinationId = string.Empty,
                    stepOperationId =
                        ProductionFacilityDestructiveDrainCanonical
                            .BuildStepOperationId(
                                operationId,
                                participant.ParticipantId,
                                genericOwnerStableId),
                    phase = ProductionFacilityDestructiveDrainStepPhase.Planned,
                    requestFingerprint =
                        ProductionFacilityDestructiveDrainCanonical
                            .ComputeFingerprint(
                                "qa:request:" + genericOwnerStableId),
                    commitId = string.Empty,
                    receiptFingerprint = string.Empty
                });
            }
            result.Add(row);
        }
        return result;
    }

    private static IProductionFacilityDestructiveDrainParticipant[]
        CreateRequiredParticipants() => new IProductionFacilityDestructiveDrainParticipant[]
        {
            new FakeParticipant(
                ProductionFacilityDestructiveDrainParticipantIds
                    .ApparelWorkOrders,
                1),
            new FakeParticipant(
                ProductionFacilityDestructiveDrainParticipantIds
                    .CapacityRoutingOutbox,
                1,
                new[]
                {
                    ProductionFacilityDestructiveDrainParticipantIds
                        .GenericProductionBills,
                    ProductionFacilityDestructiveDrainParticipantIds
                        .ApparelWorkOrders,
                    ProductionFacilityDestructiveDrainParticipantIds
                        .CombatEquipmentCrafting
                }),
            new FakeParticipant(
                ProductionFacilityDestructiveDrainParticipantIds
                    .CombatEquipmentCrafting,
                1),
            new FakeParticipant(
                ProductionFacilityDestructiveDrainParticipantIds
                    .GenericProductionBills,
                1),
            new FakeParticipant(
                ProductionFacilityDestructiveDrainParticipantIds
                    .PhysicalCustodyCarryRecovery,
                1,
                new[]
                {
                    ProductionFacilityDestructiveDrainParticipantIds
                        .CapacityRoutingOutbox
                })
        };

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void RequireThrows(Action action, string message)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private sealed class FakeParticipant :
        IProductionFacilityDestructiveDrainParticipant
    {
        public FakeParticipant(
            string participantId,
            int contractVersion,
            IReadOnlyList<string> dependencies = null)
        {
            ParticipantId = participantId;
            ContractVersion = contractVersion;
            DependsOnParticipantIds = dependencies ?? Array.Empty<string>();
        }

        public string ParticipantId { get; }
        public int ContractVersion { get; }
        public IReadOnlyList<string> DependsOnParticipantIds { get; }

        public ProductionFacilityDestructiveDrainParticipantPlan Prepare(
            ProductionFacilityDestructiveDrainPrepareContext context) =>
            throw new NotSupportedException();

        public ProductionFacilityDestructiveDrainStepResult TryCommit(
            ProductionFacilityDestructiveDrainStepContext context) =>
            throw new NotSupportedException();

        public ProductionFacilityDestructiveDrainStepResult TryAcknowledge(
            ProductionFacilityDestructiveDrainStepContext context) =>
            throw new NotSupportedException();

        public ProductionFacilityDestructiveDrainRecoveryResult Recover(
            ProductionFacilityDestructiveDrainStepContext context) =>
            throw new NotSupportedException();
    }
}
