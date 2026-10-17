using VContainer;
using VContainer.Unity;

public static class ExternalFactionOutcomeRegistration
{
    public static void RegisterExternalFactionGameplayOutcomes(
        this IContainerBuilder builder)
    {
        builder.RegisterGameplayDomainFact<OffenseTruthRevealedFact, OffenseTruthRevealOutcomeReceipt>(
            "log:offense.truth-reveal", OffenseTruthRevealFactMapping.Capture);
        builder.Register<OffenseTruthFactPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>().As<IDungeonCapturedSavePreflightValidator>();
        builder.RegisterEntryPoint<OffenseTruthPresentationObserver>(Lifetime.Singleton);
        builder.RegisterGameplayActionLogs(nameof(OffenseTruthRevealOutcomeAdapter));
    }
}
