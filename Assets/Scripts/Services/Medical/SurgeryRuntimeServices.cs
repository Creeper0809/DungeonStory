using System;
using System.Collections.Generic;
using DungeonStory.Foundation;

public sealed class SurgeryContentServices
{
    public SurgeryContentServices(
        ISurgicalProcedureCatalog procedures,
        ISurgicalFacilityQuery facilities,
        ISurgeryRiskEvaluator risk,
        ISurgicalPartRuntime parts,
        ISurgeryPolicyRuntime policies,
        IAnatomyProfileCatalog anatomyProfiles,
        ICharacterSpeciesCatalog species,
        ICharacterSpeciesQuery speciesRuntime,
        IReadOnlyList<ISurgicalProcedureEffectHandler> effects,
        ICharacterPerformanceQuery performance)
    {
        Procedures = procedures ?? throw new ArgumentNullException(nameof(procedures));
        Facilities = facilities ?? throw new ArgumentNullException(nameof(facilities));
        Risk = risk ?? throw new ArgumentNullException(nameof(risk));
        Parts = parts ?? throw new ArgumentNullException(nameof(parts));
        Policies = policies ?? throw new ArgumentNullException(nameof(policies));
        AnatomyProfiles = anatomyProfiles ?? throw new ArgumentNullException(nameof(anatomyProfiles));
        Species = species ?? throw new ArgumentNullException(nameof(species));
        SpeciesRuntime = speciesRuntime
            ?? throw new ArgumentNullException(nameof(speciesRuntime));
        Effects = effects ?? throw new ArgumentNullException(nameof(effects));
        Performance = performance ?? throw new ArgumentNullException(nameof(performance));
    }

    public ISurgicalProcedureCatalog Procedures { get; }
    public ISurgicalFacilityQuery Facilities { get; }
    public ISurgeryRiskEvaluator Risk { get; }
    public ISurgicalPartRuntime Parts { get; }
    public ISurgeryPolicyRuntime Policies { get; }
    public IAnatomyProfileCatalog AnatomyProfiles { get; }
    public ICharacterSpeciesCatalog Species { get; }
    public ICharacterSpeciesQuery SpeciesRuntime { get; }
    public IReadOnlyList<ISurgicalProcedureEffectHandler> Effects { get; }
    public ICharacterPerformanceQuery Performance { get; }
}

public sealed class SurgeryWorldServices
{
    public SurgeryWorldServices(
        ISurgicalCorpseFreshnessRuntime corpseFreshness,
        ICharacterWorldQuery characters,
        IWildlifeWorldQuery wildlife,
        ICaptivityRuntime captivity,
        IBuildingWorldQuery buildings,
        ISurgicalPatientTransportRuntime patientTransport,
        ICharacterMedicalCommand medicalCommands,
        ICharacterBodyHealthQuery bodyHealthQuery)
    {
        CorpseFreshness = corpseFreshness ?? throw new ArgumentNullException(nameof(corpseFreshness));
        Characters = characters ?? throw new ArgumentNullException(nameof(characters));
        Wildlife = wildlife ?? throw new ArgumentNullException(nameof(wildlife));
        Captivity = captivity ?? throw new ArgumentNullException(nameof(captivity));
        Buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
        PatientTransport = patientTransport ?? throw new ArgumentNullException(nameof(patientTransport));
        MedicalCommands = medicalCommands
            ?? throw new ArgumentNullException(nameof(medicalCommands));
        BodyHealthQuery = bodyHealthQuery
            ?? throw new ArgumentNullException(nameof(bodyHealthQuery));
    }

    public ISurgicalCorpseFreshnessRuntime CorpseFreshness { get; }
    public ICharacterWorldQuery Characters { get; }
    public IWildlifeWorldQuery Wildlife { get; }
    public ICaptivityRuntime Captivity { get; }
    public IBuildingWorldQuery Buildings { get; }
    public ISurgicalPatientTransportRuntime PatientTransport { get; }
    public ICharacterMedicalCommand MedicalCommands { get; }
    public ICharacterBodyHealthQuery BodyHealthQuery { get; }
}

public sealed class SurgeryResourceServices
{
    public SurgeryResourceServices(
        ISurgeryExtractionLedger extractionLedger,
        IWorldItemStackRuntime items,
        IAnatomyHealthRuntime anatomy,
        IWildlifeAnatomyHealthRuntime wildlifeAnatomy,
        IBlueprintResearchStateService research,
        IWorkforceReplanService workforce,
        IProcessFluidUseRuntime processFluids,
        IEnvironmentalFieldQuery environmentalField)
    {
        ExtractionLedger = extractionLedger ?? throw new ArgumentNullException(nameof(extractionLedger));
        Items = items ?? throw new ArgumentNullException(nameof(items));
        Anatomy = anatomy ?? throw new ArgumentNullException(nameof(anatomy));
        WildlifeAnatomy = wildlifeAnatomy ?? throw new ArgumentNullException(nameof(wildlifeAnatomy));
        Research = research ?? throw new ArgumentNullException(nameof(research));
        Workforce = workforce ?? throw new ArgumentNullException(nameof(workforce));
        ProcessFluids = processFluids ?? throw new ArgumentNullException(nameof(processFluids));
        EnvironmentalField = environmentalField ?? throw new ArgumentNullException(nameof(environmentalField));
    }

    public ISurgeryExtractionLedger ExtractionLedger { get; }
    public IWorldItemStackRuntime Items { get; }
    public IAnatomyHealthRuntime Anatomy { get; }
    public IWildlifeAnatomyHealthRuntime WildlifeAnatomy { get; }
    public IBlueprintResearchStateService Research { get; }
    public IWorkforceReplanService Workforce { get; }
    public IProcessFluidUseRuntime ProcessFluids { get; }
    public IEnvironmentalFieldQuery EnvironmentalField { get; }
}

public sealed class SurgeryExecutionServices
{
    public SurgeryExecutionServices(
        IGameClock clock,
        IRandomStreamProvider randomStreams,
        ICharacterEnvironmentStatusQuery environmentStatus,
        ISurgeryEnvironmentRiskEvaluator environmentRisk,
        ExtremeTraitRuntime extremeTraits = null,
        IRunSeedProvider runSeedProvider = null,
        CharacterIdentityEventPublisher identityEvents = null)
    {
        Clock = clock ?? throw new ArgumentNullException(nameof(clock));
        EnvironmentStatus = environmentStatus ?? throw new ArgumentNullException(nameof(environmentStatus));
        EnvironmentRisk = environmentRisk ?? throw new ArgumentNullException(nameof(environmentRisk));
        OutcomeRandom = (randomStreams ?? throw new ArgumentNullException(nameof(randomStreams)))
            .Get("medical:surgery-outcomes");
        ExtremeTraits = extremeTraits;
        RunSeedProvider = runSeedProvider;
        IdentityEvents = identityEvents;
    }

    public IGameClock Clock { get; }
    public IRandomStream OutcomeRandom { get; }
    public ICharacterEnvironmentStatusQuery EnvironmentStatus { get; }
    public ISurgeryEnvironmentRiskEvaluator EnvironmentRisk { get; }
    public ExtremeTraitRuntime ExtremeTraits { get; }
    public IRunSeedProvider RunSeedProvider { get; }
    public CharacterIdentityEventPublisher IdentityEvents { get; }
}

internal static class SurgeryRuntimeSupport
{
    internal static SurgicalSubjectRef CreateCharacterSubject(
        CharacterActor actor,
        bool automaticEmergencyDefault)
    {
        return new SurgicalSubjectRef
        {
            kind = SurgicalSubjectKind.Character,
            subjectId = actor?.Identity?.PersistentId ?? string.Empty,
            displayName = actor?.Identity?.DisplayName ?? string.Empty,
            speciesId = actor?.Identity?.SpeciesTag ?? string.Empty,
            willing = actor != null && actor.characterType == CharacterType.NPC,
            automaticEmergencyDefault = automaticEmergencyDefault
        };
    }

    internal static Dictionary<Type, ISurgicalProcedureEffectHandler> BuildEffectIndex(
        IReadOnlyList<ISurgicalProcedureEffectHandler> handlers)
    {
        Dictionary<Type, ISurgicalProcedureEffectHandler> index = new();
        foreach (ISurgicalProcedureEffectHandler handler in
                 handlers ?? Array.Empty<ISurgicalProcedureEffectHandler>())
        {
            if (handler == null || handler.EffectType == null)
            {
                continue;
            }

            if (!index.TryAdd(handler.EffectType, handler))
            {
                throw new InvalidOperationException(
                    $"Duplicate surgical effect handler: {handler.EffectType.Name}");
            }
        }

        return index;
    }
}
