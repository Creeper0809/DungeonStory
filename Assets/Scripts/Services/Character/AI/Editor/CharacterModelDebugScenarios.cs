using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class CharacterModelDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Character/Run P1 Character Model Scenarios")]
    public static void RunFromMenu()
    {
        bool success = RunAll(true);
        if (!success)
        {
            Debug.LogError("P1 character model scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> errors = new List<string>();

        RunScenario("종족/특성 에셋 수", VerifyAssetCounts, errors);
        RunScenario("능력치 합산", VerifyStatComposition, errors);
        RunScenario("개인 특성 소비/사고 보정", VerifyTraitConsumptionAndAccidentDifferences, errors);
        RunScenario("작업 적성 보정", VerifyWorkAffinityDifferences, errors);
        RunScenario("역할 전환 유지", VerifyRoleSwitchKeepsProfile, errors);
        RunScenario("Character 런타임 프로필 연결", VerifyCharacterRuntimeProfile, errors);
        RunScenario("종족 운영 데이터", VerifySpeciesOperationalData, errors);
        RunScenario("종족 체류/전투/사고 차이", VerifySpeciesRuntimeTendencies, errors);
        RunScenario("종족 혼잡 민감도", VerifySpeciesCrowdSensitivity, errors);

        if (errors.Count > 0)
        {
            foreach (string error in errors)
            {
                Debug.LogError(error);
            }

            return false;
        }

        if (logSuccess)
        {
            Debug.Log("P1 character model scenarios passed.");
        }

        return true;
    }

    private static void RunScenario(string name, Func<bool> scenario, List<string> errors)
    {
        try
        {
            if (scenario()) return;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }

        errors.Add(name);
    }

    private static bool VerifyAssetCounts()
    {
        string[] species = AssetDatabase.FindAssets("t:CharacterSpeciesSO", new[] { "Assets/Resources/SO/Character/Species" });
        string[] traits = AssetDatabase.FindAssets("t:CharacterTraitSO", new[] { "Assets/Resources/SO/Character/Traits" });
        return species.Length == 3 && traits.Length == 8;
    }

    private static bool VerifyStatComposition()
    {
        CharacterRuntimeProfile orcFighter = CreateProfile("Species_Orc", "Trait_Fighter");
        CharacterRuntimeProfile vampireResearcher = CreateProfile("Species_Vampire", "Trait_Researcher");
        CharacterRuntimeProfile slimeClean = CreateProfile("Species_Slime", "Trait_Clean");

        return orcFighter.GetStat(CharacterStatType.Attack) == 10
            && orcFighter.GetStat(CharacterStatType.Strength) == 8
            && orcFighter.GetStat(CharacterStatType.Research) == 4
            && vampireResearcher.GetStat(CharacterStatType.Research) == 11
            && slimeClean.GetStat(CharacterStatType.Cleaning) == 10;
    }

    private static bool VerifyTraitConsumptionAndAccidentDifferences()
    {
        CharacterRuntimeProfile bigEater = CreateProfile("Species_Orc", "Trait_BigEater");
        CharacterRuntimeProfile frugal = CreateProfile("Species_Orc", "Trait_Frugal");
        CharacterRuntimeProfile fighter = CreateProfile("Species_Orc", "Trait_Fighter");

        return bigEater.GetConsumptionMultiplier() > frugal.GetConsumptionMultiplier()
            && bigEater.GetAccidentChanceMultiplier() > frugal.GetAccidentChanceMultiplier()
            && fighter.GetAccidentChanceMultiplier() > frugal.GetAccidentChanceMultiplier();
    }

    private static bool VerifyWorkAffinityDifferences()
    {
        CharacterRuntimeProfile fighter = CreateProfile("Species_Orc", "Trait_Fighter");
        CharacterRuntimeProfile researcher = CreateProfile("Species_Orc", "Trait_Researcher");
        CharacterRuntimeProfile clean = CreateProfile("Species_Slime", "Trait_Clean");

        return fighter.GetWorkSpeedMultiplier(BuiltInWorkTypeIds.Guard) > researcher.GetWorkSpeedMultiplier(BuiltInWorkTypeIds.Guard)
            && researcher.GetWorkSpeedMultiplier(BuiltInWorkTypeIds.Research) > fighter.GetWorkSpeedMultiplier(BuiltInWorkTypeIds.Research)
            && clean.GetWorkSpeedMultiplier(BuiltInWorkTypeIds.Clean) > fighter.GetWorkSpeedMultiplier(BuiltInWorkTypeIds.Clean);
    }

    private static bool VerifyRoleSwitchKeepsProfile()
    {
        CharacterSO data = CreateCharacterData("Species_Slime", "Trait_Clean");
        data.characterType = CharacterType.Customer;
        CharacterRuntimeProfile customerProfile = data.CreateRuntimeProfile();
        data.characterType = CharacterType.NPC;
        CharacterRuntimeProfile staffProfile = data.CreateRuntimeProfile();

        bool sameStats = customerProfile.GetStat(CharacterStatType.Cleaning) == staffProfile.GetStat(CharacterStatType.Cleaning)
            && Mathf.Approximately(customerProfile.GetConsumptionMultiplier(), staffProfile.GetConsumptionMultiplier())
            && Mathf.Approximately(customerProfile.GetWorkSpeedMultiplier(BuiltInWorkTypeIds.Clean), staffProfile.GetWorkSpeedMultiplier(BuiltInWorkTypeIds.Clean));

        Object.DestroyImmediate(data);
        return sameStats;
    }

    private static bool VerifyCharacterRuntimeProfile()
    {
        CharacterSO data = CreateCharacterData("Species_Vampire", "Trait_Researcher");
        GameObject obj = CharacterAiPlanDebugFixtures.CreateActorObject(
            "Character Model Scenario Character");
        CharacterActor character = obj.GetComponent<CharacterActor>();

        character.Initialization(data);
        bool connected = character.SpeciesTag == "Vampire"
            && character.GetCharacterStat(CharacterStatType.Research) == 11
            && character.GetFacilityPreferenceScore(FacilityRole.Mana) > 0.5f
            && character.GetWorkSpeedMultiplier(BuiltInWorkTypeIds.Research) > 1f;

        Object.DestroyImmediate(obj);
        Object.DestroyImmediate(data);
        return connected;
    }

    private static bool VerifySpeciesOperationalData()
    {
        CharacterSpeciesSO slime = LoadSpecies("Species_Slime");
        CharacterSpeciesSO orc = LoadSpecies("Species_Orc");
        CharacterSpeciesSO vampire = LoadSpecies("Species_Vampire");

        return HasCompleteSpeciesData(
                slime,
                CharacterSpeciesIncidentType.SlimeContamination,
                "저가 음식점",
                "화염 시설")
            && HasCompleteSpeciesData(
                orc,
                CharacterSpeciesIncidentType.OrcRampage,
                "고기 식당",
                "마력 시설")
            && HasCompleteSpeciesData(
                vampire,
                CharacterSpeciesIncidentType.VampireFear,
                "연구실",
                "밝은 시설");
    }

    private static bool VerifySpeciesRuntimeTendencies()
    {
        CharacterRuntimeProfile slime = CreateProfile("Species_Slime");
        CharacterRuntimeProfile orc = CreateProfile("Species_Orc");
        CharacterRuntimeProfile vampire = CreateProfile("Species_Vampire");

        return vampire.GetStayDurationMultiplier() > orc.GetStayDurationMultiplier()
            && orc.GetStayDurationMultiplier() > slime.GetStayDurationMultiplier()
            && orc.GetSpendingMultiplier() > slime.GetSpendingMultiplier()
            && orc.GetCombatPowerMultiplier() > slime.GetCombatPowerMultiplier()
            && orc.GetAccidentChanceMultiplier() > vampire.GetAccidentChanceMultiplier()
            && slime.GetIncidentType() == CharacterSpeciesIncidentType.SlimeContamination
            && orc.GetIncidentType() == CharacterSpeciesIncidentType.OrcRampage
            && vampire.GetIncidentType() == CharacterSpeciesIncidentType.VampireFear;
    }

    private static bool VerifySpeciesCrowdSensitivity()
    {
        CharacterRuntimeProfile orc = CreateProfile("Species_Orc");
        CharacterRuntimeProfile vampire = CreateProfile("Species_Vampire");

        return vampire.GetCrowdSensitivityMultiplier() > orc.GetCrowdSensitivityMultiplier();
    }

    private static bool HasCompleteSpeciesData(
        CharacterSpeciesSO species,
        CharacterSpeciesIncidentType expectedIncident,
        string expectedPreferredFacility,
        string expectedDislikedEnvironment)
    {
        return species != null
            && !string.IsNullOrWhiteSpace(species.shortDescription)
            && species.preferredFacilityLabels.Contains(expectedPreferredFacility)
            && species.dislikedEnvironmentLabels.Contains(expectedDislikedEnvironment)
            && species.stayDurationMultiplier > 0f
            && species.incidentType == expectedIncident
            && !string.IsNullOrWhiteSpace(species.incidentName)
            && !string.IsNullOrWhiteSpace(species.incidentDescription)
            && species.incidentMitigatingRoles != FacilityRole.None;
    }

    private static CharacterRuntimeProfile CreateProfile(string speciesAssetName, params string[] traitAssetNames)
    {
        CharacterSO data = CreateCharacterData(speciesAssetName, traitAssetNames);
        CharacterRuntimeProfile profile = data.CreateRuntimeProfile();
        Object.DestroyImmediate(data);
        return profile;
    }

    private static CharacterSO CreateCharacterData(string speciesAssetName, params string[] traitAssetNames)
    {
        CharacterSO data = ScriptableObject.CreateInstance<CharacterSO>();
        data.characterType = CharacterType.Customer;
        data.characterName = "Model Scenario";
        data.species = LoadSpecies(speciesAssetName);
        data.speciesTag = data.species != null ? data.species.speciesTag : string.Empty;
        data.baseStats = CharacterStatBlock.CreateDefault();
        data.traits = traitAssetNames
            .Select(LoadTrait)
            .Where((trait) => trait != null)
            .ToArray();
        return data;
    }

    private static CharacterSpeciesSO LoadSpecies(string assetName)
    {
        return AssetDatabase.LoadAssetAtPath<CharacterSpeciesSO>(
            $"Assets/Resources/SO/Character/Species/{assetName}.asset");
    }

    private static CharacterTraitSO LoadTrait(string assetName)
    {
        return AssetDatabase.LoadAssetAtPath<CharacterTraitSO>(
            $"Assets/Resources/SO/Character/Traits/{assetName}.asset");
    }
}
