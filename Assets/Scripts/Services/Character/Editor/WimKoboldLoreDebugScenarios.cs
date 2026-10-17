#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class WimKoboldLoreDebugScenarios
{
    private const string MenuPath =
        "Tools/Dungeon Story/QA/WIM Kobold Lore Only";
    private const string KoboldAssetPath =
        "Assets/Resources/SO/Character/Species/Species_Kobold.asset";
    private const string ApprovedLore =
        "불만이 커지면 부품을 모으려는 성향이 두드러진다.";

    [MenuItem(MenuPath)]
    public static void RunFromMenu()
    {
        VerifyKoboldHoardingIsLoreOnly();
        Debug.Log("[PASS] WIM_KOBOLD_HOARDING_LORE_ONLY");
    }

    internal static void VerifyKoboldHoardingIsLoreOnly()
    {
        CharacterSpeciesSO kobold = AssetDatabase.LoadAssetAtPath<CharacterSpeciesSO>(
            KoboldAssetPath);
        Require(kobold != null, "Kobold species asset is missing.");
        Require(kobold.IncidentId == CharacterSpeciesIncidentIds.KoboldPartsHoarding,
            "Kobold must retain its stable lore incident ID.");
        Require(kobold.IncidentDescription == ApprovedLore
                && kobold.incidentDescription == ApprovedLore,
            "Kobold authored incident text must match the approved lore-only description.");

        SpeciesIncidentHandlerRegistry registry = new(
            items: null,
            relocations: null,
            filth: new FailingFilthSpy(),
            water: new FailingWaterContaminationSpy(),
            world: null);
        bool handled = registry.TryExecute(
            new SpeciesIncidentContext(
                null,
                kobold,
                new CharacterSpeciesRuntimeState()),
            out string summary);

        Require(!handled && string.IsNullOrEmpty(summary),
            "Kobold lore must not have a live incident handler or notification summary.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class FailingFilthSpy : IWorldFilthQuery
    {
        public int StateVersion => throw UnexpectedInvocation();
        public int NextFilthSequence => throw UnexpectedInvocation();

        public IReadOnlyList<WorldFilthSnapshot> GetAll() => throw UnexpectedInvocation();

        public IReadOnlyList<WorldFilthSnapshot> GetAt(Vector2Int position) =>
            throw UnexpectedInvocation();

        public WorldFilthSnapshot AddFilth(
            WorldFilthType type,
            Vector2Int position,
            float amount,
            string sourceCharacterId,
            float infectionRisk,
            bool wallStain = false) => throw UnexpectedInvocation();

        public bool Clean(
            string filthId,
            float workAmount,
            out float remainingAmount)
        {
            throw UnexpectedInvocation();
        }

        public float GetCleanlinessPenalty(Vector2Int position, int radius = 0) =>
            throw UnexpectedInvocation();

        public List<WorldFilthSaveData> CaptureFilth() => throw UnexpectedInvocation();

        public void RestoreFilth(
            IEnumerable<WorldFilthSaveData> saveData,
            int nextSequence) => throw UnexpectedInvocation();
    }

    private sealed class FailingWaterContaminationSpy :
        IWorldWaterContaminationCommand
    {
        public bool TryContaminate(
            string sourceId,
            string pathogenDiseaseId,
            WorldWaterQuality minimumQuality) => throw UnexpectedInvocation();

        public bool TryContaminateNearest(
            Vector2Int origin,
            int maximumDistance,
            string pathogenDiseaseId,
            WorldWaterQuality minimumQuality,
            out string sourceId)
        {
            throw UnexpectedInvocation();
        }

        public bool TryClearPathogen(string sourceId) => throw UnexpectedInvocation();
    }

    private static InvalidOperationException UnexpectedInvocation() =>
        new("Kobold lore-only registry lookup invoked an unrelated dependency.");
}
#endif
