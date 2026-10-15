#if UNITY_EDITOR
using System;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;

public static class CharacterDeprivationExperienceFactDebugScenarios
{
    public static string Run()
    {
        var gameObject = new GameObject("Deprivation experience observer fixture");
        CharacterSO definition = null;
        using var facts = new DomainFactDispatcher();
        try
        {
            gameObject.AddComponent<SpriteRenderer>();
            var actor = gameObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(gameObject);
            definition = CharacterAiEditorTestDependencies.CreateCharacterFixtureData(CharacterType.NPC, "경험 관찰자", "Orc");
            actor.Initialization(definition);
            actor.Identity.SetPersistentId("character:deprivation-experience-test");
            using var binding = new CharacterDeprivationExperienceFactBinding().Connect(facts);
            var state = new CharacterDeprivationStateStore(new DungeonRuntimeAggregateRootStore());
            var consequences = new CharacterDeprivationConsequences(state,
                CharacterAiEditorTestDependencies.WorldRegistry, new EditorFixedGameCalendar(3), facts);
            consequences.RecordTaboo(actor, " 금기를 경험했다 ");
            var personal = actor.Progression.NarrativeLedger.Facts.Single(value => value.factId == "survival/taboo");
            Require(personal.domain == CharacterNarrativeDomain.Survival && personal.subjectId == string.Empty
                && personal.outcome == "금기를 경험했다" && personal.totalValue == 1f && personal.lastDay == 3
                && personal.count == 1 && state.Capture().Single().tabooMemories.Contains("금기를 경험했다"),
                "personal experience changed its state or narrative projection");
            var witnessed = new CharacterDeprivationExperienceFact(CharacterDeprivationExperienceKind.WitnessedTaboo,
                actor, "character:witness-source", "금기를 목격했다", -8f, 3);
            Require(facts.TryReserve(witnessed, out var lease, out string failure), failure);
            Require(facts.Publish(lease, witnessed).Completed, "witness projection did not complete");
            var witness = actor.Progression.NarrativeLedger.Facts.Single(value => value.factId == "survival/taboo-witness");
            Require(witness.domain == CharacterNarrativeDomain.Relationship && witness.subjectId == witnessed.SubjectId
                && witness.outcome == witnessed.Memory && witness.totalValue == -8f && witness.count == 1,
                "witness projection changed the original evidence");
            Require(!facts.Publish(lease, witnessed).Completed && witness.count == 1,
                "reusing one notification lease recorded a second experience");
            binding.Dispose();
            Require(!facts.TryReserve(witnessed, out _, out _) && witness.count == 1,
                "detached observer still accepted notifications");
            return "deprivation-personal-producer/witness-observer/exact-evidence/lease-and-detach=PASS;natural-AI=NOT-RUN";
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
            if (definition != null) UnityEngine.Object.DestroyImmediate(definition);
        }
    }

    private static void Require(bool condition, string detail)
    { if (!condition) throw new InvalidOperationException(detail); }
}
#endif
