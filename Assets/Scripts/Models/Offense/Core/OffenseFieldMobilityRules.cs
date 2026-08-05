using System;
using System.Collections.Generic;

public readonly struct OffenseFieldMobilityMemberSnapshot
{
    public OffenseFieldMobilityMemberSnapshot(
        string characterId,
        bool downed,
        float locomotion,
        float sustain,
        bool stabilizationActive,
        float locomotionFloor,
        float sustainFloor,
        float maximumAllowedCarryWeight,
        float baseCarryLimit,
        float currentCarryWeight,
        float maximumHealth)
    {
        CharacterId = characterId ?? string.Empty;
        Downed = downed;
        Locomotion = locomotion;
        Sustain = sustain;
        StabilizationActive = stabilizationActive;
        LocomotionFloor = locomotionFloor;
        SustainFloor = sustainFloor;
        MaximumAllowedCarryWeight = maximumAllowedCarryWeight;
        BaseCarryLimit = baseCarryLimit;
        CurrentCarryWeight = currentCarryWeight;
        MaximumHealth = maximumHealth;
    }

    public string CharacterId { get; }
    public bool Downed { get; }
    public float Locomotion { get; }
    public float Sustain { get; }
    public bool StabilizationActive { get; }
    public float LocomotionFloor { get; }
    public float SustainFloor { get; }
    public float MaximumAllowedCarryWeight { get; }
    public float BaseCarryLimit { get; }
    public float CurrentCarryWeight { get; }
    public float MaximumHealth { get; }
}

public readonly struct OffenseFieldCarryPlan
{
    public OffenseFieldCarryPlan(
        string casualtyCharacterId,
        string carrierCharacterId,
        float bodyWeight,
        float casualtyCarryWeight,
        float carrierCapacity,
        float carrierCurrentLoad)
    {
        CasualtyCharacterId = casualtyCharacterId ?? string.Empty;
        CarrierCharacterId = carrierCharacterId ?? string.Empty;
        BodyWeight = bodyWeight;
        CasualtyCarryWeight = casualtyCarryWeight;
        CarrierCapacity = carrierCapacity;
        CarrierCurrentLoad = carrierCurrentLoad;
    }

    public string CasualtyCharacterId { get; }
    public string CarrierCharacterId { get; }
    public float BodyWeight { get; }
    public float CasualtyCarryWeight { get; }
    public float CarrierCapacity { get; }
    public float CarrierCurrentLoad { get; }
}

public static class OffenseFieldMobilityRules
{
    public static bool IsImmobile(OffenseFieldMobilityMemberSnapshot member)
    {
        float locomotion = member.StabilizationActive
            ? Max(member.Locomotion, member.LocomotionFloor)
            : member.Locomotion;
        float sustain = member.StabilizationActive
            ? Max(member.Sustain, member.SustainFloor)
            : member.Sustain;
        return member.Downed || locomotion <= 0.05f || sustain <= 0.05f;
    }

    public static int FindBestCarrierIndex(
        IReadOnlyList<OffenseFieldMobilityMemberSnapshot> mobileMembers,
        IReadOnlyCollection<string> assignedCarrierIds)
    {
        int selectedIndex = -1;
        float selectedCapacity = default;
        for (int index = 0; index < (mobileMembers?.Count ?? 0); index++)
        {
            OffenseFieldMobilityMemberSnapshot candidate = mobileMembers[index];
            if (ContainsId(assignedCarrierIds, candidate.CharacterId))
            {
                continue;
            }

            if (selectedIndex < 0
                || Comparer<float>.Default.Compare(
                    candidate.MaximumAllowedCarryWeight,
                    selectedCapacity) > 0)
            {
                selectedIndex = index;
                selectedCapacity = candidate.MaximumAllowedCarryWeight;
            }
        }

        return selectedIndex;
    }

    public static OffenseFieldCarryPlan CreateCarryPlan(
        OffenseFieldMobilityMemberSnapshot casualty,
        OffenseFieldMobilityMemberSnapshot carrier)
    {
        float capacity = Max(40f, carrier.BaseCarryLimit * 3f);
        float bodyWeight = Clamp(
            20f + casualty.MaximumHealth * 0.15f,
            25f,
            55f);
        return new OffenseFieldCarryPlan(
            casualty.CharacterId,
            carrier.CharacterId,
            bodyWeight,
            casualty.CurrentCarryWeight,
            capacity,
            carrier.CurrentCarryWeight);
    }

    public static bool AreAllCasualtiesAssigned(
        IReadOnlyList<OffenseFieldMobilityMemberSnapshot> immobileMembers,
        IReadOnlyCollection<string> assignedCasualtyIds)
    {
        for (int index = 0; index < (immobileMembers?.Count ?? 0); index++)
        {
            if (!ContainsId(
                    assignedCasualtyIds,
                    immobileMembers[index].CharacterId))
            {
                return false;
            }
        }

        return true;
    }

    public static float CalculateEstimatedSurvivalHours(float remainingSupply)
    {
        return 6f + remainingSupply * 3f;
    }

    private static bool ContainsId(
        IReadOnlyCollection<string> values,
        string characterId)
    {
        if (values == null)
        {
            return false;
        }

        foreach (string value in values)
        {
            if (string.Equals(value, characterId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static float Max(float left, float right)
    {
        return left > right ? left : right;
    }

    private static float Clamp(float value, float minimum, float maximum)
    {
        return value < minimum
            ? minimum
            : value > maximum
                ? maximum
                : value;
    }
}
