using System;
using System.Collections.Generic;
using UnityEngine;

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
}
