using System.Collections.Generic;
using System.Linq;
using DungeonStory.Buildings;
using UnityEngine;

public enum GuestRequestKind { LuxuryMeal, Medical, Trade, Spectacle, Refuge, Research, Armament }

[CreateAssetMenu(fileName = "GuestRequest", menuName = "DungeonStory/V20/Guest Request")]
public sealed class GuestRequestDefinitionSO : V20AuthoredContentSO
{
    [TextArea, Tooltip("Retained compatibility asset; a reason prevents publication in the active catalog.")]
    public string retirementReason = string.Empty;
    public bool IsRetired => !string.IsNullOrWhiteSpace(retirementReason);
    public GuestRequestKind kind;
    public ExperienceEventRiskTier riskTier;
    [TextArea] public string riskReason = string.Empty;
    [Min(1)] public int deadlineDays = 5;
    public V20ContentRequirementSet serviceRequirements = new();
    public FacilityVenueRequirements venueRequirements = new();
    public List<V20ContentEffect> successEffects = new();
    public List<V20ContentEffect> failureEffects = new();

    public override IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = base.ValidateDefinition().ToList();
        if (IsRetired) errors.Add($"'{StableId}' is retired: {retirementReason}");
        errors.AddRange(SocietyEventRiskContract.Validate(
            StableId,
            riskTier,
            riskReason));
        errors.AddRange((serviceRequirements ?? new()).Validate(StableId));
        errors.AddRange((venueRequirements ?? new()).Validate(StableId));
        if ((serviceRequirements?.facilities?.Count ?? 0) > 0)
            errors.Add($"'{StableId}' must author facility conditions through venue requirements only.");
        if (successEffects == null || successEffects.Count == 0 || successEffects.Any(value => value == null || !value.IsValid))
            errors.Add($"'{StableId}' requires success effects.");
        return errors;
    }
}
