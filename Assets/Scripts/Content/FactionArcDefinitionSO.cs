using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "FactionArc", menuName = "DungeonStory/V20/Faction Arc")]
public sealed class FactionArcDefinitionSO : V20AuthoredContentSO
{
    public string factionId = string.Empty;
    public List<string> chapterIds = new();
    public List<string> contractIds = new();
    public List<string> relicItemIds = new();

    public override IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = base.ValidateDefinition().ToList();
        if (string.IsNullOrWhiteSpace(factionId)) errors.Add($"'{StableId}' requires a faction id.");
        if ((chapterIds ?? new()).Count != 6) errors.Add($"'{StableId}' requires exactly six chapters.");
        if ((contractIds ?? new()).Count != 3) errors.Add($"'{StableId}' requires exactly three contracts.");
        if ((relicItemIds ?? new()).Count != 3) errors.Add($"'{StableId}' requires exactly three relics.");
        return errors;
    }
}
