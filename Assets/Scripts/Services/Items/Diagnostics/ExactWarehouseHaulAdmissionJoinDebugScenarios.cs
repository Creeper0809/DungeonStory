#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ExactWarehouseHaulAdmissionJoinDebugScenarios
{
    private const string Owner = "character:qa:warehouse-admission";
    private const string Destination = "warehouse:building:qa:warehouse-admission";
    private const string WarehouseId = "building:qa:warehouse-admission";
    private const string ItemId = "material:lumber";

    [MenuItem("DungeonStory/Debug/Items/Run Exact Warehouse Admission Restore Join")]
    public static void RunAll()
    {
        Fixture valid = CreateFixture("one", 2, 2_000L);
        RequireValid(valid, "valid exact admission join was rejected");

        Fixture wrongMass = CreateFixture("mass", 2, 2_000L);
        wrongMass.Intent.warehouseAdmissions[0].reservedMassGrams = 1_999L;
        RequireInvalid(wrongMass, "custody gram tamper was accepted");

        Fixture wrongDestination = CreateFixture("destination", 2, 2_000L);
        wrongDestination.Intent.destinationId = "warehouse:building:qa:other";
        RequireInvalid(wrongDestination, "warehouse destination tamper was accepted");

        Fixture wrongSignature = CreateFixture("signature", 2, 2_000L);
        wrongSignature.Intent.warehouseAdmissions[0].lotFingerprint = Sha('9');
        RequireInvalid(wrongSignature, "physical lot fingerprint tamper was accepted");

        Fixture missing = CreateFixture("missing", 2, 2_000L);
        missing.Intent.warehouseAdmissions.Clear();
        RequireInvalid(missing, "exact custody without an admission was accepted");

        Fixture stale = CreateFixture("stale", 2, 2_000L);
        Require(!ExactWarehouseHaulAdmissionJoin
                .TryValidateCurrentAuthorityProvenance(
                    stale.Intent.warehouseAdmissions[0],
                    currentCatalogRevision: 8L,
                    out _),
            "stale catalog authority revision was accepted");
        stale.Intent.warehouseAdmissions[0].sourceRevision = 0L;
        RequireInvalid(stale, "missing source provenance revision was accepted");

        Fixture first = CreateFixture("duplicate-a", 2, 2_000L);
        Fixture second = CreateFixture("duplicate-b", 3, 3_000L);

        Fixture partialPickup = CreateFixture("partial-picked", 2, 2_000L);
        partialPickup.Intent.warehouseAdmissions.Add(
            second.Intent.warehouseAdmissions[0]);
        ExactWarehouseHaulAdmissionJoin.RetainCommittedAdmissions(
            partialPickup.Intent);
        Require(partialPickup.Intent.warehouseAdmissions.Count == 1,
            "unpicked admission leaked into the durable committed projection");
        RequireValid(partialPickup,
            "partial-pickup committed projection was not restorable");

        second.Intent.warehouseAdmissions[0].ownerAdmissionOperationId =
            first.Intent.operationId + ":warehouse-admission:01";
        first.Intent.commitments.Add(second.Intent.commitments[0]);
        first.Intent.warehouseAdmissions.Add(second.Intent.warehouseAdmissions[0]);
        first.Carried.Add(second.Carried[0]);
        first.Intent.warehouseAdmissions[1].tokenId =
            first.Intent.warehouseAdmissions[0].tokenId;
        RequireInvalid(first, "duplicate ephemeral token id was accepted");

        Debug.Log("Exact warehouse admission restore join PASS.");
    }

    private static Fixture CreateFixture(
        string suffix,
        int quantity,
        long massGrams)
    {
        string stackId = "item-stack:qa:warehouse-admission:" + suffix;
        ItemInstanceComponentSaveData custody =
            FacilityOutputExactRouteCustodyCodec.Create(
                new FacilityOutputExactRouteCustodyMetadata(
                    FacilityOutputExactRouteCustodyPhase.Routable,
                    "batch:qa:" + suffix,
                    Sha('1'),
                    Sha('2'),
                    "main",
                    "line-commit:qa:" + suffix,
                    0,
                    1,
                    quantity,
                    massGrams,
                    1,
                    quantity,
                    massGrams,
                    ItemId,
                    string.Empty,
                    Sha('3'),
                    "production:qa:" + suffix + ":output",
                    Destination,
                    stackId,
                    stackId,
                    Vector2Int.zero,
                    0,
                    quantity,
                    massGrams,
                    "route:qa:" + suffix,
                    Sha('4'),
                    Sha('5'),
                    1L,
                    Sha('6'),
                    "reroute:qa:" + suffix,
                    Destination,
                    5,
                    7,
                    Sha('7')));
        CharacterCarriedItemSaveData carried = new()
        {
            carriedStackId = stackId,
            sourceStackId = stackId,
            ownerOperationId = "haul:" + Owner + ":000000000001",
            itemId = ItemId,
            quantity = quantity,
            components = new List<ItemInstanceComponentSaveData> { custody }
        };
        string signature = ItemReservationSignature.Create(
            carried.itemId,
            carried.components);
        HaulDeliveryItemCommitmentSaveData commitment = new()
        {
            carriedStackId = stackId,
            sourceStackId = stackId,
            itemId = ItemId,
            expectedStackSignature = signature,
            quantity = quantity
        };
        WarehouseHaulAdmissionSaveData admission = new()
        {
            tokenId = "warehouse-mass-token:qa:" + suffix,
            ownerAdmissionOperationId = carried.ownerOperationId
                + ":warehouse-admission:00",
            warehouseId = WarehouseId,
            sourceStackId = stackId,
            itemId = ItemId,
            lotFingerprint = signature,
            quantity = quantity,
            reservedMassGrams = massGrams,
            catalogRevision = 7L,
            sourceRevision = 11L
        };
        HaulDeliveryIntentSaveData intent = new()
        {
            operationId = carried.ownerOperationId,
            ownerCharacterId = Owner,
            destinationKind = WorldItemHaulDestinationKind.Warehouse,
            destinationId = Destination,
            warehouseAdmissions = new List<WarehouseHaulAdmissionSaveData>
            {
                admission
            },
            commitments = new List<HaulDeliveryItemCommitmentSaveData>
            {
                commitment
            }
        };
        return new Fixture(intent, new List<CharacterCarriedItemSaveData>
        {
            carried
        });
    }

    private static void RequireValid(Fixture fixture, string message)
    {
        Require(ExactWarehouseHaulAdmissionJoin.TryValidateSavedIntent(
                fixture.Intent,
                fixture.Carried,
                out string failure),
            message + ":" + failure);
    }

    private static void RequireInvalid(Fixture fixture, string message)
    {
        Require(!ExactWarehouseHaulAdmissionJoin.TryValidateSavedIntent(
                fixture.Intent,
                fixture.Carried,
                out _),
            message);
    }

    private static string Sha(char value) => new(value, 64);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class Fixture
    {
        internal Fixture(
            HaulDeliveryIntentSaveData intent,
            List<CharacterCarriedItemSaveData> carried)
        {
            Intent = intent;
            Carried = carried;
        }

        internal HaulDeliveryIntentSaveData Intent { get; }
        internal List<CharacterCarriedItemSaveData> Carried { get; }
    }
}
#endif
