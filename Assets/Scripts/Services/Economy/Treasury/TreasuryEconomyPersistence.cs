using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class TreasuryEconomyPersistence : ITreasuryEconomyPersistence
{
    private readonly EconomyTransactionLedgerRuntime transactionLedger;
    private readonly EmploymentContractRuntime employment;
    private readonly AutoProcurementRuntime autoProcurement;
    private readonly PaidFacilityContractRuntime facilityContracts;
    private readonly EquipmentOverclockRuntime overclock;
    private readonly TreasuryDefenseRuntime treasuryDefense;
    private readonly TreasuryEconomyAggregateStateStore stateStore;

    public TreasuryEconomyPersistence(
        EconomyTransactionLedgerRuntime transactionLedger,
        EmploymentContractRuntime employment,
        AutoProcurementRuntime autoProcurement,
        PaidFacilityContractRuntime facilityContracts,
        EquipmentOverclockRuntime overclock,
        TreasuryDefenseRuntime treasuryDefense,
        TreasuryEconomyAggregateStateStore stateStore)
    {
        this.transactionLedger = transactionLedger
            ?? throw new ArgumentNullException(nameof(transactionLedger));
        this.employment = employment
            ?? throw new ArgumentNullException(nameof(employment));
        this.autoProcurement = autoProcurement
            ?? throw new ArgumentNullException(nameof(autoProcurement));
        this.facilityContracts = facilityContracts
            ?? throw new ArgumentNullException(nameof(facilityContracts));
        this.overclock = overclock
            ?? throw new ArgumentNullException(nameof(overclock));
        this.treasuryDefense = treasuryDefense
            ?? throw new ArgumentNullException(nameof(treasuryDefense));
        this.stateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
    }

    public TreasuryEconomySaveData Capture()
    {
        stateStore.AssertStockSupplyIdle();
        return new TreasuryEconomySaveData
        {
            warehouseSupply = StockSupplyFactSaveData.ValidateAndClone(stateStore.Current.WarehouseSupply),
            transactionLedger = transactionLedger.Capture(),
            employment = employment.Capture(),
            autoProcurement = autoProcurement.Capture(),
            facilityContracts = facilityContracts.Capture(),
            overclock = overclock.Capture(),
            treasuryDefense = treasuryDefense.Capture()
        };
    }

    public TreasuryEconomyRestoreCandidate BuildRestore(
        TreasuryEconomySaveData saveData)
    {
        TreasuryEconomySaveValidation.Validate(saveData);
        TreasuryEconomyAggregateState restored = new();
        restored.WarehouseSupply = StockSupplyFactSaveData.ValidateAndClone(saveData.warehouseSupply);
        transactionLedger.PopulateRestoreState(
            restored,
            saveData.transactionLedger);
        employment.PopulateRestoreState(restored, saveData.employment);
        facilityContracts.PopulateRestoreState(
            restored,
            saveData.facilityContracts);
        autoProcurement.PopulateRestoreState(
            restored,
            saveData.autoProcurement);
        overclock.PopulateRestoreState(restored, saveData.overclock);
        treasuryDefense.PopulateRestoreState(
            restored,
            saveData.treasuryDefense);
        return new TreasuryEconomyRestoreCandidate(restored);
    }

    public void PublishRestoreCandidate(
        TreasuryEconomyRestoreCandidate candidate)
    {
        stateStore.AssertStockSupplyIdle();
        stateStore.Replace(
            (candidate ?? throw new ArgumentNullException(nameof(candidate)))
            .State);
    }
}

internal static class TreasuryEconomySaveValidation
{
    private const int MaxLedgerRecords = 256;
    private const int MaxEntityRecords = 1024;
    private const int MaxRuleRecords = 256;
    private const int MaxResultRecords = 256;
    private const int MaxRememberedInvasions = 32;
    private const float MaxOverclockSeconds = 180f;

    internal static void Validate(TreasuryEconomySaveData data)
    {
        if (data == null)
        {
            throw new InvalidOperationException(
                "Treasury payload and restore references are required.");
        }
        if (data.version != TreasuryEconomySaveData.CurrentVersion)
        {
            throw new InvalidOperationException(
                $"Treasury payload version {data.version} is not current V{TreasuryEconomySaveData.CurrentVersion}.");
        }
        if (data.transactionLedger == null
            || data.warehouseSupply == null
            || data.employment == null
            || data.autoProcurement == null
            || data.facilityContracts == null
            || data.overclock == null
            || data.treasuryDefense == null)
        {
            throw new InvalidOperationException(
                "Treasury payload is missing a required aggregate component.");
        }

        ValidateLedger(data.transactionLedger);
        StockSupplyFactSaveData.ValidateAndClone(data.warehouseSupply);
        ValidateEmployment(data.employment);
        ValidateProcurement(data.autoProcurement);
        ValidateFacilityContracts(data.facilityContracts);
        ValidateOverclock(data.overclock);
        ValidateDefense(data.treasuryDefense);
    }

    private static void ValidateLedger(EconomyTransactionLedgerSaveData data)
    {
        RequireList(data.records, MaxLedgerRecords, "ledger records");
        if (data.nextSequence < 1)
        {
            throw new InvalidOperationException(
                "Treasury ledger next sequence must be positive.");
        }
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (EconomyTransactionRecord record in data.records)
        {
            if (record == null
                || !Enum.IsDefined(typeof(EconomyTransactionKind), record.kind)
                || record.balanceBefore < 0
                || record.balanceAfter < 0
                || !IsFiniteNonNegative(record.gameTime))
            {
                throw new InvalidOperationException(
                    "Treasury ledger contains a null, unknown, or out-of-range record.");
            }
            RequireCanonical(record.transactionId, "transaction ID");
            RequireCanonical(record.sourceId, "transaction source", allowEmpty: true);
            RequireCanonical(record.targetId, "transaction target", allowEmpty: true);
            if (!ids.Add(record.transactionId))
            {
                throw new InvalidOperationException(
                    $"Treasury transaction '{record.transactionId}' is duplicated.");
            }
        }
    }

    private static void ValidateEmployment(EmploymentContractSaveData data)
    {
        RequireList(data.wageStates, MaxEntityRecords, "wage states");
        RequireList(
            data.mercenaryContracts,
            MaxEntityRecords,
            "mercenary contracts");
        HashSet<CharacterId> wages = new();
        foreach (EmployeeWageState state in data.wageStates)
        {
            CharacterId id = RequireCharacter(
                state?.characterId,
                "wage character");
            if (!wages.Add(id)
                || !Enum.IsDefined(
                    typeof(EmploymentContractKind),
                    state.contractKind)
                || state.rolePremium < 0
                || state.equipmentGradePremium < 0
                || state.unpaidWages < 0
                || state.lastSettledDay < 0)
            {
                throw new InvalidOperationException(
                    $"Wage state for '{id.Value}' is duplicated or out of range.");
            }
            RequireCanonical(
                state.departureFinanceSourceId,
                "departure finance source",
                allowEmpty: true);
            RequireCanonical(
                state.departureFinanceTransactionId,
                "departure finance transaction",
                allowEmpty: true);
            RequireCanonical(
                state.departureFailureReason,
                "departure failure",
                allowEmpty: true);
            bool hasDeparture = state.departurePending || state.departed;
            if (state.departureContractDay < 0
                || (hasDeparture
                    && (state.contractKind != EmploymentContractKind.Mercenary
                        || state.active
                        || state.departureContractDay < 1
                        || state.departureFinanceSourceId.Length == 0
                        || state.departureFinanceTransactionId.Length == 0))
                || (state.departurePending
                    && (state.departed
                        || state.departureFailureReason.Length == 0))
                || (state.departed
                    && state.departureFailureReason.Length != 0)
                || (!hasDeparture
                    && (state.departureContractDay != 0
                        || state.departureFinanceSourceId.Length != 0
                        || state.departureFinanceTransactionId.Length != 0
                        || state.departureFailureReason.Length != 0)))
            {
                throw new InvalidOperationException(
                    $"Wage departure state for '{id.Value}' is inconsistent.");
            }
        }

        HashSet<CharacterId> mercenaries = new();
        foreach (MercenaryContract contract in data.mercenaryContracts)
        {
            CharacterId id = RequireCharacter(
                contract?.characterId,
                "mercenary character");
            if (!mercenaries.Add(id)
                || contract.hiredDay < 0
                || contract.lastRenewedDay < contract.hiredDay)
            {
                throw new InvalidOperationException(
                    $"Mercenary contract for '{id.Value}' is duplicated or out of range.");
            }
        }

        foreach (EmployeeWageState state in data.wageStates.Where(value =>
                     value.contractKind == EmploymentContractKind.Mercenary))
        {
            MercenaryContract contract = data.mercenaryContracts
                .SingleOrDefault(value => string.Equals(
                    value.characterId,
                    state.characterId,
                    StringComparison.Ordinal));
            if (contract == null
                || ((state.departurePending || state.departed)
                    && contract.active))
            {
                throw new InvalidOperationException(
                    $"Mercenary wage state for '{state.characterId}' has no matching contract state.");
            }
        }
    }

    private static void ValidateProcurement(AutoProcurementSaveData data)
    {
        RequireList(data.stockRules, MaxRuleRecords, "stock rules");
        RequireList(data.wishlistRules, MaxRuleRecords, "wishlist rules");
        RequireList(
            data.processedOfferKeys,
            MaxRuleRecords,
            "processed offer keys");
        RequireList(data.lastResults, MaxResultRecords, "procurement results");
        RequireList(data.pendingFacts, 1, "procurement pending facts");
        if (data.parentResults != null)
            RequireList(data.parentResults, MaxRuleRecords,
                "procurement parent results");
        if (data.dailyBudget < 0
            || data.minimumReserve < 0
            || data.lastProcessedDay < 0)
        {
            throw new InvalidOperationException(
                "Treasury procurement budget or day is negative.");
        }

        HashSet<string> ruleIds = new(StringComparer.Ordinal);
        foreach (AutoProcurementRule rule in data.stockRules)
        {
            RequireCanonical(rule?.ruleId, "stock rule ID");
            if (!ruleIds.Add(rule.ruleId)
                || !Enum.IsDefined(typeof(StockCategory), rule.category)
                || rule.targetQuantity < 0
                || rule.maximumUnitPrice < 0
                || rule.dailyMaximumQuantity < 0)
            {
                throw new InvalidOperationException(
                    $"Stock rule '{rule?.ruleId}' is duplicated or out of range.");
            }
        }
        foreach (ProcurementWishlistRule rule in data.wishlistRules)
        {
            RequireCanonical(rule?.ruleId, "wishlist rule ID");
            RequireCanonical(
                rule.offerTypeId,
                "wishlist offer type",
                allowEmpty: true);
            RequireCanonical(
                rule.requiredTag,
                "wishlist tag",
                allowEmpty: true);
            if (!ruleIds.Add(rule.ruleId)
                || rule.dataId < -1
                || rule.maximumPrice < 0
                || rule.maximumOwned < 0)
            {
                throw new InvalidOperationException(
                    $"Wishlist rule '{rule?.ruleId}' is duplicated or out of range.");
            }
        }
        RequireUniqueCanonical(
            data.processedOfferKeys,
            "processed offer key");
        foreach (AutoProcurementResult result in data.lastResults)
        {
            if (result == null
                || result.day < 0
                || result.quantity < 0
                || result.cost < 0
                || result.attemptedQuantity < 0
                || result.attemptedCost < 0
                || result.committedQuantity < 0
                || result.committedCost < 0)
            {
                throw new InvalidOperationException(
                    "Procurement result is null or out of range.");
            }
            RequireCanonical(result.ruleId, "result rule", allowEmpty: true);
            RequireCanonical(result.attemptedItemId,
                "attempted procurement item", allowEmpty: true);
            RequireCanonical(result.committedItemId,
                "committed procurement item", allowEmpty: true);
            RequireCanonical(result.childPurchaseResultKey,
                "procurement child result key", allowEmpty: true);
            foreach (AutoProcurementPhysicalStackRef physical in
                     result.physicalStacks
                     ?? new List<AutoProcurementPhysicalStackRef>())
            {
                if (physical == null || physical.quantity <= 0)
                    throw new InvalidOperationException(
                        "Procurement physical stack reference is invalid.");
                RequireCanonical(physical.stackId,
                    "procurement physical stack ID");
                RequireCanonical(physical.itemInstanceId,
                    "procurement physical instance ID", allowEmpty: true);
            }
        }
        HashSet<string> parentKeys = new(StringComparer.Ordinal);
        foreach (AutoProcurementParentSaveData parent in data.parentResults
                     ?? new List<AutoProcurementParentSaveData>())
        {
            RequireCanonical(parent?.offerKey,
                "procurement parent offer key");
            RequireCanonical(parent.childPurchaseResultKey,
                "procurement parent child key");
            if (!parentKeys.Add(parent.offerKey)
                || !Enum.IsDefined(typeof(AutoProcurementParentPhase),
                    parent.phase)
                || parent.result == null
                || parent.result.childPurchaseResultKey
                    != parent.childPurchaseResultKey)
                throw new InvalidOperationException(
                    "Procurement parent result is duplicated or invalid.");
        }
        foreach (var pending in data.pendingFacts)
        {
            RequireCanonical(pending?.publicationKey, "procurement pending publication key");
            RequireCanonical(pending.offerKey, "procurement pending offer key", allowEmpty: true);
            var fact = new AutoProcurementCompletedFact(pending.offerKey, pending.result);
            string snapshot = JsonUtility.ToJson(fact.CaptureResult());
            if (fact.Day > data.lastProcessedDay
                || !data.lastResults.Any(value => JsonUtility.ToJson(value) == snapshot)
                || pending.offerKey.Length > 0 && !data.processedOfferKeys.Contains(pending.offerKey))
                throw new InvalidOperationException("Procurement pending fact does not match its applied result.");
            if (fact.ChildPurchaseResultKey.Length > 0
                && !(data.parentResults ?? new List<AutoProcurementParentSaveData>()).Any(parent =>
                    parent.offerKey == pending.offerKey && parent.childPurchaseResultKey == fact.ChildPurchaseResultKey
                    && parent.phase == AutoProcurementParentPhase.ParentPending
                    && JsonUtility.ToJson(parent.result) == snapshot))
                throw new InvalidOperationException("Procurement pending fact does not match its delivery parent.");
        }
    }

    private static void ValidateFacilityContracts(
        PaidFacilityContractSaveData data)
    {
        RequireList(data.contracts, MaxEntityRecords, "facility contracts");
        RequireList(data.chargedOrderKeys, 256, "charged order keys");
        HashSet<string> contractIds = new(StringComparer.Ordinal);
        HashSet<BuildingInstanceId> facilityIds = new();
        foreach (PaidFacilityContractState contract in data.contracts)
        {
            RequireCanonical(contract?.contractId, "facility contract ID");
            BuildingInstanceId facilityId = RequireBuilding(
                contract.facilityPersistentId,
                "facility contract building");
            if (!contractIds.Add(contract.contractId)
                || !facilityIds.Add(facilityId)
                || contract.dailyCost < 0
                || contract.lastSettledDay < 0)
            {
                throw new InvalidOperationException(
                    $"Facility contract '{contract?.contractId}' is duplicated or out of range.");
            }
        }
        RequireUniqueCanonical(data.chargedOrderKeys, "charged order key");
    }

    private static void ValidateOverclock(EquipmentOverclockSaveData data)
    {
        RequireList(data.states, MaxEntityRecords, "overclock states");
        HashSet<string> keys = new(StringComparer.Ordinal);
        foreach (OverclockState state in data.states)
        {
            if (state == null
                || !Enum.IsDefined(typeof(OverclockTargetKind), state.targetKind)
                || !Enum.IsDefined(typeof(OverclockTier), state.tier)
                || !IsFiniteRange(
                    state.remainingGameSeconds,
                    0f,
                    MaxOverclockSeconds)
                || !IsFiniteRange(state.overload, 0f, 100f)
                || state.activationCost < 0
                || ((state.tier == OverclockTier.None)
                    != (state.remainingGameSeconds <= 0f)))
            {
                throw new InvalidOperationException(
                    "Overclock state is null, unknown, or out of range.");
            }

            string targetId;
            if (state.targetKind == OverclockTargetKind.Equipment)
            {
                ItemInstanceId itemId = (ItemInstanceId)state.targetId;
                if (!itemId.IsValid
                    || !string.Equals(
                        itemId.Value,
                        state.targetId,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Overclock equipment '{state.targetId}' is missing.");
                }
                targetId = itemId.Value;
            }
            else
            {
                targetId = RequireBuilding(
                    state.targetId,
                    "overclock building").Value;
            }
            if (!keys.Add($"{(int)state.targetKind}:{targetId}"))
            {
                throw new InvalidOperationException(
                    $"Overclock target '{targetId}' is duplicated.");
            }
        }
    }

    private static void ValidateDefense(TreasuryDefenseSaveData data)
    {
        RequireList(data.policies, MaxEntityRecords, "defense policies");
        RequireList(
            data.invasionSpending,
            MaxEntityRecords,
            "defense spending");
        HashSet<BuildingInstanceId> policies = new();
        foreach (TreasuryDefensePolicy policy in data.policies)
        {
            BuildingInstanceId facilityId = RequireBuilding(
                policy?.facilityPersistentId,
                "defense policy building");
            if (!policies.Add(facilityId)
                || policy.minimumThreat < 0
                || policy.invasionBudget < 0
                || policy.protectedFunds < -1)
            {
                throw new InvalidOperationException(
                    $"Defense policy for '{facilityId.Value}' is duplicated or out of range.");
            }
        }

        HashSet<string> spendingKeys = new(StringComparer.Ordinal);
        HashSet<string> invasions = new(StringComparer.Ordinal);
        foreach (TreasuryDefenseInvasionSpendState spending in
                 data.invasionSpending)
        {
            RequireCanonical(spending?.invasionId, "invasion ID");
            BuildingInstanceId facilityId = RequireBuilding(
                spending.facilityPersistentId,
                "defense spending building");
            invasions.Add(spending.invasionId);
            if (spending.spent < 0
                || !spendingKeys.Add(
                    $"{spending.invasionId}|{facilityId.Value}"))
            {
                throw new InvalidOperationException(
                    "Defense spending is duplicated or negative.");
            }
        }
        if (invasions.Count > MaxRememberedInvasions)
        {
            throw new InvalidOperationException(
                "Defense spending exceeds the 32-invasion retention limit.");
        }
    }

    private static CharacterId RequireCharacter(
        string value,
        string label)
    {
        RequireCanonical(value, label);
        CharacterId id = (CharacterId)value;
        if (!id.IsValid)
        {
            throw new InvalidOperationException(
                $"{label} '{value}' is not a typed CharacterId.");
        }
        return id;
    }

    private static BuildingInstanceId RequireBuilding(
        string value,
        string label)
    {
        RequireCanonical(value, label);
        BuildingInstanceId id = (BuildingInstanceId)value;
        if (!id.IsValid)
        {
            throw new InvalidOperationException(
                $"{label} '{value}' is not a typed BuildingInstanceId.");
        }
        return id;
    }

    private static void RequireCanonical(
        string value,
        string label,
        bool allowEmpty = false)
    {
        if (value == null
            || (!allowEmpty && value.Length == 0)
            || value.Length > 512
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Treasury {label} is missing or non-canonical.");
        }
    }

    private static void RequireUniqueCanonical(
        IEnumerable<string> values,
        string label)
    {
        HashSet<string> unique = new(StringComparer.Ordinal);
        foreach (string value in values)
        {
            RequireCanonical(value, label);
            if (!unique.Add(value))
            {
                throw new InvalidOperationException(
                    $"Treasury {label} '{value}' is duplicated.");
            }
        }
    }

    private static void RequireList<T>(
        ICollection<T> values,
        int maximum,
        string label)
    {
        if (values == null || values.Count > maximum)
        {
            throw new InvalidOperationException(
                $"Treasury {label} must contain at most {maximum} entries.");
        }
    }

    private static bool IsFiniteNonNegative(float value) =>
        IsFiniteRange(value, 0f, float.MaxValue);

    private static bool IsFiniteRange(float value, float minimum, float maximum) =>
        !float.IsNaN(value)
        && !float.IsInfinity(value)
        && value >= minimum
        && value <= maximum;
}
