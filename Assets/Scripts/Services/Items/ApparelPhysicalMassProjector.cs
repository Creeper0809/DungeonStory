using System;

/// <summary>
/// Projects apparel from the physical item definition's gram authority. Apparel
/// quality, material, durability, moisture, contamination, and fit state are not
/// additional matter in V27 and therefore cannot alter physical mass.
/// </summary>
public sealed class ApparelPhysicalItemMassProjector :
    IPhysicalItemMassProjector
{
    public PhysicalItemMassSubjectKind SubjectKind =>
        PhysicalItemMassSubjectKind.Apparel;

    public PhysicalMassGrams GetUnitMass(PhysicalItemMassSubject subject)
    {
        if (subject == null
            || subject.Kind != SubjectKind
            || subject.Components.Count != 1)
        {
            throw new ArgumentException(
                "Apparel mass requires one validated apparel component.",
                nameof(subject));
        }

        PhysicalItemComponentSnapshot component = subject.Components[0];
        if (!string.Equals(
                component.ComponentTypeId,
                ItemInstanceComponentIds.Apparel,
                StringComparison.Ordinal)
            || component.SchemaVersion != ApparelItemStateCodec.SchemaVersion
            || !component.PreparedUnitMass.HasValue)
        {
            throw new InvalidOperationException(
                "Apparel mass component type, schema, or prepared mass is invalid.");
        }

        return component.PreparedUnitMass.Value;
    }
}
