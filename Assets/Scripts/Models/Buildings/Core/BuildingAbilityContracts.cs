using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class BuildingAbilityDisplayNameAttribute : Attribute
{
    public BuildingAbilityDisplayNameAttribute(string displayName)
    {
        DisplayName = displayName;
    }

    public string DisplayName { get; }
}

public interface IBuildingConstructionMaterialValidator
{
    void ValidateConstructionMaterialsOrThrow(
        Func<string, bool> itemDefinitionExists = null);
}

public interface IBuildingWorkCompletionAbility
{
}

// A definition declares executable capability; concrete execution is bound by the scene dispatcher.
public interface IBuildingRuntimeAbilityDefinition
{
}

public interface IBuildingStockCategorySignal
{
    IEnumerable<StockCategory> GetStockCategorySignals();
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class BuildingAbilityCollection : ISerializationCallbackReceiver
{
    [SerializeReference, SerializeField]
    private List<BuildingAbility> items = new List<BuildingAbility>();
    [NonSerialized] private IReadOnlyList<BuildingAbility> itemsView;
    [NonSerialized] private int revision;

    public int Revision => revision;

    public void NotifyAuthoringChanged() => revision++;
    void ISerializationCallbackReceiver.OnBeforeSerialize() { }
    void ISerializationCallbackReceiver.OnAfterDeserialize()
    {
        itemsView = null;
        revision++;
    }

    public IReadOnlyList<BuildingAbility> Items
    {
        get
        {
            items ??= new List<BuildingAbility>();
            return itemsView ??= ReadOnlyView.List(items);
        }
    }

    public int Count => items?.Count ?? 0;

    public void Add(BuildingAbility ability)
    {
        if (ability == null)
        {
            throw new ArgumentNullException(nameof(ability));
        }

        items ??= new List<BuildingAbility>();
        Type abilityType = ability.GetType();
        if (!abilityType.IsSerializable)
        {
            throw new InvalidOperationException(
                $"Building ability '{abilityType.FullName}' must be marked Serializable.");
        }

        if (items.Any(candidate => candidate != null
                && candidate.GetType() == abilityType))
        {
            throw new InvalidOperationException(
                $"Building ability type '{abilityType.FullName}' is already registered.");
        }

        items.Add(ability);
        revision++;
    }

    public int RemoveNullEntries()
    {
        int removed = items?.RemoveAll(ability => ability == null) ?? 0;
        if (removed > 0) revision++;
        return removed;
    }

    public int Remove<TAbility>()
        where TAbility : BuildingAbility
    {
        int removed = items?.RemoveAll(ability => ability is TAbility) ?? 0;
        if (removed > 0) revision++;
        return removed;
    }

    public int EnsureStableIds()
    {
        int changed = 0;
        if (items == null)
        {
            return changed;
        }

        foreach (BuildingAbility ability in items)
        {
            if (ability != null && ability.EnsureStableId())
            {
                changed++;
            }
        }

        if (changed > 0) revision++;
        return changed;
    }

    public void ValidateOrThrow(string ownerDescription)
    {
        if (items == null)
        {
            return;
        }

        string owner = string.IsNullOrWhiteSpace(ownerDescription)
            ? "Building ability collection"
            : ownerDescription;
        HashSet<Type> types = new HashSet<Type>();
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < items.Count; index++)
        {
            BuildingAbility ability = items[index];
            if (ability == null)
            {
                throw new InvalidOperationException(
                    $"{owner} contains a null or missing ability at index {index}.");
            }

            Type abilityType = ability.GetType();
            if (!abilityType.IsSerializable)
            {
                throw new InvalidOperationException(
                    $"{owner} ability '{abilityType.FullName}' must be marked Serializable.");
            }

            if (!types.Add(abilityType))
            {
                throw new InvalidOperationException(
                    $"{owner} contains duplicate ability type '{abilityType.FullName}'.");
            }

            string abilityId = ability.AbilityId;
            if (string.IsNullOrWhiteSpace(abilityId))
            {
                throw new InvalidOperationException(
                    $"{owner} ability '{abilityType.FullName}' has no stable ability ID.");
            }

            if (abilityId.Contains(':'))
            {
                throw new InvalidOperationException(
                    $"{owner} ability '{abilityType.FullName}' ID '{abilityId}' cannot contain ':'.");
            }

            if (!ids.Add(abilityId))
            {
                throw new InvalidOperationException(
                    $"{owner} contains duplicate ability ID '{abilityId}'.");
            }

            if (ability is IBuildingConstructionMaterialValidator validator)
            {
                validator.ValidateConstructionMaterialsOrThrow();
            }
        }
    }

    public bool TryGet<TAbility>(out TAbility ability)
        where TAbility : BuildingAbility
    {
        if (items != null)
        {
            foreach (BuildingAbility candidate in items)
            {
                if (candidate is TAbility typed)
                {
                    ability = typed;
                    return true;
                }
            }
        }

        ability = null;
        return false;
    }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public abstract class BuildingAbility
{
    [ContentIdentity, SerializeField, InspectorName("설정 능력 ID")]
    private string abilityId;

    protected BuildingAbility()
    {
        abilityId = GetType().Name;
    }

    public string AbilityId => abilityId?.Trim() ?? string.Empty;

    internal bool EnsureStableId()
    {
        if (!string.IsNullOrWhiteSpace(abilityId))
        {
            string normalized = abilityId.Trim();
            if (string.Equals(abilityId, normalized, StringComparison.Ordinal))
            {
                return false;
            }

            abilityId = normalized;
            return true;
        }

        abilityId = GetType().Name;
        return true;
    }
}

// 저장 모듈의 주소로 AbilityId를 쓰는 정의. 게시 후 ID 변경을 보호한다.
public interface IBuildingPersistentAbilityDefinition : IBuildingRuntimeAbilityDefinition
{
}
