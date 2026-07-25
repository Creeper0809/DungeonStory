using UnityEngine;

public interface IDefenseStatusRuntimeFactory
{
    DefenseStatusRuntime GetOrAdd(CharacterActor character);
    DefenseStatusRuntime Get(CharacterActor character);
}

public sealed class DefenseStatusRuntimeFactory : IDefenseStatusRuntimeFactory
{
    public DefenseStatusRuntime GetOrAdd(CharacterActor character)
    {
        if (character == null)
        {
            return null;
        }

        if (!character.TryGetComponent(out DefenseStatusRuntime runtime))
        {
            runtime = character.gameObject.AddComponent<DefenseStatusRuntime>();
        }

        return runtime;
    }

    public DefenseStatusRuntime Get(CharacterActor character)
    {
        return character != null && character.TryGetComponent(out DefenseStatusRuntime runtime)
            ? runtime
            : null;
    }
}
