public static class CharacterSummaryHealthStatusTextFormatter
{
    public static string Get(string key, params object[] arguments)
    {
        return CharacterSummaryUiTextQuery.Get(key, arguments);
    }

    public static string DietPolicy(CharacterDietPolicyKind policy)
    {
        return policy switch
        {
            CharacterDietPolicyKind.Vegan => Get("CharacterSummary.Health.DietPolicy.Vegan"),
            CharacterDietPolicyKind.Vegetarian => Get("CharacterSummary.Health.DietPolicy.Vegetarian"),
            CharacterDietPolicyKind.CarnivorePreferred => Get("CharacterSummary.Health.DietPolicy.CarnivorePreferred"),
            CharacterDietPolicyKind.StrictTaboo => Get("CharacterSummary.Health.DietPolicy.StrictTaboo"),
            _ => Get("CharacterSummary.Health.DietPolicy.Free")
        };
    }

    public static string MealQualityLimit(CharacterMealQualityLimit limit)
    {
        return limit switch
        {
            CharacterMealQualityLimit.Inherit => Get("CharacterSummary.Health.MealQuality.Inherit"),
            CharacterMealQualityLimit.Poor => Get("CharacterSummary.Health.MealQuality.Poor"),
            CharacterMealQualityLimit.Simple => Get("CharacterSummary.Health.MealQuality.Simple"),
            CharacterMealQualityLimit.Decent => Get("CharacterSummary.Health.MealQuality.Decent"),
            CharacterMealQualityLimit.Fine => Get("CharacterSummary.Health.MealQuality.Fine"),
            CharacterMealQualityLimit.Lavish => Get("CharacterSummary.Health.MealQuality.Lavish"),
            _ => throw new System.ArgumentOutOfRangeException(nameof(limit), limit, null)
        };
    }

    public static string SubstancePolicy(SubstancePolicyMode mode)
    {
        return mode switch
        {
            SubstancePolicyMode.MedicalOnly => Get("CharacterSummary.Health.SubstancePolicy.MedicalOnly"),
            SubstancePolicyMode.CombatOnly => Get("CharacterSummary.Health.SubstancePolicy.CombatOnly"),
            SubstancePolicyMode.MoodThreshold => Get("CharacterSummary.Health.SubstancePolicy.MoodThreshold"),
            SubstancePolicyMode.Scheduled => Get("CharacterSummary.Health.SubstancePolicy.Scheduled"),
            _ => Get("CharacterSummary.Health.SubstancePolicy.Forbidden")
        };
    }

    public static string ToxicityTreatment(CharacterToxicityStatus status)
    {
        if (status.TreatmentPending)
        {
            return Get(
                "CharacterSummary.Health.Consumables.ToxicityTreatment.Pending");
        }
        if (status.TreatmentAvailable)
        {
            return Get(
                "CharacterSummary.Health.Consumables.ToxicityTreatment.Available");
        }
        return status.TreatmentUnavailableReason switch
        {
            "toxicity-zero" => Get(
                "CharacterSummary.Health.Consumables.ToxicityTreatment.NotNeeded"),
            "detox-medicine-undefined" => Get(
                "CharacterSummary.Health.Consumables.ToxicityTreatment.Unauthored"),
            _ => Get(
                "CharacterSummary.Health.Consumables.ToxicityTreatment.Unavailable")
        };
    }

    public static string TreatmentKind(SurvivalTreatmentKind kind) => kind switch
    {
        SurvivalTreatmentKind.Detox => Get(
            "CharacterSummary.Health.Consumables.TreatmentSupply.Kind.Detox"),
        SurvivalTreatmentKind.Standard => Get(
            "CharacterSummary.Health.Consumables.TreatmentSupply.Kind.Standard"),
        _ => throw new System.ArgumentOutOfRangeException(
            nameof(kind), kind, null)
    };

    public static string TreatmentSupply(SurvivalTreatmentSupplyState state) =>
        state switch
        {
            SurvivalTreatmentSupplyState.Ready => Get(
                "CharacterSummary.Health.Consumables.TreatmentSupply.State.Ready"),
            SurvivalTreatmentSupplyState.Requested => Get(
                "CharacterSummary.Health.Consumables.TreatmentSupply.State.Requested"),
            SurvivalTreatmentSupplyState.InTransit => Get(
                "CharacterSummary.Health.Consumables.TreatmentSupply.State.InTransit"),
            SurvivalTreatmentSupplyState.CapacityUnavailable => Get(
                "CharacterSummary.Health.Consumables.TreatmentSupply.State.CapacityUnavailable"),
            SurvivalTreatmentSupplyState.NoPath => Get(
                "CharacterSummary.Health.Consumables.TreatmentSupply.State.NoPath"),
            SurvivalTreatmentSupplyState.Processing => Get(
                "CharacterSummary.Health.Consumables.TreatmentSupply.State.Processing"),
            SurvivalTreatmentSupplyState.AwaitingRequest => Get(
                "CharacterSummary.Health.Consumables.TreatmentSupply.State.AwaitingRequest"),
            SurvivalTreatmentSupplyState.StockMissing => Get(
                "CharacterSummary.Health.Consumables.TreatmentSupply.State.StockMissing"),
            _ => throw new System.ArgumentOutOfRangeException(
                nameof(state), state, null)
        };

    public static string PartKind(SurgicalPartKind kind)
    {
        return kind switch
        {
            global::SurgicalPartKind.NaturalOrgan => Get("CharacterSummary.Health.Anatomy.PartKind.NaturalOrgan"),
            global::SurgicalPartKind.Prosthetic => Get("CharacterSummary.Health.Anatomy.PartKind.Prosthetic"),
            global::SurgicalPartKind.Implant => Get("CharacterSummary.Health.Anatomy.PartKind.Implant"),
            global::SurgicalPartKind.ArcaneGraft => Get("CharacterSummary.Health.Anatomy.PartKind.ArcaneGraft"),
            _ => kind.ToString()
        };
    }
}
