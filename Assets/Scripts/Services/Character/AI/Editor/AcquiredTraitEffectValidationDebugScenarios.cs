#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public static class AcquiredTraitEffectValidationDebugScenarios
{
    public static void VerifyForBatch()
    {
        const string source = "Artifacts/QA/R80Latency-20261028-r1/soak-r17/unity-evidence/persistent-data/Saves/r80-extended-soak-final.json";
        JObject save = JObject.Parse(File.ReadAllText(source));
        JArray actors = (JArray)JObject.Parse((string)save["sections"]
            .Single(value => (string)value["sectionId"] == "characters.world")["payloadJson"])["actors"];
        var modules = Definitions<CharacterAcquiredTraitModuleSO>();
        var settings = Definitions<CharacterAcquiredTraitSettingsSO>();
        int active = 0, pending = 0;
        foreach (JToken actor in actors)
        {
            if (active > 0 && pending > 0) break;
            var state = actor["acquiredTraits"].ToObject<CharacterAcquiredTraitAggregateState>();
            var ledger = actor["narrative"].ToObject<CharacterNarrativeLedger>();
            if (!state.HasPersistentData) continue;
            if ((state.instances.Count == 0 || active > 0)
                && (state.pendingRequests.Count == 0 || pending > 0)) continue;
            CharacterAcquiredTraitSettingsSO authority = settings.Single(value =>
                Reference(state, ledger, value, modules).Count == 0);
            Compare(state, ledger, authority, modules);
            Compare(state, ledger, null, modules);
            Compare(state, ledger, authority, modules.Concat(new CharacterAcquiredTraitModuleSO[] { null }).ToArray());
            var broken = state.Clone(); broken.revision = -1;
            Compare(broken, ledger, authority, modules);
            Compare(state, new CharacterNarrativeLedger { facts = null }, authority, modules);
            Compare(null, ledger, authority, modules);
            active += state.instances.Count > 0 ? 1 : 0;
            pending += state.pendingRequests.Count > 0 ? 1 : 0;
        }
        Require(active > 0 && pending > 0, "Original fixture no longer covers active and pending acquired traits.");
    }

    private static void Compare(CharacterAcquiredTraitAggregateState state, CharacterNarrativeLedger ledger,
        CharacterAcquiredTraitSettingsSO settings, CharacterAcquiredTraitModuleSO[] modules)
    {
        int enumerations = 0;
        IEnumerable<CharacterAcquiredTraitModuleSO> Counted()
        { enumerations++; foreach (var module in modules) yield return module; }
        var expected = Reference(state, ledger, settings, modules);
        var actual = CharacterAcquiredTraitStateValidator.ValidateEffectSourceInput(
            state, ledger, settings, Counted(), out var captured);
        Require(expected.Select(value => value.ToString()).SequenceEqual(actual.Select(value => value.ToString())),
            "Effect validation changed an original issue or its order.");
        bool structurallyValid = CharacterAcquiredTraitStateValidator.ValidatePersistentState(state, ledger).Count == 0;
        Require(enumerations == (structurallyValid ? 1 : 0),
            "Definitions were enumerated before structural rejection or more than once.");
        Require(!structurallyValid || captured.SequenceEqual(modules),
            "Definition order or identities changed during validation.");
    }

    // 변경 전 효과 조회의 두 단계 검증을 참조값으로 유지한다.
    private static IReadOnlyList<CharacterAcquiredTraitValidationIssue> Reference(
        CharacterAcquiredTraitAggregateState state, CharacterNarrativeLedger ledger,
        CharacterAcquiredTraitSettingsSO settings, CharacterAcquiredTraitModuleSO[] modules)
    {
        var structural = CharacterAcquiredTraitStateValidator.ValidatePersistentState(state, ledger);
        return structural.Count > 0 ? structural
            : CharacterAcquiredTraitStateValidator.ValidateWithDefinitions(state, ledger, settings, modules);
    }

    private static T[] Definitions<T>() where T : ScriptableObject =>
        AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { "Assets/Resources/SO" })
            .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<T>)
            .Where(value => value != null).OrderBy(AssetDatabase.GetAssetPath, StringComparer.Ordinal).ToArray();

    private static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }
}
#endif
