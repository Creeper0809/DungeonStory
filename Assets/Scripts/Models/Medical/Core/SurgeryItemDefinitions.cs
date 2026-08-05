public static class SurgeryItemDefinitions
{
    public const string OrganPrefix = "surgery:organ:";
    public const string ProstheticPrefix = "surgery:prosthetic:";
    public const string ContaminatedTissueId = "surgery:contaminated-tissue";
    public const string DisinfectantId = "medicine:disinfectant";
    public const string AnestheticId = "medicine:anesthetic";
    public const string ImmunosuppressantId = "medicine:immunosuppressant";
    public const string BloodPackId = "medicine:blood-pack";
    public const string FieldEmergencyKitId = "medicine:field-emergency-kit";
    public const string RuneSlimePatchId = "medicine:rune-slime-patch";
    public const string MycelialCulturePackId = "medicine:mycelial-culture-pack";
    public const string WingSplintKitId = "medicine:wing-splint-kit";
    public const string TemporaryPowerBypassId = "medicine:temporary-power-bypass";
    public const string BloodSealKitId = "medicine:blood-seal-kit";
    public const string ManaCoreRestraintId = "medicine:mana-core-restraint";

    public static string GetOrganItemId(string nodeId) =>
        OrganPrefix + (nodeId?.Trim() ?? "unknown");

    public static string GetProstheticItemId(string nodeId) =>
        ProstheticPrefix + (nodeId?.Trim() ?? "unknown");
}
