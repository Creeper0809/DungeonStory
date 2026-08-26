using System;

public readonly struct PackagedLotDefinitionSnapshot
{
    public PackagedLotDefinitionSnapshot(
        ItemDefinitionId itemId,
        PhysicalMassGrams totalUnitMass,
        PhysicalMassGrams tareMass,
        PackageTareDisposition tareDisposition,
        ItemDefinitionId containerItemId)
    {
        if (!itemId.IsValid)
            throw new ArgumentException("Packaged lot item ID is required.", nameof(itemId));
        if (tareMass.Value >= totalUnitMass.Value)
            throw new ArgumentOutOfRangeException(
                nameof(tareMass),
                "Packaged lot tare must be smaller than total unit mass.");

        ItemId = itemId;
        TotalUnitMass = totalUnitMass;
        TareMass = tareMass;
        ContentMass = new PhysicalMassGrams(
            checked(totalUnitMass.Value - tareMass.Value));
        TareDisposition = tareDisposition;
        ContainerItemId = containerItemId;
    }

    public ItemDefinitionId ItemId { get; }
    public PhysicalMassGrams TotalUnitMass { get; }
    public PhysicalMassGrams ContentMass { get; }
    public PhysicalMassGrams TareMass { get; }
    public PackageTareDisposition TareDisposition { get; }
    public ItemDefinitionId ContainerItemId { get; }
}

public interface IPackagedLotDefinitionQuery
{
    bool TryGetPackagedLot(
        ItemDefinitionId itemId,
        out PackagedLotDefinitionSnapshot packagedLot);
}

public sealed class PackagedLotPhysicalItemMassProjector :
    IPhysicalItemMassProjector
{
    public const string ComponentTypeId = "physical-packaged-lot";
    public const int SchemaVersion = 1;

    public PhysicalItemMassSubjectKind SubjectKind =>
        PhysicalItemMassSubjectKind.PackagedLot;

    public PhysicalMassGrams GetUnitMass(PhysicalItemMassSubject subject)
    {
        if (subject == null || subject.Kind != SubjectKind)
        {
            throw new ArgumentException(
                "Packaged-lot projector requires a packaged-lot subject.",
                nameof(subject));
        }
        if (subject.Components.Count != 1
            || !string.Equals(
                subject.Components[0].ComponentTypeId,
                ComponentTypeId,
                StringComparison.Ordinal)
            || subject.Components[0].SchemaVersion != SchemaVersion
            || !subject.Components[0].PreparedUnitMass.HasValue)
        {
            throw new InvalidOperationException(
                $"Packaged lot '{subject.ItemId.Value}' has invalid immutable mass state.");
        }

        return subject.Components[0].PreparedUnitMass.Value;
    }
}
