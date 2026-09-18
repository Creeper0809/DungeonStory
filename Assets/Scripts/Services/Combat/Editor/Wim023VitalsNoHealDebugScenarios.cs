#if UNITY_EDITOR
using System;
using System.Reflection;
using DungeonStory.Foundation;
using UnityEngine;

// Root-owned authoritative-vitals boundary test, not a live surgical/UI witness.
public static class Wim023VitalsNoHealDebugScenarios
{
    public static string RunFocused()
    {
        var go = new GameObject("WIM023 no-heal maximum-health fixture");
        try
        {
            var actor = go.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(go);
            actor.EnsureRuntimeState();
            actor.Identity.SetPersistentId("character:qa:wim023-no-heal");
            var clock = new UnityGameClock();
            var bus = new GameEventBus();
            var runtime = new CharacterBodyHealthRuntime(
                CharacterAiEditorTestDependencies.WorldRegistry, clock, bus,
                new DynamicFrameWorkBudget(clock, new UnityUiClock()),
                new ResourceAnatomyProfileCatalog(
                    new ResourceGameContentCatalog(new UnityGameContentRootLoader())),
                new DungeonRuntimeAggregateRootStore());
            actor.Stats.ConstructCharacterVitals(new CharacterStatsVitalsService(
                runtime, runtime, bus,
                new CharacterDeathEventFactory(CharacterAiEditorTestDependencies.WorldRegistry,
                    CharacterAiEditorTestDependencies.GameCalendar),
                new NoopOwnerRunLifecycleService()));
            var section = new CharacterBodyHealthSaveSection(runtime);
            runtime.ConfigureVitals(actor, 100f, resetCurrentHealth: true);
            runtime.ApplyLegacyDamage(actor, 50f, "qa:wim023-initial-damage", allowDeath: false);
            AssertVitals(100f, 50f);
            string unrelatedBefore = UnrelatedState();

            // Each expectation is authored independently of the tested calculation.
            runtime.RefreshMaximumHealthPreservingCurrent(actor, 110f);
            AssertVitals(110f, 50f);
            Require(UnrelatedState() == unrelatedBefore, "Maximum refresh altered anatomy, mana or damage state.");
            string installed = section.Capture();
            var report = new DungeonGameRestoreReport();
            section.Restore(installed, section.SectionVersion, report);
            Require(report.Success && section.Capture() == installed, "Current body section round trip drifted.");
            // Poison only the derived UI cache: the actual Stats query must
            // project from the restored aggregate, not pass on its old 110/50.
            // This does not claim world-registry publication or whole-save coverage.
            var applyProjection = typeof(CharacterStats).GetMethod("ApplyVitalsProjection",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(applyProjection != null, "Existing derived UI cache seam is missing.");
            applyProjection.Invoke(actor.Stats, new object[] { new CharacterVitalsSnapshot(77f, 12f, 0f) });
            AssertVitals(110f, 50f);

            for (int i = 0; i < 3; i++)
            {
                runtime.RefreshMaximumHealthPreservingCurrent(actor, 100f);
                AssertVitals(100f, 50f);
                runtime.RefreshMaximumHealthPreservingCurrent(actor, 110f);
                AssertVitals(110f, 50f);
            }
            string beforeNoop = section.Capture();
            runtime.RefreshMaximumHealthPreservingCurrent(actor, 110f);
            Require(section.Capture() == beforeNoop, "No-op refresh changed authoritative state.");

            runtime.RefreshMaximumHealthPreservingCurrent(actor, 40f);
            AssertVitals(40f, 40f);
            runtime.RefreshMaximumHealthPreservingCurrent(actor, 100f);
            AssertVitals(100f, 40f);
            Require(UnrelatedState() == unrelatedBefore, "Clamping vitality modified unrelated body state.");

            runtime.RefreshMaximumHealthPreservingCurrent(actor, .5f);
            AssertVitals(1f, 1f);
            var minimumState = JsonUtility.FromJson<DungeonCharacterBodyHealthSaveData>(section.Capture());
            Require(minimumState.characters[0].maxHealth == 1f,
                "Positive sub-unit maximum diverged between aggregate and UI minimum.");

            foreach (float invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                string before = section.Capture();
                bool rejected = false;
                try { runtime.RefreshMaximumHealthPreservingCurrent(actor, invalid); }
                catch (ArgumentException) { rejected = true; }
                Require(rejected && section.Capture() == before, "Invalid maximum was accepted or partially applied.");
            }

            // Controlled existing restore boundary prepares zero HP without a
            // kill command/owner-run exit; the refresh must never resurrect it.
            runtime.RestoreLegacyVitalsProjection(actor, 100f, 0f, 1f);
            runtime.RefreshMaximumHealthPreservingCurrent(actor, 110f);
            AssertVitals(110f, 0f);
            return "PASS body authority50/100->50/110;current JSON exact+poisoned UI cache refreshed;3replace cycles no healing;clamp40;minimum1 consistent;zero remains0;invalid5 atomic;non-vitals unchanged;scope=controlled body authority, not actual installed-part lifecycle/world restore";

            void AssertVitals(float maximum, float current)
            {
                var snapshot = runtime.GetVitals(actor);
                Require(Mathf.Abs(snapshot.MaximumHealth - maximum) < 0.00001f
                    && Mathf.Abs(snapshot.CurrentHealth - current) < 0.00001f
                    && Mathf.Abs(actor.Stats.MaxHealth - maximum) < 0.00001f
                    && Mathf.Abs(actor.Stats.CurrentHealth - current) < 0.00001f,
                    "Authoritative/UI vitality mismatch or unintended healing.");
            }

            string UnrelatedState()
            {
                var saved = JsonUtility.FromJson<DungeonCharacterBodyHealthSaveData>(section.Capture());
                Require(saved.characters.Count == 1, "Fixture unexpectedly owns multiple body states.");
                saved.characters[0].maxHealth = 0f;
                saved.characters[0].currentHealth = 0f;
                saved.characters[0].injurySeverity = 0f;
                return JsonUtility.ToJson(saved);
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class NoopOwnerRunLifecycleService : IOwnerRunLifecycleService
    {
        public void HandleOwnerDeath(CharacterActor owner, string reason) { }
    }
}
#endif
