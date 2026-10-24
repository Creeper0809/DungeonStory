using System;
using UnityEngine;

[CreateAssetMenu(
    fileName = "FuneralCulture",
    menuName = "DungeonStory/Population/Funeral Culture")]
public sealed class FuneralCultureSO : ScriptableObject
{
    [ContentIdentity] public string cultureId = string.Empty;
    [ContentReference(ContentReferenceKind.Species)]
    public string speciesTag = string.Empty;
    public string ritualName = string.Empty;
    [ContentReference(ContentReferenceKind.BuildingSemanticTag)]
    public string requiredFacilityTag = string.Empty;
    public CharacterSpeciesId SpeciesId => new(speciesTag);
}
