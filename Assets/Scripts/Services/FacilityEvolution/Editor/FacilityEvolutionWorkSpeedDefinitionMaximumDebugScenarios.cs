#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class FacilityEvolutionWorkSpeedDefinitionMaximumDebugScenarios
{
    [MenuItem("DungeonStory/V27/Facility/Validate Evolution Work Speed Definition Maximums")]
    public static void Validate()
    {
        VerifyCurrentServiceOperateMaximum();
        VerifyNonServiceAndOtherWorkRemainNeutral();
        VerifyModuleOrderDoesNotChangeDigest();
        VerifyNoPositiveServiceModuleRemainsNeutral();
        VerifyInvalidInputsFailLoud();
        Debug.Log(
            "[FacilityEvolutionWorkSpeedDefinitionMaximum] focused scenarios passed.");
    }

    private static void VerifyCurrentServiceOperateMaximum()
    {
        BuildingSO service = Definition(
            "building:qa-evolution-service",
            FacilityRole.Meal);
        try
        {
            FacilityEvolutionWorkSpeedDefinitionMaximumSnapshot snapshot =
                new FacilityEvolutionWorkSpeedDefinitionMaximumQuery(
                        new EvolutionModuleRegistry())
                    .Capture(service, BuiltInWorkTypeIds.Operate);
            Require(snapshot.AppliesServiceSpeed
                    && snapshot.FacilityRoles == FacilityRole.Meal
                    && Exactly(snapshot.MaximumMultiplier, 8d),
                "The current service.speed module registry did not saturate at 8.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(service);
        }
    }

    private static void VerifyNonServiceAndOtherWorkRemainNeutral()
    {
        BuildingSO nonService = Definition(
            "building:qa-evolution-nonservice",
            FacilityRole.Research);
        BuildingSO service = Definition(
            "building:qa-evolution-other-work",
            FacilityRole.Rest);
        try
        {
            FacilityEvolutionWorkSpeedDefinitionMaximumQuery query = new(
                new EvolutionModuleRegistry());
            FacilityEvolutionWorkSpeedDefinitionMaximumSnapshot nonServiceOperate =
                query.Capture(nonService, BuiltInWorkTypeIds.Operate);
            FacilityEvolutionWorkSpeedDefinitionMaximumSnapshot serviceCraft =
                query.Capture(service, BuiltInWorkTypeIds.Craft);
            Require(!nonServiceOperate.AppliesServiceSpeed
                    && Exactly(nonServiceOperate.MaximumMultiplier, 1d),
                "A non-service Operate definition received service.speed.");
            Require(!serviceCraft.AppliesServiceSpeed
                    && Exactly(serviceCraft.MaximumMultiplier, 1d),
                "A non-Operate work type received service.speed.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(nonService);
            UnityEngine.Object.DestroyImmediate(service);
        }
    }

    private static void VerifyModuleOrderDoesNotChangeDigest()
    {
        EvolutionModuleDefinition benefit = Module(
            "facility:qa-service-benefit",
            Benefit("service.speed", 1.05f),
            Burden("staff.required", 1f));
        EvolutionModuleDefinition burden = Module(
            "facility:qa-service-burden",
            Benefit("work.output", 1.01f),
            Burden("service.speed", 1.02f));
        BuildingSO service = Definition(
            "building:qa-evolution-shuffle",
            FacilityRole.Hygiene);
        try
        {
            FacilityEvolutionWorkSpeedDefinitionMaximumSnapshot first =
                new FacilityEvolutionWorkSpeedDefinitionMaximumQuery(
                        new FixtureRegistry(new[] { benefit, burden }))
                    .Capture(service, BuiltInWorkTypeIds.Operate);
            FacilityEvolutionWorkSpeedDefinitionMaximumSnapshot second =
                new FacilityEvolutionWorkSpeedDefinitionMaximumQuery(
                        new FixtureRegistry(new[] { burden, benefit }))
                    .Capture(service, BuiltInWorkTypeIds.Operate);
            Require(Exactly(first.MaximumMultiplier, second.MaximumMultiplier)
                    && string.Equals(
                        first.SourceDigest,
                        second.SourceDigest,
                        StringComparison.Ordinal),
                "Evolution module registration order changed the maximum digest.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(service);
        }
    }

    private static void VerifyNoPositiveServiceModuleRemainsNeutral()
    {
        EvolutionModuleDefinition unrelated = Module(
            "facility:qa-unrelated",
            Benefit("work.output", 1.5f),
            Burden("maintenance.work", 1.2f));
        BuildingSO service = Definition(
            "building:qa-evolution-no-positive-service",
            FacilityRole.Purchase);
        try
        {
            FacilityEvolutionWorkSpeedDefinitionMaximumSnapshot snapshot =
                new FacilityEvolutionWorkSpeedDefinitionMaximumQuery(
                        new FixtureRegistry(new[] { unrelated }))
                    .Capture(service, BuiltInWorkTypeIds.Operate);
            Require(snapshot.AppliesServiceSpeed
                    && Exactly(snapshot.MaximumMultiplier, 1d),
                "A registry without service.speed modifiers was not neutral.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(service);
        }
    }

    private static void VerifyInvalidInputsFailLoud()
    {
        EvolutionModuleDefinition invalid = Module(
            "facility:qa-invalid-service",
            Benefit("service.speed", 1.1f),
            Burden("maintenance.work", 1f));
        invalid.Benefits[0].multiplier = float.NaN;
        BuildingSO service = Definition(
            "building:qa-evolution-invalid",
            FacilityRole.Training);
        try
        {
            FacilityEvolutionWorkSpeedDefinitionMaximumQuery invalidQuery = new(
                new FixtureRegistry(new[] { invalid }));
            RequireThrows<InvalidOperationException>(
                () => invalidQuery.Capture(
                    service,
                    BuiltInWorkTypeIds.Operate),
                "A non-finite service.speed modifier was accepted.");

            FacilityEvolutionWorkSpeedDefinitionMaximumQuery validQuery = new(
                new FixtureRegistry(Array.Empty<EvolutionModuleDefinition>()));
            RequireThrows<ArgumentNullException>(
                () => validQuery.Capture(null, BuiltInWorkTypeIds.Operate),
                "A null building definition was accepted.");
            RequireThrows<InvalidOperationException>(
                () => validQuery.Capture(
                    service,
                    new WorkTypeId("work:qa-unknown")),
                "An unknown work type received an implicit neutral maximum.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(service);
        }
    }

    private static BuildingSO Definition(string definitionId, FacilityRole roles)
    {
        BuildingSO definition = ScriptableObject.CreateInstance<BuildingSO>();
        definition.ConfigureAuthoredContentIdentity(
            definitionId,
            1,
            "facility evolution work-speed maximum QA");
        definition.Facility = new FacilityData
        {
            roles = roles,
            capacity = 1
        };
        return definition;
    }

    private static EvolutionModuleDefinition Module(
        string moduleId,
        EvolutionEffectModifier benefit,
        EvolutionEffectModifier burden) => new(
        moduleId,
        moduleId,
        "qa",
        new[] { benefit },
        new[] { burden });

    private static EvolutionEffectModifier Benefit(
        string statId,
        float multiplier) => new()
    {
        statId = statId,
        multiplier = multiplier
    };

    private static EvolutionEffectModifier Burden(
        string statId,
        float multiplier) => Benefit(statId, multiplier);

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

    private sealed class FixtureRegistry : IEvolutionModuleRegistry
    {
        private readonly IReadOnlyDictionary<string, EvolutionModuleDefinition> byId;

        public FixtureRegistry(IEnumerable<EvolutionModuleDefinition> modules)
        {
            EvolutionModuleDefinition[] authored = (modules
                    ?? Array.Empty<EvolutionModuleDefinition>())
                .ToArray();
            All = Array.AsReadOnly(authored);
            byId = authored.ToDictionary(
                module => module.ModuleId,
                StringComparer.Ordinal);
        }

        public IReadOnlyList<EvolutionModuleDefinition> All { get; }

        public bool TryGet(
            string moduleId,
            out EvolutionModuleDefinition definition) => byId.TryGetValue(
            moduleId ?? string.Empty,
            out definition);
    }
}
#endif
