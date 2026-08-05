using System;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public interface IWildlifeFoodRaidOutcomeTextQuery
{
    string Get(string outcomeCode);
}

public sealed class WildlifeFoodRaidOutcomeTextLocalizer :
    IWildlifeFoodRaidOutcomeTextQuery
{
    public const string TableName = "WildlifeUI";

    public string Get(string outcomeCode)
    {
        string key = outcomeCode switch
        {
            WildlifeFoodRaidOutcomeCodes.RaidActorRemoved =>
                "Wildlife.FoodRaid.RaidActorRemoved",
            _ => throw new ArgumentOutOfRangeException(
                nameof(outcomeCode),
                outcomeCode,
                "Unknown wildlife food-raid outcome code.")
        };

        LocalizationSettings.InitializationOperation.WaitForCompletion();
        string localized = new LocalizedString(TableName, key).GetLocalizedString();
        if (string.IsNullOrWhiteSpace(localized))
        {
            throw new InvalidOperationException(
                $"Missing localized wildlife UI entry '{key}' "
                + $"in String Table '{TableName}'.");
        }
        return localized;
    }
}
