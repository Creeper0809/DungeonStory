using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CharacterAiActionDescriptorDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Character/Run AI Action Descriptor Scenarios")]
    public static void RunFromMenu()
    {
        if (!RunAll(true))
        {
            Debug.LogError("AI action descriptor scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> errors = new List<string>();
        VerifyCustomActionDescriptor(errors);
        VerifyBuiltInDescriptors(errors);
        VerifyFacilitySemanticTags(errors);

        if (errors.Count > 0)
        {
            for (int i = 0; i < errors.Count; i++)
            {
                Debug.LogError(errors[i]);
            }

            return false;
        }

        if (logSuccess)
        {
            Debug.Log("AI action descriptor scenarios passed.");
        }

        return true;
    }

    private static void VerifyCustomActionDescriptor(List<string> errors)
    {
        TestDescriptorAction action = ScriptableObject.CreateInstance<TestDescriptorAction>();
        try
        {
            CharacterAiPersonality personality = new CharacterAiPersonality
            {
                curiosity = 1.7f,
                diligence = 0.4f
            };
            WorkJobGiver workGiver = new WorkJobGiver();
            CharacterAiJobGiverCatalog catalog = new CharacterAiJobGiverCatalog();
            TestJobGiver replacement = new TestJobGiver();
            catalog.Register(replacement, replace: true);

            action.Execute(null);
            bool valid = CharacterAiDecisionPipeline.GetBranchForActionSet(action)
                    == CharacterAiBranch.Work
                && CharacterMoodImpulseUtility.GetBranchForActionSet(action)
                    == CharacterAiBranch.Work
                && workGiver.MatchesAction(action)
                && Mathf.Approximately(personality.GetActionMultiplier(action), 1.7f)
                && catalog.Get(CharacterAiBranch.Work) == replacement
                && action.Executed
                && action.GetDisplayLabel() == "확장 액션";
            if (!valid)
            {
                errors.Add("Custom AI action did not flow through descriptor branch/tag/execution contracts.");
            }
        }
        finally
        {
            Object.DestroyImmediate(action);
        }
    }

    private static void VerifyBuiltInDescriptors(List<string> errors)
    {
        AIWork work = ScriptableObject.CreateInstance<AIWork>();
        AIShopping shopping = ScriptableObject.CreateInstance<AIShopping>();
        AIFacilityRoleAction role = ScriptableObject.CreateInstance<AIFacilityRoleAction>();
        try
        {
            role.Role = FacilityRole.Hygiene;
            bool valid = work.Branch == CharacterAiBranch.Work
                && work.HasSemanticTag(CharacterAiActionTags.Work)
                && shopping.Branch == CharacterAiBranch.Shopping
                && shopping.HasSemanticTag(CharacterAiActionTags.Shopping)
                && role.Branch == CharacterAiBranch.Hygiene
                && role.HasSemanticTag(CharacterAiActionTags.SelfCare);
            if (!valid)
            {
                errors.Add("One or more built-in AI actions has incomplete descriptor metadata.");
            }
        }
        finally
        {
            Object.DestroyImmediate(work);
            Object.DestroyImmediate(shopping);
            Object.DestroyImmediate(role);
        }
    }

    private static void VerifyFacilitySemanticTags(List<string> errors)
    {
        BuildingSO building = ScriptableObject.CreateInstance<BuildingSO>();
        try
        {
            building.objectName = "Meal";
            building.ReplaceAbilities(new BuildingAbilityCollection());
            building.AbilityModules.Add(new BuildingFacilityAbility
            {
                settings = new FacilityData { roles = FacilityRole.Research | FacilityRole.Mana }
            });
            building.AbilityModules.Add(new BuildingSemanticTagsAbility
            {
                tags = new[] { "Alchemy", " research " }
            });

            bool valid = building.HasSemanticTag("Research")
                && building.HasSemanticTag("mana")
                && building.HasSemanticTag("alchemy")
                && !building.HasSemanticTag("Meal")
                && building.GetSemanticTags().Count() == 3;
            if (!valid)
            {
                errors.Add("Facility semantic matching still depends on display names or enum formatting.");
            }
        }
        finally
        {
            Object.DestroyImmediate(building);
        }
    }

    private sealed class TestDescriptorAction : AIActionSet
    {
        private static readonly CharacterAiActionDescriptor TestDescriptor =
            new CharacterAiActionDescriptor(
                CharacterAiBranch.Work,
                "확장 액션",
                CharacterAiActionTags.Curiosity);

        public bool Executed { get; private set; }
        public override CharacterAiActionDescriptor Descriptor => TestDescriptor;

        public override void Execute(CharacterActor actor)
        {
            Executed = true;
        }
    }

    private sealed class TestJobGiver : CharacterAiJobGiver
    {
        public override CharacterAiBranch Branch => CharacterAiBranch.Work;
        public override string Name => "TestJobGiver";

        protected override float GetDomainScore(CharacterActor actor, out string reason)
        {
            reason = "test";
            return 1f;
        }
    }
}
