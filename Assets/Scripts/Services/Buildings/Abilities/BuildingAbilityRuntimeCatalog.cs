using System;
using System.Collections.Generic;

public interface IBuildingAbilityRuntimeFactory
{
    Type DefinitionType { get; }
    object Bind(BuildingAbility definition);
}

public sealed class BuildingAbilityRuntimeFactory<TDefinition, TRuntime> : IBuildingAbilityRuntimeFactory
    where TDefinition : BuildingAbility, IBuildingRuntimeAbilityDefinition
    where TRuntime : class
{
    private readonly Func<TDefinition, TRuntime> bind;

    public BuildingAbilityRuntimeFactory(Func<TDefinition, TRuntime> bind)
    {
        this.bind = bind ?? throw new ArgumentNullException(nameof(bind));
    }

    public Type DefinitionType => typeof(TDefinition);

    public object Bind(BuildingAbility definition)
    {
        if (definition is not TDefinition typed)
            throw new InvalidOperationException($"Runtime factory for '{typeof(TDefinition).FullName}' received '{definition?.GetType().FullName ?? "null"}'.");
        return bind(typed) ?? throw new InvalidOperationException($"Runtime factory for '{typeof(TDefinition).FullName}' returned null.");
    }
}

public sealed class BuildingAbilityRuntimeCatalog
{
    private readonly Dictionary<Type, IBuildingAbilityRuntimeFactory> factories = new();

    public BuildingAbilityRuntimeCatalog(IReadOnlyList<IBuildingAbilityRuntimeFactory> factories)
    {
        if (factories == null) throw new ArgumentNullException(nameof(factories));
        foreach (IBuildingAbilityRuntimeFactory factory in factories)
        {
            Type type = factory?.DefinitionType;
            if (type == null || !typeof(BuildingAbility).IsAssignableFrom(type)
                || !typeof(IBuildingRuntimeAbilityDefinition).IsAssignableFrom(type))
                throw new InvalidOperationException("A building runtime factory must declare an executable definition type.");
            if (!this.factories.TryAdd(type, factory))
                throw new InvalidOperationException($"Duplicate building runtime factory for '{type.FullName}'.");
        }
    }

    public IReadOnlyList<object> Bind(IReadOnlyList<BuildingAbility> definitions)
    {
        if (definitions == null) throw new ArgumentNullException(nameof(definitions));
        List<object> result = new();
        foreach (BuildingAbility definition in definitions)
        {
            if (definition == null) throw new InvalidOperationException("Cannot bind a null building ability definition.");
            if (definition is not IBuildingRuntimeAbilityDefinition) continue;
            if (!factories.TryGetValue(definition.GetType(), out IBuildingAbilityRuntimeFactory factory))
                throw new InvalidOperationException($"No runtime factory is registered for '{definition.GetType().FullName}'.");
            object runtime = factory.Bind(definition);
            if ((runtime is IBuildingRuntimeStateAbility)
                != (definition is IBuildingPersistentAbilityDefinition))
                throw new InvalidOperationException($"Saved-state capability and published identity declaration differ for '{definition.GetType().FullName}'.");
            if (runtime is not IBuildingRuntimeStateAbility && runtime is not IBuildingVisualRuntimeAbility
                && runtime is not IBuildingUseCompletedRuntimeAbility && runtime is not IBuildingExteriorWorkRuntimeAbility
                && runtime is not IBuildingWorkAmountRuntimeAbility && runtime is not IBuildingCrimeRiskModifier)
                throw new InvalidOperationException($"Runtime factory for '{definition.GetType().FullName}' has no executable capability.");
            result.Add(runtime);
        }
        return result.AsReadOnly();
    }

    public static BuildingAbilityRuntimeCatalog CreateBuiltIns(params IBuildingAbilityRuntimeFactory[] additionalFactories)
    {
        List<IBuildingAbilityRuntimeFactory> factories = new()
        {
            new BuildingAbilityRuntimeFactory<BuildingCoverAbility, BuildingCoverAbilityRuntime>(d => new(d)),
            new BuildingAbilityRuntimeFactory<BuildingStructuralIntegrityAbility, BuildingStructuralIntegrityAbilityRuntime>(d => new(d)),
            new BuildingAbilityRuntimeFactory<BuildingProductionAbility, BuildingProductionAbilityRuntime>(d => new(d)),
            new BuildingAbilityRuntimeFactory<BuildingLightingAbility, BuildingLightingAbilityRuntime>(d => new(d)),
            new BuildingAbilityRuntimeFactory<BuildingWorkAmountAbility, BuildingWorkAmountAbilityRuntime>(d => new(d)),
            new BuildingAbilityRuntimeFactory<BuildingExpeditionRecoveryAbility, BuildingExpeditionRecoveryAbilityRuntime>(d => new(d)),
            new BuildingAbilityRuntimeFactory<BuildingReceptionAbility, BuildingReceptionAbilityRuntime>(d => new(d)),
            new BuildingAbilityRuntimeFactory<BuildingPatrolPostAbility, BuildingPatrolPostAbilityRuntime>(d => new(d)),
            new BuildingAbilityRuntimeFactory<BuildingOutdoorRestAbility, BuildingOutdoorRestAbilityRuntime>(d => new(d)),
            new BuildingAbilityRuntimeFactory<BuildingExteriorMaintenanceAbility, BuildingExteriorMaintenanceAbilityRuntime>(d => new(d)),
            new BuildingAbilityRuntimeFactory<BuildingTrainingAbility, BuildingTrainingAbilityRuntime>(d => new(d)),
            new BuildingAbilityRuntimeFactory<BuildingSecurityAbility, BuildingSecurityAbilityRuntime>(d => new(d)),
            new BuildingAbilityRuntimeFactory<BuildingCrimeRiskModifierAbility, BuildingCrimeRiskModifierAbilityRuntime>(d => new(d))
        };
        factories.AddRange(additionalFactories ?? throw new ArgumentNullException(nameof(additionalFactories)));
        return new BuildingAbilityRuntimeCatalog(factories);
    }
}
