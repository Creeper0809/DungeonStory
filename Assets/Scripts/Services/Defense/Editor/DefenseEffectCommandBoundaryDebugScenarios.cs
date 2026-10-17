#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class DefenseEffectCommandBoundaryDebugScenarios
{
    public static void RunForBatch()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Defense boundary checks require isolated batch execution.");
        var cleanup = new List<Object>();
        try
        {
            T Make<T>(float amount, float duration = 0f, int stacks = 1) where T : DefenseEffectSO
            {
                var effect = ScriptableObject.CreateInstance<T>();
                cleanup.Add(effect);
                effect.Configure(amount, duration, stacks, "effect-tag");
                return effect;
            }

            var commands = new RecordingCommands { CombatLogText = "authored-damage-reason" };
            Make<DefenseDamageEffectSO>(9f).Apply(commands);
            commands.Require("damage:9:authored-damage-reason", "tag:effect-tag");

            var guard = Make<DefenseGuardAttackEffectSO>(11f);
            guard.DamageReason = "guard-reason";
            guard.Apply(commands);
            commands.Require("damage:11:guard-reason", "tag:effect-tag");

            Make<DefenseCorrosionEffectSO>(0.2f, 8f, 2).Apply(commands);
            commands.Require("status:Corrosion:0.2:8:2", "tag:effect-tag");
            Make<DefenseSlowEffectSO>(6f, 2f, 2).Apply(commands);
            commands.Require("status:Slow:6:2:2", "delay:6", "tag:effect-tag");

            var burn = Make<DefenseBurnEffectSO>(3f, 7f, 2);
            burn.ConfigureEnvironmentalIgnition(0.7f);
            burn.Apply(commands);
            commands.Require("status:Burn:3:7:2", "ignite:0.7", "tag:effect-tag");

            var charge = Make<DefenseChargeEffectSO>(4f, 5f, 1);
            charge.DischargeThreshold = 3;
            charge.DischargeReason = "discharge-reason";
            commands.StatusStacks = 2;
            charge.Apply(commands);
            commands.Require("status:Charge:4:5:1", "tag:effect-tag 2");
            commands.StatusStacks = 3;
            charge.Apply(commands);
            commands.Require("status:Charge:4:5:1", "tag:effect-tag 3", "clear:Charge",
                "damage:12:discharge-reason", "tag:discharge-reason");

            int configuredRoundTrips = 0;
            foreach (DefenseEffectSO effect in cleanup.OfType<DefenseEffectSO>().ToArray())
            {
                VerifyRoundTrip(effect, cleanup);
                configuredRoundTrips++;
            }
            int authoredRoundTrips = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:DefenseEffectSO", new[] { "Assets/Resources" }))
            {
                var effect = AssetDatabase.LoadAssetAtPath<DefenseEffectSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (effect == null)
                    throw new InvalidOperationException("Authored defense effect could not be loaded: " + guid);
                VerifyRoundTrip(effect, cleanup);
                authoredRoundTrips++;
            }
            if (configuredRoundTrips != 6 || authoredRoundTrips == 0)
                throw new InvalidOperationException("Defense effect serialization coverage was empty or incomplete.");

            VerifyLiveConfiguration();

            string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
            if (string.IsNullOrWhiteSpace(output))
                throw new InvalidOperationException("Missing isolated defense evidence directory.");
            Directory.CreateDirectory(output);
            string result = $"ARCHITECTURE_DEFENSE_COMMAND_BOUNDARY=PASS; effectTypes={configuredRoundTrips}; authoredAssets={authoredRoundTrips}; charge below/at threshold; command order; live configuration; JSON/type/fields unchanged";
            using (var file = new StreamWriter(new FileStream(Path.Combine(output, "defense-command-boundary.txt"),
                       FileMode.CreateNew, FileAccess.Write, FileShare.Read)))
                file.WriteLine(result);
            Debug.Log(result);
        }
        finally
        {
            for (int index = cleanup.Count - 1; index >= 0; index--)
                if (cleanup[index] != null)
                    Object.DestroyImmediate(cleanup[index]);
        }
    }

    private static void VerifyLiveConfiguration()
    {
        using var scenario = new MigratedProducerCombatDefenseEntryE2EDebugScenarios.D06Scenario("live-command-context");
        var definition = new DefenseFacilityData { combatLogText = "before-context" };
        var status = new DefenseStatusRuntimeService(new DefenseStatusRuntimeFactory()).GetOrAdd(scenario.Target);
        var report = new DefenseActivationReport(scenario.Facility, scenario.Target, DefenseTriggerTiming.OnEnter);
        IDefenseEffectCommands commands = new DefenseEffectContext(scenario.Target, status, report, definition);
        if (commands.CombatLogText != "before-context")
            throw new InvalidOperationException("Defense context lost its configured damage reason.");
        definition.combatLogText = "after-context";
        if (commands.CombatLogText != "after-context")
            throw new InvalidOperationException("Defense context copied configuration instead of reading its definition.");
    }

    private static void VerifyRoundTrip(DefenseEffectSO effect, ICollection<Object> cleanup)
    {
        string json = EditorJsonUtility.ToJson(effect);
        var restored = (DefenseEffectSO)ScriptableObject.CreateInstance(effect.GetType());
        cleanup.Add(restored);
        EditorJsonUtility.FromJsonOverwrite(json, restored);
        if (restored.GetType() != effect.GetType() || restored.EffectId != effect.EffectId
            || EditorJsonUtility.ToJson(restored) != json)
            throw new InvalidOperationException("Defense definition JSON/type/field round trip changed: " + effect.name);
    }

    // No Actor, scene, runtime, health transaction or report is required to run a definition.
    private sealed class RecordingCommands : IDefenseEffectCommands
    {
        private readonly List<string> calls = new();
        public string CombatLogText { get; set; }
        public int StatusStacks { get; set; } = 1;
        private static string Number(float value) => value.ToString("0.####", CultureInfo.InvariantCulture);
        public void ApplyDamage(float amount, string reason) => calls.Add("damage:" + Number(amount) + ":" + reason);
        public int ApplyStatus(DefenseStatusKind kind, float value, float duration, int stacks)
        {
            calls.Add($"status:{kind}:{Number(value)}:{Number(duration)}:{stacks}");
            return StatusStacks;
        }
        public void ClearStatus(DefenseStatusKind kind) => calls.Add("clear:" + kind);
        public void AddMovementDelay(float seconds) => calls.Add("delay:" + Number(seconds));
        public void AddEffectTag(string tag) => calls.Add("tag:" + tag);
        public void RecordEnvironmentalIgnition(float intensity) => calls.Add("ignite:" + Number(intensity));
        public void Require(params string[] expected)
        {
            if (!calls.SequenceEqual(expected))
                throw new InvalidOperationException("Defense command order/value mismatch: " + string.Join("|", calls));
            calls.Clear();
        }
    }
}
#endif
