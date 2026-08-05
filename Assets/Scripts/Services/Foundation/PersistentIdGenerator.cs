using System;

public interface IPersistentIdGenerator
{
    ItemInstanceId NewItemInstanceId();
    ItemStackId NewItemStackId();
    CharacterId NewCharacterId();
    BuildingInstanceId NewBuildingInstanceId();
    WildlifeHabitatPatchId NewWildlifeHabitatPatchId();
}

public sealed class GuidPersistentIdGenerator : IPersistentIdGenerator
{
    public ItemInstanceId NewItemInstanceId() =>
        new($"item-instance:{Guid.NewGuid():N}");

    public ItemStackId NewItemStackId() =>
        new($"stack:{Guid.NewGuid():N}");

    public CharacterId NewCharacterId() =>
        new($"character:{Guid.NewGuid():N}");

    public BuildingInstanceId NewBuildingInstanceId() =>
        new($"building:{Guid.NewGuid():N}");

    public WildlifeHabitatPatchId NewWildlifeHabitatPatchId() =>
        new($"wildlife-habitat:{Guid.NewGuid():N}");
}
