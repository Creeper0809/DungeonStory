using System;
using System.Collections.Generic;

public interface IEquippedApparelPhysicalMassQuery
{
    long GetEquippedMassGrams(CharacterId characterId);
}

/// <summary>
/// Rebuildable read model over the apparel aggregate and physical item authority.
/// It owns no gameplay or save state. The index is invalidated by either aggregate
/// revision, so movement hot loops do not enumerate apparel or item stacks.
/// </summary>
public sealed class EquippedApparelPhysicalMassQuery :
    IEquippedApparelPhysicalMassQuery
{
    private readonly ICharacterApparelQuery apparel;
    private readonly IWorldItemStackRuntime items;
    private readonly IPhysicalItemMassQuery massQuery;
    private readonly Dictionary<CharacterId, PhysicalMassGrams> massByCharacter =
        new();
    private int cachedApparelVersion = int.MinValue;
    private int cachedItemStackVersion = int.MinValue;

    public EquippedApparelPhysicalMassQuery(
        ICharacterApparelQuery apparel,
        IWorldItemStackRuntime items,
        IPhysicalItemMassQuery massQuery)
    {
        this.apparel = apparel ?? throw new ArgumentNullException(nameof(apparel));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.massQuery = massQuery ?? throw new ArgumentNullException(nameof(massQuery));
    }

    public long GetEquippedMassGrams(CharacterId characterId)
    {
        if (!characterId.IsValid)
        {
            return 0L;
        }

        RebuildIfStale();
        return massByCharacter.TryGetValue(characterId, out PhysicalMassGrams mass)
            ? mass.Value
            : 0L;
    }

    private void RebuildIfStale()
    {
        int apparelVersion = apparel.Version;
        int itemStackVersion = items.ItemStackVersion;
        if (cachedApparelVersion == apparelVersion
            && cachedItemStackVersion == itemStackVersion)
        {
            return;
        }

        IReadOnlyList<WorldItemStackSnapshot> stacks = items.GetAllStacks();
        Dictionary<string, WorldItemStackSnapshot> stackByInstance =
            new(StringComparer.Ordinal);
        for (int index = 0; index < stacks.Count; index++)
        {
            WorldItemStackSnapshot stack = stacks[index];
            if (stack == null || string.IsNullOrWhiteSpace(stack.ItemInstanceId))
            {
                continue;
            }
            if (!stackByInstance.TryAdd(stack.ItemInstanceId, stack))
            {
                throw new InvalidOperationException(
                    $"Duplicate physical item instance '{stack.ItemInstanceId}'.");
            }
        }

        Dictionary<CharacterId, long> rebuilt = new();
        HashSet<string> seenEquippedInstances = new(StringComparer.Ordinal);
        IReadOnlyList<EquippedApparelSnapshot> equipped = apparel.GetAllEquipped();
        for (int index = 0; index < equipped.Count; index++)
        {
            EquippedApparelSnapshot entry = equipped[index];
            string instanceId = entry.ItemInstanceId.Value;
            if (!entry.CharacterId.IsValid
                || !entry.ItemInstanceId.IsValid
                || !seenEquippedInstances.Add(instanceId)
                || !stackByInstance.TryGetValue(instanceId, out WorldItemStackSnapshot stack)
                || stack.Quantity != 1
                || stack.State != WorldItemStackState.Carried
                || !string.Equals(
                    stack.DestinationId,
                    CharacterApparelAggregate.EquippedDestinationPrefix
                        + entry.CharacterId.Value,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Equipped apparel '{instanceId}' has no exact physical ownership.");
            }

            PhysicalItemMassSubject subject = PhysicalItemMassSubjectAdapter.Create(
                massQuery,
                (ItemDefinitionId)stack.ItemId,
                stack.ItemInstanceId,
                stack.Components);
            if (subject.Kind != PhysicalItemMassSubjectKind.Apparel)
            {
                throw new InvalidOperationException(
                    $"Equipped physical item '{instanceId}' has no apparel mass subject.");
            }

            long unitMass = massQuery.GetPreparedStackUnitMass(subject).Value;
            rebuilt.TryGetValue(entry.CharacterId, out long current);
            rebuilt[entry.CharacterId] = checked(current + unitMass);
        }

        massByCharacter.Clear();
        foreach (KeyValuePair<CharacterId, long> pair in rebuilt)
        {
            massByCharacter.Add(pair.Key, new PhysicalMassGrams(pair.Value));
        }
        cachedApparelVersion = apparelVersion;
        cachedItemStackVersion = itemStackVersion;
    }
}
