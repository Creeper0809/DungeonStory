using System;
using System.Globalization;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public enum ProductionUiTextId
{
    Header = 0,
    PriorityIncrease = 1,
    WeightIncrease = 2,
    MinimumReserveIncrease = 3,
    StatusDemandReserved = 4,
    StatusBlocked = 5,
    StatusInactiveConsumer = 6
}

public interface IProductionUiTextQuery
{
    string Get(ProductionUiTextId textId, params object[] arguments);
}

/// <summary>
/// Strict presentation-only lookup for production UI display text.
/// </summary>
public sealed class ProductionUiTextLocalizer : IProductionUiTextQuery
{
    public const string TableName = "ProductionUI";

    public string Get(ProductionUiTextId textId, params object[] arguments)
    {
        string key = textId switch
        {
            ProductionUiTextId.Header => "Production.Route.Header",
            ProductionUiTextId.PriorityIncrease =>
                "Production.Route.PriorityIncrease",
            ProductionUiTextId.WeightIncrease =>
                "Production.Route.WeightIncrease",
            ProductionUiTextId.MinimumReserveIncrease =>
                "Production.Route.MinimumReserveIncrease",
            ProductionUiTextId.StatusDemandReserved =>
                "Production.Route.Status.DemandReserved",
            ProductionUiTextId.StatusBlocked =>
                "Production.Route.Status.Blocked",
            ProductionUiTextId.StatusInactiveConsumer =>
                "Production.Route.Status.InactiveConsumer",
            _ => throw new ArgumentOutOfRangeException(
                nameof(textId),
                textId,
                null)
        };

        LocalizationSettings.InitializationOperation.WaitForCompletion();
        string template = new LocalizedString(TableName, key).GetLocalizedString();
        if (string.IsNullOrWhiteSpace(template))
        {
            throw new InvalidOperationException(
                $"Missing localized production UI entry '{key}' "
                + $"in String Table '{TableName}'.");
        }

        object[] resolvedArguments = arguments ?? Array.Empty<object>();
        return resolvedArguments.Length == 0
            ? template
            : string.Format(
                CultureInfo.CurrentCulture,
                template,
                resolvedArguments);
    }
}
