using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

/// <summary>
/// Minimal read-only port for authored ScriptableObject definitions. Domain assemblies
/// depend on this contract while the composition-owned catalog remains the implementation.
/// </summary>
[MovedFrom(true, sourceAssembly: "DungeonStory.Economy")]
public interface IGameContentDefinitionSource
{
    IReadOnlyList<T> GetAll<T>() where T : ScriptableObject;
    T RequireSingle<T>() where T : ScriptableObject;
}
