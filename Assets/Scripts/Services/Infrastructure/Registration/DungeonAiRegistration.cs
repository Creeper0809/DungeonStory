using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public static class DungeonAiRegistration
{
    public static void RegisterDungeonAiAndRooms(
        this IContainerBuilder builder,
        Transform runtimeRoot,
        CharacterSceneRuntimeReferences runtimeReferences)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        if (runtimeRoot == null)
        {
            throw new ArgumentNullException(nameof(runtimeRoot));
        }

        if (runtimeReferences == null)
        {
            throw new ArgumentNullException(nameof(runtimeReferences));
        }

        builder.Register<AiDirectorContextSceneQuery>(Lifetime.Singleton)
            .As<IAiDirectorContextSceneQuery>();
        builder.Register<ResourceCharacterAiPerfSettingsProvider>(Lifetime.Singleton)
            .As<ICharacterAiPerfSettingsProvider>();
        builder.Register<CharacterAiPerformanceCaptureScope>(Lifetime.Singleton)
            .As<ICharacterAiPerformanceCaptureScope>();
        builder.Register<CharacterAiPerformanceRecorder>(Lifetime.Singleton)
            .As<ICharacterAiPerformanceRecorder>()
            .As<IGridPathPerformanceRecorder>();
        builder.Register<CharacterAiFacilityLookup>(Lifetime.Singleton)
            .As<ICharacterAiFacilityLookup>();
        builder.Register<DefaultCharacterAiWorldSignalQuery>(Lifetime.Singleton)
            .As<ICharacterAiWorldSignalQuery>();
        builder.Register<FacilityCandidateCacheStore>(Lifetime.Singleton)
            .As<IFacilityCandidateCache>()
            .As<IBuildingFacilityStateChangePort>();

        builder.Register<RoomLayoutCache>(Lifetime.Singleton)
            .As<IRoomLayoutCache>();
        builder.Register<BuildingDoorCharacterWildlifeAdapter>(Lifetime.Singleton)
            .As<IBuildingDoorTraversalSubjectPort>()
            .As<IBuildingDoorAccessSubjectPort>()
            .As<IBuildingDoorPolicyInvalidationPort>();
        builder.Register<BuildingDoorRoomPolicyAdapter>(Lifetime.Singleton)
            .As<IBuildingDoorRoomPolicyPort>();
        builder.Register<DoorAccessService>(Lifetime.Singleton)
            .AsSelf();
        builder.Register<DoorAccessUnityAdapter>(Lifetime.Singleton)
            .As<IDoorAccessQuery>()
            .As<IDoorAccessCommandService>()
            .As<IDoorAccessSubjectRegistry>()
            .As<IDoorAccessStateChangeSink>()
            .As<IGridTraversalAccessQuery>();
        builder.Register<ResourceRoomEnvironmentSettingsProvider>(Lifetime.Singleton)
            .As<IRoomEnvironmentSettingsProvider>();
        builder.Register<RoomEnvironmentEvaluator>(Lifetime.Singleton)
            .As<IRoomEnvironmentEvaluator>();
        builder.Register<RoomEnvironmentQuery>(Lifetime.Singleton)
            .As<IRoomEnvironmentQuery>()
            .As<IWorkEnvironmentDefinitionMaximumQuery>();
        builder.Register<RoomEnvironmentExperienceService>(Lifetime.Singleton)
            .As<IRoomEnvironmentExperienceService>();
        builder.Register<RoomInspectionInteractionContext>(Lifetime.Singleton)
            .As<IRoomInspectionInteractionContext>();
        builder.RegisterEntryPoint<RoomInspectionRuntime>(Lifetime.Singleton)
            .AsSelf();
        builder.Register<RoomFacilityPolicyService>(Lifetime.Singleton)
            .As<IRoomFacilityPolicy>()
            .As<IBuildingRoomPolicyPort>();

        builder.Register<CharacterBehaviorTreeRuntimeConfigurator>(Lifetime.Singleton)
            .As<ICharacterBehaviorTreeRuntimeConfigurator>();
        builder.Register<CharacterAiSchedulingService>(Lifetime.Singleton)
            .As<ICharacterAiSchedulingService>()
            .As<ICharacterAiDiagnosticsQuery>();
        builder.RegisterEntryPoint<CharacterAlarmResponseRuntime>(Lifetime.Singleton)
            .AsSelf();
        builder.Register<CharacterMoodImpulseQuery>(Lifetime.Singleton)
            .As<ICharacterMoodImpulseQuery>();
        builder.Register<CharacterSocialMemoryFactory>(Lifetime.Singleton)
            .As<ICharacterSocialMemoryFactory>();
        builder.Register<CharacterFeedbackBubbleFactory>(Lifetime.Singleton)
            .As<ICharacterFeedbackBubbleFactory>();
        builder.Register<DungeonRuntimeWorldUiHierarchyAdapter>(Lifetime.Singleton)
            .As<IWorldUiHierarchy>();
        builder.Register<CharacterFeedbackBubbleViewFactory>(Lifetime.Singleton)
            .As<ICharacterFeedbackBubbleViewFactory>();
        builder.Register<CharacterDialogueBubbleFactory>(Lifetime.Singleton)
            .As<ICharacterDialogueBubbleFactory>();
        builder.Register<CharacterAiJobGiverCatalog>(Lifetime.Singleton)
            .As<ICharacterAiJobGiverCatalog>();
        builder.Register<CharacterAiDecisionPipeline>(Lifetime.Singleton)
            .As<ICharacterAiDecisionPipeline>();
        builder.Register<ResourceCharacterAiActionAssetCatalog>(Lifetime.Singleton)
            .As<ICharacterAiActionAssetCatalog>();
        builder.Register<AIBrainDecisionServices>(Lifetime.Singleton);
        builder.Register<AIBrainExecutionServices>(Lifetime.Singleton);

        builder.Register<SocialReputationBiasService>(Lifetime.Singleton)
            .As<ISocialReputationBiasService>();
        builder.Register<SettlementPopulationCapacityRuntime>(Lifetime.Singleton)
            .As<ISettlementPopulationCapacityQuery>();
        RegularCustomerRuntime sceneRuntime = runtimeReferences.RegularCustomers;
        if (sceneRuntime != null)
        {
            builder.RegisterComponent(sceneRuntime);
        }
        else
        {
            builder.RegisterComponentOnNewGameObject<RegularCustomerRuntime>(
                Lifetime.Singleton,
                nameof(RegularCustomerRuntime))
                .UnderTransform(runtimeRoot);
        }
        builder.Register<RegularCustomerPersistenceAdapter>(Lifetime.Singleton)
            .As<IRegularCustomerPersistence>()
            .As<IRecruitmentCharacterDefinitionCatalog>();
    }
}
