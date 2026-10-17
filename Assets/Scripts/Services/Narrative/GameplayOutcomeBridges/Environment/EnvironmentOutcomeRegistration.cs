using VContainer;
using VContainer.Unity;

public static class EnvironmentOutcomeRegistration
{
    public static void RegisterEnvironmentGameplayOutcomeBridges(
        this IContainerBuilder builder)
    {
        builder.Register<EnvironmentGameplayOutcomeBridge>(Lifetime.Singleton)
            .As<IEnvironmentGameplayOutcomeCommitter>();
        builder.Register(resolver => WildlifeFoodRaidFactMapping.CreateBinding(
            resolver.Resolve<IGameplayOutcomeRegistry>(), resolver.Resolve<GameplayOutcomeLedger>(),
            resolver.Resolve<IGameplayOutcomeRecorder>(), resolver.Resolve<GameplayDomainFactCommitGroup>(),
            resolver.Resolve<IGameplayOutcomeDiagnosticsQuery>()), Lifetime.Singleton)
            .As<DungeonStory.Foundation.Facts.IDomainFactObserverBinding>();
        builder.Register<WildlifeFoodRaidFactPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>().As<IDungeonCapturedSavePreflightValidator>();
        builder.Register(resolver => WildlifeFactMapping.CreateCommandBinding(
            resolver.Resolve<IGameplayOutcomeRegistry>(), resolver.Resolve<GameplayOutcomeLedger>(),
            resolver.Resolve<IGameplayOutcomeRecorder>(), resolver.Resolve<GameplayDomainFactCommitGroup>()),
            Lifetime.Singleton).As<DungeonStory.Foundation.Facts.IDomainFactObserverBinding>();
        builder.Register(resolver => WildlifeFactMapping.CreateActionBinding(
            resolver.Resolve<IGameplayOutcomeRegistry>(), resolver.Resolve<GameplayOutcomeLedger>(),
            resolver.Resolve<IGameplayOutcomeRecorder>(), resolver.Resolve<GameplayDomainFactCommitGroup>()),
            Lifetime.Singleton).As<DungeonStory.Foundation.Facts.IDomainFactObserverBinding>();
        builder.Register<WildlifeFactPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>().As<IDungeonCapturedSavePreflightValidator>();
        builder.Register(resolver => WorkAccidentFactMapping.CreateBinding(
            resolver.Resolve<IGameplayOutcomeRegistry>(), resolver.Resolve<GameplayOutcomeLedger>(),
            resolver.Resolve<IGameplayOutcomeRecorder>(), resolver.Resolve<GameplayDomainFactCommitGroup>()),
            Lifetime.Singleton).As<DungeonStory.Foundation.Facts.IDomainFactObserverBinding>();
        builder.RegisterEntryPoint<WorkAccidentActivityObserver>(Lifetime.Singleton);
        builder.Register<WorkAccidentFactPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>().As<IDungeonCapturedSavePreflightValidator>();
        builder.Register(resolver => SpeciesActionFactMapping.CreateGolemBinding(
            resolver.Resolve<IGameplayOutcomeRegistry>(), resolver.Resolve<GameplayOutcomeLedger>(),
            resolver.Resolve<IGameplayOutcomeRecorder>(), resolver.Resolve<GameplayDomainFactCommitGroup>()),
            Lifetime.Singleton).As<DungeonStory.Foundation.Facts.IDomainFactObserverBinding>();
        builder.Register(resolver => SpeciesActionFactMapping.CreateSpeciesIncidentBinding(
            resolver.Resolve<IGameplayOutcomeRegistry>(), resolver.Resolve<GameplayOutcomeLedger>(),
            resolver.Resolve<IGameplayOutcomeRecorder>(), resolver.Resolve<GameplayDomainFactCommitGroup>()),
            Lifetime.Singleton).As<DungeonStory.Foundation.Facts.IDomainFactObserverBinding>();
        builder.Register(resolver => SpeciesActionFactMapping.CreateHarpyGaleBinding(
            resolver.Resolve<IGameplayOutcomeRegistry>(), resolver.Resolve<GameplayOutcomeLedger>(),
            resolver.Resolve<IGameplayOutcomeRecorder>(), resolver.Resolve<GameplayDomainFactCommitGroup>()),
            Lifetime.Singleton).As<DungeonStory.Foundation.Facts.IDomainFactObserverBinding>();
        builder.RegisterEntryPoint<SpeciesIncidentActivityObserver>(Lifetime.Singleton);
        builder.Register<SpeciesActionFactPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>().As<IDungeonCapturedSavePreflightValidator>();
        builder.Register(resolver =>
            new GameplayDomainFactObserverBinding<DungeonStory.Environment.EnvironmentalFireDamageFact, EnvironmentalFireDamageOutcomeReceipt>(
                "log:environment.fire-damage", EnvironmentalFireDamageFactMapping.Capture,
                resolver.Resolve<IGameplayOutcomeRegistry>(), resolver.Resolve<GameplayOutcomeLedger>(),
                resolver.Resolve<IGameplayOutcomeRecorder>(), resolver.Resolve<GameplayDomainFactCommitGroup>(),
                EnvironmentalFireDamageFactMapping.Reserve), Lifetime.Singleton)
            .As<DungeonStory.Foundation.Facts.IDomainFactObserverBinding>();
        builder.Register<EnvironmentalFireDamageFactPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>().As<IDungeonCapturedSavePreflightValidator>();
        builder.RegisterGameplayDomainFact<EnvironmentalFireFuelLostFact, EnvironmentalFireFuelOutcomeReceipt>(
            "log:environment.fire-fuel-loss", EnvironmentalFireResourceFactMapping.Capture);
        builder.RegisterGameplayDomainFact<EnvironmentalFireWaterConsumedFact, EnvironmentalFireWaterOutcomeReceipt>(
            "log:environment.fire-water-consumed", EnvironmentalFireResourceFactMapping.Capture);
        builder.Register<EnvironmentalFireResourceFactPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>().As<IDungeonCapturedSavePreflightValidator>();
        builder.RegisterGameplayDomainFact<
            DungeonStory.Environment.EnvironmentalFireIgnitedFact,
            EnvironmentalFireIgnitionOutcomeReceipt>(
                "log:environment.fire-ignition",
                EnvironmentalFireActionFactMapping.Capture);
        builder.RegisterGameplayDomainFact<
            DungeonStory.Environment.EnvironmentalFireSuppressedFact,
            EnvironmentalFireSuppressionOutcomeReceipt>(
                "log:environment.fire-suppression",
                EnvironmentalFireActionFactMapping.Capture);
        builder.RegisterWorkCompletionDomainFact<
            DungeonStory.Environment.EnvironmentalFireSuppressionCompletedFact>(
                "log:environment.fire-suppression-completed",
                EnvironmentalFireActionFactMapping.CreateRequest,
                WorkCompletionNotificationMode.ApplyOnly);
        builder.Register<EnvironmentalFireActionFactPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>().As<IDungeonCapturedSavePreflightValidator>();
        builder.RegisterGameplayDomainFact<CharacterEnvironmentExposureFact, EnvironmentalExposureThresholdReceipt>(
            "log:environment.exposure-threshold", fact =>
                EnvironmentConditionOutcomeReceiptFactory.CreateEnvironmentalExposureThreshold(fact.Snapshot()));
        builder.RegisterGameplayDomainFact<EnvironmentalControlFact, EnvironmentalControlCommandReceipt>(
            "log:environment.control-command", fact =>
                EnvironmentConditionOutcomeReceiptFactory.CreateEnvironmentalControl(fact.Snapshot()));
        builder.RegisterGameplayDomainFact<RoomEnvironmentExperienceFact, RoomEnvironmentExperienceOutcomeReceipt>(
            "log:environment.room-experience", RoomEnvironmentFactObserverMapping.CaptureExperience);
        builder.RegisterGameplayDomainFact<RoomEnvironmentConditionFact, RoomConditionOutcomeReceipt>(
            "log:environment.room-condition", RoomEnvironmentFactObserverMapping.CaptureCondition);
        builder.RegisterGameplayDomainFact<CertifiedSeedCompletedFact, CertifiedSeedCompletionOutcomeReceipt>(
            "log:production.certified-seed-completed", CertifiedSeedFactObserverMapping.Capture);
        builder.Register<CertifiedSeedFactPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>().As<IDungeonCapturedSavePreflightValidator>();
        builder.RegisterGameplayDomainFact<CropSelectionChangedFact, CropSelectionCommandReceipt>(
            "log:agriculture.crop-selection", CropCommandFactMapping.Capture);
        builder.RegisterGameplayDomainFact<CropCycleTerminalFact, CropPlanGameplayOutcomeReceipt>(
            "log:agriculture.crop-cycle-terminal", CropCycleTerminalFactMapping.Capture);
        builder.RegisterWorkCompletionDomainFact<CropHarvestCompletedFact>(
            "log:agriculture.harvest-completed", CropHarvestCompletionObservation.CreateRequest,
            WorkCompletionNotificationMode.ApplyOnly);
        builder.Register<CropPlotRetirementObserver>(Lifetime.Singleton);
        builder.RegisterDeferredDomainFactBinding<CropPlotRetiredFact>((resolver, dispatcher) =>
            dispatcher.Subscribe("log:agriculture.plot-retired", resolver.Resolve<CropPlotRetirementObserver>()));
        builder.RegisterGameplayDomainFact<GoldenHarvestScheduledFact, GoldenHarvestScheduleReceipt>(
            "log:agriculture.golden-harvest-schedule", CropCommandFactMapping.Capture);
        builder.RegisterGameplayDomainFact<CropIrrigationSuppliedFact, CropIrrigationSupplyOutcomeReceipt>(
            "log:agriculture.irrigation-supply", CropIrrigationFactMapping.Capture);
        builder.Register(resolver =>
            new GameplayDomainFactObserverBinding<CropTreatmentFact, CropTreatmentOutcomeReceipt>(
                "log:agriculture.crop-treatment", CropTreatmentFactMapping.Capture,
                resolver.Resolve<IGameplayOutcomeRegistry>(), resolver.Resolve<GameplayOutcomeLedger>(),
                resolver.Resolve<IGameplayOutcomeRecorder>(), resolver.Resolve<GameplayDomainFactCommitGroup>(),
                CropTreatmentFactMapping.Reserve), Lifetime.Singleton)
            .As<DungeonStory.Foundation.Facts.IDomainFactObserverBinding>();
        builder.Register(resolver =>
            new GameplayDomainFactObserverBinding<CropWaterRefillFact, CropManualWaterRefillOutcomeReceipt>(
                "log:agriculture.manual-water-refill",
                new CropWaterRefillFactMapping(resolver.Resolve<IGameplayOutcomeDiagnosticsQuery>()).Capture,
                resolver.Resolve<IGameplayOutcomeRegistry>(),
                resolver.Resolve<GameplayOutcomeLedger>(),
                resolver.Resolve<IGameplayOutcomeRecorder>(),
                resolver.Resolve<GameplayDomainFactCommitGroup>(),
                CropWaterRefillFactMapping.Reserve), Lifetime.Singleton)
            .As<DungeonStory.Foundation.Facts.IDomainFactObserverBinding>();
        builder.RegisterGameplayDomainFact<PopulationDiseaseExposureFact, PopulationDiseaseExposureOutcomeReceipt>(
            "log:environment.disease-exposure", PopulationDiseaseExposureFactMapping.Capture);

        builder.RegisterGameplayActionLogs(nameof(CertifiedSeedCompletionOutcomeAdapter));
        builder.RegisterGameplayActionLogs(nameof(CropPlanGameplayOutcomeAdapter));
        builder.RegisterGameplayActionLogs(nameof(CropIrrigationSupplyOutcomeAdapter));
        builder.RegisterGameplayActionLogs(nameof(CropTreatmentOutcomeAdapter));
        builder.RegisterGameplayActionLogs(nameof(CropSelectionCommandOutcomeAdapter));
        builder.RegisterGameplayActionLogs(nameof(GoldenHarvestScheduleOutcomeAdapter));
        builder.RegisterGameplayActionLogs(nameof(CropManualWaterRefillOutcomeAdapter));
        builder.RegisterGameplayActionLogs(nameof(EnvironmentalControlCommandOutcomeAdapter));
        builder.RegisterGameplayActionLogs(nameof(EnvironmentalExposureThresholdOutcomeAdapter));
        builder.RegisterGameplayActionLogs(nameof(WildlifeFoodRaidOutcomeAdapter));
        builder.RegisterGameplayActionLogs(nameof(WildlifeCommandOutcomeAdapter));
        builder.RegisterGameplayActionLogs(nameof(WildlifeActionOutcomeAdapter));
        builder.RegisterGameplayActionLogs(nameof(EnvironmentalFireIgnitionOutcomeAdapter));
        builder.RegisterGameplayActionLogs(nameof(EnvironmentalFireSuppressionOutcomeAdapter));
        builder.RegisterGameplayActionLogs(nameof(EnvironmentalFireDamageOutcomeAdapter));
        builder.RegisterGameplayActionLogs(nameof(EnvironmentalFireFuelOutcomeAdapter));
        builder.RegisterGameplayActionLogs(nameof(EnvironmentalFireWaterOutcomeAdapter));
        builder.RegisterGameplayActionLogs(nameof(PopulationDiseaseExposureOutcomeAdapter));
        builder.RegisterGameplayActionLogs(nameof(DiseaseAnatomyBurdenOutcomeAdapter));
        builder.RegisterGameplayActionLogs(nameof(ProcessAccidentOutcomeAdapter));
        builder.RegisterGameplayActionLogs(nameof(RoomConditionOutcomeAdapter));
        builder.RegisterGameplayActionLogs(nameof(RoomEnvironmentExperienceOutcomeAdapter));
        builder.RegisterGameplayActionLogs(nameof(SpeciesIncidentOutcomeAdapter));
        builder.RegisterGameplayActionLogs(nameof(GolemUpkeepOutcomeAdapter));
        builder.RegisterGameplayActionLogs(nameof(HarpyGaleRelocationOutcomeAdapter));

    }
}
