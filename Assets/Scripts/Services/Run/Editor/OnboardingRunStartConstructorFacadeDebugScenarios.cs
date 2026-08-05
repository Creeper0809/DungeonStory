using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class OnboardingRunStartConstructorFacadeDebugScenarios
{
    private const int DependencyLimit = 8;

    [MenuItem("Dungeon Story/QA/Architecture/Verify Onboarding Run Start Constructor Facades")]
    public static void VerifyFromMenu()
    {
        if (!Verify())
        {
            throw new InvalidOperationException(
                "Onboarding/run-start constructor facade verification failed.");
        }

        Debug.Log(
            "[OnboardingRunStartConstructorFacadeDebugScenarios] PASS "
            + "PreparedStartPartyGameplayApplier=2, FirstRunObjectiveRuntime=2");
    }

    public static bool Verify()
    {
        Type[] ownedTypes =
        {
            typeof(PreparedStartPartyGameplayApplier),
            typeof(PreparedStartPartyCharacterContext),
            typeof(PreparedStartPartyWorldContext),
            typeof(FirstRunObjectiveRuntime),
            typeof(FirstRunObjectiveProgressContext),
            typeof(FirstRunObjectivePresentationContext)
        };
        bool dependencyLimitSatisfied = ownedTypes
            .SelectMany(type => type.GetConstructors())
            .All(constructor => constructor.GetParameters().Length <= DependencyLimit);

        return dependencyLimitSatisfied
            && HasExactContextConstructor(
                typeof(PreparedStartPartyGameplayApplier),
                typeof(PreparedStartPartyCharacterContext),
                typeof(PreparedStartPartyWorldContext))
            && HasExactContextConstructor(
                typeof(FirstRunObjectiveRuntime),
                typeof(FirstRunObjectiveProgressContext),
                typeof(FirstRunObjectivePresentationContext))
            && ThrowsArgumentNull(
                () => new PreparedStartPartyGameplayApplier(null, null))
            && ThrowsArgumentNull(
                () => new FirstRunObjectiveRuntime(null, null))
            && ThrowsArgumentNull(
                () => new PreparedStartPartyCharacterContext(
                    null,
                    null,
                    null,
                    null,
                    null))
            && ThrowsArgumentNull(
                () => new PreparedStartPartyWorldContext(
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null))
            && ThrowsArgumentNull(
                () => new FirstRunObjectiveProgressContext(
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null))
            && ThrowsArgumentNull(
                () => new FirstRunObjectivePresentationContext(
                    null,
                    null,
                    null));
    }

    private static bool HasExactContextConstructor(
        Type target,
        Type firstContext,
        Type secondContext)
    {
        var constructors = target.GetConstructors();
        return constructors.Length == 1
            && constructors[0].GetParameters().Length == 2
            && constructors[0].GetParameters()[0].ParameterType == firstContext
            && constructors[0].GetParameters()[1].ParameterType == secondContext;
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
