using System;
using System.Collections.Generic;
using System.Linq;

public static partial class InstanceEvolutionDebugScenarios
{
    public static void VerifyCapabilityAlignedGenerationForBatch()
    {
        using FacilityFixture service = new FacilityFixture("Service offer A");
        using FacilityFixture serviceRepeat = new FacilityFixture("Service offer B");
        using FacilityFixture support = new FacilityFixture("Support offer");
        using FacilityFixture security = new FacilityFixture("Security offer");

        ConfigureOfferFacility(service, FacilityRole.Meal, BuiltInWorkTypeIds.Operate);
        ConfigureOfferFacility(serviceRepeat, FacilityRole.Meal, BuiltInWorkTypeIds.Operate);
        support.Data.AbilityModules.Add(new BuildingServiceSupportAbility
        {
            supportId = "service:qa-offer",
            featureTags = new[] { "service:display" },
            compatibleHubTags = new[] { "service:retail" },
            modifierType = ServiceSupportModifierType.Security,
            workSpeedMultiplier = 1f
        });
        ConfigureOfferFacility(security, FacilityRole.Security, BuiltInWorkTypeIds.Guard);
        security.Data.AbilityModules.Add(new BuildingSecurityAbility
        {
            maxAlarmCharges = 3,
            chargesPerGuardWork = 1
        });

        FacilityInstanceEvolutionRuntime runtime = new FacilityInstanceEvolutionRuntime(
            new FacilityEvolutionStateComponentFactory(),
            new UsageLedgerCompactor(),
            new EvolutionModuleRegistry(),
            roomEnvironment: null,
            facilityCandidateCache: null,
            worldItems: null,
            relocationWorld: new NoopRelocationWorldService(),
            runSeedProvider: new FixedRunSeedProvider(0));

        IReadOnlyList<FacilityGenerationCandidate> serviceOffers =
            RecordOfferUsage(runtime, service, "facility:qa-service");
        IReadOnlyList<FacilityGenerationCandidate> repeatedOffers =
            RecordOfferUsage(runtime, serviceRepeat, "facility:qa-service");
        IReadOnlyList<FacilityGenerationCandidate> supportOffers =
            RecordOfferUsage(runtime, support, "facility:qa-support");
        IReadOnlyList<FacilityGenerationCandidate> securityOffers =
            RecordOfferUsage(runtime, security, "facility:qa-security");

        Require(serviceOffers.Any(candidate => candidate.benefitModuleId == "facility:service-operations"),
            "service without production did not receive service-operations");
        Require(!serviceOffers.Any(candidate => candidate.benefitModuleId == "facility:service"
                                                || candidate.benefitModuleId == "facility:room-synergy"),
            "service received an unreachable or self-cancelling offer");
        Require(serviceOffers.Select(DescribeCandidate).SequenceEqual(
                repeatedOffers.Select(DescribeCandidate), StringComparer.Ordinal),
            "same service identity and history produced different offers");
        Require(supportOffers.Any(candidate => candidate.benefitModuleId == "facility:service-support"),
            "service-support ability did not receive its matching offer");
        Require(securityOffers.Any(candidate => candidate.benefitModuleId == "facility:security-operations")
                && !securityOffers.Any(candidate => candidate.benefitModuleId == "facility:service-operations"),
            "guard-only security offers did not match the target capability");
    }

    private static void ConfigureOfferFacility(
        FacilityFixture fixture, FacilityRole roles, WorkTypeId workType)
    {
        FacilityData facility = new FacilityData
        {
            roles = roles,
            capacity = 1,
            requiredWorkers = 1
        };
        facility.SetSupportedWorkTypeIds(new[] { workType });
        fixture.Data.Facility = facility;
    }

    private static IReadOnlyList<FacilityGenerationCandidate> RecordOfferUsage(
        FacilityInstanceEvolutionRuntime runtime, FacilityFixture fixture, string persistentId)
    {
        fixture.State.ReplaceInstanceEvolution(new FacilityEvolutionState
        {
            facilityPersistentId = persistentId,
            generation = 0,
            mastery = 0f
        });
        runtime.RecordUsage(
            fixture.Building,
            "service.completed",
            FacilityEvolutionProgression.GetRequiredMastery(0),
            3f,
            "worker:qa",
            new[] { "service" });
        return runtime.GetGenerationCandidates(fixture.Building);
    }
}
