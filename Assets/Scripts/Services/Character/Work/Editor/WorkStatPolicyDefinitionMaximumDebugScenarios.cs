#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class WorkStatPolicyDefinitionMaximumDebugScenarios
{
    [MenuItem("DungeonStory/V27/Character/Validate Work Stat Policy Definition Maximums")]
    public static void Validate()
    {
        VerifyKnownUnregisteredPolicyIsNeutral();
        VerifyGatheringPolicyMaximums();
        VerifyAnimalCarePolicyMaximum();
        VerifyRegistrationOrderDoesNotChangeDigest();
        VerifyMissingMaximumSourceFailsLoud();
        VerifyUnknownWorkTypeFailsLoud();
        Debug.Log(
            "[WorkStatPolicyDefinitionMaximum] focused scenarios passed.");
    }

    private static void VerifyKnownUnregisteredPolicyIsNeutral()
    {
        WorkStatPolicyRegistry first = Registry();
        WorkStatPolicyRegistry second = Registry();
        WorkStatPolicyDefinitionMaximumSnapshot firstSnapshot = first
            .CaptureDefinitionMaximum(BuiltInWorkTypeIds.Craft);
        WorkStatPolicyDefinitionMaximumSnapshot repeatedSnapshot = first
            .CaptureDefinitionMaximum(BuiltInWorkTypeIds.Craft);
        WorkStatPolicyDefinitionMaximumSnapshot secondSnapshot = second
            .CaptureDefinitionMaximum(BuiltInWorkTypeIds.Craft);

        Require(firstSnapshot.WorkTypeId == BuiltInWorkTypeIds.Craft
                && Exactly(firstSnapshot.MaximumMultiplier, 1d),
            "A known work type without a registered policy was not explicitly neutral.");
        Require(string.Equals(
                    firstSnapshot.SourceDigest,
                    repeatedSnapshot.SourceDigest,
                    StringComparison.Ordinal)
                && string.Equals(
                    firstSnapshot.SourceDigest,
                    secondSnapshot.SourceDigest,
                    StringComparison.Ordinal),
            "The neutral work-stat maximum digest is not deterministic.");
    }

    private static void VerifyGatheringPolicyMaximums()
    {
        WorkStatPolicyRegistry registry = Registry(
            new GatheringStatPolicy(new EmptyFacilityCapabilityQuery()));

        RequireMaximum(registry, BuiltInWorkTypeIds.Gather, 1.10d);
        RequireMaximum(
            registry,
            BuiltInWorkTypeIds.Logging,
            1.08d * 1.08d);
        RequireMaximum(registry, BuiltInWorkTypeIds.Sow, 1d);
        RequireMaximum(registry, BuiltInWorkTypeIds.Harvest, 1d);
        RequireMaximum(registry, BuiltInWorkTypeIds.Quarry, 1d);
    }

    private static void VerifyAnimalCarePolicyMaximum()
    {
        WorkStatPolicyRegistry registry = Registry(
            new AnimalCareStatPolicy(new EmptyFacilityCapabilityQuery()));
        RequireMaximum(
            registry,
            BuiltInWorkTypeIds.AnimalCare,
            1.04d * 1.04d * 1.04d * 1.04d);
    }

    private static void VerifyRegistrationOrderDoesNotChangeDigest()
    {
        IFacilityCapabilityQuery facilities = new EmptyFacilityCapabilityQuery();
        WorkStatPolicyRegistry gatheringFirst = Registry(
            new GatheringStatPolicy(facilities),
            new AnimalCareStatPolicy(facilities));
        WorkStatPolicyRegistry animalFirst = Registry(
            new AnimalCareStatPolicy(facilities),
            new GatheringStatPolicy(facilities));

        WorkTypeId[] audited =
        {
            BuiltInWorkTypeIds.Gather,
            BuiltInWorkTypeIds.Logging,
            BuiltInWorkTypeIds.Sow,
            BuiltInWorkTypeIds.Harvest,
            BuiltInWorkTypeIds.Quarry,
            BuiltInWorkTypeIds.AnimalCare
        };
        foreach (WorkTypeId workTypeId in audited)
        {
            WorkStatPolicyDefinitionMaximumSnapshot first = gatheringFirst
                .CaptureDefinitionMaximum(workTypeId);
            WorkStatPolicyDefinitionMaximumSnapshot second = animalFirst
                .CaptureDefinitionMaximum(workTypeId);
            Require(Exactly(first.MaximumMultiplier, second.MaximumMultiplier)
                    && string.Equals(
                        first.SourceDigest,
                        second.SourceDigest,
                        StringComparison.Ordinal),
                "Registration order changed the maximum or digest for '"
                + workTypeId.Value + "'.");
        }
    }

    private static void VerifyMissingMaximumSourceFailsLoud()
    {
        WorkStatPolicyRegistry registry = Registry(new MissingMaximumSourcePolicy());
        RequireThrows<InvalidOperationException>(
            () => registry.CaptureDefinitionMaximum(BuiltInWorkTypeIds.Craft),
            "A registered policy without a definition-maximum source was accepted.");
    }

    private static void VerifyUnknownWorkTypeFailsLoud()
    {
        WorkStatPolicyRegistry registry = Registry();
        RequireThrows<InvalidOperationException>(
            () => registry.CaptureDefinitionMaximum(
                new WorkTypeId("work:qa-unknown")),
            "An unknown work type received an implicit neutral maximum.");
    }

    private static WorkStatPolicyRegistry Registry(
        params IWorkStatPolicy[] policies) => new(policies);

    private static void RequireMaximum(
        IWorkStatPolicyDefinitionMaximumQuery query,
        WorkTypeId workTypeId,
        double expected)
    {
        WorkStatPolicyDefinitionMaximumSnapshot first = query
            .CaptureDefinitionMaximum(workTypeId);
        WorkStatPolicyDefinitionMaximumSnapshot second = query
            .CaptureDefinitionMaximum(workTypeId);
        Require(first.WorkTypeId == workTypeId
                && Exactly(first.MaximumMultiplier, expected),
            "Unexpected work-stat maximum for '" + workTypeId.Value + "': "
            + first.MaximumMultiplier.ToString("R") + ".");
        Require(string.Equals(
                first.SourceDigest,
                second.SourceDigest,
                StringComparison.Ordinal),
            "Repeated capture changed the digest for '" + workTypeId.Value + "'.");
    }

    private static bool Exactly(double left, double right) => left.Equals(right);

    private static void RequireThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class EmptyFacilityCapabilityQuery : IFacilityCapabilityQuery
    {
        public IReadOnlyList<BuildableObject> FindOperational(
            FacilityCapabilityKind capability,
            string buildingDefinitionId = "") => Array.Empty<BuildableObject>();

        public IReadOnlyList<BuildableObject> FindOperational(
            ResearchFacilityCommandKind command) => Array.Empty<BuildableObject>();
    }

    private sealed class MissingMaximumSourcePolicy : IWorkStatPolicy
    {
        private static readonly WorkTypeId[] WorkTypes =
        {
            BuiltInWorkTypeIds.Craft
        };

        public IReadOnlyCollection<WorkTypeId> WorkTypeIds => WorkTypes;

        public float GetWorkSpeedMultiplier(
            CharacterActor actor,
            BuildableObject target) => 1f;
    }
}
#endif
