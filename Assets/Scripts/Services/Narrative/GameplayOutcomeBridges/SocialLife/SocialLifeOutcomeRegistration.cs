using VContainer;
using VContainer.Unity;

public static class SocialLifeOutcomeRegistration
{
    public static void RegisterSocialLifeGameplayOutcomeBridges(
        this IContainerBuilder builder)
    {
        builder.Register(resolver =>
                SocialInteractionFactMapping.CreateConflictBinding(
                    resolver.Resolve<IGameplayOutcomeRegistry>(),
                    resolver.Resolve<GameplayOutcomeLedger>(),
                    resolver.Resolve<IGameplayOutcomeRecorder>(),
                    resolver.Resolve<GameplayDomainFactCommitGroup>()),
                Lifetime.Singleton)
            .As<DungeonStory.Foundation.Facts.IDomainFactObserverBinding>();
        builder.Register(resolver =>
                SocialInteractionFactMapping.CreateApologyBinding(
                    resolver.Resolve<IGameplayOutcomeRegistry>(),
                    resolver.Resolve<GameplayOutcomeLedger>(),
                    resolver.Resolve<IGameplayOutcomeRecorder>(),
                    resolver.Resolve<GameplayDomainFactCommitGroup>()),
                Lifetime.Singleton)
            .As<DungeonStory.Foundation.Facts.IDomainFactObserverBinding>();
        builder.Register<SocialOffenseHistory>(Lifetime.Singleton)
            .As<ISocialOffenseHistory>();
        builder.Register<SocialInteractionFactPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>().As<IDungeonCapturedSavePreflightValidator>();
        builder.RegisterEntryPoint<SocialInteractionActivityObserver>(
            Lifetime.Singleton);
        builder.Register(resolver => new GameplayDomainFactObserverBinding<IdleSocialInteractionResolvedFact, IdleSocialInteractionOutcomeReceipt>(
            "log:social.idle-interaction", IdleSocialInteractionFactMapping.Capture,
            resolver.Resolve<IGameplayOutcomeRegistry>(), resolver.Resolve<GameplayOutcomeLedger>(),
            resolver.Resolve<IGameplayOutcomeRecorder>(), resolver.Resolve<GameplayDomainFactCommitGroup>(),
            IdleSocialInteractionFactMapping.ReserveShape), Lifetime.Singleton)
            .As<DungeonStory.Foundation.Facts.IDomainFactObserverBinding>();
        builder.Register<IdleSocialInteractionFactPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>().As<IDungeonCapturedSavePreflightValidator>();
        builder.RegisterEntryPoint<IdleSocialInteractionCommandRuntime>(Lifetime.Singleton)
            .AsSelf().As<IIdleSocialInteractionCommand>().As<IDungeonSaveRestoreCompletedHook>();
        builder.RegisterGameplayDomainFact<StaffDiscontentTransitionedFact, StaffDiscontentTransitionOutcomeReceipt>(
            "log:staff-discontent.transition", StaffDiscontentFactMapping.Capture);
        builder.RegisterGameplayDomainFact<StaffRebellionResponseResolvedFact, StaffDiscontentOutcomeReceipt>(
            "log:staff-discontent.response", StaffDiscontentFactMapping.Capture);
        builder.Register<StaffDiscontentFactPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>().As<IDungeonCapturedSavePreflightValidator>();
        builder.RegisterEntryPoint<StaffDiscontentPresentationObserver>(Lifetime.Singleton);
        builder.Register(resolver => new GameplayDomainFactObserverBinding<CaptivePerformerMilestoneUnlockedFact, CaptivePerformerMilestoneOutcomeReceipt>(
            "log:captivity.performer-milestone", CaptivePerformerMilestoneFactMapping.Capture,
            resolver.Resolve<IGameplayOutcomeRegistry>(), resolver.Resolve<GameplayOutcomeLedger>(),
            resolver.Resolve<IGameplayOutcomeRecorder>(), resolver.Resolve<GameplayDomainFactCommitGroup>()), Lifetime.Singleton)
            .As<DungeonStory.Foundation.Facts.IDomainFactObserverBinding>();
        builder.Register<CaptivePerformerMilestoneFactPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>().As<IDungeonCapturedSavePreflightValidator>();
        builder.RegisterGameplayDomainFact<CaptiveEscapedFact, CaptiveEscapeOutcomeReceipt>(
            "log:captivity.captive-escape", CaptiveEscapeFactMapping.Capture);
        builder.Register<CaptiveEscapeFactPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>().As<IDungeonCapturedSavePreflightValidator>();
        builder.RegisterGameplayDomainFact<CaptiveRansomAcceptedFact, CaptiveRansomOutcomeReceipt>(
            "log:captivity.captive-ransom", CaptiveRansomFactMapping.Capture);
        builder.Register<CaptiveRansomFactPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>().As<IDungeonCapturedSavePreflightValidator>();
        builder.RegisterGameplayDomainFact<CaptivityInteractionResolvedFact, CaptivityInteractionOutcomeReceipt>(
            "log:captivity.interaction-result", CaptivityInteractionFactMapping.Capture);
        builder.Register<CaptivityInteractionFactPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>().As<IDungeonCapturedSavePreflightValidator>();
        builder.RegisterGameplayDomainFact<CaptivityRehabilitationProgressedFact, CaptivityRehabilitationOutcomeReceipt>(
            "log:captivity.rehabilitation", CaptivityTransitionFactMapping.Capture);
        builder.RegisterGameplayDomainFact<CaptivityReleasedFact, CaptivityReleaseOutcomeReceipt>(
            "log:captivity.release", CaptivityTransitionFactMapping.Capture);
        builder.Register<CaptivityTransitionFactPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>().As<IDungeonCapturedSavePreflightValidator>();
        builder.RegisterGameplayActionLogs(nameof(SocialConflictOutcomeAdapter));
        builder.RegisterGameplayActionLogs(nameof(ApologyOutcomeAdapter));
        builder.RegisterGameplayActionLogs(nameof(IdleSocialInteractionOutcomeAdapter));
        builder.RegisterGameplayActionLogs(nameof(StaffDiscontentOutcomeAdapter));
        builder.RegisterGameplayActionLogs(nameof(StaffDiscontentTransitionOutcomeAdapter));
        builder.RegisterGameplayActionLogs(nameof(CaptivePerformerMilestoneOutcomeAdapter));
        builder.RegisterGameplayActionLogs(nameof(CaptiveEscapeOutcomeAdapter));
        builder.RegisterGameplayActionLogs(nameof(CaptiveRansomOutcomeAdapter));
        builder.RegisterGameplayActionLogs(nameof(CaptivityInteractionOutcomeAdapter));
        builder.RegisterGameplayActionLogs(nameof(CaptivityRehabilitationOutcomeAdapter));
        builder.RegisterGameplayActionLogs(nameof(CaptivityReleaseOutcomeAdapter));
    }
}
