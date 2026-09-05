using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal static class CombatEquipmentRestoreBuilder
{
    internal static CombatEquipmentRestoreCandidate Build(
        DungeonCombatEquipmentSaveData source,
        ICombatEquipmentCatalog catalog,
        CombatEquipmentCraftingRuntime crafting)
    {
        if (source == null
            || source.loadouts == null
            || source.craftOrders == null
            || source.craftMaterialPolicies == null
            || source.craftTerminalEffects == null
            || source.historyTransferOrders == null
            || source.claimedLineageSealRegionIds == null)
        {
            throw new InvalidOperationException(
                "Combat equipment V8 payload is missing a required collection.");
        }

        CombatEquipmentRuntimeState restored = new()
        {
            NextCraftSequence = ResolveNextCraftSequence(source)
        };
        RestoreLoadouts(source.loadouts, restored, catalog);
        RestoreCraftOrders(source.craftOrders, restored, catalog, crafting);
        RestoreCraftTerminalEffects(source.craftTerminalEffects, restored);
        RestoreMaterialPolicies(
            source.craftMaterialPolicies,
            restored,
            catalog,
            crafting);
        RestoreHistoryOrders(source.historyTransferOrders, restored);
        RestoreClaimedRegions(source.claimedLineageSealRegionIds, restored);
        return new CombatEquipmentRestoreCandidate(restored);
    }

    private static int ResolveNextCraftSequence(
        DungeonCombatEquipmentSaveData source)
    {
        if (source.nextCraftSequence < 0)
            throw new InvalidOperationException(
                "Combat equipment next craft sequence is invalid.");
        int minimum = source.craftOrders?.Count ?? 0;
        foreach (CombatEquipmentCraftOrderSaveData order in source.craftOrders
                     ?? new List<CombatEquipmentCraftOrderSaveData>())
        {
            const string prefix = "combat-craft:";
            string orderId = order?.orderId?.Trim() ?? string.Empty;
            if (orderId.StartsWith(prefix, StringComparison.Ordinal)
                && int.TryParse(orderId.Substring(prefix.Length), out int sequence))
                minimum = Math.Max(minimum, checked(sequence + 1));
        }
        return Math.Max(source.nextCraftSequence, minimum);
    }

    private static void RestoreLoadouts(
        IEnumerable<CharacterCombatLoadoutState> source,
        CombatEquipmentRuntimeState restored,
        ICombatEquipmentCatalog catalog)
    {
        foreach (CharacterCombatLoadoutState loadout in source)
        {
            if (loadout == null)
            {
                throw new InvalidOperationException(
                    "Combat equipment loadout collection contains null.");
            }
            RequireCanonicalId(loadout.characterId, "loadout character");
            RequireCanonicalId(loadout.activeProfileId, "active loadout profile");
            if (loadout.profiles == null
                || !restored.Loadouts.TryAdd(
                    loadout.characterId,
                    CloneLoadout(loadout, catalog)))
            {
                throw new InvalidOperationException(
                    $"Loadout for character '{loadout.characterId}' is duplicate or incomplete.");
            }
        }
    }

    private static CharacterCombatLoadoutState CloneLoadout(
        CharacterCombatLoadoutState source,
        ICombatEquipmentCatalog catalog)
    {
        CharacterCombatLoadoutState clone = new()
        {
            characterId = source.characterId,
            activeProfileId = source.activeProfileId,
            profiles = new List<CharacterCombatLoadoutProfile>()
        };
        HashSet<string> profileIds = new(StringComparer.Ordinal);
        foreach (CharacterCombatLoadoutProfile profile in source.profiles)
        {
            if (profile == null)
            {
                throw new InvalidOperationException(
                    $"Loadout '{source.characterId}' contains a null profile.");
            }
            RequireCanonicalId(profile.profileId, "loadout profile");
            RequireCanonicalTextOrEmpty(profile.displayName, "loadout display name");
            RequireCanonicalTextOrEmpty(
                profile.desiredShieldDefinitionId,
                "desired shield definition");
            RequireCanonicalTextOrEmpty(
                profile.shieldInstanceId,
                "shield instance");
            RequireCanonicalTextOrEmpty(
                profile.activeWeaponInstanceId,
                "active weapon instance");
            if (!profileIds.Add(profile.profileId)
                || profile.weaponInstanceIds == null
                || profile.armorInstanceIds == null
                || profile.desiredWeaponDefinitionIds == null
                || profile.desiredArmorDefinitionIds == null
                || profile.desiredAmmo < 0
                || !Enum.IsDefined(typeof(CombatFireMode), profile.fireMode))
            {
                throw new InvalidOperationException(
                    $"Loadout profile '{profile.profileId}' has duplicate IDs or invalid fields.");
            }

            ValidateUniqueIds(profile.weaponInstanceIds, "weapon instance");
            ValidateUniqueIds(profile.armorInstanceIds, "armor instance");
            ValidateDefinitions<CombatWeaponSO>(
                profile.desiredWeaponDefinitionIds,
                "desired weapon",
                catalog);
            ValidateDefinitions<CombatArmorSO>(
                profile.desiredArmorDefinitionIds,
                "desired armor",
                catalog);
            if (!string.IsNullOrEmpty(profile.desiredShieldDefinitionId)
                && (!catalog.TryGet(
                        profile.desiredShieldDefinitionId,
                        out CombatEquipmentDefinitionSO shield)
                    || shield is not CombatShieldSO))
            {
                throw new InvalidOperationException(
                    $"Loadout profile '{profile.profileId}' references unknown shield definition '{profile.desiredShieldDefinitionId}'.");
            }
            if (!string.IsNullOrEmpty(profile.activeWeaponInstanceId)
                && !profile.weaponInstanceIds.Contains(
                    profile.activeWeaponInstanceId,
                    StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Active weapon '{profile.activeWeaponInstanceId}' is not in profile '{profile.profileId}'.");
            }
            clone.profiles.Add(profile.Clone());
        }
        if (clone.profiles.Count == 0
            || !profileIds.Contains(clone.activeProfileId))
        {
            throw new InvalidOperationException(
                $"Loadout '{source.characterId}' has no active profile definition.");
        }
        return clone;
    }

    private static void RestoreCraftOrders(
        IEnumerable<CombatEquipmentCraftOrderSaveData> source,
        CombatEquipmentRuntimeState restored,
        ICombatEquipmentCatalog catalog,
        CombatEquipmentCraftingRuntime crafting)
    {
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (CombatEquipmentCraftOrderSaveData order in source)
        {
            if (order == null)
            {
                throw new InvalidOperationException(
                    "Combat craft order collection contains null.");
            }
            RequireCanonicalId(order.orderId, "combat craft order");
            RequireCanonicalId(order.definitionId, "combat craft definition");
            RequireCanonicalTextOrEmpty(order.materialId, "combat craft material");
            RequireCanonicalId(
                order.materialDestinationId,
                "combat craft material destination");
            RequireCanonicalId(
                order.facilityPersistentId,
                "combat craft facility");
            bool ammunition = CombatEquipmentCraftingRuntime.IsAmmunitionRecipe(
                order.definitionId);
            if (!ids.Add(order.orderId)
                || !IsFinitePositive(order.requiredWork)
                || !IsFiniteInRange(
                    order.completedWork,
                    0f,
                    order.requiredWork,
                    includeMaximum: true)
                || !string.Equals(
                    order.materialDestinationId,
                    WorldItemStackRuntime.FacilityInputDestinationPrefix
                        + order.orderId,
                    StringComparison.Ordinal)
                || !Enum.IsDefined(
                    typeof(CraftsmanshipQualityTier),
                    order.minimumQuality)
                || !Enum.IsDefined(
                    typeof(RejectedOutputDisposition),
                    order.rejectedDisposition)
                || !Enum.IsDefined(
                    typeof(QualityRepeatLimitMode),
                    order.repeatLimitMode)
                || !Enum.IsDefined(
                    typeof(QualityTargetPipelineStage),
                    order.qualityStage)
                || order.maximumAttempts <= 0
                || order.requiredAcceptedCount <= 0
                || order.acceptedCount < 0
                || order.acceptedCount > order.requiredAcceptedCount
                || order.consumedWork < 0f
                || (order.rejectedOutputConsumed
                    && !order.dismantlingRejectedOutput)
                || (order.dismantlingRejectedOutput
                    && (string.IsNullOrWhiteSpace(order.rejectedInstanceId)
                        || string.IsNullOrWhiteSpace(order.rejectedStackId)))
                || (!ammunition
                    && (order.qualityRoll == null
                        || order.qualityRoll.attemptIndex
                            != order.qualityAttemptIndex))
                || !ValidateCraftTransaction(order, crafting)
                || !crafting.TryValidateInputDestinationProjection(
                    order,
                    out _))
            {
                throw new InvalidOperationException(
                    $"Combat craft order '{order.orderId}' has duplicate ID or invalid work.");
            }
            if (!ammunition
                && (!catalog.TryGet(order.definitionId, out CombatEquipmentDefinitionSO definition)
                    || !ValidateMaterial(definition, order.materialId, crafting)))
            {
                throw new InvalidOperationException(
                    $"Combat craft order '{order.orderId}' references invalid authored content.");
            }
            if (!ammunition
                && !crafting.IsDefinitionUnlocked(order.definitionId, out _))
            {
                throw new InvalidOperationException(
                    $"Combat craft order '{order.orderId}' bypasses its current research lock.");
            }
            if (ammunition && !string.IsNullOrEmpty(order.materialId))
            {
                throw new InvalidOperationException(
                    $"Ammunition craft order '{order.orderId}' cannot carry an equipment material ID.");
            }
            restored.CraftOrders.Add(order.Clone());
        }
    }

    private static void RestoreCraftTerminalEffects(
        IEnumerable<CombatEquipmentCraftTerminalEffectSaveData> source,
        CombatEquipmentRuntimeState restored)
    {
        HashSet<string> wipCommits = new(StringComparer.Ordinal);
        HashSet<string> removalCommits = new(StringComparer.Ordinal);
        foreach (CombatEquipmentCraftTerminalEffectSaveData row in source)
        {
            if (row == null
                || row.schemaVersion !=
                    CombatEquipmentCraftTerminalEffectSaveData
                        .CurrentSchemaVersion
                || !Enum.IsDefined(
                    typeof(CombatEquipmentCraftTerminalEffectPhase), row.phase)
                || row.releasedInputQuantity < 0
                || row.releasedInputMassGrams < 0L
                || (row.releasedInputQuantity == 0)
                    != (row.releasedInputMassGrams == 0L)
                || row.wipInputQuantity < 0
                || row.wipInputMassGrams < 0L
                || row.committedOutputMassGrams < 0L
                || row.declaredLossMassGrams < 0L
                || row.committedOutputMassGrams >
                    long.MaxValue - row.declaredLossMassGrams
                || row.committedOutputMassGrams + row.declaredLossMassGrams
                    != row.wipInputMassGrams)
            {
                throw new InvalidOperationException(
                    "Combat craft terminal effect has an invalid shape.");
            }
            RequireCanonicalId(row.ownerStableId,
                "combat craft terminal owner");
            RequireCanonicalId(row.sourceId, "combat craft terminal source");
            RequireCanonicalId(row.facilityId, "combat craft terminal facility");
            if (string.IsNullOrEmpty(row.frozenSourcePayload)
                || !CombatEquipmentTerminalDrainCanonical.IsDigest(
                    row.sourceFingerprint))
            {
                throw new InvalidOperationException(
                    "Combat craft terminal frozen source is invalid.");
            }

            bool hasInput = row.releasedInputQuantity > 0;
            if (hasInput
                ? string.IsNullOrEmpty(row.inputDispositionStepOperationId)
                    || !CombatEquipmentTerminalDrainCanonical.IsDigest(
                        row.inputDispositionRequestFingerprint)
                    || string.IsNullOrEmpty(row.inputDispositionCommitId)
                    || !CombatEquipmentTerminalDrainCanonical.IsDigest(
                        row.inputDispositionReceiptFingerprint)
                : !string.IsNullOrEmpty(row.inputDispositionStepOperationId)
                    || !string.IsNullOrEmpty(
                        row.inputDispositionRequestFingerprint)
                    || !string.IsNullOrEmpty(row.inputDispositionCommitId)
                    || !string.IsNullOrEmpty(
                        row.inputDispositionReceiptFingerprint))
            {
                throw new InvalidOperationException(
                    "Combat craft terminal input evidence is invalid.");
            }

            CombatEquipmentCraftOrderSaveData frozenOrder;
            try
            {
                frozenOrder = JsonUtility.FromJson<
                    CombatEquipmentCraftOrderSaveData>(row.frozenSourcePayload);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "Combat craft terminal source payload is invalid.",
                    exception);
            }
            CombatEquipmentTerminalMassAccounting mass = new(
                row.releasedInputQuantity,
                row.releasedInputMassGrams,
                row.wipInputQuantity,
                row.wipInputMassGrams,
                row.committedOutputMassGrams,
                row.declaredLossMassGrams);
            if (!CombatEquipmentTerminalFrozenSubject.TryCreateCraftOrder(
                    frozenOrder,
                    mass,
                    out CombatEquipmentTerminalFrozenSubject frozen,
                    out _)
                || !string.Equals(frozen.OwnerStableId, row.ownerStableId,
                    StringComparison.Ordinal)
                || !string.Equals(frozen.SourceId, row.sourceId,
                    StringComparison.Ordinal)
                || !string.Equals(frozen.FacilityId, row.facilityId,
                    StringComparison.Ordinal)
                || !string.Equals(frozen.SourceFingerprint,
                    row.sourceFingerprint, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Combat craft terminal frozen source drifted.");
            }

            CombatEquipmentTerminalWipLossReceiptSaveData wip =
                row.wipInputMassGrams == 0L
                    ? null
                    : new CombatEquipmentTerminalWipLossReceiptSaveData
                    {
                        commitId = row.wipLossCommitId,
                        sourceKind = CombatEquipmentTerminalSourceKind.CraftOrder,
                        ownerStableId = row.ownerStableId,
                        sourceId = row.sourceId,
                        facilityId = row.facilityId,
                        sourceFingerprint = row.sourceFingerprint,
                        inputQuantity = row.wipInputQuantity,
                        inputMassGrams = row.wipInputMassGrams,
                        committedOutputMassGrams =
                            row.committedOutputMassGrams,
                        declaredLossMassGrams = row.declaredLossMassGrams,
                        reason = (ProductionWipTerminalReason)row.terminalReason,
                        lossKind = (ProductionWipTerminalLossKind)row.lossKind,
                        receiptFingerprint = row.wipLossReceiptFingerprint
                    };
            if (wip != null
                && (!CombatEquipmentTerminalDrainCanonical
                        .IsValidWipLossReceipt(wip)
                    || !wipCommits.Add(wip.commitId)))
            {
                throw new InvalidOperationException(
                    "Combat craft terminal WIP receipt is invalid or duplicate.");
            }
            if (wip == null
                && (!string.IsNullOrEmpty(row.wipLossCommitId)
                    || !string.IsNullOrEmpty(row.wipLossReceiptFingerprint)))
            {
                throw new InvalidOperationException(
                    "Combat craft terminal empty WIP has receipt authority.");
            }

            bool removed = row.phase ==
                CombatEquipmentCraftTerminalEffectPhase.SourceRemoved;
            CombatEquipmentTerminalSourceRemovalReceiptSaveData removal =
                removed
                    ? new CombatEquipmentTerminalSourceRemovalReceiptSaveData
                    {
                        commitId = row.sourceRemovalCommitId,
                        sourceKind = CombatEquipmentTerminalSourceKind.CraftOrder,
                        ownerStableId = row.ownerStableId,
                        sourceId = row.sourceId,
                        facilityId = row.facilityId,
                        sourceFingerprint = row.sourceFingerprint,
                        receiptFingerprint =
                            row.sourceRemovalReceiptFingerprint
                    }
                    : null;
            if (removed
                ? !CombatEquipmentTerminalDrainCanonical
                        .IsValidSourceRemovalReceipt(removal)
                    || !removalCommits.Add(removal.commitId)
                : !string.IsNullOrEmpty(row.sourceRemovalCommitId)
                    || !string.IsNullOrEmpty(
                        row.sourceRemovalReceiptFingerprint))
            {
                throw new InvalidOperationException(
                    "Combat craft terminal removal receipt is invalid.");
            }

            CombatEquipmentCraftOrderSaveData live = restored.CraftOrders
                .SingleOrDefault(value => string.Equals(
                    value.orderId, row.sourceId, StringComparison.Ordinal));
            if (removed == (live != null)
                || live != null
                    && !string.Equals(JsonUtility.ToJson(live),
                        row.frozenSourcePayload, StringComparison.Ordinal)
                || !restored.CraftTerminalEffects.TryAdd(
                    row.sourceId, row.Clone()))
            {
                throw new InvalidOperationException(
                    "Combat craft terminal source/receipt join is invalid.");
            }
        }
    }

    private static void RestoreMaterialPolicies(
        IEnumerable<CombatEquipmentCraftMaterialPolicySaveData> source,
        CombatEquipmentRuntimeState restored,
        ICombatEquipmentCatalog catalog,
        CombatEquipmentCraftingRuntime crafting)
    {
        foreach (CombatEquipmentCraftMaterialPolicySaveData policy in source)
        {
            if (policy == null)
            {
                throw new InvalidOperationException(
                    "Combat craft material policy collection contains null.");
            }
            RequireCanonicalId(policy.facilityKey, "material policy facility");
            RequireCanonicalId(policy.definitionId, "material policy definition");
            if (policy.priorityMaterialIds == null
                || policy.allowedMaterialIds == null
                || !catalog.TryGet(
                    policy.definitionId,
                    out CombatEquipmentDefinitionSO definition))
            {
                throw new InvalidOperationException(
                    $"Material policy '{policy.facilityKey}/{policy.definitionId}' is incomplete or references an unknown definition.");
            }

            string[] authored = crafting.GetAllowedMaterials(policy.definitionId)
                .Select(material => material.MaterialId)
                .ToArray();
            ValidateUniqueIds(policy.priorityMaterialIds, "priority material");
            ValidateUniqueIds(policy.allowedMaterialIds, "allowed material");
            if (policy.priorityMaterialIds.Count != authored.Length
                || policy.priorityMaterialIds.Any(id =>
                    !authored.Contains(id, StringComparer.Ordinal))
                || policy.allowedMaterialIds.Count == 0
                || policy.allowedMaterialIds.Any(id =>
                    !authored.Contains(id, StringComparer.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"Material policy '{policy.facilityKey}/{policy.definitionId}' does not exactly match authored materials.");
            }

            string key = policy.facilityKey + "|" + definition.EquipmentId;
            if (!restored.CraftMaterialPolicies.TryAdd(key, policy.Clone()))
            {
                throw new InvalidOperationException(
                    $"Duplicate combat material policy '{key}'.");
            }
        }
    }

    private static void RestoreHistoryOrders(
        IEnumerable<EquipmentHistoryTransferOrder> source,
        CombatEquipmentRuntimeState restored)
    {
        HashSet<string> ids = new(StringComparer.Ordinal);
        HashSet<string> equipmentReservations = new(StringComparer.Ordinal);
        foreach (EquipmentHistoryTransferOrder order in source)
        {
            if (order == null)
            {
                throw new InvalidOperationException(
                    "Equipment history transfer order collection contains null.");
            }
            RequireCanonicalId(order.orderId, "history transfer order");
            RequireCanonicalId(order.sourceEquipmentInstanceId, "history source equipment");
            RequireCanonicalId(order.targetEquipmentInstanceId, "history target equipment");
            RequireCanonicalId(order.lineageSealStackId, "history lineage seal stack");
            RequireCanonicalId(order.facilityPersistentId, "history transfer facility");
            RequireCanonicalId(order.destinationId, "history transfer destination");
            if (!((BuildingInstanceId)order.facilityPersistentId).IsValid
                || !string.Equals(
                    order.destinationId,
                    order.facilityPersistentId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"History transfer order '{order.orderId}' has an invalid facility buffer identity.");
            }
            if (!ids.Add(order.orderId)
                || order.completed
                || string.Equals(
                    order.sourceEquipmentInstanceId,
                    order.targetEquipmentInstanceId,
                    StringComparison.Ordinal)
                || !equipmentReservations.Add(order.sourceEquipmentInstanceId)
                || !equipmentReservations.Add(order.targetEquipmentInstanceId)
                || !IsFinitePositive(order.requiredWork)
                || !IsFiniteInRange(
                    order.completedWork,
                    0f,
                    order.requiredWork,
                    includeMaximum: false))
            {
                throw new InvalidOperationException(
                    $"History transfer order '{order.orderId}' is duplicate, completed, conflicting, or out of range.");
            }
            restored.HistoryTransferOrders.Add(order.Clone());
        }
    }

    private static void RestoreClaimedRegions(
        IEnumerable<string> source,
        CombatEquipmentRuntimeState restored)
    {
        foreach (string id in source)
        {
            RequireCanonicalId(id, "claimed lineage region");
            if (!restored.ClaimedLineageSealRegionIds.Add(id))
            {
                throw new InvalidOperationException(
                    $"Duplicate claimed lineage region '{id}'.");
            }
        }
    }

    private static bool ValidateCraftTransaction(
        CombatEquipmentCraftOrderSaveData order,
        CombatEquipmentCraftingRuntime crafting)
    {
        if (order.materialTransferInputs == null
            || order.recoveryOutputs == null
            || order.spawnedRecoveryAmounts == null
            || !crafting.TryGetConcreteMaterials(
                order,
                out IReadOnlyDictionary<string, int> materials))
        {
            return false;
        }

        if (order.dismantlingRejectedOutput)
        {
            bool recoveryShape = !string.IsNullOrWhiteSpace(
                    order.rejectedInstanceId)
                && !string.IsNullOrWhiteSpace(order.rejectedStackId)
                && order.spawnedRecoveryAmounts.Count <=
                    order.recoveryOutputs.Count
                && order.recoveryOutputs.All(output =>
                    output != null
                    && !string.IsNullOrWhiteSpace(output.itemId)
                    && output.amount > 0)
                && order.spawnedRecoveryAmounts.Select((amount, index) =>
                        amount >= 0
                        && amount <= order.recoveryOutputs[index].amount)
                    .All(value => value)
                && (!order.rejectedRecoveryPublished
                    || order.recoveryOutputs.Select((output, index) =>
                            index < order.spawnedRecoveryAmounts.Count
                            && order.spawnedRecoveryAmounts[index]
                                == output.amount)
                        .All(value => value));
            if (!recoveryShape)
            {
                return false;
            }
            if (!crafting.TryValidateFrozenRejectedRecovery(order, out _))
            {
                return false;
            }
            if (!string.IsNullOrEmpty(order.rejectedDismantleOperationId)
                && !CombatEquipmentRejectedDismantleOutbox
                    .ValidateProvenance(order, out _))
            {
                return false;
            }
        }
        else if (!crafting.TryValidateEmptyRejectedRecovery(order, out _))
        {
            return false;
        }

        bool hasMaterials = materials.Any(pair => pair.Value > 0);
        bool hasTransfer = !string.IsNullOrEmpty(
            order.materialTransferOperationId);
        if (hasMaterials != hasTransfer
            && !order.dismantlingRejectedOutput)
        {
            // Before a craft attempt starts the physical lots are delivered but
            // not yet transferred. A resolved or in-progress WIP attempt must
            // always own the receipt.
            if (hasTransfer
                || order.materialsReady
                || order.completedWork > 0f
                || order.attemptOutcomeResolved)
            {
                return false;
            }
        }
        if (hasTransfer
            && !CombatEquipmentCraftMaterialOutbox.ValidateProvenance(
                order,
                materials,
                out _))
        {
            return false;
        }

        if (!order.attemptOutcomeResolved)
        {
            return !order.outputPublished
                && string.IsNullOrEmpty(order.outputOperationId)
                && string.IsNullOrEmpty(order.outputItemId)
                && order.outputQuantity == 0
                && string.IsNullOrEmpty(order.outputCommitId)
                && string.IsNullOrEmpty(order.outputInstanceId)
                && string.IsNullOrEmpty(order.outputStackId)
                && (order.outputCapability == null
                    || order.outputCapability.IsEmpty)
                && order.outputPhase == CombatEquipmentCraftOutputPhase.None
                && (order.outputPublication == null
                    || order.outputPublication.IsEmpty)
                && !order.outputMarketRouted
                && order.outputPreparedComponent == null
                && order.resolvedMythicProvenance == null
                && !order.completionEffectsPublished;
        }

        bool ammunition = CombatEquipmentCraftingRuntime.IsAmmunitionRecipe(
            order.definitionId);
        if (order.outputPhase !=
                CombatEquipmentCraftOutputPhase.LegacyUniqueOutput)
        {
            bool ownerValid = ProductionDomainOutputPublicationService
                .TryValidateRestorableOwner(
                    order.outputPublication,
                    out bool committed,
                    out _);
            bool phaseValid = order.outputPhase switch
            {
                CombatEquipmentCraftOutputPhase
                    .ResolvedWaitingForPublication => !committed
                        && order.outputPublication is { outputAcknowledged: false }
                        && !order.outputPublished,
                CombatEquipmentCraftOutputPhase
                    .PublishedAwaitingInputAcknowledgement => committed
                        && !order.outputPublication.outputAcknowledged
                        && order.outputPublished,
                CombatEquipmentCraftOutputPhase
                    .RestoredOutputAwaitingInputAcknowledgement => committed
                        && order.outputPublication.outputAcknowledged
                        && order.outputPublished,
                _ => false
            };
            bool identityValid = ammunition
                ? string.IsNullOrEmpty(order.outputInstanceId)
                    && order.outputPreparedComponent == null
                    && (!committed || order.outputPublication.stacks.All(
                        value => string.IsNullOrEmpty(value.itemInstanceId)))
                : ((ItemInstanceId)order.outputInstanceId).IsValid
                    && order.outputPreparedComponent != null
                    && EquipmentItemStateCodec.TryDecode(
                        order.outputPreparedComponent,
                        out CombatEquipmentInstance prepared,
                        out _)
                    && string.Equals(
                        prepared.instanceId,
                        order.outputInstanceId,
                        StringComparison.Ordinal)
                    && string.IsNullOrEmpty(prepared.sourceStackId)
                    && (!committed
                        || order.outputPublication.stacks.Count == 1
                            && string.Equals(
                                order.outputPublication.stacks[0]
                                    .itemInstanceId,
                                order.outputInstanceId,
                                StringComparison.Ordinal));
            return order.materialsReady
                && order.completionEffectsPublished
                && crafting.TryValidateResolvedOutputCapability(order, out _)
                && Enum.IsDefined(
                    typeof(CombatEquipmentQuality),
                    order.resolvedQuality)
                && !string.IsNullOrWhiteSpace(order.outputOperationId)
                && !string.IsNullOrWhiteSpace(order.outputItemId)
                && order.outputQuantity > 0
                && ownerValid
                && phaseValid
                && identityValid
                && (committed
                    ? string.Equals(
                        order.outputCommitId,
                        order.outputPublication.batchCommitId,
                        StringComparison.Ordinal)
                    : string.IsNullOrEmpty(order.outputCommitId))
                && (ammunition
                    ? string.IsNullOrEmpty(order.outputStackId)
                    : !committed
                        ? string.IsNullOrEmpty(order.outputStackId)
                        : string.Equals(
                            order.outputStackId,
                            order.outputPublication.stacks[0].stackId,
                            StringComparison.Ordinal))
                && (!order.outputMarketRouted
                    || order.outputPublication.outputAcknowledged)
                && (!order.materialTransferAcknowledged || committed);
        }
        return order.materialsReady
            && order.completionEffectsPublished
            && crafting.TryValidateResolvedOutputCapability(order, out _)
            && Enum.IsDefined(
                typeof(CombatEquipmentQuality),
                order.resolvedQuality)
            && !string.IsNullOrWhiteSpace(order.outputOperationId)
            && string.Equals(
                order.outputOperationId,
                CombatEquipmentCraftOutputOutbox.FormatOperationId(
                    order.orderId,
                    order.qualityAttemptIndex),
                StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(order.outputItemId)
            && order.outputQuantity > 0
            && (!order.outputPublished
                || (!string.IsNullOrWhiteSpace(order.outputCommitId)
                    && (ammunition
                        ? string.IsNullOrEmpty(order.outputInstanceId)
                            && string.IsNullOrEmpty(order.outputStackId)
                        : !string.IsNullOrWhiteSpace(order.outputInstanceId)
                            && !string.IsNullOrWhiteSpace(order.outputStackId))))
            && (!order.materialTransferAcknowledged
                || order.outputPublished)
            && order.outputPhase ==
                CombatEquipmentCraftOutputPhase.LegacyUniqueOutput
            && (order.outputPublication == null
                || order.outputPublication.IsEmpty)
            && !order.outputMarketRouted
            && order.outputPreparedComponent == null;
    }

    private static bool ValidateMaterial(
        CombatEquipmentDefinitionSO definition,
        string materialId,
        CombatEquipmentCraftingRuntime crafting)
    {
        IReadOnlyList<CraftMaterialDefinitionSO> allowed =
            crafting.GetAllowedMaterials(definition.EquipmentId);
        return allowed.Count == 0
            ? string.Equals(
                materialId,
                definition.DefaultMaterialId,
                StringComparison.Ordinal)
                || string.IsNullOrEmpty(materialId)
            : allowed.Any(material => string.Equals(
                material.MaterialId,
                materialId,
                StringComparison.Ordinal));
    }

    private static void ValidateDefinitions<TDefinition>(
        IEnumerable<string> source,
        string label,
        ICombatEquipmentCatalog catalog)
        where TDefinition : CombatEquipmentDefinitionSO
    {
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (string id in source)
        {
            RequireCanonicalId(id, label);
            if (!ids.Add(id)
                || !catalog.TryGet(id, out CombatEquipmentDefinitionSO definition)
                || definition is not TDefinition)
            {
                throw new InvalidOperationException(
                    $"{label} definition '{id}' is duplicate, unknown, or has the wrong kind.");
            }
        }
    }

    private static void ValidateUniqueIds(
        IEnumerable<string> source,
        string label)
    {
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (string id in source)
        {
            RequireCanonicalId(id, label);
            if (!ids.Add(id))
            {
                throw new InvalidOperationException(
                    $"Duplicate {label} id '{id}'.");
            }
        }
    }

    private static bool IsFinitePositive(float value) =>
        float.IsFinite(value) && value > 0f;

    private static bool IsFiniteInRange(
        float value,
        float minimum,
        float maximum,
        bool includeMaximum)
    {
        return float.IsFinite(value)
            && value >= minimum
            && (includeMaximum ? value <= maximum : value < maximum);
    }

    private static void RequireCanonicalId(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{label} id must be non-empty and canonical.");
        }
    }

    private static void RequireCanonicalTextOrEmpty(string value, string label)
    {
        if (value == null
            || (!string.IsNullOrEmpty(value)
                && !string.Equals(value, value.Trim(), StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"{label} must be non-null and canonical.");
        }
    }
}
