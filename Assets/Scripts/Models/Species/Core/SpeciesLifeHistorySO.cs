using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "SpeciesLifeHistory",
    menuName = "DungeonStory/Species/Life History")]
public sealed class SpeciesLifeHistorySO : ScriptableObject
{
    public string definitionId = string.Empty;
    public string speciesTag = string.Empty;
    [Tooltip("-1 means this stage does not exist.")]
    public int infantEndAgeYears = 1;
    [Tooltip("-1 means this stage does not exist.")]
    public int adolescentStartAgeYears = 6;
    [Min(0)] public int adultAgeYears = 8;
    [Min(1)] public int elderAgeYears = 42;
    [Min(0f)] public float untreatedExpectedLifeYears = 74.2f;
    public bool construct;

    public CharacterSpeciesId SpeciesId => new(speciesTag);

    public CharacterLifeStage ResolveStage(double biologicalAgeDayUnits)
    {
        double ageYears = Math.Max(0d, biologicalAgeDayUnits)
            / GameCalendarRules.DaysPerYear;
        if (ageYears >= elderAgeYears) return CharacterLifeStage.Elder;
        if (ageYears >= adultAgeYears) return CharacterLifeStage.Adult;
        if (adolescentStartAgeYears >= 0 && ageYears >= adolescentStartAgeYears)
        {
            return CharacterLifeStage.Adolescent;
        }
        if (infantEndAgeYears >= 0 && ageYears < infantEndAgeYears)
        {
            return CharacterLifeStage.Infant;
        }
        return CharacterLifeStage.Child;
    }

    public IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = new();
        if (string.IsNullOrWhiteSpace(definitionId)) errors.Add("Definition id is required.");
        if (!SpeciesId.IsValid) errors.Add("Species id is required.");
        if (adultAgeYears < 0 || elderAgeYears <= adultAgeYears)
            errors.Add("Adult and elder ages are not ordered.");
        if (construct)
        {
            if (infantEndAgeYears != -1 || adolescentStartAgeYears != -1 || adultAgeYears != 0)
                errors.Add("Constructs must activate as adults without child stages.");
        }
        else if (infantEndAgeYears < 0
                 || adolescentStartAgeYears < infantEndAgeYears
                 || adultAgeYears < adolescentStartAgeYears)
        {
            errors.Add("Biological life stages are not ordered.");
        }
        if (untreatedExpectedLifeYears <= elderAgeYears)
            errors.Add("Untreated expected life must exceed elder age.");
        return errors;
    }
}
