using System;

public enum RegularCustomerObservationKind
{
    BecameRegular,
    BecameRecruitCandidate,
    Recruited,
    ExpeditionCandidate
}

/// <summary>
/// A frozen customer transition for presentation observers. Recruitment state
/// remains owned by the regular-customer runtime.
/// </summary>
public readonly struct RegularCustomerObservation
{
    public RegularCustomerObservation(
        RegularCustomerObservationKind kind,
        RegularCustomerSnapshot customer,
        RegularCustomerRecruitDeliveryKind deliveryKind =
            RegularCustomerRecruitDeliveryKind.None,
        bool deliveredImmediately = false)
    {
        if (!Enum.IsDefined(typeof(RegularCustomerObservationKind), kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
        if (customer == null)
        {
            throw new ArgumentNullException(nameof(customer));
        }
        if (!Enum.IsDefined(
                typeof(RegularCustomerRecruitDeliveryKind),
                deliveryKind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(deliveryKind), deliveryKind, null);
        }
        if (kind == RegularCustomerObservationKind.Recruited)
        {
            if (deliveryKind == RegularCustomerRecruitDeliveryKind.None)
            {
                throw new ArgumentException(
                    "A recruited observation requires a delivery kind.",
                    nameof(deliveryKind));
            }
        }
        else if (deliveryKind != RegularCustomerRecruitDeliveryKind.None
            || deliveredImmediately)
        {
            throw new ArgumentException(
                "Only recruited observations may carry delivery state.");
        }

        Kind = kind;
        Customer = new RegularCustomerSnapshot(
            customer.customerId,
            customer.displayName,
            customer.speciesTag,
            customer.visitCount,
            customer.averageSatisfaction,
            customer.status,
            customer.recruitCapabilities);
        DeliveryKind = deliveryKind;
        DeliveredImmediately = deliveredImmediately;
    }

    public RegularCustomerObservationKind Kind { get; }
    public RegularCustomerSnapshot Customer { get; }
    public RegularCustomerRecruitDeliveryKind DeliveryKind { get; }
    public bool DeliveredImmediately { get; }
}
