#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// Detached DTO-only coverage for the saved shop-use/purchase-receipt join.
/// Live shop execution and world restore publication are outside this scenario.
/// </summary>
public static class ShopPurchaseOwnerSaveValidationDebugScenarios
{
    private const string FacilityId = "building:shop-purchase-owner-validation";
    private const string ActorId = "character:shop-purchase-owner-validation";
    private const string PurchaseOperationId = "shop-purchase:owner-validation:1";
    private const int CartIndex = 0;
    private const int SaleItemId = 101;
    private const int Price = 7;

    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        Culture = System.Globalization.CultureInfo.InvariantCulture,
        DateParseHandling = DateParseHandling.None,
        FloatParseHandling = FloatParseHandling.Double,
        TypeNameHandling = TypeNameHandling.None,
        MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
        ObjectCreationHandling = ObjectCreationHandling.Replace,
        NullValueHandling = NullValueHandling.Include
    };

    public static bool RunAll(bool log = true)
    {
        try
        {
            VerifyExactPendingPurchaseOwner();
            VerifyMissingOwnerBoundaries();
            VerifyReceiptIdentityBoundaries();
            VerifyDuplicateReceiptRejected();
            VerifyCommittedLotIdentity();
            VerifyAllPreflightBoundariesAreNonMutating();
            if (log)
            {
                Debug.Log(
                    "SHOP_PURCHASE_OWNER_SAVE_VALIDATION=PASS; "
                    + "scope=detached-shop-use-receipt-join; "
                    + "live-execution-and-world-publication=not-covered");
            }
            return true;
        }
        catch (Exception exception)
        {
            if (log)
            {
                Debug.LogError(
                    nameof(ShopPurchaseOwnerSaveValidationDebugScenarios)
                    + " failed: " + exception);
            }
            return false;
        }
    }

    private static void VerifyExactPendingPurchaseOwner()
    {
        RequireShopPurchaseValidation(
            CreateValidInput(),
            expectedSuccess: true,
            "exact pending purchase owner");
    }

    private static void VerifyMissingOwnerBoundaries()
    {
        OwnerInput missingActor = CreateValidInput();
        missingActor.Characters.actors.Clear();
        RequireShopPurchaseValidation(
            missingActor,
            expectedSuccess: false,
            "missing actor");

        OwnerInput missingReceipt = CreateValidInput();
        missingReceipt.Characters.actors[0].pendingRetailPurchases.Clear();
        RequireShopPurchaseValidation(
            missingReceipt,
            expectedSuccess: false,
            "missing receipt");

        OwnerInput missingShop = CreateValidInput();
        missingShop.Facilities.buildings.Clear();
        RequireShopPurchaseValidation(
            missingShop,
            expectedSuccess: false,
            "missing shop");
    }

    private static void VerifyReceiptIdentityBoundaries()
    {
        OwnerInput wrongFacility = CreateValidInput();
        OnlyReceipt(wrongFacility).facilityId = "building:other-shop";
        RequireShopPurchaseValidation(
            wrongFacility,
            expectedSuccess: false,
            "wrong receipt facility");

        OwnerInput wrongBuyer = CreateValidInput();
        OnlyReceipt(wrongBuyer).buyerId = "character:other-buyer";
        RequireShopPurchaseValidation(
            wrongBuyer,
            expectedSuccess: false,
            "wrong receipt buyer");

        OwnerInput wrongSlot = CreateValidInput();
        OnlyReceipt(wrongSlot).shopUseCartIndex = CartIndex + 1;
        RequireShopPurchaseValidation(
            wrongSlot,
            expectedSuccess: false,
            "wrong receipt cart slot");

        OwnerInput wrongPrice = CreateValidInput();
        OnlyReceipt(wrongPrice).purchaseCost = Price + 1;
        RequireShopPurchaseValidation(
            wrongPrice,
            expectedSuccess: false,
            "wrong receipt price");

        OwnerInput wrongPendingOperation = CreateValidInput();
        OnlyReceipt(wrongPendingOperation).operationId =
            "shop-purchase:owner-validation:other";
        RequireShopPurchaseValidation(
            wrongPendingOperation,
            expectedSuccess: false,
            "wrong pending purchase operation");
    }

    private static void VerifyDuplicateReceiptRejected()
    {
        OwnerInput duplicate = CreateValidInput();
        duplicate.Characters.actors[0].pendingRetailPurchases.Add(
            OnlyReceipt(duplicate).Clone());
        RequireShopPurchaseValidation(
            duplicate,
            expectedSuccess: false,
            "duplicate receipt binding");
    }

    private static void VerifyCommittedLotIdentity()
    {
        OwnerInput exact = CreateValidInput(committed: true);
        RequireShopPurchaseValidation(
            exact,
            expectedSuccess: true,
            "exact committed lot owner");

        OwnerInput changedLot = CreateValidInput(committed: true);
        OnlyReceipt(changedLot).lot.itemDefinitionId =
            "item:shop-purchase-owner-validation:changed";
        RequireShopPurchaseValidation(
            changedLot,
            expectedSuccess: false,
            "changed committed exact lot");
    }

    private static void VerifyAllPreflightBoundariesAreNonMutating()
    {
        DungeonGameSaveData valid = CreateEnvelope(CreateValidInput());
        RequireAllPreflightResults(
            valid,
            expectedSuccess: true,
            "exact five-section shop purchase owner envelope");

        OwnerInput invalidInput = CreateValidInput();
        OnlyReceipt(invalidInput).purchaseCost = Price + 1;
        DungeonGameSaveData invalid = CreateEnvelope(invalidInput);
        RequireAllPreflightResults(
            invalid,
            expectedSuccess: false,
            "wrong-price shop purchase owner envelope");

        DungeonGameSaveData missingCharacters = CreateEnvelope(CreateValidInput());
        missingCharacters.sections.RemoveAll(section => section.sectionId == CharacterWorldSaveSection.Id);
        RequireAllPreflightResults(missingCharacters, false, "shop without required character section");
        DungeonGameSaveData wrongCharacterVersion = CreateEnvelope(CreateValidInput());
        wrongCharacterVersion.sections.Single(section => section.sectionId == CharacterWorldSaveSection.Id).sectionVersion--;
        RequireAllPreflightResults(wrongCharacterVersion, false, "shop with wrong character section version");
        OwnerInput orphan = CreateValidInput();
        orphan.Facilities.buildings.Clear();
        RequireAllPreflightResults(CreateEnvelope(orphan), false, "orphan receipt without any shop modules");
    }

    private static OwnerInput CreateValidInput(bool committed = false)
    {
        RetailStockLotSnapshot lot = CreateLot();
        string shopUseOperationId =
            ShopUseExecutionState.OperationFor(FacilityId, 1L);
        ShopUseCartLine line = new()
        {
            saleItemId = SaleItemId,
            pricingCaptured = true,
            price = Price,
            workerId = string.Empty,
            priceMultiplier = 1f,
            modifierProvenance = WorkCompletionIdentityReferenceDefaults.None,
            purchaseOperationId = committed ? PurchaseOperationId : string.Empty,
            pendingPurchaseOperationId = committed
                ? string.Empty
                : PurchaseOperationId,
            committedLot = committed ? lot.Clone() : null,
            postCommitApplied = false,
            feedbackStarted = true,
            feedbackRemainingSeconds = 0f
        };
        ShopInteractionState interaction = new()
        {
            version = ShopInteractionState.CurrentVersion,
            nextSequence = 2L,
            activeUses = new List<ShopUseExecutionState>
            {
                new()
                {
                    version = ShopUseExecutionState.CurrentVersion,
                    sequence = 1L,
                    operationId = shopUseOperationId,
                    actorId = ActorId,
                    actionType = nameof(AIShopping),
                    actionBranch = (int)CharacterAiBranch.Shopping,
                    phase = ShopUseExecutionPhase.Purchase,
                    createsRevenue = true,
                    selectionCount = 1,
                    selectionIndex = 1,
                    selectedItemId = SaleItemId,
                    cart = new List<ShopUseCartLine> { line },
                    purchaseIndex = CartIndex,
                    purchaseCount = 0,
                    lastPurchaseFailure = "no-purchasable-item"
                }
            }
        };

        RetailPurchaseRecoverySaveData receipt = new()
        {
            shopUseOperationId = shopUseOperationId,
            shopUseCartIndex = CartIndex,
            operationId = PurchaseOperationId,
            buyerId = ActorId,
            facilityId = FacilityId,
            absoluteDay = 3,
            purchaseCost = Price,
            buyerMoneyBefore = 100,
            buyerMoneyAfter = 100 - Price,
            revenueWorkerId = string.Empty,
            revenuePriceMultiplier = 1f,
            revenueSkillModifierProvenance =
                WorkCompletionIdentityReferenceDefaults.None,
            phase = committed
                ? RetailPurchaseRecoveryPhase.PhysicalCommitted
                : RetailPurchaseRecoveryPhase.LotTaken,
            lot = lot.Clone()
        };

        return new OwnerInput
        {
            Facilities = new ModularFacilityWorldSaveData
            {
                buildings = new List<ModularFacilityBuildingSaveData>
                {
                    new()
                    {
                        persistentInstanceId = FacilityId,
                        stateModules = new List<BuildingStateModuleSaveData>
                        {
                            new()
                            {
                                moduleId = BuildingStateModuleIds.ShopInteraction,
                                version = ShopInteractionState.CurrentVersion,
                                payload = Serialize(interaction)
                            }
                        }
                    }
                }
            },
            Characters = new DungeonCharacterWorldSaveData
            {
                actors = new List<DungeonCharacterSaveData>
                {
                    new()
                    {
                        persistentId = ActorId,
                        pendingRetailPurchases =
                            new List<RetailPurchaseRecoverySaveData> { receipt }
                    }
                }
            }
        };
    }

    private static RetailStockLotSnapshot CreateLot() => new()
    {
        saleItemId = SaleItemId,
        itemDefinitionId = "item:shop-purchase-owner-validation",
        itemInstanceId = "item-instance:shop-purchase-owner-validation",
        sourceStackId = "world-stack:shop-purchase-owner-validation",
        quantity = 1,
        unitMassGrams = 10L,
        sourceOperationId = "retail-stock:shop-purchase-owner-validation",
        componentFingerprint = "none",
        components = new List<RetailStockComponentSnapshot>()
    };

    private static RetailPurchaseRecoverySaveData OnlyReceipt(
        OwnerInput input) => input.Characters.actors.Single()
            .pendingRetailPurchases.Single();

    private static void RequireShopPurchaseValidation(
        OwnerInput input,
        bool expectedSuccess,
        string scenario)
    {
        string before = Serialize(input);
        bool succeeded = false;
        Exception unexpected = null;
        try
        {
            ServiceExecutionOwnerSaveValidation.ValidateShopPurchases(
                input.Facilities,
                input.Characters);
            succeeded = true;
        }
        catch (InvalidOperationException)
        {
        }
        catch (Exception exception)
        {
            unexpected = exception;
        }

        Require(Serialize(input) == before,
            scenario + " mutated detached DTO inputs.");
        if (unexpected != null)
        {
            throw new InvalidOperationException(
                scenario + " raised an unexpected exception.",
                unexpected);
        }
        Require(succeeded == expectedSuccess,
            scenario + " verdict was " + succeeded + ".");
    }

    private static DungeonGameSaveData CreateEnvelope(OwnerInput input) => new()
    {
        sections = new List<DungeonSaveSectionEnvelope>
        {
            CreateSection(
                ServiceRoomsSaveSection.Id,
                ServiceRoomsSaveData.CurrentVersion,
                new ServiceRoomsSaveData()),
            CreateSection(
                ModularFacilityWorldSaveSection.Id,
                ModularFacilityWorldSaveSection.CurrentSectionVersion,
                input.Facilities),
            CreateSection(
                SurvivalResourcesSaveSection.Id,
                DungeonSurvivalSaveData.CurrentVersion,
                new DungeonSurvivalSaveData()),
            CreateSection(
                CharacterConsumablesSaveSection.Id,
                DungeonCharacterConsumablesSaveData.CurrentVersion,
                new DungeonCharacterConsumablesSaveData
                {
                    timelineTime = 0f
                }),
            CreateSection(
                CharacterWorldSaveSection.Id,
                CharacterWorldSaveSection.CurrentVersion,
                input.Characters)
        }
    };

    private static DungeonSaveSectionEnvelope CreateSection<T>(
        string id,
        int version,
        T payload)
        where T : class => new()
        {
            sectionId = id,
            sectionVersion = version,
            restorePhase = DungeonSaveRestorePhase.RuntimeState,
            optional = false,
            payloadJson = Serialize(payload)
        };

    private static void RequireAllPreflightResults(
        DungeonGameSaveData save,
        bool expectedSuccess,
        string scenario)
    {
        string before = Serialize(save);
        ServiceExecutionOwnerSaveValidation validator = new();
        DungeonGameRestoreReport capturedReport = new();
        DungeonGameRestoreReport restoreReport = new();
        DungeonGameRestoreReport registryReport = new();

        ((IDungeonCapturedSavePreflightValidator)validator).Validate(
            save,
            capturedReport);
        ((IDungeonSavePreflightValidator)validator).Validate(
            save,
            restoreReport);
        ((IDungeonSaveRegistryPreflightValidator)validator).Validate(
            save.sections.ToDictionary(
                section => section.sectionId,
                StringComparer.Ordinal),
            registryReport);

        Require(capturedReport.Success == expectedSuccess
                && restoreReport.Success == expectedSuccess
                && registryReport.Success == expectedSuccess,
            scenario + " had inconsistent captured/restore/registry verdicts: "
            + capturedReport.Success + "/" + restoreReport.Success + "/"
            + registryReport.Success + "; errors="
            + string.Join(" | ", capturedReport.Errors
                .Concat(restoreReport.Errors)
                .Concat(registryReport.Errors)));
        Require(Serialize(save) == before,
            scenario + " preflight validation mutated saved JSON.");
    }

    private static string Serialize(object value) =>
        JsonConvert.SerializeObject(value, Formatting.None, JsonSettings);

    private sealed class OwnerInput
    {
        public ModularFacilityWorldSaveData Facilities { get; set; }
        public DungeonCharacterWorldSaveData Characters { get; set; }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
#endif
