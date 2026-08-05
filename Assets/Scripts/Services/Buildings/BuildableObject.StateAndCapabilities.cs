using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal sealed class BuildableObjectStateAndCapabilityController
{
    private readonly MonoBehaviour host;
    private readonly Action markDynamicStateDirty;
    private readonly List<IBuildingStateModule> runtimeStateModules =
        new List<IBuildingStateModule>();

    internal BuildableObjectStateAndCapabilityController(
        MonoBehaviour host,
        Action markDynamicStateDirty)
    {
        this.host = host ?? throw new ArgumentNullException(nameof(host));
        this.markDynamicStateDirty = markDynamicStateDirty
            ?? throw new ArgumentNullException(nameof(markDynamicStateDirty));
    }

    internal void RestoreFacilityState(
        FacilityRuntimeState target,
        FacilityRuntimeState restored)
    {
        (target ?? throw new ArgumentNullException(nameof(target)))
            .CopyFrom(restored);
        markDynamicStateDirty();
    }

    internal void RecordCompletedWorkCycle(FacilityRuntimeState state)
    {
        (state ?? throw new ArgumentNullException(nameof(state)))
            .completedWorkCycles++;
        markDynamicStateDirty();
    }

    internal void SetCleanliness(FacilityRuntimeState state, float value)
    {
        (state ?? throw new ArgumentNullException(nameof(state))).cleanliness =
            Mathf.Clamp(value, 0f, 100f);
        markDynamicStateDirty();
    }

    internal IReadOnlyList<IBuildingStateModule> GetStateModules()
    {
        List<IBuildingStateModule> modules = new(runtimeStateModules);
        MonoBehaviour[] components = host.GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour component in components)
        {
            if (component is IBuildingStateModule module
                && !modules.Contains(module))
            {
                modules.Add(module);
            }
        }

        return modules;
    }

    internal void ResetStateModules()
    {
        runtimeStateModules.Clear();
    }

    internal void RegisterStateModule(IBuildingStateModule module)
    {
        if (module == null)
        {
            throw new ArgumentNullException(nameof(module));
        }

        string moduleId = module.ModuleId?.Trim();
        if (string.IsNullOrWhiteSpace(moduleId))
        {
            throw new InvalidOperationException(
                $"{host.GetType().Name} '{host.name}' cannot register a state module without an ID.");
        }

        if (module.CurrentVersion <= 0)
        {
            throw new InvalidOperationException(
                $"{host.GetType().Name} '{host.name}' state module '{moduleId}' has invalid version {module.CurrentVersion}.");
        }

        if (runtimeStateModules.Any(candidate => candidate != null
                && string.Equals(
                    candidate.ModuleId?.Trim(),
                    moduleId,
                    StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"{host.GetType().Name} '{host.name}' already registered state module '{moduleId}'.");
        }

        runtimeStateModules.Add(module);
    }

    internal bool TryGetStateModule<TModule>(
        string moduleId,
        out TModule module)
        where TModule : class, IBuildingStateModule
    {
        foreach (IBuildingStateModule candidate in runtimeStateModules)
        {
            if (candidate is TModule typed
                && string.Equals(
                    candidate.ModuleId,
                    moduleId,
                    StringComparison.Ordinal))
            {
                module = typed;
                return true;
            }
        }

        module = null;
        return false;
    }

    internal TModule RequireStateModule<TModule>(string moduleId)
        where TModule : class, IBuildingStateModule
    {
        if (TryGetStateModule(moduleId, out TModule module))
        {
            return module;
        }

        throw new InvalidOperationException(
            $"{host.GetType().Name} '{host.name}' is missing runtime state module '{moduleId}'.");
    }

    internal TDependency RequireDependency<TDependency>(TDependency dependency)
        where TDependency : class
    {
        bool missing = dependency == null
            || (dependency is UnityEngine.Object unityObject
                && unityObject == null);
        if (!missing)
        {
            return dependency;
        }

        throw new InvalidOperationException(
            $"{host.GetType().Name} on '{GetDependencyLocation()}' requires {typeof(TDependency).Name}, but it was not injected. "
            + "Create the object through the DungeonRuntimeLifetimeScope or inject it explicitly in tests.");
    }

    internal static bool HasPendingEquipmentCraftWork(
        BuildingSO buildingData,
        IBuildingEquipmentCraftingRuntimePort runtime)
    {
        IBuildingEquipmentCraftingDefinition crafting = buildingData?.Abilities
            .OfType<IBuildingEquipmentCraftingDefinition>()
            .FirstOrDefault();
        return crafting != null
            && runtime != null
            && runtime.HasPendingCraftWork(crafting.CraftableEquipmentIds);
    }

    private string GetDependencyLocation()
    {
        Transform current = host.transform;
        string path = current.name;
        while (current.parent != null)
        {
            current = current.parent;
            path = current.name + "/" + path;
        }

        string sceneName = host.gameObject.scene.IsValid()
            ? host.gameObject.scene.name
            : "NoScene";
        return sceneName + ":" + path;
    }
}
