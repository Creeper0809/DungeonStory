#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VContainer;

public static class V16IntegrationDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Strategic/Run Integration Contracts")]
    public static void RunFromMenu()
    {
        if (!RunAll(true))
        {
            Debug.LogError("Strategic integration contracts failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> failures = new List<string>();
        Check(
            DungeonGameSaveData.CurrentVersion == 24,
            "save version",
            $"expected 24, got {DungeonGameSaveData.CurrentVersion}",
            failures);
        CheckLegacyEquipmentRemoved(failures);
        CheckGameplaySceneComposition(failures);
        CheckRegionPressure(failures);
        CheckTruthTargetHasNoPressure(failures);
        CheckExteriorRiskWeights(failures);
        CheckSeparatedExtractStock(failures);
        CheckExtractConsumers(failures);
        CheckChangedSurfaceEncoding(failures);

        if (Application.isPlaying)
        {
            CheckRuntimeServices(failures);
        }

        if (failures.Count > 0)
        {
            foreach (string failure in failures)
            {
                Debug.LogError($"[Strategic] {failure}");
            }

            return false;
        }

        if (logSuccess)
        {
            Debug.Log("Strategic integration contracts passed.");
        }

        return true;
    }

    private static void CheckLegacyEquipmentRemoved(List<string> failures)
    {
        string[] removedPaths =
        {
            "Assets/Scripts/Services/Offense/ExpeditionEquipmentSystem.cs",
            "Assets/Scripts/Services/Offense/ExpeditionEquipmentCatalogSO.cs",
            "Assets/Resources/Config/ExpeditionEquipmentCatalog.asset"
        };
        Check(
            removedPaths.All(path => !File.Exists(path)),
            "legacy equipment files",
            string.Join(", ", removedPaths.Where(File.Exists)),
            failures);

        string[] removedTypes =
        {
            "IExpeditionEquipmentRuntime",
            "ExpeditionEquipmentRuntime",
            "BuildingExpeditionSupportAbility"
        };
        HashSet<string> loadedTypeNames = AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(assembly => !assembly.IsDynamic)
            .SelectMany(SafeGetTypes)
            .Select(type => type.Name)
            .ToHashSet(StringComparer.Ordinal);
        Check(
            removedTypes.All(typeName => !loadedTypeNames.Contains(typeName)),
            "legacy equipment types",
            string.Join(", ", removedTypes.Where(loadedTypeNames.Contains)),
            failures);
    }

    private static void CheckGameplaySceneComposition(List<string> failures)
    {
        const string scenePath = "Assets/Scenes/GameplayScene.unity";
        string yaml = File.ReadAllText(scenePath);
        Check(
            !yaml.Contains(
                "m_Name: Priority Command Controller",
                StringComparison.Ordinal),
            "duplicate priority controller",
            "GameplayScene still contains Priority Command Controller",
            failures);
        Check(
            !yaml.Contains(
                "m_Name: RegularCustomerRuntime_Test",
                StringComparison.Ordinal),
            "duplicate regular customer runtime",
            "GameplayScene still contains RegularCustomerRuntime_Test",
            failures);
        Check(
            Count(yaml, "m_Name: OwnerCommandController") == 1,
            "owner command composition",
            "GameplayScene must contain exactly one OwnerCommandController",
            failures);
    }

    private static void CheckRegionPressure(List<string> failures)
    {
        OffenseRegionRuntime runtime = new OffenseRegionRuntime();
        OffenseTargetDefinition localTarget = CreateTarget(
            "local-logistics",
            OffenseRegionRuntime.BorderTradeRegionId,
            "변경 교역권",
            OffenseRegionRuntime.HumanFactionId,
            StrategicPressureAxis.Logistics,
            40f);
        OffenseTargetDefinition peerTarget = CreateTarget(
            "peer-logistics",
            "human-peer",
            "북부 교역로",
            OffenseRegionRuntime.HumanFactionId,
            StrategicPressureAxis.Logistics,
            1f);

        bool localApplied = runtime.TryApplyTargetPressure(
            localTarget,
            1,
            out StrategicPressureAxis axis,
            out float amount);
        runtime.TryApplyTargetPressure(
            peerTarget,
            1,
            out _,
            out _);
        OffenseRegionState peer = runtime.Regions.First(region =>
            string.Equals(
                region.regionId,
                peerTarget.regionId,
                StringComparison.Ordinal));
        peer.logisticsDamage = 0f;

        OffenseStrategicPressureSnapshot local =
            runtime.GetPressureForTarget(localTarget);
        OffenseStrategicPressureSnapshot spillover =
            runtime.GetPressureForTarget(peerTarget);
        Check(
            localApplied
            && axis == StrategicPressureAxis.Logistics
            && Mathf.Approximately(amount, 40f)
            && Mathf.Approximately(local.Logistics, 40f)
            && Mathf.Approximately(spillover.Logistics, 10f),
            "regional pressure spillover",
            $"applied={localApplied}; axis={axis}; amount={amount}; local={local.Logistics}; peer={spillover.Logistics}",
            failures);

        DungeonOffenseRegionSaveData saved = runtime.Capture();
        OffenseRegionRuntime restored = new OffenseRegionRuntime();
        restored.PublishRestoreCandidate(
            restored.BuildRestoreCandidate(saved));
        Check(
            Mathf.Approximately(
                restored.GetPressureForTarget(peerTarget).Logistics,
                10f),
            "regional pressure save roundtrip",
            "25% same-faction spillover changed after restore",
            failures);
    }

    private static void CheckTruthTargetHasNoPressure(List<string> failures)
    {
        OffenseRegionRuntime runtime = new OffenseRegionRuntime();
        OffenseTargetDefinition truth = CreateTarget(
            "truth_core",
            OffenseRegionRuntime.SealedZoneRegionId,
            "봉인 지대",
            OffenseRegionRuntime.SealFactionId,
            StrategicPressureAxis.Manpower,
            100f);
        truth.revealsTruth = true;
        bool applied = runtime.TryApplyTargetPressure(
            truth,
            5,
            out _,
            out float amount);
        Check(
            !applied && Mathf.Approximately(amount, 0f),
            "truth target pressure exclusion",
            $"final truth target applied meaningless pressure {amount}",
            failures);

        IReadOnlyList<OffenseTargetDefinition> targets =
            new ResourceOffenseCampaignCatalog(
                new ResourceGameContentCatalog(
                    new UnityGameContentRootLoader())).Targets;
        bool ordinaryTargetsHavePressure = targets
            .Where(target => target != null && !target.revealsTruth)
            .All(target => target.rewards.Any(reward =>
                reward?.GrantSpec is OffenseRegionalPressureRewardSpec));
        OffenseTargetDefinition defaultTruth = targets.FirstOrDefault(target =>
            target != null && target.revealsTruth);
        Check(
            ordinaryTargetsHavePressure
            && defaultTruth != null
            && defaultTruth.rewards.All(reward =>
                reward?.GrantSpec is not OffenseRegionalPressureRewardSpec),
            "default target regional pressure rewards",
            "an ordinary target lacks regional pressure or the truth target still grants it",
            failures);
    }

    private static void CheckExteriorRiskWeights(List<string> failures)
    {
        SurvivalEnvironmentSnapshot calm = new SurvivalEnvironmentSnapshot(
            SurvivalWeatherType.Clear,
            20f,
            5f,
            0f,
            0f);
        SurvivalEnvironmentSnapshot dangerous = new SurvivalEnvironmentSnapshot(
            SurvivalWeatherType.Storm,
            4f,
            95f,
            25f,
            20f);
        float calmThief = ExteriorActivityRuntime.GetIncidentSelectionWeight(
            ExteriorIncidentKind.Thief,
            calm,
            90f);
        float dangerThief = ExteriorActivityRuntime.GetIncidentSelectionWeight(
            ExteriorIncidentKind.Thief,
            dangerous,
            10f);
        float calmPredator = ExteriorActivityRuntime.GetIncidentSelectionWeight(
            ExteriorIncidentKind.PredatorApproach,
            calm,
            90f);
        float dangerPredator = ExteriorActivityRuntime.GetIncidentSelectionWeight(
            ExteriorIncidentKind.PredatorApproach,
            dangerous,
            10f);
        float calmCargo = ExteriorActivityRuntime.GetIncidentSelectionWeight(
            ExteriorIncidentKind.CargoDamage,
            calm,
            90f);
        float dangerCargo = ExteriorActivityRuntime.GetIncidentSelectionWeight(
            ExteriorIncidentKind.CargoDamage,
            dangerous,
            10f);
        Check(
            dangerThief > calmThief
            && dangerPredator > calmPredator
            && dangerCargo > calmCargo,
            "exterior danger weighting",
            $"thief {calmThief:0.00}->{dangerThief:0.00}; predator {calmPredator:0.00}->{dangerPredator:0.00}; cargo {calmCargo:0.00}->{dangerCargo:0.00}",
            failures);
    }

    private static void CheckSeparatedExtractStock(List<string> failures)
    {
        Check(
            StockCategory.Biological != StockCategory.Mana
            && StockCategory.Knowledge != StockCategory.Mana
            && StockCategory.Biological != StockCategory.Knowledge,
            "extract stock categories",
            "blood or memory residue still aliases Mana",
            failures);
    }

    private static void CheckExtractConsumers(List<string> failures)
    {
        CaptivityCorruptionRitualHandler ritual =
            new CaptivityCorruptionRitualHandler();
        Check(
            ritual.MaterialRequirements.TryGetValue(
                StockCategory.Biological,
                out int bloodCost)
            && bloodCost == 1,
            "blood corruption consumer",
            "the corruption ritual does not consume one Biological stack",
            failures);

        const string medicalPath =
            "Assets/Scripts/Services/Combat/CharacterMedicalSupplyCoordinator.cs";
        const string medicalDestinationPath =
            "Assets/Scripts/Services/Combat/CharacterMedicalSupplyDestinationRuntime.cs";
        string medicalSource = File.ReadAllText(medicalPath);
        string medicalDestinationSource = File.ReadAllText(
            medicalDestinationPath);
        Check(
            medicalSource.Contains(
                "TryRequestItemDelivery(",
                StringComparison.Ordinal)
            && medicalSource.Contains(
                "ExtractedBloodItemId",
                StringComparison.Ordinal)
            && medicalSource.Contains(
                "TryCommitSinkPending(",
                StringComparison.Ordinal)
            && !medicalSource.Contains(
                "TryConsumeFacilityBuffer(",
                StringComparison.Ordinal)
            && !medicalSource.Contains(
                "TryConsumeStoredStock(",
                StringComparison.Ordinal)
            && medicalDestinationSource.Contains(
                "FacilityBufferDestinationAdmissionPolicy.ExactGramRequired",
                StringComparison.Ordinal),
            "physical blood treatment consumer",
            "medical treatment bypasses delivery or still consumes abstract stock",
            failures);

        CharacterMedicalOrder savedOrder = new CharacterMedicalOrder
        {
            orderId = "medical-v16",
            treatmentSupply = CharacterMedicalSupplyKind.ExtractedBlood,
            statusCode = CharacterMedicalStatusCode.TreatingWithExtractedBlood,
            treatmentSupplyConsumed = true,
            treatmentMaterialDestinationId =
                "facility-input:medical:medical-v16"
        };
        DungeonCharacterMedicalSaveData medicalSave =
            JsonUtility.FromJson<DungeonCharacterMedicalSaveData>(
                JsonUtility.ToJson(new DungeonCharacterMedicalSaveData
                {
                    version = DungeonCharacterMedicalSaveData.CurrentVersion,
                    orders = new List<CharacterMedicalOrder> { savedOrder }
                }));
        CharacterMedicalOrder restoredOrder =
            medicalSave?.orders?.FirstOrDefault();
        Check(
            restoredOrder != null
            && restoredOrder.treatmentSupply
                == CharacterMedicalSupplyKind.ExtractedBlood
            && restoredOrder.statusCode
                == CharacterMedicalStatusCode.TreatingWithExtractedBlood
            && restoredOrder.treatmentSupplyConsumed
            && string.Equals(
                restoredOrder.treatmentMaterialDestinationId,
                savedOrder.treatmentMaterialDestinationId,
                StringComparison.Ordinal),
            "blood treatment save roundtrip",
            "in-flight physical treatment supply was not preserved",
            failures);

        const string knowledgePath =
            "Assets/Scripts/Services/Infrastructure/KnowledgeResidueProcessingRuntime.cs";
        string knowledgeSource = File.ReadAllText(knowledgePath);
        Check(
            knowledgeSource.Contains(
                "KnowledgeResidueDestinationAuthority.MemoryResidueItemId",
                StringComparison.Ordinal)
            && knowledgeSource.Contains(
                "TryRequestItemDelivery(",
                StringComparison.Ordinal)
            && knowledgeSource.Contains(
                "TryCommitSinkPending(",
                StringComparison.Ordinal)
            && knowledgeSource.Contains(
                "KnowledgeResidueUse.CodexAnalysis",
                StringComparison.Ordinal)
            && knowledgeSource.Contains(
                "KnowledgeResidueUse.RegionReconnaissance",
                StringComparison.Ordinal)
            && !knowledgeSource.Contains(
                "StockCategory.Knowledge",
                StringComparison.Ordinal)
            && !knowledgeSource.Contains(
                "TryConsumeFacilityBuffer(",
                StringComparison.Ordinal),
            "knowledge residue work consumers",
            "memory residue lacks an exact item and pending Sink consumer",
            failures);
    }

    private static void CheckRuntimeServices(List<string> failures)
    {
        DungeonRuntimeLifetimeScope scope =
            UnityEngine.Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        if (scope?.Container == null)
        {
            failures.Add("runtime services: DungeonRuntimeLifetimeScope is unavailable");
            return;
        }

        ICombatEquipmentRuntime equipment =
            scope.Container.Resolve<ICombatEquipmentRuntime>();
        IDungeonSaveSectionRegistry saveSections =
            scope.Container.Resolve<IDungeonSaveSectionRegistry>();
        ExteriorIncidentHandlerRegistry incidentHandlers =
            scope.Container.Resolve<ExteriorIncidentHandlerRegistry>();
        ICharacterAiPerformanceRecorder performance =
            scope.Container.Resolve<ICharacterAiPerformanceRecorder>();
        string[] requiredSections =
        {
            CombatEquipmentSaveSection.Id,
            OffenseAggregateSaveSection.Id,
            ExteriorActivitySaveSection.Id,
            BlueprintResearchSaveSection.Id,
            CaptivitySaveSection.Id,
            CircusSaveSection.Id
        };

        Check(
            equipment != null,
            "common equipment runtime",
            "ICombatEquipmentRuntime was not resolved",
            failures);
        Check(
            requiredSections.All(required => saveSections.OrderedSections.Any(
                section => string.Equals(
                    section.SectionId,
                    required,
                    StringComparison.Ordinal))),
            "V16 save sections",
            "one or more V16 sections are missing",
            failures);
        Check(
            incidentHandlers.TryGet(
                ExteriorIncidentKind.PredatorApproach,
                out _)
            && incidentHandlers.TryGet(
                ExteriorIncidentKind.CargoDamage,
                out _),
            "physical exterior risk handlers",
            "predator or cargo damage handler is missing",
            failures);

        CharacterAiPerformanceReport report = performance.CaptureReport(0);
        Check(
            report != null
            && report.metrics.Count
                == Enum.GetValues(typeof(AiPerformanceCategory)).Length,
            "AI performance recorder",
            report == null
                ? "report was null"
                : $"metrics={report.metrics.Count}; frames={report.sampleFrames}",
            failures);
    }

    private static void CheckChangedSurfaceEncoding(List<string> failures)
    {
        string[] playerFacingPaths =
        {
            "Assets/Scripts/Services/Infrastructure/Exterior/ExteriorIncidentHandlers.cs",
            "Assets/Scripts/Services/Infrastructure/DungeonGameSaveService.cs",
            "Assets/Scripts/Services/Offense/OffenseRegionRuntime.cs",
            "Assets/Scripts/Services/Offense/OffenseReturnArrivalRuntime.cs",
            "Assets/Scripts/Services/Infrastructure/KnowledgeResidueProcessingRuntime.cs",
            "Assets/Scripts/Services/Combat/CharacterMedicalRuntime.cs",
            "Assets/Scripts/Services/Offense/OffenseWorldMapService.cs"
        };
        List<string> corrupted = new List<string>();
        foreach (string path in playerFacingPaths)
        {
            if (!File.Exists(path))
            {
                corrupted.Add($"{path}: missing");
                continue;
            }

            string text = File.ReadAllText(path);
            if (text.Any(IsMojibakeCharacter))
            {
                corrupted.Add(path);
            }
        }

        Check(
            corrupted.Count == 0,
            "V16 player-facing UTF-8",
            string.Join(", ", corrupted),
            failures);
    }

    private static bool IsMojibakeCharacter(char character)
    {
        return character == '\uFFFD'
            || character is >= '\u3400' and <= '\u9FFF'
            || character is >= '\uF900' and <= '\uFAFF';
    }

    private static OffenseTargetDefinition CreateTarget(
        string id,
        string regionId,
        string regionName,
        string factionId,
        StrategicPressureAxis axis,
        float amount)
    {
        return new OffenseTargetDefinition
        {
            id = id,
            title = id,
            regionId = regionId,
            regionDisplayName = regionName,
            factionId = factionId,
            strategicPressureAxis = axis,
            strategicPressureAmount = amount,
            distance = 1f,
            requiredMembers = 1
        };
    }

    private static IEnumerable<Type> SafeGetTypes(
        System.Reflection.Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (System.Reflection.ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(type => type != null);
        }
    }

    private static int Count(string source, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = source.IndexOf(
                   value,
                   index,
                   StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static void Check(
        bool condition,
        string name,
        string failure,
        ICollection<string> failures)
    {
        if (!condition)
        {
            failures.Add($"{name}: {failure}");
        }
    }
}
#endif
