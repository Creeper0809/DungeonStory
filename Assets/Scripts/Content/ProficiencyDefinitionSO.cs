using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "ProficiencyDefinition",
    menuName = "DungeonStory/Character/Proficiency Definition")]
public sealed class ProficiencyDefinitionSO : V20AuthoredContentSO
{
    [SerializeField] private int displayOrder;

    public CharacterProficiencyId ProficiencyId =>
        new CharacterProficiencyId(StableId);
    public int DisplayOrder => displayOrder;

    public override IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = new(base.ValidateDefinition());
        if (!ProficiencyId.IsValid)
        {
            errors.Add($"'{StableId}' must use the proficiency:* id namespace.");
        }

        return errors;
    }

#if UNITY_EDITOR
    public void ConfigureProficiency(int order)
    {
        displayOrder = Math.Max(0, order);
    }
#endif
}
