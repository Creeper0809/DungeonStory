using System;
using System.Collections.Generic;
using DungeonStory.Factions;
using UnityEngine;

/// <summary>
/// Explicit early-V18 compatibility map. Every rewritten field is a known
/// character reference in its owning save contract; generic names such as
/// persistentId or targetId are never discovered by reflection.
/// </summary>
public static class V18TypedCharacterReferenceRestoreNormalizer
{
    internal static string RewriteRecognizedCharacterReference(
        string value,
        Func<string, string, string> rewrite,
        string path)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Invalid union entity ID at '{path}': '{value}'.");
        }

        if (CharacterId.TryCanonicalizeV18Restore(
                value,
                out _,
                out _))
        {
            // Union fields can point at operational characters whose early-V18
            // IDs use domain prefixes such as invasion:. The restored actor is
            // registered under the canonical character: form, so every value
            // recognized by the CharacterId contract must follow the same
            // rewrite path. Non-character runtime IDs remain unrecognized and
            // are preserved below.
            return rewrite(value, path);
        }

        if (value.StartsWith("character:", StringComparison.Ordinal))
        {
            return rewrite(value, path);
        }

        return value;
    }

    internal static string PreserveRuntimeReference(
        string value,
        string path)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Invalid runtime entity ID at '{path}': '{value}'.");
        }

        return value;
    }

    public static string RewriteLegacyReference(
        string value,
        DungeonGameRestoreReport report,
        string sectionId,
        string path)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        if (!CharacterId.TryCanonicalizeV18Restore(
                value,
                out CharacterId canonical,
                out bool wasLegacy))
        {
            throw new InvalidOperationException(
                $"Invalid CharacterId in '{sectionId}' at '{path}': "
                + $"'{value}'.");
        }

        if (!wasLegacy)
        {
            return canonical.Value;
        }

        report?.AddWarning(
            $"V18 legacy CharacterId normalized in '{sectionId}' at "
            + $"'{path}': '{value}' -> '{canonical.Value}'.");
        return canonical.Value;
    }

    public static void Normalize(
        DungeonCombatEquipmentSaveData payload,
        Func<string, string, string> rewrite) =>
        V18CombatOffenseCharacterReferenceRestoreNormalizer.Normalize(
            payload,
            rewrite);

    public static void Normalize(
        CharacterCombatCommandSaveData payload,
        Func<string, string, string> rewrite) =>
        V18CombatOffenseCharacterReferenceRestoreNormalizer.Normalize(
            payload,
            rewrite);

    public static void Normalize(
        DefenseTacticalCoordinatorSaveData payload,
        Func<string, string, string> rewrite) =>
        V18CombatOffenseCharacterReferenceRestoreNormalizer.Normalize(
            payload,
            rewrite);

    public static void Normalize(
        DungeonSurgerySaveData payload,
        Func<string, string, string> rewrite) =>
        V18CombatOffenseCharacterReferenceRestoreNormalizer.Normalize(
            payload,
            rewrite);

    public static void Normalize(
        DungeonExteriorActivitySaveData payload,
        Func<string, string, string> rewrite) =>
        V18CombatOffenseCharacterReferenceRestoreNormalizer.Normalize(
            payload,
            rewrite);

    public static void Normalize(
        DungeonRegularCustomerSaveData payload,
        Func<string, string, string> rewrite) =>
        V18CombatOffenseCharacterReferenceRestoreNormalizer.Normalize(
            payload,
            rewrite);

    public static void Normalize(
        DungeonFactionSaveData payload,
        Func<string, string, string> rewrite) =>
        V18CombatOffenseCharacterReferenceRestoreNormalizer.Normalize(
            payload,
            rewrite);

    public static void Normalize(
        DungeonInvasionSaveData payload,
        Func<string, string, string> rewrite) =>
        V18CombatOffenseCharacterReferenceRestoreNormalizer.Normalize(
            payload,
            rewrite);

    public static void Normalize(
        DungeonOffenseAggregateSaveData payload,
        Func<string, string, string> rewrite) =>
        V18CombatOffenseCharacterReferenceRestoreNormalizer.Normalize(
            payload,
            rewrite);
}

public static class V18WorkProductionCharacterReferenceRestoreNormalizer
{
    public static void Normalize(
        DungeonWorkOrderSaveData payload,
        Func<string, string, string> rewrite)
    {
        if (payload?.orders == null)
        {
            return;
        }

        for (int index = 0; index < payload.orders.Count; index++)
        {
            WorkOrderSaveData order = payload.orders[index];
            if (order != null)
            {
                order.reservedWorkerPersistentId = rewrite(
                    order.reservedWorkerPersistentId,
                    $"orders[{index}].reservedWorkerPersistentId");
            }
        }
    }

    public static void Normalize(
        DungeonProductionBillSaveData payload,
        Func<string, string, string> rewrite)
    {
        if (payload?.bills == null)
        {
            return;
        }

        for (int index = 0; index < payload.bills.Count; index++)
        {
            ProductionBillSaveData bill = payload.bills[index];
            if (bill == null)
            {
                continue;
            }

            string prefix = $"bills[{index}]";
            bill.reservedWorkerId = rewrite(
                bill.reservedWorkerId,
                prefix + ".reservedWorkerId");
            RewriteList(
                bill.allowedWorkerIds,
                rewrite,
                prefix + ".allowedWorkerIds");
        }
    }

    public static void Normalize(
        ServiceRoomsSaveData payload,
        Func<string, string, string> rewrite)
    {
        if (payload?.sessions == null)
        {
            return;
        }

        for (int index = 0; index < payload.sessions.Count; index++)
        {
            ServiceSessionSaveData session = payload.sessions[index];
            if (session != null)
            {
                session.actorId = rewrite(
                    session.actorId,
                    $"sessions[{index}].actorId");
            }
        }
    }

    private static void RewriteList(
        IList<string> values,
        Func<string, string, string> rewrite,
        string path)
    {
        if (values == null)
        {
            return;
        }

        bool changed = false;
        for (int index = 0; index < values.Count; index++)
        {
            string previous = values[index];
            values[index] = rewrite(previous, $"{path}[{index}]");
            changed |= !string.Equals(
                previous,
                values[index],
                StringComparison.Ordinal);
        }
        if (changed && values is List<string> list)
        {
            list.Sort(StringComparer.Ordinal);
        }
    }
}

public static class V18SurvivalEnvironmentCharacterReferenceRestoreNormalizer
{
    public static void Normalize(
        DungeonCharacterEnvironmentSaveData payload,
        Func<string, string, string> rewrite)
    {
        bool exposuresChanged = false;
        if (payload?.exposures != null)
        {
            for (int index = 0; index < payload.exposures.Length; index++)
            {
                CharacterEnvironmentExposure exposure = payload.exposures[index];
                if (exposure != null)
                {
                    string previous = exposure.characterId;
                    exposure.characterId = rewrite(
                        previous,
                        $"exposures[{index}].characterId");
                    exposuresChanged |= !string.Equals(
                        previous,
                        exposure.characterId,
                        StringComparison.Ordinal);
                }
            }

            if (exposuresChanged)
            {
                Array.Sort(
                    payload.exposures,
                    (left, right) => string.CompareOrdinal(
                        left?.characterId,
                        right?.characterId));
            }
        }

        if (payload?.equippedWorkwear == null)
        {
            return;
        }

        bool workwearChanged = false;
        for (int index = 0; index < payload.equippedWorkwear.Length; index++)
        {
            EnvironmentalWorkwearSaveData workwear =
                payload.equippedWorkwear[index];
            if (workwear != null)
            {
                string previous = workwear.characterId;
                workwear.characterId = rewrite(
                    previous,
                    $"equippedWorkwear[{index}].characterId");
                workwearChanged |= !string.Equals(
                    previous,
                    workwear.characterId,
                    StringComparison.Ordinal);
            }
        }

        if (workwearChanged)
        {
            Array.Sort(
                payload.equippedWorkwear,
                (left, right) => string.CompareOrdinal(
                    left?.characterId,
                    right?.characterId));
        }
    }

    public static void Normalize(
        CharacterSpeciesRuntimeSaveData payload,
        Func<string, string, string> rewrite)
    {
        if (payload?.characters == null)
        {
            return;
        }

        bool changed = false;
        for (int index = 0; index < payload.characters.Count; index++)
        {
            CharacterSpeciesRuntimeRecordSaveData record =
                payload.characters[index];
            if (record != null)
            {
                string previous = record.characterInstanceId;
                record.characterInstanceId = rewrite(
                    previous,
                    $"characters[{index}].characterInstanceId");
                changed |= !string.Equals(
                    previous,
                    record.characterInstanceId,
                    StringComparison.Ordinal);
            }
        }

        if (changed)
        {
            payload.characters.Sort((left, right) => string.CompareOrdinal(
                left?.characterInstanceId,
                right?.characterInstanceId));
        }
    }

    public static void Normalize(
        DungeonSurvivalSaveData payload,
        Func<string, string, string> rewrite)
    {
        if (payload?.health != null)
        {
            bool healthChanged = false;
            for (int index = 0; index < payload.health.Count; index++)
            {
                SurvivalHealthSaveData state = payload.health[index];
                if (state != null)
                {
                    string previous = state.persistentId;
                    state.persistentId = rewrite(
                        previous,
                        $"health[{index}].persistentId");
                    healthChanged |= !string.Equals(
                        previous,
                        state.persistentId,
                        StringComparison.Ordinal);
                }
            }
            if (healthChanged)
            {
                payload.health.Sort((left, right) => string.CompareOrdinal(
                    left?.persistentId,
                    right?.persistentId));
            }
        }

        if (payload?.mealLedger == null)
        {
            return;
        }

        for (int index = 0; index < payload.mealLedger.Count; index++)
        {
            CharacterMealLedgerSaveData meal = payload.mealLedger[index];
            if (meal != null)
            {
                string previousCharacterId = meal.characterId;
                string normalizedCharacterId = rewrite(
                    previousCharacterId,
                    $"mealLedger[{index}].characterId");
                if (!string.Equals(
                        previousCharacterId,
                        normalizedCharacterId,
                        StringComparison.Ordinal))
                {
                    string previousPrefix =
                        $"meal:{meal.day}:{previousCharacterId}:";
                    if (meal.mealId != null
                        && meal.mealId.StartsWith(
                            previousPrefix,
                            StringComparison.Ordinal))
                    {
                        meal.mealId = $"meal:{meal.day}:{normalizedCharacterId}:"
                            + meal.mealId.Substring(previousPrefix.Length);
                    }
                }

                meal.characterId = normalizedCharacterId;
            }
        }
    }

    public static void Normalize(
        DungeonDarkSurvivalSaveData payload,
        Func<string, string, string> rewrite)
    {
        if (payload?.characters != null)
        {
            bool charactersChanged = false;
            for (int index = 0; index < payload.characters.Count; index++)
            {
                CharacterDeprivationState state = payload.characters[index];
                if (state != null)
                {
                    string previous = state.characterId;
                    state.characterId = rewrite(
                        previous,
                        $"characters[{index}].characterId");
                    charactersChanged |= !string.Equals(
                        previous,
                        state.characterId,
                        StringComparison.Ordinal);
                }
            }
            if (charactersChanged)
            {
                payload.characters.Sort((left, right) => string.CompareOrdinal(
                    left?.characterId,
                    right?.characterId));
            }
        }

        if (payload?.filth == null)
        {
            return;
        }

        for (int index = 0; index < payload.filth.Count; index++)
        {
            WorldFilthSaveData filth = payload.filth[index];
            if (filth != null)
            {
                filth.sourceCharacterId = rewrite(
                    filth.sourceCharacterId,
                    $"filth[{index}].sourceCharacterId");
            }
        }
    }

    public static void Normalize(
        DungeonWildlifeSaveData payload,
        Func<string, string, string> rewrite)
    {
        if (payload?.wildlife == null)
        {
            return;
        }

        for (int index = 0; index < payload.wildlife.Count; index++)
        {
            WildlifeSaveData animal = payload.wildlife[index];
            if (animal != null)
            {
                animal.reservedByPersistentId = rewrite(
                    animal.reservedByPersistentId,
                    $"wildlife[{index}].reservedByPersistentId");
            }
        }
    }

    public static void Normalize(
        DungeonStaffDiscontentSaveData payload,
        Func<string, string, string> rewrite)
    {
        if (payload?.records == null)
        {
            return;
        }

        bool changed = false;
        for (int index = 0; index < payload.records.Count; index++)
        {
            DungeonStaffDiscontentRecordSaveData record = payload.records[index];
            if (record != null)
            {
                string previous = record.staffId;
                record.staffId = rewrite(
                    previous,
                    $"records[{index}].staffId");
                changed |= !string.Equals(
                    previous,
                    record.staffId,
                    StringComparison.Ordinal);
            }
        }
        if (changed)
        {
            payload.records.Sort((left, right) => string.CompareOrdinal(
                left?.staffId,
                right?.staffId));
        }
    }

    private static void RewriteCharacterIds<T>(
        IList<T> values,
        Func<T, string> get,
        Action<T, string> set,
        Func<string, string, string> rewrite,
        string path)
        where T : class
    {
        if (values == null)
        {
            return;
        }

        bool changed = false;
        for (int index = 0; index < values.Count; index++)
        {
            T value = values[index];
            if (value != null)
            {
                string previous = get(value);
                string normalized = rewrite(
                    previous,
                    $"{path}[{index}].characterId");
                set(value, normalized);
                changed |= !string.Equals(
                    previous,
                    normalized,
                    StringComparison.Ordinal);
            }
        }
        if (changed && values is List<T> list)
        {
            list.Sort((left, right) => string.CompareOrdinal(
                left == null ? null : get(left),
                right == null ? null : get(right)));
        }
    }

    private static void RewriteList(
        IList<string> values,
        Func<string, string, string> rewrite,
        string path)
    {
        if (values == null)
        {
            return;
        }

        bool changed = false;
        for (int index = 0; index < values.Count; index++)
        {
            string previous = values[index];
            values[index] = rewrite(previous, $"{path}[{index}]");
            changed |= !string.Equals(
                previous,
                values[index],
                StringComparison.Ordinal);
        }
        if (changed && values is List<string> list)
        {
            list.Sort(StringComparer.Ordinal);
        }
    }
}

public static class V18WorldEconomyCharacterReferenceRestoreNormalizer
{
    public static void Normalize(
        TreasuryEconomySaveData payload,
        Func<string, string, string> rewrite)
    {
        if (payload?.employment?.wageStates != null)
        {
            for (int index = 0;
                 index < payload.employment.wageStates.Count;
                 index++)
            {
                EmployeeWageState wage = payload.employment.wageStates[index];
                if (wage != null)
                {
                    wage.characterId = rewrite(
                        wage.characterId,
                        $"employment.wageStates[{index}].characterId");
                }
            }
        }

        if (payload?.employment?.mercenaryContracts == null)
        {
            return;
        }

        for (int index = 0;
             index < payload.employment.mercenaryContracts.Count;
             index++)
        {
            MercenaryContract contract =
                payload.employment.mercenaryContracts[index];
            if (contract != null)
            {
                contract.characterId = rewrite(
                    contract.characterId,
                    $"employment.mercenaryContracts[{index}].characterId");
            }
        }
    }

    public static void Normalize(
        DungeonCharacterWorldSaveData payload,
        Func<string, string, string> rewrite)
    {
        if (payload?.actors != null)
        {
            for (int index = 0; index < payload.actors.Count; index++)
            {
                DungeonCharacterSaveData actor = payload.actors[index];
                if (actor == null)
                {
                    continue;
                }

                string prefix = $"actors[{index}]";
                V18CharacterReferenceComponentRestoreNormalizer.NormalizeSocialMemory(
                    actor.socialMemory,
                    rewrite,
                    prefix + ".socialMemory");
                V18CharacterReferenceComponentRestoreNormalizer.NormalizeCarryInventory(
                    actor.carryInventory,
                    rewrite,
                    prefix + ".carryInventory");
            }
        }

        if (payload?.populationProfiles == null)
        {
            return;
        }

        for (int index = 0; index < payload.populationProfiles.Count; index++)
        {
            WorldCharacterProfile profile = payload.populationProfiles[index];
            if (profile != null)
            {
                V18CharacterReferenceComponentRestoreNormalizer.NormalizeSocialMemory(
                    profile.socialMemory,
                    rewrite,
                    $"populationProfiles[{index}].socialMemory");
            }
        }
    }

    public static void Normalize(
        DungeonPhysicalItemSaveData payload,
        Func<string, string, string> rewrite)
    {
        if (payload?.stacks != null)
        {
            for (int stackIndex = 0;
                 stackIndex < payload.stacks.Count;
                 stackIndex++)
            {
                WorldItemStackSaveData stack = payload.stacks[stackIndex];
                if (stack == null)
                {
                    continue;
                }

                string prefix = $"stacks[{stackIndex}]";
                stack.sourceCharacterId = rewrite(
                    stack.sourceCharacterId,
                    prefix + ".sourceCharacterId");
                V18CharacterReferenceComponentRestoreNormalizer.NormalizeProvenanceComponents(
                    stack.components,
                    rewrite,
                    prefix + ".components");
            }
        }

        if (payload?.uniqueItems == null)
        {
            return;
        }

        for (int itemIndex = 0;
             itemIndex < payload.uniqueItems.Count;
             itemIndex++)
        {
            UniqueItemInstanceSaveData item = payload.uniqueItems[itemIndex];
            if (item?.components == null)
            {
                continue;
            }

            string prefix = $"uniqueItems[{itemIndex}].components";
            V18CharacterReferenceComponentRestoreNormalizer
                .NormalizeProvenanceComponents(item.components, rewrite, prefix);
            for (int componentIndex = 0;
                 componentIndex < item.components.Count;
                 componentIndex++)
            {
                ItemInstanceComponentSaveData component =
                    item.components[componentIndex];
                if (component == null
                    || !string.Equals(
                        component.componentTypeId,
                        ItemInstanceComponentIds.Equipment,
                        StringComparison.Ordinal)
                    || !EquipmentItemStateCodec.TryDecodeFull(
                        component,
                        out EquipmentPhysicalStatePayload equipment,
                        out _))
                {
                    continue;
                }

                string equipmentPath = $"{prefix}[{componentIndex}].state-json";
                equipment.equipment.ownerCharacterId = rewrite(
                    equipment.equipment.ownerCharacterId,
                    equipmentPath + ".equipment.ownerCharacterId");
                V18CharacterReferenceComponentRestoreNormalizer.NormalizeEvolutionState(
                    equipment.equipment.evolution,
                    rewrite,
                    equipmentPath + ".equipment.evolution");
                item.components[componentIndex] = EquipmentItemStateCodec.Encode(
                    equipment.equipment,
                    equipment.attachedModules);
            }
        }
    }

    public static void Normalize(
        ModularFacilityWorldSaveData payload,
        Func<string, string, string> rewrite)
    {
        if (payload?.buildings == null)
        {
            return;
        }

        for (int buildingIndex = 0;
             buildingIndex < payload.buildings.Count;
             buildingIndex++)
        {
            ModularFacilityBuildingSaveData building =
                payload.buildings[buildingIndex];
            if (building?.stateModules == null)
            {
                continue;
            }

            for (int moduleIndex = 0;
                 moduleIndex < building.stateModules.Count;
                 moduleIndex++)
            {
                BuildingStateModuleSaveData module =
                    building.stateModules[moduleIndex];
                if (module == null || string.IsNullOrWhiteSpace(module.payload))
                {
                    continue;
                }

                string prefix =
                    $"buildings[{buildingIndex}].stateModules[{moduleIndex}]";
                if (string.Equals(
                        module.moduleId,
                        DoorAccessStateModule.StateModuleId,
                        StringComparison.Ordinal))
                {
                    module.payload = V18CharacterReferenceComponentRestoreNormalizer
                        .NormalizeDoorAccessPolicy(
                            module.payload,
                            rewrite,
                            prefix + ".payload");
                }
                else if (string.Equals(
                             module.moduleId,
                             BuildingStateModuleIds.FacilityEvolution,
                             StringComparison.Ordinal))
                {
                    FacilityEvolutionStateSnapshot evolution =
                        JsonUtility.FromJson<FacilityEvolutionStateSnapshot>(
                            module.payload);
                    if (evolution != null)
                    {
                        V18CharacterReferenceComponentRestoreNormalizer
                            .NormalizeEvolutionState(
                            evolution.instanceEvolution,
                            rewrite,
                            prefix + ".payload.instanceEvolution");
                        module.payload = JsonUtility.ToJson(evolution);
                    }
                }
            }
        }
    }

    private static void RewriteList(
        IList<string> values,
        Func<string, string, string> rewrite,
        string path)
    {
        if (values == null)
        {
            return;
        }

        for (int index = 0; index < values.Count; index++)
        {
            values[index] = rewrite(values[index], $"{path}[{index}]");
        }
    }
}

public static class V18CombatOffenseCharacterReferenceRestoreNormalizer
{
    public static void Normalize(
        DungeonCombatEquipmentSaveData payload,
        Func<string, string, string> rewrite)
    {
        if (payload?.loadouts == null)
        {
            return;
        }

        bool changed = false;
        for (int index = 0; index < payload.loadouts.Count; index++)
        {
            CharacterCombatLoadoutState loadout = payload.loadouts[index];
            if (loadout != null)
            {
                string previous = loadout.characterId;
                loadout.characterId = rewrite(
                    previous,
                    $"loadouts[{index}].characterId");
                changed |= !string.Equals(
                    previous,
                    loadout.characterId,
                    StringComparison.Ordinal);
            }
        }
        if (changed)
        {
            payload.loadouts.Sort((left, right) => string.CompareOrdinal(
                left?.characterId,
                right?.characterId));
        }
    }

    public static void Normalize(
        DungeonCharacterBodyHealthSaveData payload,
        Func<string, string, string> rewrite)
    {
        if (payload?.characters == null)
        {
            return;
        }

        bool changed = false;
        for (int index = 0; index < payload.characters.Count; index++)
        {
            CharacterBodyHealthState state = payload.characters[index];
            if (state != null)
            {
                string previous = state.characterId;
                state.characterId = rewrite(
                    previous,
                    $"characters[{index}].characterId");
                changed |= !string.Equals(
                    previous,
                    state.characterId,
                    StringComparison.Ordinal);
            }
        }
        if (changed)
        {
            payload.characters.Sort((left, right) => string.CompareOrdinal(
                left?.characterId,
                right?.characterId));
        }
    }

    public static void Normalize(
        DungeonCharacterMedicalSaveData payload,
        Func<string, string, string> rewrite)
    {
        if (payload?.orders == null)
        {
            return;
        }

        for (int index = 0; index < payload.orders.Count; index++)
        {
            CharacterMedicalOrder order = payload.orders[index];
            if (order == null)
            {
                continue;
            }

            order.patientId = rewrite(
                order.patientId,
                $"orders[{index}].patientId");
            order.rescuerId = rewrite(
                order.rescuerId,
                $"orders[{index}].rescuerId");
        }
    }

    public static void Normalize(
        DungeonSurgerySaveData payload,
        Func<string, string, string> rewrite)
    {
        if (payload == null)
        {
            return;
        }

        if (payload.orders != null)
        {
            for (int index = 0; index < payload.orders.Count; index++)
            {
                SurgeryOrder order = payload.orders[index];
                if (order == null)
                {
                    continue;
                }

                order.preferredDoctorId = rewrite(
                    order.preferredDoctorId,
                    $"orders[{index}].preferredDoctorId");
                order.doctorId = rewrite(
                    order.doctorId,
                    $"orders[{index}].doctorId");
                order.patientTransporterId = rewrite(
                    order.patientTransporterId,
                    $"orders[{index}].patientTransporterId");
                if (order.subject != null
                    && order.subject.kind == SurgicalSubjectKind.Character)
                {
                    order.subject.subjectId = rewrite(
                        order.subject.subjectId,
                        $"orders[{index}].subject.subjectId");
                }
            }
        }

        if (payload.parts != null)
        {
            for (int index = 0; index < payload.parts.Count; index++)
            {
                SurgicalPartInstance part = payload.parts[index];
                if (part == null)
                {
                    continue;
                }

                part.donorId = V18TypedCharacterReferenceRestoreNormalizer
                    .RewriteRecognizedCharacterReference(
                        part.donorId,
                        rewrite,
                        $"parts[{index}].donorId");
                part.installedSubjectId =
                    V18TypedCharacterReferenceRestoreNormalizer
                        .RewriteRecognizedCharacterReference(
                            part.installedSubjectId,
                            rewrite,
                            $"parts[{index}].installedSubjectId");
            }
        }

        if (payload.policies != null)
        {
            for (int index = 0; index < payload.policies.Count; index++)
            {
                SurgerySubjectPolicyState policy = payload.policies[index];
                if (policy != null)
                {
                    policy.subjectId =
                        V18TypedCharacterReferenceRestoreNormalizer
                            .RewriteRecognizedCharacterReference(
                                policy.subjectId,
                                rewrite,
                                $"policies[{index}].subjectId");
                }
            }
        }
    }

    public static void Normalize(
        DefenseTacticalCoordinatorSaveData payload,
        Func<string, string, string> rewrite)
    {
        if (payload?.reservations == null)
        {
            return;
        }

        for (int index = 0; index < payload.reservations.Count; index++)
        {
            CombatPositionReservation reservation = payload.reservations[index];
            if (reservation != null)
            {
                reservation.actorId = rewrite(
                    reservation.actorId,
                    $"reservations[{index}].actorId");
                reservation.targetId =
                    V18TypedCharacterReferenceRestoreNormalizer
                        .RewriteRecognizedCharacterReference(
                            reservation.targetId,
                            rewrite,
                            $"reservations[{index}].targetId");
            }
        }
    }

    public static void Normalize(
        CombatEquipmentMaintenanceSaveData payload,
        Func<string, string, string> rewrite)
    {
        if (payload == null)
        {
            return;
        }

        if (payload.assignments != null)
        {
            for (int index = 0; index < payload.assignments.Count; index++)
            {
                EquipmentMaintenanceAssignmentSaveData assignment =
                    payload.assignments[index];
                if (assignment != null)
                {
                    assignment.characterId = rewrite(
                        assignment.characterId,
                        $"assignments[{index}].characterId");
                }
            }
        }

        if (payload.orders == null)
        {
            return;
        }

        for (int index = 0; index < payload.orders.Count; index++)
        {
            CombatEquipmentRepairOrder order = payload.orders[index];
            if (order == null)
            {
                continue;
            }

            string prefix = $"orders[{index}]";
            order.originalOwnerCharacterId = rewrite(
                order.originalOwnerCharacterId,
                prefix + ".originalOwnerCharacterId");
            order.reservedWorkerId = rewrite(
                order.reservedWorkerId,
                prefix + ".reservedWorkerId");
        }
    }

    public static void Normalize(
        CharacterCombatCommandSaveData payload,
        Func<string, string, string> rewrite)
    {
        if (payload == null)
        {
            return;
        }

        RewriteList(payload.stanceCharacterIds, rewrite, "stanceCharacterIds");

        if (payload.commands != null)
        {
            for (int index = 0; index < payload.commands.Count; index++)
            {
                CharacterCombatCommand command = payload.commands[index];
                if (command == null)
                {
                    continue;
                }

                command.actorId = rewrite(
                    command.actorId,
                    $"commands[{index}].actorId");
                if (command.type == CombatCommandType.Rescue)
                {
                    command.targetId = rewrite(
                        command.targetId,
                        $"commands[{index}].targetId");
                }
                else if (command.type == CombatCommandType.Attack)
                {
                    command.targetId =
                        V18TypedCharacterReferenceRestoreNormalizer
                            .RewriteRecognizedCharacterReference(
                                command.targetId,
                                rewrite,
                                $"commands[{index}].targetId");
                }
            }
        }

        if (payload.revisions != null)
        {
            for (int index = 0; index < payload.revisions.Count; index++)
            {
                CharacterCombatCommandRevisionSaveData revision =
                    payload.revisions[index];
                if (revision != null)
                {
                    revision.actorId = rewrite(
                        revision.actorId,
                        $"revisions[{index}].actorId");
                }
            }
        }
    }

    public static void Normalize(
        DungeonExteriorActivitySaveData payload,
        Func<string, string, string> rewrite)
    {
        if (payload?.incidentStates == null)
        {
            return;
        }

        for (int index = 0; index < payload.incidentStates.Count; index++)
        {
            ExteriorIncidentRuntimeState state = payload.incidentStates[index];
            if (state != null)
            {
                RewriteList(
                    state.actorIds,
                    rewrite,
                    $"incidentStates[{index}].actorIds");
            }
        }
    }

    public static void Normalize(
        DungeonRegularCustomerSaveData payload,
        Func<string, string, string> rewrite)
    {
        if (payload?.records == null)
        {
            return;
        }

        for (int index = 0; index < payload.records.Count; index++)
        {
            DungeonRegularCustomerRecordSaveData record = payload.records[index];
            if (record != null)
            {
                record.customerId = rewrite(
                    record.customerId,
                    $"records[{index}].customerId");
            }
        }
    }

    public static void Normalize(
        DungeonFactionSaveData payload,
        Func<string, string, string> rewrite)
    {
        if (payload?.routes == null)
        {
            return;
        }

        for (int index = 0; index < payload.routes.Count; index++)
        {
            FactionRouteState route = payload.routes[index];
            if (route != null)
            {
                RewriteList(
                    route.reinforcementActorIds,
                    rewrite,
                    $"routes[{index}].reinforcementActorIds");
            }
        }
    }

    public static void Normalize(
        DungeonInvasionSaveData payload,
        Func<string, string, string> rewrite)
    {
        if (payload?.responsePolicies?.assignments != null)
        {
            for (int index = 0;
                 index < payload.responsePolicies.assignments.Count;
                 index++)
            {
                DefensePolicyAssignmentSaveData assignment =
                    payload.responsePolicies.assignments[index];
                if (assignment != null)
                {
                    assignment.characterId = rewrite(
                        assignment.characterId,
                        $"responsePolicies.assignments[{index}].characterId");
                }
            }
        }

        if (payload?.engagements?.engagements == null)
        {
            return;
        }

        for (int index = 0;
             index < payload.engagements.engagements.Count;
             index++)
        {
            DefenseEngagementSaveData engagement =
                payload.engagements.engagements[index];
            if (engagement == null)
            {
                continue;
            }

            string prefix = $"engagements.engagements[{index}]";
            engagement.leadGuardId = rewrite(
                engagement.leadGuardId,
                prefix + ".leadGuardId");
            engagement.reserveGuardId = rewrite(
                engagement.reserveGuardId,
                prefix + ".reserveGuardId");
            engagement.rangedGuardId = rewrite(
                engagement.rangedGuardId,
                prefix + ".rangedGuardId");
            engagement.secondaryRangedGuardId = rewrite(
                engagement.secondaryRangedGuardId,
                prefix + ".secondaryRangedGuardId");
        }
    }

    public static void Normalize(
        DungeonOffenseAggregateSaveData payload,
        Func<string, string, string> rewrite)
    {
        if (payload == null)
        {
            return;
        }

        NormalizeOffenseExpeditions(payload.expedition, rewrite);
        NormalizeOffenseBattle(payload.expedition?.activeBattle, rewrite);
        NormalizeOffenseWorld(payload.world, rewrite);
        NormalizeOffenseReturnArrivals(payload.returnArrivals, rewrite);
    }

    private static void NormalizeOffenseExpeditions(
        DungeonOffenseSaveData payload,
        Func<string, string, string> rewrite)
    {
        if (payload?.activeExpeditions == null)
        {
            return;
        }

        for (int runIndex = 0;
             runIndex < payload.activeExpeditions.Count;
             runIndex++)
        {
            DungeonOffenseExpeditionRunSaveData run =
                payload.activeExpeditions[runIndex];
            if (run == null)
            {
                continue;
            }

            string prefix = $"expedition.activeExpeditions[{runIndex}]";
            RewriteList(
                run.memberPersistentIds,
                rewrite,
                prefix + ".memberPersistentIds");
            RewriteList(
                run.protectedRescueMemberPersistentIds,
                rewrite,
                prefix + ".protectedRescueMemberPersistentIds");
            if (run.memberStates == null)
            {
                continue;
            }

            for (int memberIndex = 0;
                 memberIndex < run.memberStates.Count;
                 memberIndex++)
            {
                DungeonOffenseExpeditionMemberStateSaveData member =
                    run.memberStates[memberIndex];
                if (member != null)
                {
                    member.persistentId = rewrite(
                        member.persistentId,
                        $"{prefix}.memberStates[{memberIndex}].persistentId");
                }
            }
        }
    }

    private static void NormalizeOffenseBattle(
        OffenseBattlePersistenceState battle,
        Func<string, string, string> rewrite)
    {
        if (battle == null)
        {
            return;
        }

        RewriteList(
            battle.initiativeOrder,
            rewrite,
            "expedition.activeBattle.initiativeOrder");
        if (battle.thrownEquipment != null)
        {
            for (int index = 0; index < battle.thrownEquipment.Count; index++)
            {
                OffenseThrownEquipmentPersistenceState thrown =
                    battle.thrownEquipment[index];
                if (thrown != null)
                {
                    thrown.ownerCharacterId = rewrite(
                        thrown.ownerCharacterId,
                        $"expedition.activeBattle.thrownEquipment[{index}].ownerCharacterId");
                }
            }
        }

        if (battle.combatants == null)
        {
            return;
        }

        for (int index = 0; index < battle.combatants.Count; index++)
        {
            OffenseBattleCombatantPersistenceState combatant =
                battle.combatants[index];
            if (combatant != null)
            {
                combatant.persistentId = rewrite(
                    combatant.persistentId,
                    $"expedition.activeBattle.combatants[{index}].persistentId");
            }
        }
    }

    private static void NormalizeOffenseWorld(
        OffenseWorldSaveData world,
        Func<string, string, string> rewrite)
    {
        if (world == null)
        {
            return;
        }

        if (world.fieldStabilizations != null)
        {
            for (int index = 0; index < world.fieldStabilizations.Count; index++)
            {
                FieldStabilizationState state = world.fieldStabilizations[index];
                if (state != null)
                {
                    state.characterId = rewrite(
                        state.characterId,
                        $"world.fieldStabilizations[{index}].characterId");
                }
            }
        }

        if (world.casualtyCarries != null)
        {
            for (int index = 0; index < world.casualtyCarries.Count; index++)
            {
                OffenseCasualtyCarryState carry = world.casualtyCarries[index];
                if (carry == null)
                {
                    continue;
                }

                string prefix = $"world.casualtyCarries[{index}]";
                carry.casualtyCharacterId = rewrite(
                    carry.casualtyCharacterId,
                    prefix + ".casualtyCharacterId");
                carry.carrierCharacterId = rewrite(
                    carry.carrierCharacterId,
                    prefix + ".carrierCharacterId");
            }
        }

        if (world.rescueConvoys != null)
        {
            for (int index = 0; index < world.rescueConvoys.Count; index++)
            {
                RescueConvoyState convoy = world.rescueConvoys[index];
                if (convoy == null)
                {
                    continue;
                }

                string prefix = $"world.rescueConvoys[{index}]";
                RewriteList(
                    convoy.rescuerCharacterIds,
                    rewrite,
                    prefix + ".rescuerCharacterIds");
                RewriteList(
                    convoy.protectedCasualtyIds,
                    rewrite,
                    prefix + ".protectedCasualtyIds");
            }
        }

        if (world.battles == null)
        {
            return;
        }

        for (int battleIndex = 0; battleIndex < world.battles.Count; battleIndex++)
        {
            OffenseBattleDirectorStateData battle = world.battles[battleIndex];
            if (battle == null)
            {
                continue;
            }

            string prefix = $"world.battles[{battleIndex}]";
            if (battle.decks != null)
            {
                for (int index = 0; index < battle.decks.Count; index++)
                {
                    OffenseCommandDeckStateData deck = battle.decks[index];
                    if (deck != null)
                    {
                        deck.characterId = rewrite(
                            deck.characterId,
                            $"{prefix}.decks[{index}].characterId");
                    }
                }
            }

            if (battle.enemyIntents != null)
            {
                for (int index = 0; index < battle.enemyIntents.Count; index++)
                {
                    OffenseEnemyIntentStateData intent = battle.enemyIntents[index];
                    if (intent != null)
                    {
                        intent.targetCharacterId = rewrite(
                            intent.targetCharacterId,
                            $"{prefix}.enemyIntents[{index}].targetCharacterId");
                    }
                }
            }

            if (battle.commandQueue == null)
            {
                continue;
            }

            for (int index = 0; index < battle.commandQueue.Count; index++)
            {
                OffenseCommandQueueEntryData entry = battle.commandQueue[index];
                if (entry != null)
                {
                    entry.characterId = rewrite(
                        entry.characterId,
                        $"{prefix}.commandQueue[{index}].characterId");
                }
            }
        }
    }

    private static void NormalizeOffenseReturnArrivals(
        DungeonOffenseReturnArrivalSaveData payload,
        Func<string, string, string> rewrite)
    {
        if (payload?.arrivals == null)
        {
            return;
        }

        for (int index = 0; index < payload.arrivals.Count; index++)
        {
            OffenseReturnArrivalState arrival = payload.arrivals[index];
            if (arrival == null)
            {
                continue;
            }

            string prefix = $"returnArrivals.arrivals[{index}]";
            if (arrival.kind == OffenseReturnArrivalKind.Prisoner)
            {
                RewriteList(
                    arrival.materializedIds,
                    rewrite,
                    prefix + ".materializedIds");
                RewriteList(
                    arrival.escapedIds,
                    rewrite,
                    prefix + ".escapedIds");
            }
            else
            {
                PreserveRuntimeReferences(
                    arrival.materializedIds,
                    prefix + ".materializedIds");
                PreserveRuntimeReferences(
                    arrival.escapedIds,
                    prefix + ".escapedIds");
            }
        }
    }

    private static void PreserveRuntimeReferences(
        IList<string> values,
        string path)
    {
        if (values == null)
        {
            return;
        }

        for (int index = 0; index < values.Count; index++)
        {
            values[index] = V18TypedCharacterReferenceRestoreNormalizer
                .PreserveRuntimeReference(values[index], $"{path}[{index}]");
        }
    }

    private static void RewriteList(
        IList<string> values,
        Func<string, string, string> rewrite,
        string path)
    {
        if (values == null)
        {
            return;
        }

        for (int index = 0; index < values.Count; index++)
        {
            values[index] = rewrite(values[index], $"{path}[{index}]");
        }
    }
}

public static class V18CharacterReferenceComponentRestoreNormalizer
{
    public static string NormalizeDoorAccessPolicy(
        string payload,
        Func<string, string, string> rewrite,
        string path)
    {
        DoorAccessPolicyRestorePayload access =
            JsonUtility.FromJson<DoorAccessPolicyRestorePayload>(payload);
        if (access == null)
        {
            return payload;
        }

        bool allowedChanged = RewriteList(
            access.individuallyAllowedIds,
            rewrite,
            path + ".individuallyAllowedIds");
        bool deniedChanged = RewriteList(
            access.individuallyDeniedIds,
            rewrite,
            path + ".individuallyDeniedIds");
        if (allowedChanged)
        {
            access.individuallyAllowedIds.Sort(StringComparer.Ordinal);
        }
        if (deniedChanged)
        {
            access.individuallyDeniedIds.Sort(StringComparer.Ordinal);
        }
        return JsonUtility.ToJson(access);
    }

    public static void NormalizeSocialMemory(
        CharacterSocialMemorySnapshot memory,
        Func<string, string, string> rewrite,
        string path)
    {
        if (memory == null)
        {
            return;
        }

        if (memory.recentRumors != null)
        {
            for (int index = 0; index < memory.recentRumors.Count; index++)
            {
                SocialRumorSnapshot rumor = memory.recentRumors[index];
                if (rumor == null)
                {
                    continue;
                }

                string prefix = $"{path}.recentRumors[{index}]";
                rumor.sourceActorId = rewrite(
                    rumor.sourceActorId,
                    prefix + ".sourceActorId");
                if (rumor.targetType == SocialRumorTargetType.Character)
                {
                    rumor.targetCharacterId = rewrite(
                        rumor.targetCharacterId,
                        prefix + ".targetCharacterId");
                }
            }
        }

        RewriteSocialMemoryKeys(
            memory.characterSentiments,
            rewrite,
            path + ".characterSentiments");
        RewriteSocialMemoryKeys(
            memory.sourceTrust,
            rewrite,
            path + ".sourceTrust");
    }

    private static void RewriteSocialMemoryKeys(
        IList<SocialMemoryFloat> values,
        Func<string, string, string> rewrite,
        string path)
    {
        if (values == null)
        {
            return;
        }

        bool changed = false;
        for (int index = 0; index < values.Count; index++)
        {
            SocialMemoryFloat value = values[index];
            if (value != null)
            {
                string previous = value.key;
                value.key = rewrite(previous, $"{path}[{index}].key");
                changed |= !string.Equals(
                    previous,
                    value.key,
                    StringComparison.Ordinal);
            }
        }
        if (changed && values is List<SocialMemoryFloat> list)
        {
            list.Sort((left, right) => string.CompareOrdinal(
                left?.key,
                right?.key));
        }
    }

    public static void NormalizeCarryInventory(
        CharacterCarryInventorySaveData inventory,
        Func<string, string, string> rewrite,
        string path)
    {
        if (inventory?.items == null)
        {
            return;
        }

        for (int index = 0; index < inventory.items.Count; index++)
        {
            CharacterCarriedItemSaveData item = inventory.items[index];
            if (item != null)
            {
                NormalizeProvenanceComponents(
                    item.components,
                    rewrite,
                    $"{path}.items[{index}].components");
            }
        }
    }

    public static void NormalizeProvenanceComponents(
        IList<ItemInstanceComponentSaveData> components,
        Func<string, string, string> rewrite,
        string path)
    {
        if (components == null)
        {
            return;
        }

        for (int componentIndex = 0;
             componentIndex < components.Count;
             componentIndex++)
        {
            ItemInstanceComponentSaveData component = components[componentIndex];
            if (component?.values == null
                || !string.Equals(
                    component.componentTypeId,
                    ItemInstanceComponentIds.Provenance,
                    StringComparison.Ordinal))
            {
                continue;
            }

            for (int valueIndex = 0;
                 valueIndex < component.values.Count;
                 valueIndex++)
            {
                ItemStateValueSaveData value = component.values[valueIndex];
                if (value != null
                    && value.kind == ItemStateValueKind.String
                    && string.Equals(
                        value.key,
                        "source-character-id",
                        StringComparison.Ordinal))
                {
                    value.stringValue = rewrite(
                        value.stringValue,
                        $"{path}[{componentIndex}].values[{valueIndex}].stringValue");
                }
            }
        }
    }

    public static void NormalizeEvolutionState(
        EquipmentEvolutionState state,
        Func<string, string, string> rewrite,
        string path)
    {
        if (state == null)
        {
            return;
        }

        NormalizeUsageLedger(state.usageLedger, rewrite, path + ".usageLedger");
        if (state.attunements != null)
        {
            for (int index = 0; index < state.attunements.Count; index++)
            {
                AttunementRecord attunement = state.attunements[index];
                if (attunement != null)
                {
                    attunement.ownerPersistentId = rewrite(
                        attunement.ownerPersistentId,
                        $"{path}.attunements[{index}].ownerPersistentId");
                }
            }
        }
    }

    public static void NormalizeEvolutionState(
        FacilityEvolutionState state,
        Func<string, string, string> rewrite,
        string path)
    {
        if (state == null)
        {
            return;
        }

        NormalizeUsageLedger(state.usageLedger, rewrite, path + ".usageLedger");
    }

    private static void NormalizeUsageLedger(
        UsageLedger ledger,
        Func<string, string, string> rewrite,
        string path)
    {
        if (ledger == null)
        {
            return;
        }

        NormalizeUsageEvents(
            ledger.currentGenerationEvents,
            rewrite,
            path + ".currentGenerationEvents");
        if (ledger.compactedSegments == null)
        {
            return;
        }

        for (int index = 0; index < ledger.compactedSegments.Count; index++)
        {
            CompactedHistorySegment segment = ledger.compactedSegments[index];
            if (segment == null)
            {
                continue;
            }

            string prefix = $"{path}.compactedSegments[{index}]";
            NormalizeUsageEvents(segment.keyEvents, rewrite, prefix + ".keyEvents");
        }
    }

    private static void NormalizeUsageEvents(
        IList<UsageLedgerEvent> events,
        Func<string, string, string> rewrite,
        string path)
    {
        if (events == null)
        {
            return;
        }

        for (int index = 0; index < events.Count; index++)
        {
            UsageLedgerEvent entry = events[index];
            if (entry == null)
            {
                continue;
            }

            string prefix = $"{path}[{index}]";
            entry.actorId = rewrite(entry.actorId, prefix + ".actorId");
        }
    }

    private static void RewriteCharacterIds<T>(
        IList<T> values,
        Func<T, string> get,
        Action<T, string> set,
        Func<string, string, string> rewrite,
        string path)
        where T : class
    {
        if (values == null)
        {
            return;
        }

        for (int index = 0; index < values.Count; index++)
        {
            T value = values[index];
            if (value != null)
            {
                set(value, rewrite(get(value), $"{path}[{index}].characterId"));
            }
        }
    }

    private static bool RewriteList(
        IList<string> values,
        Func<string, string, string> rewrite,
        string path)
    {
        if (values == null)
        {
            return false;
        }

        bool changed = false;
        for (int index = 0; index < values.Count; index++)
        {
            string previous = values[index];
            values[index] = rewrite(previous, $"{path}[{index}]");
            changed |= !string.Equals(
                previous,
                values[index],
                StringComparison.Ordinal);
        }
        return changed;
    }

    [Serializable]
    private sealed class DoorAccessPolicyRestorePayload
    {
        public int allowedGroups;
        public List<string> individuallyAllowedIds = new();
        public List<string> individuallyDeniedIds = new();
    }
}
