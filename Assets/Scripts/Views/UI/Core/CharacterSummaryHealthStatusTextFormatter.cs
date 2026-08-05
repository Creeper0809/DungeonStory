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
