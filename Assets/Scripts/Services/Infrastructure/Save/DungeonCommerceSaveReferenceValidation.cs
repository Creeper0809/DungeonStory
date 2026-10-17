using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static DungeonSaveReferenceRules;

/// <summary>Validates detached commerce and treasury joins.</summary>
internal sealed class DungeonCommerceSaveReferenceValidation
{

    internal static void ValidateRetailStockJoins(
        ModularFacilityWorldSaveData buildings,
        DungeonSavePhysicalReferenceIndex physical,
        DungeonGameRestoreReport report)
    {
        HashSet<string> joinedRetailInstances = new(StringComparer.Ordinal);
        HashSet<string> joinedRetailOperations = new(StringComparer.Ordinal);
        HashSet<string> activeRestockOperations = new(StringComparer.Ordinal);
        foreach (ModularFacilityBuildingSaveData building in buildings?.buildings
                     ?? new List<ModularFacilityBuildingSaveData>())
        {
            foreach (BuildingStateModuleSaveData module in building?.stateModules
                         ?? new List<BuildingStateModuleSaveData>())
            {
                if (module == null
                    || !string.Equals(
                        module.moduleId,
                        BuildingStateModuleIds.ShopStock,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                ShopStockStateSnapshot snapshot;
                try
                {
                    snapshot = JsonUtility.FromJson<ShopStockStateSnapshot>(
                        module.payload ?? string.Empty);
                }
                catch (Exception exception)
                {
                    report.AddError(
                        $"Shop '{building.persistentInstanceId}' has an unreadable exact-stock payload: {exception.Message}");
                    continue;
                }
                if (module.version != 1
                    || snapshot == null
                    || snapshot.schemaVersion != ShopStockStateSnapshot.CurrentSchemaVersion
                    || snapshot.lots == null)
                {
                    report.AddError(
                        $"Shop '{building.persistentInstanceId}' does not contain the current exact-stock schema.");
                    continue;
                }

                int[] activatedSources = (snapshot.activatedAuthoredSaleItemIds
                        ?? new List<int>())
                    .ToArray();
                if (activatedSources.Distinct().Count()
                    != activatedSources.Length)
                {
                    report.AddError(
                        $"Shop '{building.persistentInstanceId}' contains duplicate authored-source activation authority.");
                }
                foreach (string operationId in snapshot.activeRestockOperationIds
                             ?? new List<string>())
                {
                    if (string.IsNullOrWhiteSpace(operationId)
                        || !string.Equals(
                            operationId,
                            operationId.Trim(),
                            StringComparison.Ordinal)
                        || !operationId.StartsWith(
                            "restock:",
                            StringComparison.Ordinal)
                        || !activeRestockOperations.Add(operationId)
                        || !physical.ReservationOwnerOperationIds.Contains(
                            operationId))
                    {
                        report.AddError(
                            $"Shop '{building.persistentInstanceId}' has an orphan or duplicate active restock operation '{operationId ?? string.Empty}'.");
                    }
                }

                foreach (RetailStockLotSnapshot lot in snapshot.lots)
                {
                    if (lot == null
                        || lot.quantity <= 0
                        || lot.unitMassGrams <= 0L
                        || string.IsNullOrEmpty(lot.itemDefinitionId)
                        || string.IsNullOrEmpty(lot.sourceOperationId)
                        || !string.Equals(
                            lot.itemDefinitionId,
                            lot.itemDefinitionId.Trim(),
                            StringComparison.Ordinal)
                        || !string.Equals(
                            lot.sourceOperationId,
                            lot.sourceOperationId.Trim(),
                            StringComparison.Ordinal)
                        || !joinedRetailOperations.Add(lot.sourceOperationId))
                    {
                        report.AddError(
                            $"Shop '{building.persistentInstanceId}' has a duplicate or invalid exact retail source operation '{lot?.sourceOperationId ?? string.Empty}'.");
                        continue;
                    }
                    if (string.IsNullOrEmpty(lot.itemInstanceId))
                    {
                        continue;
                    }
                    if (lot.quantity != 1
                        || !joinedRetailInstances.Add(lot.itemInstanceId))
                    {
                        report.AddError(
                            $"Shop '{building.persistentInstanceId}' has a duplicate or invalid unique retail lot '{lot.itemInstanceId ?? string.Empty}'.");
                        continue;
                    }
                    if (!physical.EquipmentInstances.TryGetValue(
                            lot.itemInstanceId,
                            out CombatEquipmentInstance equipment)
                        || equipment.worldState != CombatEquipmentWorldState.RetailStock
                        || !string.IsNullOrEmpty(equipment.ownerCharacterId)
                        || !string.Equals(
                            equipment.sourceStackId,
                            lot.sourceOperationId,
                            StringComparison.Ordinal))
                    {
                        report.AddError(
                            $"Shop '{building.persistentInstanceId}' unique retail lot '{lot.itemInstanceId}' has no exact RetailStock equipment authority.");
                        continue;
                    }

                    physical.UniqueItems.TryGetValue(
                        lot.itemInstanceId,
                        out UniqueItemInstanceSaveData unique);
                    string physicalFingerprint = unique == null
                        ? string.Empty
                        : ItemStackSignature.Create(
                            unique.definitionId,
                            unique.components);
                    if (unique == null
                        || !string.Equals(
                            unique.definitionId,
                            lot.itemDefinitionId,
                            StringComparison.Ordinal)
                        || !string.Equals(
                            physicalFingerprint,
                            lot.componentFingerprint,
                            StringComparison.Ordinal))
                    {
                        report.AddError(
                            $"Shop '{building.persistentInstanceId}' unique retail lot '{lot.itemInstanceId}' does not match its physical component envelope.");
                    }
                }
            }
        }

        foreach (CombatEquipmentInstance retail in physical.EquipmentInstances.Values
                     .Where(instance => instance != null
                         && instance.worldState == CombatEquipmentWorldState.RetailStock))
        {
            if (!joinedRetailInstances.Contains(retail.instanceId))
            {
                report.AddError(
                    $"RetailStock equipment '{retail.instanceId}' has no owning shop exact-lot record.");
            }
        }


        foreach (string reservationOwner in physical.ReservationOwnerOperationIds
                     .Where(id => id.StartsWith(
                         "restock:",
                         StringComparison.Ordinal)))
        {
            if (!activeRestockOperations.Contains(reservationOwner))
            {
                report.AddError(
                    $"Restock reservation '{reservationOwner}' has no owning shop operation record.");
            }
        }
    }

    internal static void ValidateQualityRejectedSaleJoins(
        DungeonResourceStockPolicySaveData stockPolicies,
        TreasuryEconomySaveData treasury,
        DungeonSavePhysicalReferenceIndex physical,
        DungeonGameRestoreReport report)
    {
        Dictionary<string, QualityRejectedSalePending> owners =
            (stockPolicies?.pendingRejectedSales
                ?? new List<QualityRejectedSalePending>())
            .Where(value => value != null)
            .ToDictionary(value => value.operationId, StringComparer.Ordinal);
        HashSet<string> joinedEquipment = new(StringComparer.Ordinal);
        foreach (QualityRejectedSalePending owner in owners.Values)
        {
            if (owner.phase == QualityRejectedSaleCommitPhase.Prepared)
            {
                if (physical.PendingBatchByOperation.ContainsKey(owner.operationId))
                {
                    report.AddError(
                        $"Prepared quality-rejected sale '{owner.operationId}' unexpectedly owns a physical receipt.");
                }
                continue;
            }
            if (!physical.PendingBatchByOperation.TryGetValue(
                    owner.operationId,
                    out PhysicalItemBatchDispositionSaveData receipt)
                || receipt.kind != (int)PhysicalItemDispositionKind.Transfer
                || !string.Equals(
                    receipt.reasonCode,
                    owner.reasonCode,
                    StringComparison.Ordinal)
                || !string.Equals(
                    receipt.commitId,
                    owner.commitId,
                    StringComparison.Ordinal)
                || receipt.quantity != owner.quantity
                || receipt.inputMassGrams != owner.inputMassGrams
                || receipt.ownerRevision
                    != owner.physicalOutcomeOwnerRevision
                || receipt.sourceStackIds == null
                || receipt.sourceStackIds.Count != 1
                || !string.Equals(
                    receipt.sourceStackIds[0],
                    owner.sourceStackId,
                    StringComparison.Ordinal))
            {
                report.AddError(
                    $"Quality-rejected sale '{owner.operationId}' has no exact physical Transfer receipt.");
            }

            if (owner.requiresCombatAuthority
                && owner.phase < QualityRejectedSaleCommitPhase.UniqueAuthorityReleased)
            {
                if (!physical.EquipmentInstances.TryGetValue(
                        owner.itemInstanceId,
                        out CombatEquipmentInstance equipment)
                    || equipment.worldState
                        != CombatEquipmentWorldState.MarketSalePending
                    || !string.IsNullOrEmpty(equipment.ownerCharacterId)
                    || !string.Equals(
                        equipment.sourceStackId,
                        owner.operationId,
                        StringComparison.Ordinal)
                    || !joinedEquipment.Add(owner.itemInstanceId)
                    || !physical.UniqueItems.TryGetValue(
                        owner.itemInstanceId,
                        out UniqueItemInstanceSaveData unique)
                    || !string.Equals(
                        unique.definitionId,
                        owner.itemId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        QualityRejectedSaleOutbox.ComputeComponentFingerprint(
                            unique.definitionId,
                            unique.itemInstanceId,
                            unique.components),
                        owner.componentFingerprint,
                        StringComparison.Ordinal))
                {
                    report.AddError(
                        $"Quality-rejected sale '{owner.operationId}' has no exact MarketSalePending equipment authority.");
                }
            }
            else if (owner.requiresCombatAuthority
                && owner.phase >= QualityRejectedSaleCommitPhase.UniqueAuthorityReleased
                && physical.EquipmentInstances.ContainsKey(owner.itemInstanceId))
            {
                report.AddError(
                    $"Quality-rejected sale '{owner.operationId}' retained combat authority after terminal release.");
            }

            bool expectsIncome = owner.phase is
                QualityRejectedSaleCommitPhase.IncomePublished
                or QualityRejectedSaleCommitPhase.LedgerCommitted
                or QualityRejectedSaleCommitPhase.UniqueAuthorityReleased
                or QualityRejectedSaleCommitPhase.Acknowledged;
            EconomyTransactionRecord[] income = (treasury?.transactionLedger?.records
                    ?? new List<EconomyTransactionRecord>())
                .Where(value => value != null
                    && value.succeeded
                    && value.kind == EconomyTransactionKind.SaleIncome
                    && string.Equals(
                        value.sourceId,
                        owner.operationId,
                        StringComparison.Ordinal))
                .ToArray();
            if ((expectsIncome && (income.Length != 1
                    || income[0].amount != owner.proceeds
                    || !string.Equals(
                        income[0].transactionId,
                        owner.economyTransactionId,
                        StringComparison.Ordinal)
                    || income[0].balanceBefore != owner.balanceBefore
                    || income[0].balanceAfter != owner.balanceAfter
                    || !string.Equals(
                        income[0].targetId,
                        owner.itemId,
                        StringComparison.Ordinal)))
                || (!expectsIncome && income.Length != 0))
            {
                report.AddError(
                    $"Quality-rejected sale '{owner.operationId}' has a mismatched treasury income record.");
            }
        }

        foreach (CombatEquipmentInstance equipment in
                 physical.EquipmentInstances.Values.Where(value => value != null
                     && value.worldState
                        == CombatEquipmentWorldState.MarketSalePending))
        {
            if (!owners.TryGetValue(
                    equipment.sourceStackId,
                    out QualityRejectedSalePending owner)
                || !owner.requiresCombatAuthority
                || owner.phase >= QualityRejectedSaleCommitPhase.UniqueAuthorityReleased
                || !string.Equals(
                    owner.itemInstanceId,
                    equipment.instanceId,
                    StringComparison.Ordinal))
            {
                report.AddError(
                    $"MarketSalePending equipment '{equipment.instanceId}' has no exact quality-rejected sale owner.");
            }
        }
    }

    internal static void ValidateTreasury(
        TreasuryEconomySaveData source,
        ISet<string> characterIds,
        ISet<string> buildingIds,
        ISet<string> equipmentIds,
        DungeonGameRestoreReport report)
    {
        foreach (EmployeeWageState wage in
                 source?.employment?.wageStates
                 ?? new List<EmployeeWageState>())
        {
            if (wage == null)
            {
                report.AddError("Treasury aggregate contains a null wage state.");
                continue;
            }
            RequireReference(
                wage.characterId,
                characterIds,
                "Treasury wage character",
                report);
        }
        foreach (MercenaryContract contract in
                 source?.employment?.mercenaryContracts
                 ?? new List<MercenaryContract>())
        {
            if (contract == null)
            {
                report.AddError(
                    "Treasury aggregate contains a null mercenary contract.");
                continue;
            }
            RequireReference(
                contract.characterId,
                characterIds,
                "Treasury mercenary character",
                report);
        }
        foreach (PaidFacilityContractState contract in
                 source?.facilityContracts?.contracts
                 ?? new List<PaidFacilityContractState>())
        {
            if (contract == null)
            {
                report.AddError(
                    "Treasury aggregate contains a null facility contract.");
                continue;
            }
            RequireReference(
                contract.facilityPersistentId,
                buildingIds,
                "Treasury facility contract",
                report);
        }
        foreach (OverclockState state in source?.overclock?.states
                     ?? new List<OverclockState>())
        {
            if (state == null)
            {
                report.AddError("Treasury aggregate contains a null overclock state.");
                continue;
            }
            RequireReference(
                state.targetId,
                state.targetKind == OverclockTargetKind.Equipment
                    ? equipmentIds
                    : buildingIds,
                $"Treasury {state.targetKind} overclock target",
                report);
        }
        foreach (TreasuryDefensePolicy policy in
                 source?.treasuryDefense?.policies
                 ?? new List<TreasuryDefensePolicy>())
        {
            if (policy == null)
            {
                report.AddError("Treasury aggregate contains a null defense policy.");
                continue;
            }
            RequireReference(
                policy.facilityPersistentId,
                buildingIds,
                "Treasury defense policy facility",
                report);
        }
        foreach (TreasuryDefenseInvasionSpendState spending in
                 source?.treasuryDefense?.invasionSpending
                 ?? new List<TreasuryDefenseInvasionSpendState>())
        {
            if (spending == null)
            {
                report.AddError("Treasury aggregate contains null defense spending.");
                continue;
            }
            RequireReference(
                spending.facilityPersistentId,
                buildingIds,
                "Treasury defense spending facility",
                report);
        }
    }
}
