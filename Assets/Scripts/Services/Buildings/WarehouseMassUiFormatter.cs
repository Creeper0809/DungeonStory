using System;
using System.Globalization;

public static class WarehouseMassUiFormatter
{
    public static string FormatKilograms(long grams)
    {
        if (grams < 0L)
        {
            throw new ArgumentOutOfRangeException(
                nameof(grams),
                "Warehouse presentation mass cannot be negative.");
        }

        decimal kilograms = grams / 1000m;
        return kilograms.ToString("0.###", CultureInfo.InvariantCulture) + "kg";
    }

    public static string FormatCapacity(WarehouseInventory inventory)
    {
        WarehouseInventory value = inventory
            ?? throw new ArgumentNullException(nameof(inventory));
        if (!value.HasMassCapacityAuthority)
        {
            throw new InvalidOperationException(
                "Warehouse kg presentation requires canonical mass authority.");
        }

        return FormatKilograms(value.StoredMassGrams)
            + "/"
            + FormatKilograms(value.MaxMassGrams);
    }
}
