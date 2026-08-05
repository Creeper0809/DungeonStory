using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class CharacterModelDebugScenarios
{
    private static IGameContentDefinitionSource Content =>
        CharacterAiEditorTestDependencies.ContentDefinitions;
    private static ICharacterSpeciesCatalog SpeciesCatalog =>
        CharacterAiEditorTestDependencies.CharacterSpeciesCatalog;

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
        IReadOnlyList<CharacterSpeciesSO> authoredSpecies =
            Content.GetAll<CharacterSpeciesSO>();
        IReadOnlyList<CharacterTraitSO> authoredTraits =
            Content.GetAll<CharacterTraitSO>();
        string[] requiredCoreSpecies = { "Slime", "Orc", "Vampire" };

        return authoredSpecies.Count == SpeciesCatalog.All.Count
            && authoredSpecies.Count >= requiredCoreSpecies.Length
            && requiredCoreSpecies.All(tag => SpeciesCatalog.TryGet(
                new CharacterSpeciesId(tag),
                out _))
            && authoredSpecies
                .Select(species => species.DefinitionId.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() == authoredSpecies.Count
            && authoredTraits.Count >= 8
            && authoredTraits
                .Select(trait => trait.id)
                .Distinct()
                .Count() == authoredTraits.Count;
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
                CharacterSpeciesIncidentIds.SlimeContamination)
            && HasCompleteSpeciesData(
                orc,
                CharacterSpeciesIncidentIds.OrcRampage)
            && HasCompleteSpeciesData(
                vampire,
                CharacterSpeciesIncidentIds.VampireFear);
    }

    private static bool VerifySpeciesRuntimeTendencies()
    {
        string[] requiredSpecies =
        {
            "Beastkin",
            "Demon",
            "Golem",
            "Harpy",
            "Kobold",
            "Myconid",
            "Orc",
            "Slime",
            "Vampire"
        };
        IReadOnlyList<CharacterSpeciesSO> authored = SpeciesCatalog.All;
        Dictionary<string, CharacterRuntimeProfile> profiles = authored
            .ToDictionary(
                species => species.speciesTag,
                species => CreateProfile("Species_" + species.speciesTag),
                StringComparer.Ordinal);
        profiles.TryGetValue("Slime", out CharacterRuntimeProfile slime);
        profiles.TryGetValue("Orc", out CharacterRuntimeProfile orc);
        profiles.TryGetValue("Vampire", out CharacterRuntimeProfile vampire);

        float[] stayMultipliers = profiles.Values
            .Select(profile => profile.GetStayDurationMultiplier())
            .ToArray();
        float[] combatMultipliers = profiles.Values
            .Select(profile => profile.GetCombatPowerMultiplier())
            .ToArray();
        float[] accidentMultipliers = profiles.Values
            .Select(profile => profile.GetAccidentChanceMultiplier())
            .ToArray();
        string[] incidentIds = profiles.Values
            .Select(profile => profile.GetIncidentId())
            .ToArray();

        bool catalogComplete = authored.Count == requiredSpecies.Length
            && new HashSet<string>(
                authored.Select(species => species.speciesTag),
                StringComparer.Ordinal).SetEquals(requiredSpecies)
            && profiles.Values.All(profile => profile != null);
        bool authoredVariation = stayMultipliers.All(value => value > 0f)
            && combatMultipliers.All(value => value > 0f)
            && accidentMultipliers.All(value => value > 0f)
            && stayMultipliers.Distinct().Count() >= 2
            && combatMultipliers.Distinct().Count() >= 4
            && accidentMultipliers.Distinct().Count() >= 3;
        bool incidentCoverage = incidentIds.All(id =>
                !string.IsNullOrWhiteSpace(id)
                && !string.Equals(
                    id,
                    CharacterSpeciesIncidentIds.None,
                    StringComparison.Ordinal))
            && incidentIds.Distinct(StringComparer.Ordinal).Count()
                == requiredSpecies.Length;
        bool coreTendencies = slime != null
            && orc != null
            && vampire != null
            && orc.GetStayDurationMultiplier() > slime.GetStayDurationMultiplier()
            && vampire.GetStayDurationMultiplier() > slime.GetStayDurationMultiplier()
            && orc.GetSpendingMultiplier() > slime.GetSpendingMultiplier()
            && orc.GetCombatPowerMultiplier() > vampire.GetCombatPowerMultiplier()
            && vampire.GetCombatPowerMultiplier() > slime.GetCombatPowerMultiplier()
            && orc.GetAccidentChanceMultiplier() > vampire.GetAccidentChanceMultiplier()
            && vampire.GetAccidentChanceMultiplier() > slime.GetAccidentChanceMultiplier()
            && slime.GetIncidentType()
                == CharacterSpeciesIncidentType.SlimeContamination
            && orc.GetIncidentType() == CharacterSpeciesIncidentType.OrcRampage
            && vampire.GetIncidentType()
                == CharacterSpeciesIncidentType.VampireFear;
        bool valid = catalogComplete
            && authoredVariation
            && incidentCoverage
            && coreTendencies;
        if (!valid)
        {
            Debug.LogError(
                "Species runtime tendency detail: "
                + $"catalogComplete={catalogComplete}, "
                + $"variation={authoredVariation}, incidents={incidentCoverage}, "
                + $"core={coreTendencies}, count={authored.Count}, "
                + $"stay={string.Join(",", stayMultipliers.Select(value => value.ToString("0.###")))}, "
                + $"combat={string.Join(",", combatMultipliers.Select(value => value.ToString("0.###")))}, "
                + $"accident={string.Join(",", accidentMultipliers.Select(value => value.ToString("0.###")))}");
        }

        return valid;
    }

    private static bool VerifySpeciesCrowdSensitivity()
    {
        CharacterRuntimeProfile orc = CreateProfile("Species_Orc");
        CharacterRuntimeProfile vampire = CreateProfile("Species_Vampire");

        return vampire.GetCrowdSensitivityMultiplier() > orc.GetCrowdSensitivityMultiplier();
    }

    private static bool HasCompleteSpeciesData(
        CharacterSpeciesSO species,
        string expectedIncidentId)
    {
        return species != null
            && species.DefinitionId.IsValid
            && !string.IsNullOrWhiteSpace(species.displayName)
            && !string.IsNullOrWhiteSpace(species.anatomyProfileId)
            && species.needs != null
            && species.environment != null
            && !string.IsNullOrWhiteSpace(species.shortDescription)
            && species.preferredFacilityLabels.Length > 0
            && species.dislikedEnvironmentLabels.Length > 0
            && species.stayDurationMultiplier > 0f
            && species.crimeRiskMultiplier > 0f
            && string.Equals(
                species.IncidentId,
                expectedIncidentId,
                StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(species.IncidentDisplayName)
            && species.IncidentMitigatingRoles != FacilityRole.None
            && !string.IsNullOrWhiteSpace(species.combatPassive?.StableId);
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
        string speciesTag = assetName != null
            && assetName.StartsWith("Species_", StringComparison.Ordinal)
            ? assetName.Substring("Species_".Length)
            : assetName;
        return SpeciesCatalog.TryGet(
            new CharacterSpeciesId(speciesTag),
            out CharacterSpeciesSO species)
            ? species
            : null;
    }

    private static CharacterTraitSO LoadTrait(string assetName)
    {
        return Content.GetAll<CharacterTraitSO>()
            .SingleOrDefault(trait => string.Equals(
                trait.name,
                assetName,
                StringComparison.Ordinal));
    }
}
