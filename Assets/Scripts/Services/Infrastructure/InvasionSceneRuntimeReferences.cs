public sealed class InvasionSceneRuntimeReferences
{
    public InvasionSceneRuntimeReferences(
        InvasionThreatRuntime threat,
        InvasionDirectorRuntime director,
        InvasionCombatReportRuntime combatReport)
    {
        Threat = threat;
        Director = director;
        CombatReport = combatReport;
    }

    public InvasionThreatRuntime Threat { get; }
    public InvasionDirectorRuntime Director { get; }
    public InvasionCombatReportRuntime CombatReport { get; }
}
