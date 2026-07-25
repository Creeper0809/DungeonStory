using System;

public interface IDefenseStatusRuntimeService
{
    DefenseStatusRuntime GetOrAdd(CharacterActor character);
    DefenseStatusRuntime Get(CharacterActor character);
    float TickStatuses(CharacterActor target, float deltaSeconds);
}

public sealed class DefenseStatusRuntimeService : IDefenseStatusRuntimeService
{
    private readonly IDefenseStatusRuntimeFactory runtimeFactory;

    public DefenseStatusRuntimeService(IDefenseStatusRuntimeFactory runtimeFactory)
    {
        this.runtimeFactory = runtimeFactory
            ?? throw new ArgumentNullException(nameof(runtimeFactory));
    }

    public DefenseStatusRuntime GetOrAdd(CharacterActor character)
    {
        return runtimeFactory.GetOrAdd(character);
    }

    public DefenseStatusRuntime Get(CharacterActor character)
    {
        return runtimeFactory.Get(character);
    }

    public float TickStatuses(CharacterActor target, float deltaSeconds)
    {
        if (target == null || target.IsDead)
        {
            return 0f;
        }

        DefenseStatusRuntime statusRuntime = Get(target);
        return statusRuntime != null
            ? statusRuntime.Tick(target, deltaSeconds)
            : 0f;
    }
}
