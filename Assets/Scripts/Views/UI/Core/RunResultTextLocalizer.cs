using System;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public enum RunResultTextId
{
    EmptyResult = 0,
    NextRun = 1
}

public interface IRunResultTextQuery
{
    string Get(RunResultTextId textId);
}

/// <summary>
/// Presentation-only localization query for the run-result surface.
/// Domain and panel code never own display-language strings.
/// </summary>
public sealed class RunResultTextLocalizer : IRunResultTextQuery
{
    public const string TableName = "DomainFailures";

    public string Get(RunResultTextId textId)
    {
        string key = textId switch
        {
            RunResultTextId.EmptyResult => "RunResultEmpty",
            RunResultTextId.NextRun => "RunResultNextRun",
            _ => throw new ArgumentOutOfRangeException(nameof(textId), textId, null)
        };

        LocalizationSettings.InitializationOperation.WaitForCompletion();
        string localized = new LocalizedString(TableName, key).GetLocalizedString();
        if (string.IsNullOrWhiteSpace(localized))
        {
            throw new InvalidOperationException(
                $"Missing localized run-result entry '{key}' in String Table '{TableName}'.");
        }

        return localized;
    }
}
