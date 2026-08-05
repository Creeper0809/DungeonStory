using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public interface IGridGhostObjectResolver
{
    GridGhostObject Resolve(Component owner, GridGhostObject configuredGhostObject);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class GridGhostObjectResolver : IGridGhostObjectResolver
{
    public GridGhostObject Resolve(Component owner, GridGhostObject configuredGhostObject)
    {
        if (configuredGhostObject != null)
        {
            return configuredGhostObject;
        }

        if (owner == null)
        {
            throw new ArgumentNullException(nameof(owner));
        }

        GridGhostObject component = owner.GetComponent<GridGhostObject>();
        if (component != null)
        {
            return component;
        }

        throw new InvalidOperationException(
            $"{owner.GetType().Name} requires a serialized or attached {nameof(GridGhostObject)}.");
    }
}
