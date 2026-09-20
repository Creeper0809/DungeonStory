using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class FacilityEvolutionConstructorFacadeDebugScenarios
{
    private const int DependencyLimit = 8;

    [MenuItem("Dungeon Story/QA/Architecture/Verify Facility Evolution Constructor Facade")]
    public static void VerifyFromMenu()
    {
        if (!Verify())
        {
            throw new InvalidOperationException(
                "Facility evolution constructor facade verification failed.");
        }

        Debug.Log(
            "[FacilityEvolutionConstructorFacadeDebugScenarios] PASS "
            + "FacilityEvolutionEngine=6, DefinitionContext=7, ExecutionContext=8");
    }

    public static bool Verify()
    {
        Type[] ownedTypes =
        {
            typeof(FacilityEvolutionEngine),
            typeof(FacilityEvolutionDefinitionContext),
            typeof(FacilityEvolutionExecutionContext),
            typeof(FacilityEvolutionEngineFactory)
        };
        bool dependencyLimitSatisfied = ownedTypes
            .SelectMany(type => type.GetConstructors())
            .All(constructor => constructor.GetParameters().Length <= DependencyLimit);
        var engineConstructors = typeof(FacilityEvolutionEngine).GetConstructors();
        bool engineUsesBoundedFacade = engineConstructors.Length == 1
            && engineConstructors[0].GetParameters().Length == 6
            && engineConstructors[0].GetParameters()[0].ParameterType
                == typeof(FacilityEvolutionDefinitionContext)
            && engineConstructors[0].GetParameters()[1].ParameterType
                == typeof(FacilityEvolutionExecutionContext)
            && engineConstructors[0].GetParameters()[2].ParameterType
                == typeof(IFacilityEvolutionOutcomeCommitter)
            && engineConstructors[0].GetParameters()[3].ParameterType
                == typeof(IGameCalendar)
            && engineConstructors[0].GetParameters()[4].ParameterType
                == typeof(IGameplayOutcomeNarrativeEvidenceQuery)
            && engineConstructors[0].GetParameters()[5].ParameterType
                == typeof(IGameplayOutcomeEvidenceUseTransaction);

        return dependencyLimitSatisfied
            && engineUsesBoundedFacade
            && ThrowsArgumentNull(() => new FacilityEvolutionEngine(null, null))
            && ThrowsArgumentNull(() => new FacilityEvolutionDefinitionContext(
                null,
                null,
                null,
                null,
                null,
                null,
                null))
            && ThrowsArgumentNull(() => new FacilityEvolutionExecutionContext(
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null));
    }

    private static bool ThrowsArgumentNull(Action create)
    {
        try
        {
            create();
        }
        catch (ArgumentNullException)
        {
            return true;
        }

        return false;
    }
}
