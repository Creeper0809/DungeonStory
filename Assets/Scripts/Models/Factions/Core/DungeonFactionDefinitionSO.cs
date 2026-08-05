using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Factions;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[CreateAssetMenu(
    menuName = "DungeonStory/Factions/Dungeon Faction",
    order = 0)]
public sealed class DungeonFactionDefinitionSO : ScriptableObject
{
    public const string ResourcePath = "SO/Factions/Dungeons";

    public string factionId = string.Empty;
    public string displayName = string.Empty;
    public string speciesTag = string.Empty;
    [TextArea] public string description = string.Empty;
    public string[] relationTags = Array.Empty<string>();
    public string[] tradeTags = Array.Empty<string>();
    public string reinforcementRole = string.Empty;
    public Sprite crest;
    public List<FactionCargoLine> tradeCargo = new List<FactionCargoLine>();
    public List<FactionCargoLine> supplyCargo = new List<FactionCargoLine>();

    public string StableId => factionId?.Trim() ?? string.Empty;

    public FactionDefinitionSnapshot ToSnapshot()
    {
        return new FactionDefinitionSnapshot(
            StableId,
            displayName,
            speciesTag,
            description,
            (relationTags ?? Array.Empty<string>()).ToArray(),
            (tradeTags ?? Array.Empty<string>()).ToArray(),
            reinforcementRole,
            (tradeCargo ?? new List<FactionCargoLine>())
                .Where(value => value != null)
                .Select(value => value.Clone())
                .ToArray(),
            (supplyCargo ?? new List<FactionCargoLine>())
                .Where(value => value != null)
                .Select(value => value.Clone())
                .ToArray());
    }
}
