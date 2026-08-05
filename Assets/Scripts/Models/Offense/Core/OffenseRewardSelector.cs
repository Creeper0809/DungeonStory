using System.Collections.Generic;

public static class OffenseRewardSelectionPolicy
{
    public static int SelectRareFacilitySourceIndex(
        IReadOnlyList<OffenseRareFacilityCandidateSnapshot> candidates,
        IReadOnlyCollection<int> excludedBuildingIds)
    {
        int selectedSourceIndex = -1;
        int selectedStar = 0;
        int selectedBuildingId = 0;
        bool hasSelection = false;

        if (candidates == null)
        {
            return selectedSourceIndex;
        }

        for (int index = 0; index < candidates.Count; index++)
        {
            OffenseRareFacilityCandidateSnapshot candidate = candidates[index];
            if (candidate.IsGridMovement
                || candidate.IsWall
                || candidate.Star < 2
                || Contains(excludedBuildingIds, candidate.BuildingId))
            {
                continue;
            }

            if (!hasSelection
                || candidate.Star < selectedStar
                || (candidate.Star == selectedStar
                    && candidate.BuildingId < selectedBuildingId))
            {
                selectedSourceIndex = candidate.SourceIndex;
                selectedStar = candidate.Star;
                selectedBuildingId = candidate.BuildingId;
                hasSelection = true;
            }
        }

        return selectedSourceIndex;
    }

    public static int SelectBlueprintSourceIndex(
        IReadOnlyList<OffenseBlueprintCandidateSnapshot> candidates)
    {
        int selectedSourceIndex = -1;
        int selectedRarity = 0;
        int selectedBlueprintId = 0;
        bool hasSelection = false;

        if (candidates == null)
        {
            return selectedSourceIndex;
        }

        for (int index = 0; index < candidates.Count; index++)
        {
            OffenseBlueprintCandidateSnapshot candidate = candidates[index];
            if (!candidate.IsEligible
                || candidate.IsRewardAcquired
                || candidate.IsShopAcquired
                || candidate.IsResearchCompleted)
            {
                continue;
            }

            if (!hasSelection
                || candidate.Rarity > selectedRarity
                || (candidate.Rarity == selectedRarity
                    && candidate.BlueprintId < selectedBlueprintId))
            {
                selectedSourceIndex = candidate.SourceIndex;
                selectedRarity = candidate.Rarity;
                selectedBlueprintId = candidate.BlueprintId;
                hasSelection = true;
            }
        }

        return selectedSourceIndex;
    }

    private static bool Contains(
        IReadOnlyCollection<int> values,
        int expected)
    {
        if (values == null)
        {
            return false;
        }

        foreach (int value in values)
        {
            if (value == expected)
            {
                return true;
            }
        }

        return false;
    }
}
