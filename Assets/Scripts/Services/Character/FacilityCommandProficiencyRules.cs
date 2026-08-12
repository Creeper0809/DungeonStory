public static class FacilityCommandProficiencyRules
{
    public static bool RequiresHigherCombat(
        ResearchFacilityCommandKind command) =>
        command == ResearchFacilityCommandKind.DefenseControl;

    public static bool TryResolve(
        ResearchFacilityCommandKind command,
        out ProficiencyWorkProfile profile)
    {
        if (command == ResearchFacilityCommandKind.ResonanceTuning)
        {
            profile = new ProficiencyWorkProfile(
                BuiltInCharacterProficiencyIds.Crafting,
                BuiltInCharacterProficiencyIds.Scholarship,
                0.80f);
            return true;
        }
        if (RequiresHigherCombat(command))
        {
            profile = default;
            return false;
        }

        CharacterProficiencyId primary = command switch
        {
            ResearchFacilityCommandKind.GatheringPreparation or
            ResearchFacilityCommandKind.LoggingPreparation or
            ResearchFacilityCommandKind.DirectionalFelling or
            ResearchFacilityCommandKind.FlowMetering or
            ResearchFacilityCommandKind.HandLaundry or
            ResearchFacilityCommandKind.IndoorDrying or
            ResearchFacilityCommandKind.PoweredLaundry or
            ResearchFacilityCommandKind.ApparelDisplay or
            ResearchFacilityCommandKind.DressingChange => BuiltInCharacterProficiencyIds.Fieldwork,

            ResearchFacilityCommandKind.SelectiveBreeding or
            ResearchFacilityCommandKind.StableHarnessing or
            ResearchFacilityCommandKind.WildlifeTaming or
            ResearchFacilityCommandKind.BreedingSchedule or
            ResearchFacilityCommandKind.CropCalendar or
            ResearchFacilityCommandKind.SoilDiagnostics or
            ResearchFacilityCommandKind.SeedSelection => BuiltInCharacterProficiencyIds.FoodProduction,

            ResearchFacilityCommandKind.ClimateControl => BuiltInCharacterProficiencyIds.ConstructionEngineering,

            ResearchFacilityCommandKind.WeaponPatternAccess or
            ResearchFacilityCommandKind.ApparelTailoring or
            ResearchFacilityCommandKind.ApparelDecoration or
            ResearchFacilityCommandKind.ApparelRepair or
            ResearchFacilityCommandKind.FiberSorting or
            ResearchFacilityCommandKind.FiberScouring or
            ResearchFacilityCommandKind.ManualSpinning or
            ResearchFacilityCommandKind.TextileFinishing or
            ResearchFacilityCommandKind.PoweredSpinning or
            ResearchFacilityCommandKind.PoweredWeaving => BuiltInCharacterProficiencyIds.Crafting,

            ResearchFacilityCommandKind.ClassroomEducation or
            ResearchFacilityCommandKind.SupervisedApprenticeship or
            ResearchFacilityCommandKind.GenerationArchive or
            ResearchFacilityCommandKind.GeneticArchive or
            ResearchFacilityCommandKind.ClimateMapping or
            ResearchFacilityCommandKind.ChronometricNavigation => BuiltInCharacterProficiencyIds.Scholarship,

            ResearchFacilityCommandKind.AgingAssessment or
            ResearchFacilityCommandKind.BiologicalAgeMeasurement or
            ResearchFacilityCommandKind.GeriatricCare or
            ResearchFacilityCommandKind.ChronicCare or
            ResearchFacilityCommandKind.PathogenDiagnosis or
            ResearchFacilityCommandKind.Serology or
            ResearchFacilityCommandKind.EpidemicBoard or
            ResearchFacilityCommandKind.GeneticCounseling or
            ResearchFacilityCommandKind.CorpseCare => BuiltInCharacterProficiencyIds.Medicine,

            ResearchFacilityCommandKind.BloodStageDrainage or
            ResearchFacilityCommandKind.HouseholdRegistry or
            ResearchFacilityCommandKind.NurseryCare or
            ResearchFacilityCommandKind.FamilyPartition or
            ResearchFacilityCommandKind.GuardianRegistry or
            ResearchFacilityCommandKind.RetireeCare or
            ResearchFacilityCommandKind.MentorAcademy or
            ResearchFacilityCommandKind.SecureTradeVault => BuiltInCharacterProficiencyIds.Social,

            _ => default
        };
        profile = primary.IsValid
            ? new ProficiencyWorkProfile(primary)
            : default;
        return profile.IsValid;
    }
}
