using System;
using System.Collections.Generic;
using System.Linq;

public interface IDungeonSavePreflightValidator
{
    void Validate(DungeonGameSaveData saveData, DungeonGameRestoreReport report);
}

public interface IDungeonSaveCaptureGuard
{
    void ValidateBeforeCapture();
}

public interface IDungeonSaveRestoreCompletedHook
{
    void OnRestoreCompleted();
}

/// <summary>
/// Validates identities and authored-content references across aggregate payloads before
/// any save section is allowed to mutate the live world.
/// </summary>
public sealed class DungeonAggregateReferencePreflight : IDungeonSavePreflightValidator
{
    private readonly IItemDefinitionCatalog itemDefinitions;
    private readonly IBuildingDefinitionLookup buildingDefinitions;
    private readonly ICombatEquipmentCatalog combatEquipmentDefinitions;
    private readonly IResourceEconomyContentCatalog economyContent;
    private readonly ICharacterLifeDefinitionCatalog lifeDefinitions;
    private readonly IDiseaseDefinitionCatalog diseaseDefinitions;

    public DungeonAggregateReferencePreflight(
        IItemDefinitionCatalog itemDefinitions,
        IBuildingDefinitionLookup buildingDefinitions,
        ICombatEquipmentCatalog combatEquipmentDefinitions,
        IResourceEconomyContentCatalog economyContent,
        ICharacterLifeDefinitionCatalog lifeDefinitions,
        IDiseaseDefinitionCatalog diseaseDefinitions)
    {
        this.itemDefinitions = itemDefinitions
            ?? throw new ArgumentNullException(nameof(itemDefinitions));
        this.buildingDefinitions = buildingDefinitions
            ?? throw new ArgumentNullException(nameof(buildingDefinitions));
        this.combatEquipmentDefinitions = combatEquipmentDefinitions
            ?? throw new ArgumentNullException(nameof(combatEquipmentDefinitions));
        this.economyContent = economyContent
            ?? throw new ArgumentNullException(nameof(economyContent));
        this.lifeDefinitions = lifeDefinitions
            ?? throw new ArgumentNullException(nameof(lifeDefinitions));
        this.diseaseDefinitions = diseaseDefinitions
            ?? throw new ArgumentNullException(nameof(diseaseDefinitions));
    }

    public void Validate(
        DungeonGameSaveData saveData,
        DungeonGameRestoreReport report)
    {
        if (saveData == null) throw new ArgumentNullException(nameof(saveData));
        if (report == null) throw new ArgumentNullException(nameof(report));

        DungeonPhysicalItemSaveData items =
            DungeonSaveSectionPayload.ReadOrNew<DungeonPhysicalItemSaveData>(
                saveData,
                PhysicalItemsSaveSection.Id);
        DungeonCharacterWorldSaveData characters =
            DungeonSaveSectionPayload.ReadOrNew<DungeonCharacterWorldSaveData>(
                saveData,
                CharacterWorldSaveSection.Id);
        ModularFacilityWorldSaveData buildings =
            DungeonSaveSectionPayload.ReadOrNew<ModularFacilityWorldSaveData>(
                saveData,
                ModularFacilityWorldSaveSection.Id);
        DungeonOffenseAggregateSaveData offense =
            DungeonSaveSectionPayload.ReadOrNew<DungeonOffenseAggregateSaveData>(
                saveData,
                OffenseAggregateSaveSection.Id);
        DungeonCombatEquipmentSaveData combat =
            DungeonSaveSectionPayload.ReadOrNew<DungeonCombatEquipmentSaveData>(
                saveData,
                CombatEquipmentSaveSection.Id);
        DungeonInvasionSaveData invasion =
            DungeonSaveSectionPayload.ReadOrNew<DungeonInvasionSaveData>(
                saveData,
                InvasionSaveSection.Id);
        EquipmentEvolutionSaveData evolution =
            DungeonSaveSectionPayload.ReadOrNew<EquipmentEvolutionSaveData>(
                saveData,
                EquipmentEvolutionSaveSection.Id);
        DungeonMetaProgressionSaveData meta =
            DungeonSaveSectionPayload.ReadOrNew<DungeonMetaProgressionSaveData>(
                saveData,
                MetaProgressionSaveSection.Id);
        DungeonResearchSaveData research =
            DungeonSaveSectionPayload.ReadOrNew<DungeonResearchSaveData>(
                saveData,
                BlueprintResearchSaveSection.Id);
        DungeonCropPlotSaveData cropPlots =
            DungeonSaveSectionPayload.ReadOrNew<DungeonCropPlotSaveData>(
                saveData,
                CropPlotSaveSection.Id);
        TreasuryEconomySaveData treasury =
            DungeonSaveSectionPayload.ReadOrNew<TreasuryEconomySaveData>(
                saveData,
                TreasuryEconomySaveSection.Id);
        CharacterLifeWorldSaveData life =
            DungeonSaveSectionPayload.ReadOrNew<CharacterLifeWorldSaveData>(
                saveData,
                CharacterLifeSaveSection.Id);
        KinshipHouseholdWorldSaveData kinshipHouseholds =
            DungeonSaveSectionPayload.ReadOrNew<KinshipHouseholdWorldSaveData>(
                saveData,
                KinshipHouseholdSaveSection.Id);
        ReproductionWorldSaveData reproduction =
            DungeonSaveSectionPayload.ReadOrNew<ReproductionWorldSaveData>(
                saveData,
                ReproductionSaveSection.Id);
        PopulationHealthWorldSaveData populationHealth =
            DungeonSaveSectionPayload.ReadOrNew<PopulationHealthWorldSaveData>(
                saveData,
                PopulationHealthSaveSection.Id);
        CharacterCareerWorldSaveData careers =
            DungeonSaveSectionPayload.ReadOrNew<CharacterCareerWorldSaveData>(
                saveData,
                CharacterCareerSaveSection.Id);
        CharacterPsychosocialWorldSaveData psychosocial =
            DungeonSaveSectionPayload.ReadOrNew<CharacterPsychosocialWorldSaveData>(
                saveData,
                CharacterPsychosocialSaveSection.Id);
        CropEcologyWorldSaveData cropEcology =
            DungeonSaveSectionPayload.ReadOrNew<CropEcologyWorldSaveData>(
                saveData,
                CropEcologySaveSection.Id);

        // ReadOrNew deserializes detached DTO graphs. Normalize only the explicitly
        // typed early-V18 character-reference paths on those graphs before building
        // the cross-section indexes. The source envelopes and live runtime remain
        // untouched; section preflight still owns warning emission and publication.
        NormalizeEarlyV18CharacterReferences(
            characters,
            offense,
            combat,
            treasury);

        PhysicalReferenceIndex physical = ValidatePhysicalItems(items, report);
        HashSet<string> characterIds = ValidateCharacters(characters, report);
        AddActiveInvasionCharacterIds(invasion, characterIds, report);
        BuildingReferenceIndex buildingIds = ValidateBuildings(buildings, report);
        ValidateOffenseMembers(offense, characterIds, report);
        ValidateOffenseReferences(
            offense,
            characterIds,
            physical,
            buildingIds.InstanceIds,
            report);
        ValidateCombat(
            combat,
            characterIds,
            physical,
            buildingIds.InstanceIds,
            research,
            report);
        ValidateEvolution(
            evolution,
            physical.EquipmentInstanceIds,
            buildingIds.InstanceIds,
            report);
        ValidateMeta(meta, report);
        ValidateResearch(research, buildings, offense, report);
        ValidateCropPlots(cropPlots, buildingIds, report);
        ValidateTreasury(
            treasury,
            characterIds,
            buildingIds.InstanceIds,
            physical.EquipmentInstanceIds,
            report);
        HashSet<string> lifeCharacterIds = ValidateCharacterLife(
            life,
            characterIds,
            report);
        KinshipReferenceIndex kinship = ValidateKinshipHouseholds(
            kinshipHouseholds,
            characterIds,
            buildingIds.InstanceIds,
            report);
        ValidateReproduction(
            reproduction,
            characterIds,
            kinship.AllCharacterIds,
            report);
        ValidatePopulationHealth(populationHealth, characterIds, report);
        ValidateCareers(
            careers,
            characterIds,
            buildingIds.InstanceIds,
            report);
        ValidatePsychosocial(
            psychosocial,
            characterIds,
            kinship.AllCharacterIds,
            report);
        ValidateCropEcology(
            cropEcology,
            cropPlots,
            buildingIds.InstanceIds,
            physical.SeedLots,
            report);

        foreach (string characterId in characterIds.Except(lifeCharacterIds))
        {
            report.AddError(
                $"Character '{characterId}' has no V19 life record.");
        }
    }

    private static void AddActiveInvasionCharacterIds(
        DungeonInvasionSaveData invasion,
        ISet<string> characterIds,
        DungeonGameRestoreReport report)
    {
        HashSet<string> activeIntruderIds = new(StringComparer.Ordinal);
        foreach (DungeonInvasionIntruderSaveData intruder in
                 invasion?.activeIntruders
                 ?? new List<DungeonInvasionIntruderSaveData>())
        {
            string rawId = intruder?.enemyIndividual?.characterId;
            CharacterId characterId = (CharacterId)rawId;
            if (intruder == null
                || !characterId.IsValid
                || !string.Equals(rawId, characterId.Value, StringComparison.Ordinal)
                || !activeIntruderIds.Add(characterId.Value))
            {
                report.AddError(
                    $"Active invasion intruder has an invalid or duplicate character ID '{rawId ?? string.Empty}'.");
                continue;
            }

            if (!characterIds.Add(characterId.Value))
            {
                report.AddError(
                    $"Active invasion intruder character '{characterId.Value}' collides with a resident character.");
            }
        }
    }

    private static void NormalizeEarlyV18CharacterReferences(
        DungeonCharacterWorldSaveData characters,
        DungeonOffenseAggregateSaveData offense,
        DungeonCombatEquipmentSaveData combat,
        TreasuryEconomySaveData treasury)
    {
        NormalizeCharacterWorldReferences(characters);
        V18TypedCharacterReferenceRestoreNormalizer.Normalize(
            offense,
            (value, _) => NormalizeCharacterReference(value));
        V18TypedCharacterReferenceRestoreNormalizer.Normalize(
            combat,
            (value, _) => NormalizeCharacterReference(value));
        NormalizeTreasuryCharacterReferences(treasury);
    }

    private static void NormalizeCharacterWorldReferences(
        DungeonCharacterWorldSaveData source)
    {
        if (source?.actors != null)
        {
            foreach (DungeonCharacterSaveData actor in source.actors)
            {
                if (actor != null)
                {
                    actor.persistentId = NormalizeCharacterReference(
                        actor.persistentId);
                }
            }
        }

        if (source?.populationProfiles == null)
        {
            return;
        }

        foreach (WorldCharacterProfile profile in source.populationProfiles)
        {
            if (profile != null)
            {
                profile.persistentId = NormalizeCharacterReference(
                    profile.persistentId);
            }
        }
    }

    private static void NormalizeTreasuryCharacterReferences(
        TreasuryEconomySaveData source)
    {
        if (source?.employment?.wageStates != null)
        {
            foreach (EmployeeWageState wage in source.employment.wageStates)
            {
                if (wage != null)
                {
                    wage.characterId = NormalizeCharacterReference(
                        wage.characterId);
                }
            }
        }

        if (source?.employment?.mercenaryContracts == null)
        {
            return;
        }

        foreach (MercenaryContract contract in
                 source.employment.mercenaryContracts)
        {
            if (contract != null)
            {
                contract.characterId = NormalizeCharacterReference(
                    contract.characterId);
            }
        }
    }

    private static string NormalizeCharacterReference(string value)
    {
        if (value == null
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            return value;
        }

        return CharacterId.TryCanonicalizeV18Restore(
                value,
                out CharacterId canonical,
                out bool wasLegacy)
            && wasLegacy
                ? canonical.Value
                : value;
    }

    private PhysicalReferenceIndex ValidatePhysicalItems(
        DungeonPhysicalItemSaveData source,
        DungeonGameRestoreReport report)
    {
        PhysicalReferenceIndex result = new PhysicalReferenceIndex();
        foreach (WorldItemStackSaveData stack in source?.stacks
                     ?? new List<WorldItemStackSaveData>())
        {
            if (stack == null)
            {
                report.AddError("Physical item aggregate contains a null stack.");
                continue;
            }

            RequireUniqueId(stack.stackId, "item stack", result.StackIds, report);
            RequireItemDefinition(stack.itemId, report);
            if (!string.IsNullOrWhiteSpace(stack.itemInstanceId))
            {
                RequireUniqueId(
                    stack.itemInstanceId,
                    "item instance",
                    result.ItemInstanceIds,
                    report);
            }

            if (stack.quantity <= 0)
            {
                report.AddError($"Item stack '{stack.stackId}' has non-positive quantity {stack.quantity}.");
            }

            CollectSeedLot(stack.components, stack.itemId, result, report);
        }

        foreach (UniqueItemInstanceSaveData unique in source?.uniqueItems
                     ?? new List<UniqueItemInstanceSaveData>())
        {
            if (unique == null)
            {
                report.AddError("Physical item aggregate contains a null unique item.");
                continue;
            }

            // A physical stack and its unique-item component payload are two
            // projections of the same item identity. Reject duplicates within
            // the unique-item collection, but merge that identity into the
            // aggregate reference index instead of treating the matching stack
            // projection as a second item.
            RequireUniqueId(
                unique.itemInstanceId,
                "unique item instance",
                result.UniqueItemInstanceIds,
                report);
            string uniqueItemInstanceId = unique.itemInstanceId?.Trim()
                ?? string.Empty;
            if (uniqueItemInstanceId.Length > 0)
            {
                result.ItemInstanceIds.Add(uniqueItemInstanceId);
            }
            RequireItemDefinition(unique.definitionId, report);
            CollectSeedLot(unique.components, unique.definitionId, result, report);
            ItemInstanceComponentSaveData equipmentComponent = unique.components?
                .FirstOrDefault(component => component != null
                    && string.Equals(
                        component.componentTypeId,
                        ItemInstanceComponentIds.Equipment,
                        StringComparison.Ordinal));
            if (equipmentComponent != null
                && EquipmentItemStateCodec.TryDecode(
                    equipmentComponent,
                    out CombatEquipmentInstance equipment,
                    out _))
            {
                if (string.IsNullOrEmpty(equipment.instanceId)
                    || !string.Equals(
                        equipment.instanceId,
                        unique.itemInstanceId,
                        StringComparison.Ordinal)
                    || !result.EquipmentInstanceIds.Add(equipment.instanceId)
                    || !result.EquipmentDefinitionIds.TryAdd(
                        equipment.instanceId,
                        equipment.definitionId))
                {
                    report.AddError(
                        $"Physical equipment component '{equipment.instanceId}' does not uniquely match owning item instance '{unique.itemInstanceId}'.");
                }
                else if (!combatEquipmentDefinitions.TryGet(
                             equipment.definitionId,
                             out _))
                {
                    report.AddError(
                        $"Physical equipment '{equipment.instanceId}' references unknown equipment definition '{equipment.definitionId}'.");
                }
            }
        }

        return result;
    }

    private static void CollectSeedLot(
        IReadOnlyList<ItemInstanceComponentSaveData> components,
        string itemDefinitionId,
        PhysicalReferenceIndex index,
        DungeonGameRestoreReport report)
    {
        if (!(components ?? Array.Empty<ItemInstanceComponentSaveData>()).Any(
                component => component != null
                    && string.Equals(
                        component.componentTypeId,
                        ItemInstanceComponentIds.SeedLot,
                        StringComparison.Ordinal)))
        {
            return;
        }

        try
        {
            SeedLotState seedLot = SeedLotItemStateCodec.Decode(components);
            index.SeedLots.Add((itemDefinitionId?.Trim() ?? string.Empty, seedLot));
        }
        catch (Exception exception)
        {
            report.AddError(
                $"Physical seed lot '{itemDefinitionId}' is invalid: {exception.Message}");
        }
    }

    private HashSet<string> ValidateCharacters(
        DungeonCharacterWorldSaveData source,
        DungeonGameRestoreReport report)
    {
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (DungeonCharacterSaveData actor in source?.actors
                     ?? new List<DungeonCharacterSaveData>())
        {
            if (actor == null)
            {
                report.AddError("Character aggregate contains a null actor.");
                continue;
            }

            RequireUniqueCharacterId(
                actor.persistentId,
                "character",
                ids,
                report,
                rejectDuplicate: true);
            foreach (CharacterCarriedItemSaveData carried in actor.carryInventory?.items
                         ?? new List<CharacterCarriedItemSaveData>())
            {
                if (carried != null)
                {
                    RequireItemDefinition(carried.itemId, report);
                }
            }
        }

        foreach (WorldCharacterProfile profile in source?.populationProfiles
                     ?? new List<WorldCharacterProfile>())
        {
            RequireUniqueCharacterId(
                profile?.persistentId,
                "character population profile",
                ids,
                report,
                rejectDuplicate: false);
        }

        return ids;
    }

    private BuildingReferenceIndex ValidateBuildings(
        ModularFacilityWorldSaveData source,
        DungeonGameRestoreReport report)
    {
        BuildingReferenceIndex result = new BuildingReferenceIndex();
        foreach (ModularFacilityBuildingSaveData building in source?.buildings
                     ?? new List<ModularFacilityBuildingSaveData>())
        {
            if (building == null)
            {
                report.AddError("Building aggregate contains a null building.");
                continue;
            }

            RequireUniqueId(
                building.persistentInstanceId,
                "building",
                result.InstanceIds,
                report);
            try
            {
                BuildingSO definition =
                    buildingDefinitions.GetBuilding(building.buildingId);
                result.InstanceDefinitions[building.persistentInstanceId] =
                    definition;
            }
            catch (Exception)
            {
                report.AddError(
                    $"Building '{building.persistentInstanceId}' references unknown definition {building.buildingId}.");
            }
        }

        return result;
    }

    private static void ValidateOffenseMembers(
        DungeonOffenseAggregateSaveData source,
        ISet<string> characterIds,
        DungeonGameRestoreReport report)
    {
        foreach (DungeonOffenseExpeditionRunSaveData run in source?.expedition?.activeExpeditions
                     ?? new List<DungeonOffenseExpeditionRunSaveData>())
        {
            if (run == null || string.IsNullOrWhiteSpace(run.expeditionId))
            {
                report.AddError("Offense aggregate contains an expedition without an ID.");
                continue;
            }

            foreach (string memberId in (run.memberPersistentIds ?? new List<string>())
                         .Concat(run.protectedRescueMemberPersistentIds ?? new List<string>())
                         .Concat((run.memberStates ?? new List<DungeonOffenseExpeditionMemberStateSaveData>())
                             .Where(state => state != null)
                             .Select(state => state.persistentId)))
            {
                string id = memberId?.Trim() ?? string.Empty;
                if (id.Length == 0 || !characterIds.Contains(id))
                {
                    report.AddError(
                        $"Expedition '{run.expeditionId}' references missing character '{id}'.");
                }
            }
        }
    }

    private void ValidateOffenseReferences(
        DungeonOffenseAggregateSaveData source,
        ISet<string> characterIds,
        PhysicalReferenceIndex physical,
        ISet<string> buildingIds,
        DungeonGameRestoreReport report)
    {
        OffenseWorldSaveData world = source?.world;
        foreach (FieldStabilizationState stabilization in
                 world?.fieldStabilizations
                 ?? new List<FieldStabilizationState>())
        {
            if (stabilization == null)
            {
                continue;
            }
            RequireReference(
                stabilization.characterId,
                characterIds,
                $"Field stabilization '{stabilization.expeditionId}' character",
                report);
            string kitId = stabilization.consumedKitInstanceId ?? string.Empty;
            if (!kitId.StartsWith("packed:", StringComparison.Ordinal)
                && !physical.ItemInstanceIds.Contains(kitId)
                && !physical.StackIds.Contains(kitId))
            {
                report.AddError(
                    $"Field stabilization '{stabilization.expeditionId}' references missing consumed kit '{kitId}'.");
            }
        }

        foreach (OffenseCasualtyCarryState carry in world?.casualtyCarries
                     ?? new List<OffenseCasualtyCarryState>())
        {
            if (carry == null)
            {
                continue;
            }
            RequireReference(
                carry.casualtyCharacterId,
                characterIds,
                $"Casualty carry '{carry.expeditionId}' casualty",
                report);
            RequireReference(
                carry.carrierCharacterId,
                characterIds,
                $"Casualty carry '{carry.expeditionId}' carrier",
                report);
        }

        foreach (RescueConvoyState convoy in world?.rescueConvoys
                     ?? new List<RescueConvoyState>())
        {
            if (convoy == null)
            {
                continue;
            }
            foreach (string characterId in
                     (convoy.rescuerCharacterIds ?? new List<string>())
                     .Concat(convoy.protectedCasualtyIds ?? new List<string>()))
            {
                RequireReference(
                    characterId,
                    characterIds,
                    $"Rescue convoy '{convoy.rescueExpeditionId}' character",
                    report);
            }
        }

        foreach (OffenseUrgentMitigationOrderStateData order in
                 world?.mitigationOrders
                 ?? new List<OffenseUrgentMitigationOrderStateData>())
        {
            if (order != null
                && !string.IsNullOrEmpty(order.facilityPersistentId))
            {
                RequireReference(
                    order.facilityPersistentId,
                    buildingIds,
                    $"Offense mitigation order '{order.orderId}' facility",
                    report);
            }
        }

        foreach (OffenseSupplyPackingItemStateData cost in
                 (world?.supplyPackages
                     ?? new List<OffenseSupplyPackingStateData>())
                 .Where(package => package != null)
                 .SelectMany(package => package.costs
                     ?? new List<OffenseSupplyPackingItemStateData>()))
        {
            if (cost != null)
            {
                RequireItemDefinition(cost.itemId, report);
            }
        }

        foreach (OffenseThrownEquipmentPersistenceState thrown in
                 source?.expedition?.activeBattle?.thrownEquipment
                 ?? new List<OffenseThrownEquipmentPersistenceState>())
        {
            if (thrown == null)
            {
                continue;
            }
            RequireReference(
                thrown.instanceId,
                physical.EquipmentInstanceIds,
                "Thrown equipment",
                report);
            RequireReference(
                thrown.ownerCharacterId,
                characterIds,
                $"Thrown equipment '{thrown.instanceId}' owner",
                report);
        }
    }

    private void ValidateCombat(
        DungeonCombatEquipmentSaveData source,
        ISet<string> characterIds,
        PhysicalReferenceIndex physical,
        ISet<string> buildingIds,
        DungeonResearchSaveData research,
        DungeonGameRestoreReport report)
    {
        foreach (CharacterCombatLoadoutState loadout in source?.loadouts
                     ?? new List<CharacterCombatLoadoutState>())
        {
            string characterId = loadout?.characterId ?? string.Empty;
            if (loadout == null || !characterIds.Contains(characterId))
            {
                report.AddError(
                    $"Combat loadout references missing character '{characterId}'.");
                continue;
            }

            foreach (CharacterCombatLoadoutProfile profile in loadout.profiles
                         ?? new List<CharacterCombatLoadoutProfile>())
            {
                if (profile == null)
                {
                    continue;
                }
                foreach (string equipmentId in profile.weaponInstanceIds
                             ?? new List<string>())
                {
                    RequireEquipmentReference<CombatWeaponSO>(
                        equipmentId,
                        physical,
                        $"Combat loadout '{characterId}' weapon",
                        report);
                }
                foreach (string equipmentId in profile.armorInstanceIds
                             ?? new List<string>())
                {
                    RequireEquipmentReference<CombatArmorSO>(
                        equipmentId,
                        physical,
                        $"Combat loadout '{characterId}' armor",
                        report);
                }
                if (!string.IsNullOrEmpty(profile.shieldInstanceId))
                {
                    RequireEquipmentReference<CombatShieldSO>(
                        profile.shieldInstanceId,
                        physical,
                        $"Combat loadout '{characterId}' shield",
                        report);
                }
            }
        }

        foreach (CombatEquipmentCraftMaterialPolicySaveData policy in
                 source?.craftMaterialPolicies
                 ?? new List<CombatEquipmentCraftMaterialPolicySaveData>())
        {
            if (policy != null)
            {
                RequireReference(
                    policy.facilityKey,
                    buildingIds,
                    "Combat material-policy facility",
                    report);
            }
        }

        HashSet<string> completedResearch = new HashSet<string>(
            research?.completedProjectIds ?? new List<string>(),
            StringComparer.Ordinal);
        foreach (CombatEquipmentCraftOrderSaveData order in source?.craftOrders
                     ?? new List<CombatEquipmentCraftOrderSaveData>())
        {
            if (order == null
                || CombatEquipmentCraftingRuntime.IsAmmunitionRecipe(
                    order.definitionId))
            {
                continue;
            }
            if (!combatEquipmentDefinitions.TryGet(
                    order.definitionId,
                    out CombatEquipmentDefinitionSO definition))
            {
                report.AddError(
                    $"Combat craft order '{order.orderId}' references unknown equipment definition '{order.definitionId}'.");
                continue;
            }
            if (!string.IsNullOrEmpty(definition.RequiredResearchId)
                && !completedResearch.Contains(definition.RequiredResearchId))
            {
                report.AddError(
                    $"Combat craft order '{order.orderId}' bypasses required research '{definition.RequiredResearchId}'.");
            }
        }

        foreach (EquipmentHistoryTransferOrder order in
                 source?.historyTransferOrders
                 ?? new List<EquipmentHistoryTransferOrder>())
        {
            if (order == null)
            {
                continue;
            }
            RequireReference(
                order.sourceEquipmentInstanceId,
                physical.EquipmentInstanceIds,
                $"History transfer '{order.orderId}' source equipment",
                report);
            RequireReference(
                order.targetEquipmentInstanceId,
                physical.EquipmentInstanceIds,
                $"History transfer '{order.orderId}' target equipment",
                report);
            RequireReference(
                order.lineageSealStackId,
                physical.StackIds,
                $"History transfer '{order.orderId}' lineage-seal stack",
                report);
            if (TryGetEquipmentDefinition(
                    order.sourceEquipmentInstanceId,
                    physical,
                    out CombatEquipmentDefinitionSO sourceDefinition)
                && TryGetEquipmentDefinition(
                    order.targetEquipmentInstanceId,
                    physical,
                    out CombatEquipmentDefinitionSO targetDefinition)
                && sourceDefinition.LineageKind != targetDefinition.LineageKind)
            {
                report.AddError(
                    $"History transfer '{order.orderId}' crosses equipment lineage kinds.");
            }
        }
    }

    private void RequireEquipmentReference<TDefinition>(
        string equipmentId,
        PhysicalReferenceIndex physical,
        string label,
        DungeonGameRestoreReport report)
        where TDefinition : CombatEquipmentDefinitionSO
    {
        if (!TryGetEquipmentDefinition(
                equipmentId,
                physical,
                out CombatEquipmentDefinitionSO definition)
            || definition is not TDefinition)
        {
            report.AddError(
                $"{label} references missing equipment '{equipmentId ?? string.Empty}' or the wrong equipment kind.");
        }
    }

    private bool TryGetEquipmentDefinition(
        string equipmentId,
        PhysicalReferenceIndex physical,
        out CombatEquipmentDefinitionSO definition)
    {
        definition = null;
        return !string.IsNullOrEmpty(equipmentId)
            && physical.EquipmentDefinitionIds.TryGetValue(
                equipmentId,
                out string definitionId)
            && combatEquipmentDefinitions.TryGet(definitionId, out definition);
    }

    private static void ValidateEvolution(
        EquipmentEvolutionSaveData source,
        ISet<string> equipmentIds,
        ISet<string> buildingIds,
        DungeonGameRestoreReport report)
    {
        foreach ((string orderId, string equipmentId, string buildingId) in
                 (source?.reforgeOrders ?? new List<EvolutionReforgeOrder>())
                 .Where(order => order != null)
                 .Select(order => (
                     order.orderId,
                     order.equipmentInstanceId,
                     order.facilityPersistentId))
                 .Concat((source?.reattunementOrders
                         ?? new List<EquipmentReattunementOrder>())
                     .Where(order => order != null)
                     .Select(order => (
                         order.orderId,
                         order.equipmentInstanceId,
                         order.facilityPersistentId))))
        {
            RequireReference(
                equipmentId,
                equipmentIds,
                $"Equipment evolution '{orderId}' equipment",
                report);
            RequireReference(
                buildingId,
                buildingIds,
                $"Equipment evolution '{orderId}' facility",
                report);
        }
    }

    private void ValidateMeta(
        DungeonMetaProgressionSaveData source,
        DungeonGameRestoreReport report)
    {
        foreach (int buildingId in source?.runProgress?.discoveredFacilityIds
                     ?? new List<int>())
        {
            try
            {
                buildingDefinitions.GetBuilding(buildingId);
            }
            catch (Exception)
            {
                report.AddError(
                    $"Meta progression references unknown discovered facility definition {buildingId}.");
            }
        }
    }

    private static void ValidateResearch(
        DungeonResearchSaveData source,
        ModularFacilityWorldSaveData buildings,
        DungeonOffenseAggregateSaveData offense,
        DungeonGameRestoreReport report)
    {
        HashSet<string> regionIds = (offense?.regions?.regions
                ?? new List<OffenseRegionState>())
            .Where(region => region != null)
            .Select(region => region.regionId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (KnowledgeResidueTaskSaveData task in source?.knowledgeTasks
                     ?? new List<KnowledgeResidueTaskSaveData>())
        {
            if (task == null)
            {
                continue;
            }
            if (task.facilityId != 0
                && !(buildings?.buildings
                    ?? new List<ModularFacilityBuildingSaveData>()).Any(building =>
                        building != null
                        && building.buildingId == task.facilityId
                        && building.centerX == task.facilityX
                        && building.centerY == task.facilityY))
            {
                report.AddError(
                    $"Knowledge task '{task.taskId}' references missing facility {task.facilityId} at ({task.facilityX},{task.facilityY}).");
            }
            if (task.use == KnowledgeResidueUse.RegionReconnaissance
                && !regionIds.Contains(task.regionId))
            {
                report.AddError(
                    $"Knowledge task '{task.taskId}' references missing offense region '{task.regionId}'.");
            }
        }
    }

    private void ValidateCropPlots(
        DungeonCropPlotSaveData source,
        BuildingReferenceIndex buildings,
        DungeonGameRestoreReport report)
    {
        foreach (CropPlotSaveData plot in source?.plots
                     ?? new List<CropPlotSaveData>())
        {
            if (plot == null)
            {
                report.AddError("Crop-plot aggregate contains a null plot.");
                continue;
            }
            RequireReference(
                plot.buildingInstanceId,
                buildings.InstanceIds,
                "Crop plot building",
                report);
            if (!buildings.InstanceDefinitions.TryGetValue(
                    plot.buildingInstanceId,
                    out BuildingSO building)
                || building?.GetAbility<BuildingCropPlotAbility>()
                    is not BuildingCropPlotAbility ability)
            {
                report.AddError(
                    $"Crop plot '{plot.buildingInstanceId}' is not backed by a crop-plot building definition.");
                continue;
            }
            if (!economyContent.TryGetCrop(
                    plot.cropId,
                    out CropDefinitionSO crop))
            {
                report.AddError(
                    $"Crop plot '{plot.buildingInstanceId}' references unknown crop '{plot.cropId}'.");
            }
            else if (ability.Indoor && !crop.IndoorAllowed)
            {
                report.AddError(
                    $"Crop '{plot.cropId}' is not allowed in indoor plot '{plot.buildingInstanceId}'.");
            }
        }
    }

    private static void ValidateTreasury(
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

    private HashSet<string> ValidateCharacterLife(
        CharacterLifeWorldSaveData source,
        ISet<string> characterIds,
        DungeonGameRestoreReport report)
    {
        HashSet<string> lifeIds = new(StringComparer.Ordinal);
        foreach (CharacterLifeRecordSaveData record in source?.characters
                     ?? new List<CharacterLifeRecordSaveData>())
        {
            if (record == null)
            {
                report.AddError("Character-life aggregate contains a null record.");
                continue;
            }

            RequireUniqueId(record.characterId, "character-life record", lifeIds, report);
            RequireReference(record.characterId, characterIds, "Character-life record", report);
            CharacterSpeciesId speciesId = new(record.phenotypeSpeciesId);
            if (!speciesId.IsValid)
            {
                report.AddError(
                    $"Character-life record '{record.characterId}' has invalid phenotype species '{record.phenotypeSpeciesId}'.");
            }
            else
            {
                try
                {
                    SpeciesLifeHistoryDefinition history =
                        lifeDefinitions.RequireLifeHistory(speciesId);
                    CharacterLifeStage expected = history.ResolveStage(
                        record.biologicalAgeDayUnits);
                    if (expected != record.lifeStage)
                    {
                        report.AddError(
                            $"Character-life record '{record.characterId}' has life stage {record.lifeStage}, expected {expected}.");
                    }
                }
                catch (Exception)
                {
                    report.AddError(
                        $"Character-life record '{record.characterId}' references unknown phenotype species '{record.phenotypeSpeciesId}'.");
                }
            }

            HashSet<string> conditionIds = new(StringComparer.Ordinal);
            foreach (CharacterAgeConditionSaveData condition in record.ageConditions
                         ?? new List<CharacterAgeConditionSaveData>())
            {
                if (condition == null
                    || string.IsNullOrWhiteSpace(condition.conditionId)
                    || !conditionIds.Add(condition.conditionId))
                {
                    report.AddError(
                        $"Character-life record '{record.characterId}' contains an invalid or duplicate aging condition.");
                }
            }
        }

        return lifeIds;
    }

    private static KinshipReferenceIndex ValidateKinshipHouseholds(
        KinshipHouseholdWorldSaveData source,
        ISet<string> livingCharacterIds,
        ISet<string> buildingIds,
        DungeonGameRestoreReport report)
    {
        KinshipReferenceIndex result = new(livingCharacterIds);
        HashSet<string> tombstoneIds = result.TombstoneIds;
        foreach (CharacterTombstoneSaveData tombstone in source?.kinship?.tombstones
                     ?? new List<CharacterTombstoneSaveData>())
        {
            if (tombstone == null)
            {
                report.AddError("Kinship aggregate contains a null tombstone.");
                continue;
            }

            RequireUniqueId(tombstone.characterId, "tombstone", tombstoneIds, report);
            if (livingCharacterIds.Contains(tombstone.characterId))
            {
                report.AddError(
                    $"Character '{tombstone.characterId}' is both living and archived as a tombstone.");
            }
            if (!new CharacterSpeciesId(tombstone.phenotypeSpeciesId).IsValid)
            {
                report.AddError(
                    $"Tombstone '{tombstone.characterId}' has invalid phenotype species '{tombstone.phenotypeSpeciesId}'.");
            }
            if (tombstone.deathAbsoluteDay < 1
                || tombstone.deathAbsoluteDay < tombstone.birthAbsoluteDay
                || tombstone.generation < 0
                || !string.IsNullOrWhiteSpace(tombstone.householdId)
                && !new HouseholdId(tombstone.householdId).IsValid)
            {
                report.AddError(
                    $"Tombstone '{tombstone.characterId}' has invalid temporal, household, or generation data.");
            }
        }

        result.AllCharacterIds.UnionWith(tombstoneIds);
        HashSet<string> linkKeys = new(StringComparer.Ordinal);
        foreach (CharacterKinshipLinkSaveData link in source?.kinship?.links
                     ?? new List<CharacterKinshipLinkSaveData>())
        {
            if (link == null)
            {
                report.AddError("Kinship aggregate contains a null relationship link.");
                continue;
            }
            RequireReference(link.sourceCharacterId, result.AllCharacterIds, "Kinship source", report);
            RequireReference(link.targetCharacterId, result.AllCharacterIds, "Kinship target", report);
            string key = $"{(int)link.kind}:{link.sourceCharacterId}:{link.targetCharacterId}";
            if (!linkKeys.Add(key))
            {
                report.AddError($"Kinship aggregate contains duplicate relationship '{key}'.");
            }
        }

        HashSet<string> assignedCharacters = new(StringComparer.Ordinal);
        HashSet<string> assignedBeds = new(StringComparer.Ordinal);
        foreach (CharacterRoomAssignmentSaveData assignment in source?.households?.assignments
                     ?? new List<CharacterRoomAssignmentSaveData>())
        {
            if (assignment == null)
            {
                report.AddError("Household aggregate contains a null room assignment.");
                continue;
            }
            RequireReference(assignment.characterId, livingCharacterIds, "Household character", report);
            RequireReference(assignment.roomBuildingId, buildingIds, "Household room", report);
            RequireReference(assignment.bedBuildingId, buildingIds, "Household bed", report);
            if (!new HouseholdId(assignment.householdId).IsValid)
            {
                report.AddError(
                    $"Household assignment for '{assignment.characterId}' has invalid household ID '{assignment.householdId}'.");
            }
            if (!assignedCharacters.Add(assignment.characterId))
            {
                report.AddError(
                    $"Character '{assignment.characterId}' has multiple room assignments.");
            }
            if (!assignedBeds.Add(assignment.bedBuildingId))
            {
                report.AddError(
                    $"Bed '{assignment.bedBuildingId}' is assigned to multiple characters.");
            }
            result.HouseholdIds.Add(assignment.householdId);
        }

        foreach (LineageSummarySaveData summary in source?.kinship?.lineageSummaries
                     ?? new List<LineageSummarySaveData>())
        {
            if (summary == null || !new HouseholdId(summary.householdId).IsValid)
            {
                report.AddError("Kinship aggregate contains an invalid lineage summary.");
            }
            else
            {
                result.HouseholdIds.Add(summary.householdId);
            }
        }

        return result;
    }

    private void ValidateReproduction(
        ReproductionWorldSaveData source,
        ISet<string> livingCharacterIds,
        ISet<string> knownCharacterIds,
        DungeonGameRestoreReport report)
    {
        HashSet<string> processIds = new(StringComparer.Ordinal);
        foreach (ReproductionProcessSaveData process in source?.processes
                     ?? new List<ReproductionProcessSaveData>())
        {
            if (process == null)
            {
                report.AddError("Reproduction aggregate contains a null process.");
                continue;
            }
            RequireUniqueId(process.processId, "reproduction process", processIds, report);
            RequireReference(process.firstParentId, knownCharacterIds, "Reproduction first parent", report);
            if (process.mode != ReproductionMode.GolemAssembly)
            {
                RequireReference(process.secondParentId, knownCharacterIds, "Reproduction second parent", report);
            }
            if (!string.IsNullOrWhiteSpace(process.carrierId))
            {
                RequireReference(process.carrierId, knownCharacterIds, "Reproduction carrier", report);
                if (process.status is ReproductionProcessStatus.Active
                        or ReproductionProcessStatus.WaitingForEnvironment
                        or ReproductionProcessStatus.WaitingForEmergencyExtraction
                    && process.carrierDeathAbsoluteDay <= 0
                    && !livingCharacterIds.Contains(process.carrierId))
                {
                    report.AddError(
                        $"Active reproduction carrier '{process.carrierId}' is not a living character.");
                }
            }
            if (process.resultPublished)
            {
                RequireReference(
                    process.resultCharacterId,
                    livingCharacterIds,
                    "Published reproduction result",
                    report);
            }
            else if (!string.IsNullOrWhiteSpace(process.resultCharacterId))
            {
                report.AddError(
                    $"Unpublished reproduction process '{process.processId}' contains a result character ID.");
            }
            CharacterSpeciesId speciesId = new(process.phenotypeSpeciesId);
            try
            {
                if (!speciesId.IsValid)
                    throw new InvalidOperationException();
                lifeDefinitions.RequireLifeHistory(speciesId);
            }
            catch (Exception)
            {
                report.AddError(
                    $"Reproduction process '{process.processId}' references unknown phenotype species '{process.phenotypeSpeciesId}'.");
            }
        }
    }

    private void ValidatePopulationHealth(
        PopulationHealthWorldSaveData source,
        ISet<string> livingCharacterIds,
        DungeonGameRestoreReport report)
    {
        HashSet<string> recordIds = new(StringComparer.Ordinal);
        foreach (CharacterPopulationHealthSaveData health in source?.characters
                     ?? new List<CharacterPopulationHealthSaveData>())
        {
            if (health == null)
            {
                report.AddError("Population-health aggregate contains a null character record.");
                continue;
            }
            RequireUniqueId(health.characterId, "population-health character", recordIds, report);
            RequireReference(health.characterId, livingCharacterIds, "Population-health character", report);
            foreach (string diseaseId in (health.immunity ?? new List<DiseaseImmunitySaveData>())
                         .Where(value => value != null).Select(value => value.diseaseId)
                         .Concat((health.activeDiseases ?? new List<ActiveDiseaseSaveData>())
                             .Where(value => value != null).Select(value => value.diseaseId)))
            {
                RequireDiseaseDefinition(diseaseId, report);
            }
        }
        foreach (DiseaseExposureSaveData exposure in source?.pendingExposures
                     ?? new List<DiseaseExposureSaveData>())
        {
            if (exposure == null)
            {
                report.AddError("Population-health aggregate contains a null exposure.");
                continue;
            }
            RequireReference(exposure.characterId, livingCharacterIds, "Disease exposure character", report);
            RequireDiseaseDefinition(exposure.diseaseId, report);
        }
        foreach (EpidemicStateSaveData epidemic in source?.epidemics
                     ?? new List<EpidemicStateSaveData>())
        {
            if (epidemic == null)
            {
                report.AddError("Population-health aggregate contains a null epidemic state.");
                continue;
            }
            RequireDiseaseDefinition(epidemic.diseaseId, report);
        }
    }

    private static void ValidateCareers(
        CharacterCareerWorldSaveData source,
        ISet<string> livingCharacterIds,
        ISet<string> buildingIds,
        DungeonGameRestoreReport report)
    {
        HashSet<string> careerIds = new(StringComparer.Ordinal);
        foreach (CharacterCareerSaveData career in source?.characters
                     ?? new List<CharacterCareerSaveData>())
        {
            if (career == null)
            {
                report.AddError("Career aggregate contains a null character record.");
                continue;
            }
            RequireUniqueId(career.characterId, "career character", careerIds, report);
            RequireReference(career.characterId, livingCharacterIds, "Career character", report);
            if (career.retiredWorkAbsoluteDay < 0
                || career.retiredWorkSeconds < 0f
                || float.IsNaN(career.retiredWorkSeconds)
                || float.IsInfinity(career.retiredWorkSeconds)
                || career.retiredWorkSeconds
                    > CareerRules.RetireeMaximumSafeWorkSeconds + 0.001f)
            {
                report.AddError(
                    $"Career character '{career.characterId}' has invalid retiree work time.");
            }
        }
        HashSet<string> mentoredStudents = new(StringComparer.Ordinal);
        foreach (CareerMentorshipSaveData mentorship in source?.mentorships
                     ?? new List<CareerMentorshipSaveData>())
        {
            if (mentorship == null)
            {
                report.AddError("Career aggregate contains a null mentorship.");
                continue;
            }
            RequireReference(
                mentorship.mentorCharacterId,
                livingCharacterIds,
                "Mentorship mentor",
                report);
            RequireReference(
                mentorship.studentCharacterId,
                livingCharacterIds,
                "Mentorship student",
                report);
            RequireReference(
                mentorship.academyBuildingId,
                buildingIds,
                "Mentorship academy",
                report);
            if (!mentoredStudents.Add(mentorship.studentCharacterId)
                || string.Equals(
                    mentorship.mentorCharacterId,
                    mentorship.studentCharacterId,
                    StringComparison.Ordinal)
                || mentorship.lastAwardAbsoluteDay < 0)
            {
                report.AddError(
                    $"Mentorship for '{mentorship.studentCharacterId}' is invalid or duplicated.");
            }
        }
    }

    private static void ValidatePsychosocial(
        CharacterPsychosocialWorldSaveData source,
        ISet<string> livingCharacterIds,
        ISet<string> knownCharacterIds,
        DungeonGameRestoreReport report)
    {
        HashSet<string> recordIds = new(StringComparer.Ordinal);
        foreach (CharacterPsychosocialRecordSaveData record in source?.characters
                     ?? new List<CharacterPsychosocialRecordSaveData>())
        {
            if (record == null)
            {
                report.AddError("Psychosocial aggregate contains a null character record.");
                continue;
            }
            RequireUniqueId(record.characterId, "psychosocial character", recordIds, report);
            RequireReference(record.characterId, livingCharacterIds, "Psychosocial character", report);
            if (record.lastLongNightMemorialYear < 0)
            {
                report.AddError(
                    $"Psychosocial record '{record.characterId}' has an invalid long-night year.");
            }
            HashSet<string> festivals = new(StringComparer.Ordinal);
            foreach (FestivalAttendanceSaveData attendance in record.festivalAttendance
                         ?? new List<FestivalAttendanceSaveData>())
            {
                if (attendance == null
                    || string.IsNullOrWhiteSpace(attendance.festivalId)
                    || attendance.year < 1
                    || !festivals.Add(attendance.festivalId))
                {
                    report.AddError(
                        $"Psychosocial record '{record.characterId}' has invalid festival attendance.");
                }
            }
            foreach (GriefIncidentSaveData incident in record.grief
                         ?? new List<GriefIncidentSaveData>())
            {
                if (incident == null)
                {
                    report.AddError(
                        $"Psychosocial record '{record.characterId}' contains a null grief incident.");
                    continue;
                }
                RequireReference(
                    incident.deceasedCharacterId,
                    knownCharacterIds,
                    "Grief deceased character",
                    report);
            }
        }
    }

    private void ValidateCropEcology(
        CropEcologyWorldSaveData source,
        DungeonCropPlotSaveData cropPlots,
        ISet<string> buildingIds,
        IReadOnlyList<(string itemDefinitionId, SeedLotState state)> physicalSeedLots,
        DungeonGameRestoreReport report)
    {
        HashSet<string> cropPlotIds = (cropPlots?.plots ?? new List<CropPlotSaveData>())
            .Where(value => value != null)
            .Select(value => value.buildingInstanceId)
            .ToHashSet(StringComparer.Ordinal);
        Dictionary<string, string> genomes = new(StringComparer.Ordinal);
        foreach (CultivarGenomeSaveData genome in (source?.activeCultivars
                     ?? new List<CultivarGenomeSaveData>())
                 .Concat(source?.frozenCultivars ?? new List<CultivarGenomeSaveData>()))
        {
            if (genome == null || string.IsNullOrWhiteSpace(genome.genomeId)
                || !genomes.TryAdd(genome.genomeId, genome.cropId))
            {
                report.AddError("Crop-ecology aggregate contains an invalid or duplicate cultivar genome.");
                continue;
            }
            RequireCropDefinition(genome.cropId, $"Cultivar genome '{genome.genomeId}'", report);
        }
        foreach (CropEcologyPlotSaveData plot in source?.plots
                     ?? new List<CropEcologyPlotSaveData>())
        {
            if (plot == null)
            {
                report.AddError("Crop-ecology aggregate contains a null plot.");
                continue;
            }
            RequireReference(plot.plotId, buildingIds, "Crop-ecology plot building", report);
            if (!cropPlotIds.Contains(plot.plotId))
            {
                report.AddError(
                    $"Crop-ecology plot '{plot.plotId}' has no matching crop-plot aggregate record.");
            }
            if (!string.IsNullOrWhiteSpace(plot.cropId))
                RequireCropDefinition(plot.cropId, $"Crop-ecology plot '{plot.plotId}'", report);
            if (!string.IsNullOrWhiteSpace(plot.cultivarGenomeId)
                && (!genomes.TryGetValue(plot.cultivarGenomeId, out string genomeCropId)
                    || !string.Equals(genomeCropId, plot.cropId, StringComparison.Ordinal)))
            {
                report.AddError(
                    $"Crop-ecology plot '{plot.plotId}' references missing or mismatched genome '{plot.cultivarGenomeId}'.");
            }
        }
        foreach ((string itemDefinitionId, SeedLotState seedLot) in physicalSeedLots)
        {
            RequireCropDefinition(seedLot.cropId, $"Physical seed lot '{itemDefinitionId}'", report);
            if (!genomes.TryGetValue(seedLot.cultivarGenomeId, out string genomeCropId)
                || !string.Equals(genomeCropId, seedLot.cropId, StringComparison.Ordinal))
            {
                report.AddError(
                    $"Physical seed lot '{itemDefinitionId}' references missing or mismatched genome '{seedLot.cultivarGenomeId}'.");
            }
        }
    }

    private void RequireCropDefinition(
        string cropId,
        string label,
        DungeonGameRestoreReport report)
    {
        if (!economyContent.TryGetCrop(cropId?.Trim() ?? string.Empty, out _))
            report.AddError($"{label} references unknown crop '{cropId ?? string.Empty}'.");
    }

    private void RequireDiseaseDefinition(
        string diseaseId,
        DungeonGameRestoreReport report)
    {
        try
        {
            diseaseDefinitions.Require(diseaseId);
        }
        catch (Exception)
        {
            report.AddError(
                $"Save references unknown disease definition '{diseaseId ?? string.Empty}'.");
        }
    }

    private static void RequireReference(
        string id,
        ISet<string> existing,
        string label,
        DungeonGameRestoreReport report)
    {
        if (string.IsNullOrEmpty(id) || !existing.Contains(id))
        {
            report.AddError($"{label} references missing ID '{id ?? string.Empty}'.");
        }
    }

    private void RequireItemDefinition(
        string rawId,
        DungeonGameRestoreReport report)
    {
        ItemDefinitionId id = (ItemDefinitionId)(rawId?.Trim() ?? string.Empty);
        if (!id.IsValid || !itemDefinitions.TryGet(id, out _))
        {
            report.AddError($"Save references unknown item definition '{rawId ?? string.Empty}'.");
        }
    }

    private static void RequireUniqueId(
        string rawId,
        string kind,
        ISet<string> ids,
        DungeonGameRestoreReport report)
    {
        string id = rawId?.Trim() ?? string.Empty;
        if (id.Length == 0)
        {
            report.AddError($"Save contains a {kind} without a persistent ID.");
        }
        else if (!ids.Add(id))
        {
            report.AddError($"Save contains duplicate {kind} ID '{id}'.");
        }
    }

    private static void RequireUniqueCharacterId(
        string rawId,
        string kind,
        ISet<string> ids,
        DungeonGameRestoreReport report,
        bool rejectDuplicate)
    {
        if (!CharacterV18RestoreIdentityResolver.TryResolve(
                rawId,
                allowLegacyCharacterIds: false,
                out CharacterId characterId,
                out _)
            || !string.Equals(
                rawId,
                characterId.Value,
                StringComparison.Ordinal))
        {
            report.AddError(
                $"Save contains a {kind} without an exact canonical persistent ID: "
                + $"'{rawId ?? "<null>"}'.");
            return;
        }

        if (!ids.Add(characterId.Value) && rejectDuplicate)
        {
            report.AddError(
                $"Save contains duplicate {kind} ID '{characterId.Value}'.");
        }
    }

    private sealed class PhysicalReferenceIndex
    {
        internal HashSet<string> StackIds { get; } =
            new HashSet<string>(StringComparer.Ordinal);
        internal HashSet<string> ItemInstanceIds { get; } =
            new HashSet<string>(StringComparer.Ordinal);
        internal HashSet<string> UniqueItemInstanceIds { get; } =
            new HashSet<string>(StringComparer.Ordinal);
        internal HashSet<string> EquipmentInstanceIds { get; } =
            new HashSet<string>(StringComparer.Ordinal);
        internal Dictionary<string, string> EquipmentDefinitionIds { get; } =
            new Dictionary<string, string>(StringComparer.Ordinal);
        internal List<(string itemDefinitionId, SeedLotState state)> SeedLots { get; } = new();
    }

    private sealed class BuildingReferenceIndex
    {
        internal HashSet<string> InstanceIds { get; } =
            new HashSet<string>(StringComparer.Ordinal);
        internal Dictionary<string, BuildingSO> InstanceDefinitions { get; } =
            new Dictionary<string, BuildingSO>(StringComparer.Ordinal);
    }

    private sealed class KinshipReferenceIndex
    {
        internal KinshipReferenceIndex(IEnumerable<string> livingCharacterIds)
        {
            AllCharacterIds.UnionWith(livingCharacterIds ?? Array.Empty<string>());
        }

        internal HashSet<string> TombstoneIds { get; } =
            new(StringComparer.Ordinal);
        internal HashSet<string> AllCharacterIds { get; } =
            new(StringComparer.Ordinal);
        internal HashSet<string> HouseholdIds { get; } =
            new(StringComparer.Ordinal);
    }
}
