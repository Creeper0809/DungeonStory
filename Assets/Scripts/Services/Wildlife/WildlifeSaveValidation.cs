using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal static class WildlifeSaveValidation
{
    internal const int MaxWildlife = 256;
    internal const int MaxCarcasses = 2048;
    internal const int MaxFoodRaidOrders = 128;
    internal const int MaxHabitatPatches = 256;

    public static void Validate(
        DungeonWildlifeSaveData payload,
        DungeonGameRestoreReport report,
        IWildlifeSpeciesCatalogProvider speciesCatalog)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }

        if (payload == null)
        {
            report.AddError("Wildlife payload is null.");
            return;
        }

        if (payload.version != DungeonWildlifeSaveData.CurrentVersion)
        {
            report.AddError(
                $"Unsupported wildlife payload version {payload.version}; expected {DungeonWildlifeSaveData.CurrentVersion}.");
        }

        if (payload.nextSequence < 1)
        {
            report.AddError("Wildlife next sequence must be positive.");
        }

        if (payload.wildlife == null
            || payload.carcasses == null
            || payload.foodRaidOrders == null
            || payload.ecosystem == null)
        {
            report.AddError("Wildlife payload is missing a required state list.");
            return;
        }

        ValidateAnimals(payload, report, speciesCatalog);
        ValidateCarcasses(payload.carcasses, report, speciesCatalog);
        ValidateFoodRaids(payload, report);
        ValidateEcosystem(payload.ecosystem, report, speciesCatalog);
    }

    public static void ValidateWorldReferences(
        DungeonWildlifeSaveData payload,
        Grid restoreGrid,
        IWorldItemStackRuntime itemStacks,
        IWildlifeSpeciesCatalogProvider speciesCatalog,
        DungeonGameRestoreReport report)
    {
        if (payload == null)
        {
            throw new ArgumentNullException(nameof(payload));
        }
        if (restoreGrid == null)
        {
            throw new ArgumentNullException(nameof(restoreGrid));
        }
        if (itemStacks == null)
        {
            throw new ArgumentNullException(nameof(itemStacks));
        }
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }

        Dictionary<string, WorldItemStackSnapshot> stacksById =
            itemStacks.GetAllStacks()
                .Where(stack => stack != null
                    && !string.IsNullOrWhiteSpace(stack.StackId))
                .GroupBy(stack => stack.StackId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.First(),
                    StringComparer.Ordinal);

        foreach (WildlifeCarcassFreshnessSaveData carcass in
                 payload.carcasses)
        {
            if (!stacksById.TryGetValue(
                    carcass.stackId,
                    out WorldItemStackSnapshot stack)
                || speciesCatalog == null
                || !speciesCatalog.TryGetSpecies(
                    carcass.speciesId,
                    out WildlifeSpeciesDefinition species)
                || !string.Equals(
                    stack.ItemId,
                    species.CarcassItemId,
                    StringComparison.Ordinal))
            {
                report.AddError(
                    $"Wildlife carcass '{carcass.stackId}' does not reference its physical carcass item.");
            }
        }

        foreach (WildlifeHabitatPatchSaveData patch in
                 payload.ecosystem.patches)
        {
            WildlifeHabitatPatch habitat =
                WildlifeHabitatPatch.FromSave(patch);
            bool hasUsableCell = habitat != null
                && restoreGrid.GetCells().Any(cell =>
                    cell != null
                    && cell.AreaType == GridCellAreaType.ExteriorPath
                    && restoreGrid.IsWalkable(cell.Position)
                    && WildlifeRuntime.IsOutdoorSurfaceCell(
                        restoreGrid,
                        cell)
                    && habitat.Contains(cell.Position));
            if (!hasUsableCell)
            {
                report.AddError(
                    $"Wildlife habitat patch '{patch.patchId}' has no usable exterior cell in the restored world.");
            }
        }
    }

    private static void ValidateAnimals(
        DungeonWildlifeSaveData payload,
        DungeonGameRestoreReport report,
        IWildlifeSpeciesCatalogProvider speciesCatalog)
    {
        if (payload.wildlife.Count > MaxWildlife)
        {
            report.AddError(
                $"Wildlife payload exceeds the {MaxWildlife}-animal limit.");
        }

        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        int highestSequence = 0;
        foreach (WildlifeSaveData animal in payload.wildlife)
        {
            if (animal == null)
            {
                report.AddError("Wildlife payload contains a null animal.");
                continue;
            }

            string id = animal.wildlifeId?.Trim() ?? string.Empty;
            if (!IsCanonicalNonEmpty(animal.wildlifeId)
                || !TryParseWildlifeSequence(id, out int sequence)
                || !ids.Add(id))
            {
                report.AddError(
                    $"Wildlife payload contains invalid or duplicate animal ID '{id}'.");
            }
            else
            {
                highestSequence = Math.Max(highestSequence, sequence);
            }

            string speciesId = animal.speciesId?.Trim() ?? string.Empty;
            if (!IsCanonicalNonEmpty(animal.speciesId)
                || speciesCatalog == null
                || !speciesCatalog.TryGetSpecies(
                    speciesId,
                    out WildlifeSpeciesDefinition species))
            {
                report.AddError(
                    $"Wildlife '{id}' references unknown species '{speciesId}'.");
                continue;
            }

            if (animal.health <= 0
                || animal.health > species.MaxHealth
                || !Enum.IsDefined(typeof(WildlifeState), animal.state)
                || animal.state == WildlifeState.Dead)
            {
                report.AddError(
                    $"Wildlife '{id}' has invalid health/state {animal.health}/{animal.state}.");
            }

            if (!Enum.IsDefined(typeof(WildlifeIntent), animal.intent)
                || animal.reservedByPersistentId == null
                || animal.intentReason == null)
            {
                report.AddError(
                    $"Wildlife '{id}' has invalid intent or null text state.");
            }

            if (!IsFiniteInRange(animal.hunger, 0f, 1f)
                || !IsFiniteInRange(animal.thirst, 0f, 1f)
                || !IsFiniteAtLeast(animal.fear, 0f)
                || !IsFiniteAtLeast(animal.headHealth, 0f)
                || !IsFiniteAtLeast(animal.torsoHealth, 0f)
                || !IsFiniteAtLeast(animal.limbHealth, 0f))
            {
                report.AddError(
                    $"Wildlife '{id}' contains non-finite or out-of-range body/need values.");
            }
        }

        if (payload.nextSequence <= highestSequence)
        {
            report.AddError(
                $"Wildlife next sequence {payload.nextSequence} does not exceed existing sequence {highestSequence}.");
        }
    }

    private static void ValidateCarcasses(
        IReadOnlyList<WildlifeCarcassFreshnessSaveData> carcasses,
        DungeonGameRestoreReport report,
        IWildlifeSpeciesCatalogProvider speciesCatalog)
    {
        if (carcasses.Count > MaxCarcasses)
        {
            report.AddError(
                $"Wildlife payload exceeds the {MaxCarcasses}-carcass limit.");
        }

        HashSet<string> stackIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (WildlifeCarcassFreshnessSaveData carcass in carcasses)
        {
            string stackId = carcass?.stackId?.Trim() ?? string.Empty;
            string speciesId = carcass?.speciesId?.Trim() ?? string.Empty;
            if (carcass == null
                || !IsCanonicalNonEmpty(carcass.stackId)
                || !IsCanonicalNonEmpty(carcass.speciesId)
                || !((ItemStackId)stackId).IsValid
                || !stackIds.Add(stackId)
                || speciesCatalog == null
                || !speciesCatalog.TryGetSpecies(speciesId, out _)
                || !IsFiniteAtLeast(
                    carcass.remainingFreshnessSeconds,
                    0f))
            {
                report.AddError(
                    $"Wildlife payload contains invalid carcass record '{stackId}'.");
            }
        }
    }

    private static void ValidateFoodRaids(
        DungeonWildlifeSaveData payload,
        DungeonGameRestoreReport report)
    {
        if (payload.foodRaidOrders.Count > MaxFoodRaidOrders)
        {
            report.AddError(
                $"Wildlife payload exceeds the {MaxFoodRaidOrders}-raid-order limit.");
        }

        HashSet<string> wildlifeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (WildlifeSaveData animal in payload.wildlife)
        {
            if (animal != null && !string.IsNullOrWhiteSpace(animal.wildlifeId))
            {
                wildlifeIds.Add(animal.wildlifeId.Trim());
            }
        }

        HashSet<string> orderedWildlife =
            new HashSet<string>(StringComparer.Ordinal);
        foreach (WildlifeFoodRaidOrderSaveData order in payload.foodRaidOrders)
        {
            string wildlifeId = order?.wildlifeId?.Trim() ?? string.Empty;
            string targetStackId = order?.targetStackId?.Trim() ?? string.Empty;
            bool terminal = order != null
                && (order.state == WildlifeFoodRaidOrderState.Stolen
                    || order.state == WildlifeFoodRaidOrderState.Cancelled
                    || order.state == WildlifeFoodRaidOrderState.Failed);
            if (order == null
                || !IsCanonicalNonEmpty(order.raidId)
                || !IsCanonicalNonEmpty(order.wildlifeId)
                || !IsCanonicalOptional(order.targetStackId)
                || !TryParseWildlifeSequence(wildlifeId, out _)
                || (!terminal && !wildlifeIds.Contains(wildlifeId))
                || !orderedWildlife.Add(wildlifeId)
                || !Enum.IsDefined(
                    typeof(WildlifeFoodRaidOrderState),
                    order.state)
                || order.stolenQuantity < 0
                || order.outcomeReason == null
                || (targetStackId.Length > 0
                    && !((ItemStackId)targetStackId).IsValid))
            {
                report.AddError(
                    $"Wildlife payload contains invalid raid order for '{wildlifeId}'.");
            }
        }
    }

    private static void ValidateEcosystem(
        DungeonWildlifeEcosystemSaveData ecosystem,
        DungeonGameRestoreReport report,
        IWildlifeSpeciesCatalogProvider speciesCatalog)
    {
        if (ecosystem.version != DungeonWildlifeEcosystemSaveData.CurrentVersion
            || ecosystem.speciesRespawns == null
            || ecosystem.patches == null
            || !IsFiniteAtLeast(ecosystem.recentHuntPressure, 0f)
            || !IsFiniteAtLeast(ecosystem.recentPredationPressure, 0f)
            || !IsFiniteAtLeast(
                ecosystem.globalRespawnRemainingSeconds,
                0f))
        {
            report.AddError("Wildlife ecosystem root state is invalid.");
            return;
        }

        HashSet<string> respawnSpecies =
            new HashSet<string>(StringComparer.Ordinal);
        foreach (WildlifeSpeciesRespawnSaveData respawn in
                 ecosystem.speciesRespawns)
        {
            string speciesId = respawn?.speciesId?.Trim() ?? string.Empty;
            if (respawn == null
                || !IsCanonicalNonEmpty(respawn.speciesId)
                || !respawnSpecies.Add(speciesId)
                || speciesCatalog == null
                || !speciesCatalog.TryGetSpecies(speciesId, out _)
                || !IsFiniteAtLeast(respawn.remainingSeconds, 0f))
            {
                report.AddError(
                    $"Wildlife ecosystem contains invalid respawn species '{speciesId}'.");
            }
        }

        if (ecosystem.patches.Count > MaxHabitatPatches)
        {
            report.AddError(
                $"Wildlife ecosystem exceeds the {MaxHabitatPatches}-patch limit.");
        }

        HashSet<string> patchIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (WildlifeHabitatPatchSaveData patch in ecosystem.patches)
        {
            string patchId = patch?.patchId?.Trim() ?? string.Empty;
            if (patch == null
                || !IsCanonicalNonEmpty(patch.patchId)
                || !((WildlifeHabitatPatchId)patchId).IsValid
                || !patchIds.Add(patchId)
                || !Enum.IsDefined(typeof(WildlifeHabitatType), patch.habitatType)
                || patch.radius < 1
                || patch.radius > 12
                || !IsFiniteAtLeast(patch.resourceCapacity, 0.1f)
                || !IsFiniteInRange(
                    patch.currentResource,
                    0f,
                    Math.Max(0f, patch.resourceCapacity))
                || !IsFiniteAtLeast(patch.regenPerSecond, 0f)
                || !IsFiniteInRange(patch.danger, 0f, 1f)
                || patch.linkedWaterSourceId == null
                || !IsCanonicalOptional(patch.linkedWaterSourceId)
                || patch.preferredSpeciesTags == null)
            {
                report.AddError(
                    $"Wildlife ecosystem contains invalid habitat patch '{patchId}'.");
                continue;
            }

            HashSet<string> tags = new HashSet<string>(StringComparer.Ordinal);
            foreach (string tag in patch.preferredSpeciesTags)
            {
                string normalized = tag?.Trim() ?? string.Empty;
                if (!IsCanonicalNonEmpty(tag) || !tags.Add(normalized))
                {
                    report.AddError(
                        $"Wildlife habitat patch '{patchId}' has invalid or duplicate species tag.");
                }
            }
        }
    }

    private static bool TryParseWildlifeSequence(
        string wildlifeId,
        out int sequence)
    {
        const string prefix = "wild:";
        sequence = 0;
        return wildlifeId.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(
                wildlifeId.Substring(prefix.Length),
                out sequence)
            && sequence > 0;
    }

    private static bool IsFiniteAtLeast(float value, float minimum)
    {
        return !float.IsNaN(value)
            && !float.IsInfinity(value)
            && value >= minimum;
    }

    private static bool IsFiniteInRange(
        float value,
        float minimum,
        float maximum)
    {
        return IsFiniteAtLeast(value, minimum) && value <= maximum;
    }

    private static bool IsCanonicalNonEmpty(string value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && string.Equals(value, value.Trim(), StringComparison.Ordinal);
    }

    private static bool IsCanonicalOptional(string value)
    {
        return value != null
            && string.Equals(value, value.Trim(), StringComparison.Ordinal);
    }
}
