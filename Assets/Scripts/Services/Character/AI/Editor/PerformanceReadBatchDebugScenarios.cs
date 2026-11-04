#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;

public static class PerformanceReadBatchDebugScenarios
{
    public static void VerifyForBatch()
    {
        var go = new GameObject("Controlled performance read batch");
        go.SetActive(false);
        var actor = go.AddComponent<CharacterActor>();
        var definition = ScriptableObject.CreateInstance<GameplayEffectDefinitionSO>();
        var condition = ScriptableObject.CreateInstance<GameplayEffectConditionDefinitionSO>();
        try
        {
            definition.Configure(1, "qa:effect", "qa:value", GameplayEffectOperation.AddFlat,
                default, GameplayEffectSourceKind.Equipment, GameplayEffectStackingPolicy.StackAll, -100f, 100f);
            condition.Configure(1, "qa:active", "같은 조회의 조건 분리");
            var binding = new GameplayEffectBinding
                { bindingId = "qa:binding", definition = definition, condition = condition, value = 2f };
            var equipment = new Equipment(binding);
            var projector = new CharacterDerivedStatsSnapshotProjector(new Content(), equipment,
                new Transient(), new ExtremeTraitRuntime(new CharacterIdentityStateStore()), new Clock());
            var active = new GameplayEffectContext(new[] { "qa:active" });
            var read = projector.CreateReadProjection(actor);
            Require(read.ProjectValue("qa:value", 1f, active).Value == 3f, "Batch lost an authored effect.");
            Require(read.ProjectValue("qa:value", 7f, active).Value == 9f, "Batch reused a previous base value.");
            Require(read.ProjectValue("qa:value", 1f).Value == 1f, "Batch reused a previous condition.");
            Require(equipment.Calls == 1, "Batch collected its same read input more than once.");
            binding.value = 4f;
            Require(projector.CreateReadProjection(actor).ProjectValue("qa:value", 1f, active).Value == 5f
                && equipment.Calls == 2, "A new query retained old effect input.");
            equipment.Reject = true;
            try
            {
                projector.CreateReadProjection(actor).ProjectValue("qa:value", 1f, active);
                throw new InvalidOperationException("A cache hid a newly rejected input.");
            }
            catch (InvalidOperationException exception) when (exception.Message == "controlled-invalid-source") { }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(condition);
            UnityEngine.Object.DestroyImmediate(definition);
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    private sealed class Content : IGameContentDefinitionSource
    {
        public IReadOnlyList<T> GetAll<T>() where T : ScriptableObject => Array.Empty<T>();
        public T RequireSingle<T>() where T : ScriptableObject => throw new InvalidOperationException("No authored fixture singleton.");
    }
    private sealed class Equipment : ICharacterEquipmentGameplayEffectSourceQuery, IGameplayEffectSource
    {
        public int Calls;
        public bool Reject;
        public Equipment(GameplayEffectBinding binding) => Effects = new[] { binding };
        public GameplayEffectSourceRef SourceRef => new(GameplayEffectSourceKind.Equipment, "qa:equipment");
        public IReadOnlyList<GameplayEffectBinding> Effects { get; }
        public IReadOnlyList<IGameplayEffectSource> GetEquipmentSources(CharacterActor actor)
        { Calls++; if (Reject) throw new InvalidOperationException("controlled-invalid-source"); return new IGameplayEffectSource[] { this }; }
    }
    private sealed class Transient : ICharacterTransientGameplayEffectSourceQuery
    {
        public IReadOnlyList<IGameplayEffectSource> GetStatusSources(CharacterActor actor) => Array.Empty<IGameplayEffectSource>();
        public IReadOnlyList<IGameplayEffectSource> GetCompletedResearchSources(CharacterActor actor) => Array.Empty<IGameplayEffectSource>();
    }
    private sealed class Clock : IGameClock
    {
        public float DeltaTime => 0;
        public float Time => 0;
        public int FrameCount => 0;
        public bool IsPaused => false;
    }
    private static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }
}
#endif
