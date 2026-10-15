#if UNITY_EDITOR
using System;
using System.Linq;
using DungeonStory.Narrative.Korean;
using UnityEngine;

public static class PhysicalItemFactDebugScenarios
{
    public static string Run()
    {
        var fact = new PhysicalItemDispositionSourceFact("stack:fact", "item:fact", "instance:fact",
            2, 1400, new Vector2Int(5, 1), "동결된 아이템 이름");
        Require(fact.IsValid, "valid neutral source rejected");
        Require(typeof(PhysicalItemDispositionSourceFact).Assembly.GetName().Name == "DungeonStory.Items",
            "physical fact returned to the gameplay/logging assembly");
        Require(typeof(PhysicalItemDispositionSourceFact).GetProperties().All(property =>
            property.PropertyType == typeof(string) || property.PropertyType == typeof(int)
            || property.PropertyType == typeof(long) || property.PropertyType == typeof(bool)
            || property.PropertyType == typeof(Vector2Int)), "physical source has a logging contract dependency");

        var saved = PhysicalItemSourceFactSaveCodec.ToSave(fact);
        string json = JsonUtility.ToJson(saved);
        Require(!json.Contains("pronunciation") && !json.Contains("displaySnapshotRevision")
            && !json.Contains("locale"), "physical save owns presentation metadata");
        var roundTrip = PhysicalItemSourceFactSaveCodec.FromSave(
            JsonUtility.FromJson<PhysicalItemDispositionSourceFactSaveData>(json));
        Require(roundTrip.IsValid && roundTrip.StackId == fact.StackId
            && roundTrip.ItemDefinitionId == fact.ItemDefinitionId && roundTrip.ItemInstanceId == fact.ItemInstanceId
            && roundTrip.Quantity == fact.Quantity && roundTrip.MassGrams == fact.MassGrams
            && roundTrip.SourcePosition == fact.SourcePosition && roundTrip.DisplayName == fact.DisplayName,
            "neutral physical fact changed across JSON");
        saved.displayText = "mutable DTO changed";
        Require(fact.DisplayName == roundTrip.DisplayName, "saved DTO changed the frozen fact");
        var display = PhysicalItemFactDisplayName.Snapshot(roundTrip.ItemDefinitionId, roundTrip.DisplayName);
        Require(display.DisplayText == fact.DisplayName
            && display.DisplaySnapshotRevision == "item-definition:item:fact:v1"
            && display.PronunciationHint.Mode == KoreanPronunciationMode.AutoHangulDisplay
            && display.PronunciationHint.Revision == "item-definition-pronunciation-v1"
            && display.Locale == "ko-KR", "observer altered the prior item-name encoding");
        Require(!default(PhysicalItemDispositionSourceFact).IsValid
            && !new PhysicalItemDispositionSourceFact("stack:a", "item:a", "", 1, 1, default, "\uD83D").IsValid
            && !new PhysicalItemDispositionSourceFact("stack:a", "item:a", "", 1, 1, default, new string('x', 257)).IsValid,
            "invalid frozen physical values accepted");
        return "physical-neutral-facts/assembly-boundary/json/name-observation/invalid-input=PASS";
    }

    private static void Require(bool success, string detail)
    { if (!success) throw new InvalidOperationException(detail); }
}
#endif
