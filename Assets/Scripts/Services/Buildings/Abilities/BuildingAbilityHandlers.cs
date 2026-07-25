using System;
using System.Collections.Generic;

public readonly struct BuildingAbilityWorkContext
{
    public BuildingAbilityWorkContext(
        CharacterActor actor,
        BuildableObject building,
        WorkTypeId workTypeId)
    {
        if (!workTypeId.IsValid)
        {
            throw new ArgumentException(
                "Building ability work context requires a valid work type id.",
                nameof(workTypeId));
        }

        Actor = actor;
        Building = building;
        WorkTypeId = workTypeId;
        WorkType = WorkTypeCatalog.TryGet(workTypeId, out WorkTypeDefinition definition)
            ? definition.Type
            : FacilityWorkType.None;
    }

    public CharacterActor Actor { get; }
    public BuildableObject Building { get; }
    public WorkTypeId WorkTypeId { get; }
    internal FacilityWorkType WorkType { get; }
}

public interface IBuildingAbilityWorkCompletedHandler
{
    IReadOnlyCollection<Type> AbilityTypes { get; }
    int Apply(BuildingAbility ability, BuildingAbilityWorkContext context);
}

public interface IBuildingWorkCompletionFallbackHandler
{
    IReadOnlyCollection<WorkTypeId> WorkTypeIds { get; }
    int Apply(BuildingAbilityWorkContext context);
}

public interface IBuildingAbilityRuntimeDispatcher
{
    int ApplyWorkCompleted(
        CharacterActor actor,
        BuildableObject building,
        WorkTypeId workTypeId);
}

public sealed class BuildingAbilityRuntimeDispatcher :
    IBuildingAbilityRuntimeDispatcher
{
    private readonly Dictionary<Type, IBuildingAbilityWorkCompletedHandler> workHandlers;
    private readonly Dictionary<WorkTypeId, IBuildingWorkCompletionFallbackHandler> fallbackHandlers;

    public BuildingAbilityRuntimeDispatcher(
        IReadOnlyList<IBuildingAbilityWorkCompletedHandler> registeredWorkHandlers,
        IReadOnlyList<IBuildingWorkCompletionFallbackHandler> registeredFallbackHandlers)
    {
        workHandlers = BuildTypeIndex(registeredWorkHandlers);
        fallbackHandlers = BuildFallbackIndex(registeredFallbackHandlers);
    }

    public int ApplyWorkCompleted(
        CharacterActor actor,
        BuildableObject building,
        WorkTypeId workTypeId)
    {
        if (building?.BuildingData == null)
        {
            return 0;
        }

        building.RecordCompletedWorkCycle();
        BuildingAbilityWorkContext context =
            new BuildingAbilityWorkContext(actor, building, workTypeId);
        int totalProduced = 0;
        foreach (BuildingAbility ability in building.BuildingData.Abilities)
        {
            if (ability == null)
            {
                continue;
            }

            if (workHandlers.TryGetValue(
                    ability.GetType(),
                    out IBuildingAbilityWorkCompletedHandler handler))
            {
                totalProduced += Math.Max(0, handler.Apply(ability, context));
                continue;
            }

            if (ability is IBuildingWorkCompletionAbility)
            {
                throw new InvalidOperationException(
                    $"No work-completion handler is registered for '{ability.GetType().FullName}'.");
            }
        }

        if (totalProduced <= 0
            && fallbackHandlers.TryGetValue(
                workTypeId,
                out IBuildingWorkCompletionFallbackHandler fallback))
        {
            totalProduced += Math.Max(0, fallback.Apply(context));
        }

        return totalProduced;
    }

    private static Dictionary<Type, IBuildingAbilityWorkCompletedHandler> BuildTypeIndex(
        IReadOnlyList<IBuildingAbilityWorkCompletedHandler> handlers)
    {
        Dictionary<Type, IBuildingAbilityWorkCompletedHandler> index =
            new Dictionary<Type, IBuildingAbilityWorkCompletedHandler>();
        foreach (IBuildingAbilityWorkCompletedHandler handler in
                 handlers ?? Array.Empty<IBuildingAbilityWorkCompletedHandler>())
        {
            if (handler == null)
            {
                throw new InvalidOperationException(
                    "A null building ability work handler was registered.");
            }

            IReadOnlyCollection<Type> types = handler.AbilityTypes;
            if (types == null || types.Count == 0)
            {
                throw new InvalidOperationException(
                    $"{handler.GetType().Name} does not declare an ability type.");
            }

            foreach (Type type in types)
            {
                if (type == null || !typeof(BuildingAbility).IsAssignableFrom(type))
                {
                    throw new InvalidOperationException(
                        $"{handler.GetType().Name} declares invalid ability type '{type}'.");
                }

                if (!index.TryAdd(type, handler))
                {
                    throw new InvalidOperationException(
                        $"Duplicate building ability work handler for '{type.FullName}'.");
                }
            }
        }

        return index;
    }

    private static Dictionary<WorkTypeId, IBuildingWorkCompletionFallbackHandler>
        BuildFallbackIndex(
            IReadOnlyList<IBuildingWorkCompletionFallbackHandler> handlers)
    {
        Dictionary<WorkTypeId, IBuildingWorkCompletionFallbackHandler> index =
            new Dictionary<WorkTypeId, IBuildingWorkCompletionFallbackHandler>();
        foreach (IBuildingWorkCompletionFallbackHandler handler in
                 handlers ?? Array.Empty<IBuildingWorkCompletionFallbackHandler>())
        {
            if (handler == null)
            {
                throw new InvalidOperationException(
                    "A null building work fallback handler was registered.");
            }

            foreach (WorkTypeId id in handler.WorkTypeIds ??
                     Array.Empty<WorkTypeId>())
            {
                if (!id.IsValid || !index.TryAdd(id, handler))
                {
                    throw new InvalidOperationException(
                        $"Duplicate or invalid building work fallback id '{id}'.");
                }
            }
        }

        return index;
    }
}
