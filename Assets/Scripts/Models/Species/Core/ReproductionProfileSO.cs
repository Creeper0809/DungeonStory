using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(
    fileName = "ReproductionProfile",
    menuName = "DungeonStory/Species/Reproduction Profile")]
public sealed class ReproductionProfileSO : ScriptableObject
{
    public string definitionId = string.Empty;
    public string speciesTag = string.Empty;
    public ReproductionMode mode;
    [Range(0f, 1f)] public float baseSuccessChance = 0.35f;
    public float viableTemperatureMinimum = 10f;
    public float viableTemperatureMaximum = 32f;
    public List<ReproductionPhaseDefinition> phases = new();

    public CharacterSpeciesId SpeciesId => new(speciesTag);
    public int TotalDurationDays => (phases ?? new List<ReproductionPhaseDefinition>())
        .Where(value => value != null)
        .Sum(value => Math.Max(1, value.durationDays));

    public IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = new();
        if (string.IsNullOrWhiteSpace(definitionId) || !SpeciesId.IsValid)
            errors.Add("Definition id and species id are required.");
        if (baseSuccessChance <= 0f || baseSuccessChance > 1f)
            errors.Add("Base success chance must be in (0, 1].");
        if (viableTemperatureMaximum <= viableTemperatureMinimum)
            errors.Add("Viable temperature range is invalid.");
        if (phases == null || phases.Count == 0 || phases.Any(value => value == null))
            errors.Add("At least one complete reproduction phase is required.");
        else if (phases.Select(value => value.phase).Distinct().Count() != phases.Count)
            errors.Add("Reproduction phases must be unique and ordered once.");
        else if (mode != ReproductionMode.GolemAssembly
                 && phases[0].phase != ReproductionPhaseKind.Attempt)
        {
            errors.Add(
                "Biological reproduction must begin with an Attempt phase so base success chance is applied.");
        }
        else if (mode == ReproductionMode.GolemAssembly
                 && phases.Any(value => value.phase == ReproductionPhaseKind.Attempt))
        {
            errors.Add("Golem assembly cannot contain a biological Attempt phase.");
        }
        return errors;
    }
}
