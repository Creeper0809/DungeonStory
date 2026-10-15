using System;

public enum RegularCustomerFactStage
{
    VisitRecorded,
    RecruitmentCommitted,
    RecruitmentDelivered
}

// Frozen domain observations, not log payloads or importance decisions.
[Serializable]
public sealed class RegularCustomerFactData
{
    public RegularCustomerFactStage stage;
    public string customerId = "", displayName = "", speciesTag = "", actorId = "";
    public int absoluteDay, visitCount, recruitedAbsoluteDay, mercenaryRolePremium;
    public float averageSatisfaction;
    public RegularCustomerStatus status;
    public RecruitCapability capabilities;
    public RegularCustomerRecruitDeliveryKind deliveryKind;
    public bool becameRegular, becameRecruitCandidate;

    public RegularCustomerFactData Clone() => (RegularCustomerFactData)MemberwiseClone();

    public void Validate()
    {
        if (!Enum.IsDefined(typeof(RegularCustomerFactStage), stage)
            || !Enum.IsDefined(typeof(RegularCustomerStatus), status)
            || !Enum.IsDefined(typeof(RegularCustomerRecruitDeliveryKind), deliveryKind)
            || !Text(customerId, false) || !Text(displayName, false)
            || !Text(speciesTag, true) || !Text(actorId, true)
            || absoluteDay < 0 || visitCount < 0 || recruitedAbsoluteDay < 0
            || !float.IsFinite(averageSatisfaction) || averageSatisfaction < 0f || averageSatisfaction > 100f
            || visitCount == 0 && averageSatisfaction != 0f
            || (capabilities & ~RecruitCapability.All) != 0 || mercenaryRolePremium < 0
            || deliveryKind != RegularCustomerRecruitDeliveryKind.Mercenary && mercenaryRolePremium != 0)
            throw new ArgumentException("regular-customer-fact-invalid");
        if (stage == RegularCustomerFactStage.VisitRecorded)
        {
            if (visitCount < 1 || status == RegularCustomerStatus.Recruited || recruitedAbsoluteDay != 0
                || deliveryKind != RegularCustomerRecruitDeliveryKind.None || actorId.Length != 0
                || becameRegular && (int)status < (int)RegularCustomerStatus.Regular
                || becameRecruitCandidate && status != RegularCustomerStatus.RecruitCandidate)
                throw new ArgumentException("regular-customer-visit-fact-invalid");
        }
        else if (status != RegularCustomerStatus.Recruited || recruitedAbsoluteDay < 1
            || deliveryKind == RegularCustomerRecruitDeliveryKind.None || becameRegular || becameRecruitCandidate
            || (stage == RegularCustomerFactStage.RecruitmentDelivered ? actorId != customerId : actorId.Length != 0))
            throw new ArgumentException("regular-customer-recruit-fact-invalid");
    }

    internal static bool Text(string value, bool empty) => value != null
        && (empty || value.Length > 0) && value.Length <= 2048 && value == value.Trim()
        && Array.TrueForAll(value.ToCharArray(), c => !char.IsControl(c));
}

[Serializable]
public sealed class RegularCustomerPendingFactData
{
    public string publicationKey = "";
    public RegularCustomerFactData fact;
    public RegularCustomerPendingFactData Clone() => new() { publicationKey = publicationKey, fact = fact?.Clone() };
    public void Validate()
    {
        if (!RegularCustomerFactData.Text(publicationKey, false) || fact == null)
            throw new ArgumentException("regular-customer-pending-fact-invalid");
        fact.Validate();
    }
}
