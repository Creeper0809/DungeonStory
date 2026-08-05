using System;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct CircusCombatant : IEquatable<CircusCombatant>
{
    private readonly CircusCombatantIdentity identity;
    private readonly object runtime;
    private readonly Func<bool> isAlive;

    public CircusCombatant(
        CircusCombatantIdentity identity,
        object runtime,
        Func<bool> isAlive)
    {
        this.identity = identity;
        this.runtime = runtime;
        this.isAlive = isAlive ?? throw new ArgumentNullException(nameof(isAlive));
    }

    public string Id => identity.Id;
    public bool IsAlive => isAlive?.Invoke() == true;

    public T GetRuntime<T>() where T : class => runtime as T;

    public bool Equals(CircusCombatant other)
    {
        return identity.Equals(other.identity);
    }

    public override bool Equals(object obj)
    {
        return obj is CircusCombatant other && Equals(other);
    }

    public override int GetHashCode()
    {
        return identity.GetHashCode();
    }
}
