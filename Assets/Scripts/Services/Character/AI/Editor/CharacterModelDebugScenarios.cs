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
        V27CharacterPerformanceDebugScenarios.RunStructuralAudit();
        return true;
    }

    private static bool VerifyTraitConsumptionAndAccidentDifferences()
    {
        CharacterTraitSO bigEater = LoadTrait("Trait_BigEater");
        CharacterTraitSO frugal = LoadTrait("Trait_Frugal");
        CharacterTraitSO fighter = LoadTrait("Trait_Fighter");
        return HasEffect(bigEater, GameplayEffectTargetIds.Consumption)
            && HasEffect(frugal, GameplayEffectTargetIds.Consumption)
            && HasEffect(frugal, GameplayEffectTargetIds.AccidentChance)
            && HasEffect(fighter, GameplayEffectTargetIds.AccidentChance);
    }

    private static bool VerifyWorkAffinityDifferences()
    {
        CharacterTraitSO fighter = LoadTrait("Trait_Fighter");
        CharacterTraitSO researcher = LoadTrait("Trait_Researcher");
        CharacterTraitSO clean = LoadTrait("Trait_Clean");
        return HasEffect(fighter, GameplayEffectTargetIds.CombatPower)
            && HasEffect(researcher, GameplayEffectTargetIds.ResearchSpeed)
            && HasEffect(clean, GameplayEffectTargetIds.WorkSpeed);
    }

    private static bool VerifyRoleSwitchKeepsProfile()
    {
        CharacterSO data = CreateCharacterData("Species_Slime", "Trait_Clean");
        data.characterType = CharacterType.Customer;
        CharacterRuntimeProfile customerProfile =
            CharacterRuntimeProfileFactory.CreateEditorSnapshot(data);
        data.characterType = CharacterType.NPC;
        CharacterRuntimeProfile staffProfile =
            CharacterRuntimeProfileFactory.CreateEditorSnapshot(data);

        bool sameStats = customerProfile.ExpressedTraitIds.SequenceEqual(
                staffProfile.ExpressedTraitIds,
                StringComparer.Ordinal)
            && Mathf.Approximately(
                customerProfile.GetWorkPreferenceScore(BuiltInWorkTypeIds.Clean),
                staffProfile.GetWorkPreferenceScore(BuiltInWorkTypeIds.Clean));

        Object.DestroyImmediate(data);
        return sameStats;
    }

    private static bool VerifyCharacterRuntimeProfile()
    {
        CharacterSO data = CreateCharacterData("Species_Vampire", "Trait_Researcher");
        GameObject obj = CharacterAiPlanDebugFixtures.CreateActorObject(
            "Character Model Scenario Character");
        CharacterActor character = obj.GetComponent<CharacterActor>();

        character.Progression.ApplyPreparedIdentity(
            data.characterName,
            "debug:character-model",
            data.traits.Select(trait => trait.id),
            CharacterPotentialGrade.Ordinary,
            generationSeed: 990001,
            autoChooseDrafts: false);
        character.Initialization(data);
        bool connected = character.SpeciesTag == "Vampire"
            && character.Progression.ResolveSelectedTraits()
                .Any(trait => trait != null && trait.id == data.traits[0].id)
            && character.Stats.EvaluatePerformance(
                "performance:work:research:speed").Value > 0f
            && character.GetFacilityPreferenceScore(FacilityRole.Mana) > 0.5f
            && character.GetWorkSpeedMultiplier(BuiltInWorkTypeIds.Research) > 0f;

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
        IReadOnlyList<CharacterSpeciesSO> authored = SpeciesCatalog.All
            .Where(species => !string.Equals(
                species.speciesTag,
                "Adventurer",
                StringComparison.Ordinal))
            .ToArray();
        string[] capacityTargets = Enum
            .GetValues(typeof(CharacterFunctionalCapacityId))
            .Cast<CharacterFunctionalCapacityId>()
            .Select(CharacterFunctionalCapacityIds.GetStableId)
            .ToArray();
        string[] incidentIds = authored
            .Select(species => species.IncidentId)
            .ToArray();

        bool catalogComplete = authored.Count == requiredSpecies.Length
            && new HashSet<string>(
                authored.Select(species => species.speciesTag),
                StringComparer.Ordinal).SetEquals(requiredSpecies)
            && authored.All(species => species != null);
        bool authoredVariation = authored.All(species => capacityTargets.All(target =>
                species.Effects.Count(binding => binding?.definition != null
                    && string.Equals(
                        binding.definition.TargetId,
                        target,
                        StringComparison.Ordinal)) == 1))
            && authored.SelectMany(species => species.Effects)
                .Where(binding => binding?.definition != null
                    && capacityTargets.Contains(
                        binding.definition.TargetId,
                        StringComparer.Ordinal))
                .Select(binding => binding.value)
                .Distinct()
                .Count() >= 3;
        bool incidentCoverage = incidentIds.All(id =>
                !string.IsNullOrWhiteSpace(id)
                && !string.Equals(
                    id,
                    CharacterSpeciesIncidentIds.None,
                    StringComparison.Ordinal))
            && incidentIds.Distinct(StringComparer.Ordinal).Count()
                == requiredSpecies.Length;
        bool coreTendencies = authored.All(species =>
            !string.IsNullOrWhiteSpace(species.anatomyProfileId)
            && species.Effects.All(binding => binding?.definition != null));
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
                + $"capacityTargets={capacityTargets.Length}");
        }

        return valid;
    }

    private static bool VerifySpeciesCrowdSensitivity()
    {
        CharacterSpeciesSO orc = LoadSpecies("Species_Orc");
        CharacterSpeciesSO vampire = LoadSpecies("Species_Vampire");
        return orc != null
            && vampire != null
            && HasEffect(vampire, GameplayEffectTargetIds.CrowdSensitivity);
    }

    private static bool HasEffect(
        IGameplayEffectSource source,
        string targetId) =>
        source?.Effects.Any(binding => binding?.definition != null
            && string.Equals(
                binding.definition.TargetId,
                targetId,
                StringComparison.Ordinal)) == true;

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
        CharacterRuntimeProfile profile =
            CharacterRuntimeProfileFactory.CreateEditorSnapshot(data);
        Object.DestroyImmediate(data);
        return profile;
    }

    private static CharacterSO CreateCharacterData(string speciesAssetName, params string[] traitAssetNames)
    {
        CharacterSO data = CharacterAiEditorTestDependencies.CreateCharacterFixtureData(
            CharacterType.Customer,
            "Model Scenario",
            "Slime");
        data.characterType = CharacterType.Customer;
        data.characterName = "Model Scenario";
        data.id = 990001;
        data.species = LoadSpecies(speciesAssetName);
        data.speciesTag = data.species != null ? data.species.speciesTag : string.Empty;
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
