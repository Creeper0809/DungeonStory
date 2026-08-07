using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public sealed class EnemyIndividualAptitudeSaveData
{
    public string skillId = string.Empty;
    public int value;
}

[Serializable]
public sealed class EnemyIndividualSaveData
{
    public string characterId = string.Empty;
    public string enemyArchetypeId = string.Empty;
    public string originFactionId = string.Empty;
    public string phenotypeSpeciesId = string.Empty;
    public string visualVariantId = string.Empty;
    public string displayName = string.Empty;
    public string backgroundId = string.Empty;
    public string cultureId = string.Empty;
    public string ambitionId = string.Empty;
    public string militaryTrainingId = string.Empty;
    public int chronologicalAgeDays;
    public double biologicalAgeDayUnits;
    public int birthdayDayOfYear = 1;
    public float loyalty;
    public float combatStatMultiplier = 1f;
    public List<string> generalTraitIds = new();
    public List<string> expressedHeritableTraitIds = new();
    public List<string> latentHeritableTraitIds = new();
    public List<EnemyIndividualAptitudeSaveData> innateAptitudes = new();

    public EnemyIndividualSaveData Clone() => new()
    {
        characterId = characterId,
        enemyArchetypeId = enemyArchetypeId,
        originFactionId = originFactionId,
        phenotypeSpeciesId = phenotypeSpeciesId,
        visualVariantId = visualVariantId,
        displayName = displayName,
        backgroundId = backgroundId,
        cultureId = cultureId,
        ambitionId = ambitionId,
        militaryTrainingId = militaryTrainingId,
        chronologicalAgeDays = chronologicalAgeDays,
        biologicalAgeDayUnits = biologicalAgeDayUnits,
        birthdayDayOfYear = birthdayDayOfYear,
        loyalty = loyalty,
        combatStatMultiplier = combatStatMultiplier,
        generalTraitIds = new List<string>(generalTraitIds ?? new List<string>()),
        expressedHeritableTraitIds = new List<string>(expressedHeritableTraitIds ?? new List<string>()),
        latentHeritableTraitIds = new List<string>(latentHeritableTraitIds ?? new List<string>()),
        innateAptitudes = (innateAptitudes ?? new List<EnemyIndividualAptitudeSaveData>())
            .Select(value => new EnemyIndividualAptitudeSaveData
            {
                skillId = value.skillId,
                value = value.value
            })
            .ToList()
    };
}
