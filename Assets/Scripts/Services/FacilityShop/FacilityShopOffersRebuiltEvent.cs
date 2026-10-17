using System;
using System.Collections.Generic;

public enum FacilityShopRefreshCause
{
    Initialization = 0,
    RunSetup = 1,
    OperatingDay = 2,
    Manual = 3,
    Restore = 4
}

public readonly struct FacilityShopOffersRebuiltEvent
{
    public FacilityShopOffersRebuiltEvent(
        int day,
        FacilityShopRefreshCause cause,
        IReadOnlyList<FacilityShopOfferSnapshot> dailyOffers,
        IReadOnlyList<FacilityShopOfferSnapshot> basicPurchaseOffers)
    {
        if (day < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(day));
        }
        if (!Enum.IsDefined(typeof(FacilityShopRefreshCause), cause))
        {
            throw new ArgumentOutOfRangeException(nameof(cause));
        }

        Day = day;
        Cause = cause;
        DailyOffers = CopyOffers(dailyOffers, nameof(dailyOffers));
        BasicPurchaseOffers = CopyOffers(
            basicPurchaseOffers,
            nameof(basicPurchaseOffers));
    }

    public int Day { get; }
    public FacilityShopRefreshCause Cause { get; }
    public IReadOnlyList<FacilityShopOfferSnapshot> DailyOffers { get; }
    public IReadOnlyList<FacilityShopOfferSnapshot> BasicPurchaseOffers { get; }

    private static IReadOnlyList<FacilityShopOfferSnapshot> CopyOffers(
        IReadOnlyList<FacilityShopOfferSnapshot> source,
        string parameterName)
    {
        if (source == null)
        {
            throw new ArgumentNullException(parameterName);
        }
        if (source.Count == 0)
        {
            return Array.Empty<FacilityShopOfferSnapshot>();
        }

        FacilityShopOfferSnapshot[] copy =
            new FacilityShopOfferSnapshot[source.Count];
        for (int index = 0; index < source.Count; index++)
        {
            FacilityShopOfferSnapshot offer = source[index]
                ?? throw new ArgumentException(
                    "Facility shop offer snapshots cannot contain null values.",
                    parameterName);
            copy[index] = new FacilityShopOfferSnapshot(
                offer.offerTypeId,
                offer.typeDisplayName,
                offer.rarity,
                offer.displayName,
                offer.cost,
                offer.star,
                offer.basicPurchase);
        }
        return Array.AsReadOnly(copy);
    }
}
