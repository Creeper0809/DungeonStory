using UnityEngine;

internal static class FacilityCrimeEditorTestDependencies
{
    private static FacilityCrimeSettingsSO settings;
    private static IFacilityCrimeRiskEvaluator evaluator;

    public static FacilityCrimeSettingsSO Settings
    {
        get
        {
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<FacilityCrimeSettingsSO>();
                settings.hideFlags = HideFlags.HideAndDontSave;
            }

            return settings;
        }
    }

    public static IFacilityCrimeRiskEvaluator Evaluator =>
        evaluator ??= new FacilityCrimeRiskEvaluator(new FixedProvider(Settings));

    private sealed class FixedProvider : IFacilityCrimeSettingsProvider
    {
        public FixedProvider(FacilityCrimeSettingsSO settings)
        {
            Settings = settings;
        }

        public FacilityCrimeSettingsSO Settings { get; }
    }
}
