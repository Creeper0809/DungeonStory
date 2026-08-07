using System;
using UnityEngine;

[CreateAssetMenu(
    fileName = "FuneralCulture",
    menuName = "DungeonStory/Population/Funeral Culture")]
public sealed class FuneralCultureSO : ScriptableObject
{
    public string cultureId = string.Empty;
    public string speciesTag = string.Empty;
    public string ritualName = string.Empty;
    public string requiredFacilityTag = string.Empty;
    public CharacterSpeciesId SpeciesId => new(speciesTag);
}
