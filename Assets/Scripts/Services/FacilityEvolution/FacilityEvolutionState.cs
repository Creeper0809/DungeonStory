using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class FacilityEvolutionHistoryEntry
{
    public string evolutionId;
    public string fromFacility;
    public string toFacility;
    public string summary;
    public int sequence;

    public FacilityEvolutionHistoryEntry Clone()
    {
        return new FacilityEvolutionHistoryEntry
        {
            evolutionId = evolutionId,
            fromFacility = fromFacility,
            toFacility = toFacility,
            summary = summary,
            sequence = sequence
        };
    }
}

public sealed class FacilityEvolutionStateSnapshot
{
    public string baseFacilityId;
    public string currentFacilityId;
    public int starGrade = 1;
    public string[] lineageTags = Array.Empty<string>();
    public string[] mutationTags = Array.Empty<string>();
    public string lastIdentitySummary;
    public FacilityEvolutionValue[] lastIdentityPressures = Array.Empty<FacilityEvolutionValue>();
    public string[] dominantIdentityTags = Array.Empty<string>();
    public List<FacilityEvolutionHistoryEntry> evolutionHistory = new List<FacilityEvolutionHistoryEntry>();
}

public class FacilityEvolutionStateComponent : MonoBehaviour
{
    [SerializeField] private string baseFacilityId;
    [SerializeField] private string currentFacilityId;
    [SerializeField] private int starGrade = 1;
    [SerializeField] private string[] lineageTags = Array.Empty<string>();
    [SerializeField] private string[] mutationTags = Array.Empty<string>();
    [SerializeField] private string lastIdentitySummary;
    [SerializeField] private FacilityEvolutionValue[] lastIdentityPressures = Array.Empty<FacilityEvolutionValue>();
    [SerializeField] private string[] dominantIdentityTags = Array.Empty<string>();
    [SerializeField] private List<FacilityEvolutionHistoryEntry> evolutionHistory =
        new List<FacilityEvolutionHistoryEntry>();

    public string BaseFacilityId => baseFacilityId;
    public string CurrentFacilityId => currentFacilityId;
    public int StarGrade => Mathf.Max(1, starGrade);
    public IReadOnlyList<string> LineageTags => EventPayloadSnapshot.Copy(lineageTags);
    public IReadOnlyList<string> MutationTags => EventPayloadSnapshot.Copy(mutationTags);
    public string LastIdentitySummary => lastIdentitySummary ?? string.Empty;
    public IReadOnlyList<FacilityEvolutionValue> LastIdentityPressures =>
        EventPayloadSnapshot.Copy(lastIdentityPressures);
    public IReadOnlyList<string> DominantIdentityTags => EventPayloadSnapshot.Copy(dominantIdentityTags);
    public IReadOnlyList<FacilityEvolutionHistoryEntry> EvolutionHistory => Array.AsReadOnly(
        (evolutionHistory ?? new List<FacilityEvolutionHistoryEntry>())
            .Where((entry) => entry != null)
            .Select((entry) => entry.Clone())
            .ToArray());

    public FacilityEvolutionStateSnapshot CreateSnapshot()
    {
        return new FacilityEvolutionStateSnapshot
        {
            baseFacilityId = baseFacilityId,
            currentFacilityId = currentFacilityId,
            starGrade = StarGrade,
            lineageTags = LineageTags.ToArray(),
            mutationTags = MutationTags.ToArray(),
            lastIdentitySummary = LastIdentitySummary,
            lastIdentityPressures = LastIdentityPressures.ToArray(),
            dominantIdentityTags = DominantIdentityTags.ToArray(),
            evolutionHistory = evolutionHistory
                .Where((entry) => entry != null)
                .Select((entry) => entry.Clone())
                .ToList()
        };
    }

    public void ApplySnapshot(FacilityEvolutionStateSnapshot snapshot)
    {
        if (snapshot == null)
        {
            return;
        }

        baseFacilityId = snapshot.baseFacilityId ?? string.Empty;
        currentFacilityId = snapshot.currentFacilityId ?? string.Empty;
        starGrade = Mathf.Max(1, snapshot.starGrade);
        lineageTags = snapshot.lineageTags?
            .Where((tag) => !string.IsNullOrWhiteSpace(tag))
            .Distinct()
            .ToArray()
            ?? Array.Empty<string>();
        mutationTags = snapshot.mutationTags?
            .Where((tag) => !string.IsNullOrWhiteSpace(tag))
            .Distinct()
            .ToArray()
            ?? Array.Empty<string>();
        lastIdentitySummary = snapshot.lastIdentitySummary ?? string.Empty;
        lastIdentityPressures = snapshot.lastIdentityPressures?
            .Where((entry) => !string.IsNullOrWhiteSpace(entry.key))
            .ToArray()
            ?? Array.Empty<FacilityEvolutionValue>();
        dominantIdentityTags = snapshot.dominantIdentityTags?
            .Where((tag) => !string.IsNullOrWhiteSpace(tag))
            .Distinct()
            .ToArray()
            ?? Array.Empty<string>();
        evolutionHistory = snapshot.evolutionHistory?
            .Where((entry) => entry != null)
            .Select((entry) => entry.Clone())
            .ToList()
            ?? new List<FacilityEvolutionHistoryEntry>();
    }

    public void InitializeIfNeeded(BuildableObject facility)
    {
        if (facility == null)
        {
            return;
        }

        string facilityId = FacilityEvolutionUtility.GetFacilityId(facility.BuildingData);
        if (string.IsNullOrWhiteSpace(baseFacilityId))
        {
            baseFacilityId = facilityId;
        }

        if (string.IsNullOrWhiteSpace(currentFacilityId))
        {
            currentFacilityId = facilityId;
        }

        starGrade = Mathf.Max(StarGrade, FacilityShopService.GetBuildingStar(facility.BuildingData));
        if (lineageTags == null || lineageTags.Length == 0)
        {
            lineageTags = FacilityEvolutionUtility.GetDefaultLineageTags(facility.BuildingData).ToArray();
        }
    }

    public void ApplyEvolution(
        BuildableObject fromFacility,
        BuildableObject toFacility,
        FacilityEvolutionRecipeSO recipe,
        FacilityEvolutionProposal proposal,
        string fromFacilityName = null,
        RoomProfile profile = null,
        IReadOnlyList<string> resolvedMutationTags = null)
    {
        if (toFacility == null || recipe == null)
        {
            return;
        }

        InitializeIfNeeded(toFacility);
        currentFacilityId = FacilityEvolutionUtility.GetFacilityId(toFacility.BuildingData);
        starGrade = Mathf.Max(1, recipe.resultStarGrade);

        HashSet<string> nextLineage = new HashSet<string>(LineageTags.Where((tag) => !string.IsNullOrWhiteSpace(tag)));
        foreach (string tag in FacilityEvolutionUtility.GetDefaultLineageTags(toFacility.BuildingData))
        {
            nextLineage.Add(tag);
        }

        lineageTags = nextLineage.ToArray();

        HashSet<string> nextMutation = new HashSet<string>(MutationTags.Where((tag) => !string.IsNullOrWhiteSpace(tag)));
        IEnumerable<string> mutationCandidates = resolvedMutationTags ?? proposal.MutationTagSuggestions;
        if (mutationCandidates != null)
        {
            foreach (string tag in mutationCandidates.Where((tag) => !string.IsNullOrWhiteSpace(tag)))
            {
                if (recipe.allowedMutationTags != null
                    && recipe.allowedMutationTags.Contains(tag))
                {
                    nextMutation.Add(tag);
                }
            }
        }

        mutationTags = nextMutation.ToArray();
        lastIdentitySummary = proposal.FacilityIdentitySummary ?? string.Empty;
        CaptureIdentity(profile);

        evolutionHistory.Add(new FacilityEvolutionHistoryEntry
        {
            evolutionId = recipe.EffectiveId,
            fromFacility = !string.IsNullOrWhiteSpace(fromFacilityName)
                ? fromFacilityName
                : FacilityShopService.GetBuildingName(fromFacility != null ? fromFacility.BuildingData : null),
            toFacility = FacilityShopService.GetBuildingName(toFacility.BuildingData),
            summary = proposal.FlavorText,
            sequence = evolutionHistory.Count + 1
        });
    }

    private void CaptureIdentity(RoomProfile profile)
    {
        if (profile == null || profile.IdentityPressures == null)
        {
            lastIdentityPressures = Array.Empty<FacilityEvolutionValue>();
            dominantIdentityTags = Array.Empty<string>();
            return;
        }

        lastIdentityPressures = profile.IdentityPressures
            .Where((entry) => entry.Value > 0.01f)
            .OrderByDescending((entry) => entry.Value)
            .Take(12)
            .Select((entry) => new FacilityEvolutionValue(entry.Key, entry.Value))
            .ToArray();
        dominantIdentityTags = lastIdentityPressures
            .Where((entry) => entry.value >= 0.35f)
            .Select((entry) => entry.key)
            .Take(6)
            .ToArray();
    }
}
