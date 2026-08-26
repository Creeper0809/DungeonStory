using System;
using System.Collections.Generic;

/// <summary>
/// Enforces the exact join between wildlife species carcass mass and the
/// physical item definition. Carcass freshness and the living animal's current
/// condition are not additional mass writers.
/// </summary>
public sealed class WildlifeCarcassPhysicalItemMassProjector :
    IPhysicalItemMassProjector
{
    public const string ComponentTypeId = "wildlife-carcass-authority";
    public const int SchemaVersion = 1;

    private readonly Dictionary<string, PhysicalMassGrams> massByItemId =
        new(StringComparer.Ordinal);

    public WildlifeCarcassPhysicalItemMassProjector(
        IWildlifeSpeciesCatalogProvider speciesCatalog,
        IDungeonItemCatalogProvider itemCatalog)
    {
        if (speciesCatalog == null)
        {
            throw new ArgumentNullException(nameof(speciesCatalog));
        }
        if (itemCatalog == null)
        {
            throw new ArgumentNullException(nameof(itemCatalog));
        }

        foreach (WildlifeSpeciesDefinition species in speciesCatalog.All)
        {
            if (species == null
                || string.IsNullOrWhiteSpace(species.SpeciesId)
                || !string.Equals(
                    species.SpeciesId,
                    species.SpeciesId.Trim(),
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Wildlife carcass mass catalog contains an invalid species.");
            }

            string itemId = WildlifeItemDefinitions.GetCarcassItemId(
                species.SpeciesId);
            if (!itemCatalog.TryGetDefinition(
                    itemId,
                    out DungeonItemDefinition definition))
            {
                throw new InvalidOperationException(
                    $"Wildlife species '{species.SpeciesId}' has no physical carcass definition '{itemId}'.");
            }

            PhysicalMassGrams speciesMass =
                PhysicalMassGrams.FromCanonicalKilograms(
                    species.CarcassWeight);
            PhysicalMassGrams definitionMass =
                PhysicalMassGrams.FromCanonicalKilograms(
                    definition.UnitWeight);
            if (!speciesMass.Equals(definitionMass))
            {
                throw new InvalidOperationException(
                    $"WILDLIFE_CARCASS_MASS_AUTHORITY_MISMATCH:{itemId}:"
                    + $"species={speciesMass.Value}g:definition={definitionMass.Value}g");
            }
            if (!massByItemId.TryAdd(itemId, definitionMass))
            {
                throw new InvalidOperationException(
                    $"Duplicate wildlife carcass mass authority '{itemId}'.");
            }
        }
    }

    public PhysicalItemMassSubjectKind SubjectKind =>
        PhysicalItemMassSubjectKind.WildlifeCarcass;

    public PhysicalMassGrams GetUnitMass(PhysicalItemMassSubject subject)
    {
        if (subject == null
            || subject.Kind != SubjectKind
            || subject.Components.Count != 1)
        {
            throw new ArgumentException(
                "Wildlife carcass mass requires one validated species authority component.",
                nameof(subject));
        }

        PhysicalItemComponentSnapshot component = subject.Components[0];
        string speciesId = component.CanonicalPayload;
        if (!string.Equals(
                component.ComponentTypeId,
                ComponentTypeId,
                StringComparison.Ordinal)
            || component.SchemaVersion != SchemaVersion
            || string.IsNullOrWhiteSpace(speciesId)
            || !string.Equals(
                subject.ItemId.Value,
                WildlifeItemDefinitions.GetCarcassItemId(speciesId),
                StringComparison.Ordinal)
            || !component.PreparedUnitMass.HasValue
            || !massByItemId.TryGetValue(
                subject.ItemId.Value,
                out PhysicalMassGrams expected)
            || !component.PreparedUnitMass.Value.Equals(expected))
        {
            throw new InvalidOperationException(
                $"Wildlife carcass mass authority is invalid for '{subject.ItemId.Value}'.");
        }

        return expected;
    }
}
