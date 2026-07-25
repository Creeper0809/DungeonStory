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
        builder.Register<CharacterAiFacilityLookup>(Lifetime.Singleton)
            .As<ICharacterAiFacilityLookup>();
        builder.Register<DefaultCharacterAiWorldSignalQuery>(Lifetime.Singleton)
            .As<ICharacterAiWorldSignalQuery>();
        builder.Register<FacilityCandidateCacheStore>(Lifetime.Singleton)
            .As<IFacilityCandidateCache>();

        builder.Register<RoomLayoutCache>(Lifetime.Singleton)
            .As<IRoomLayoutCache>();
        builder.Register<DoorAccessService>(Lifetime.Singleton)
            .As<IDoorAccessQuery>()
            .As<IDoorAccessCommandService>()
            .As<IDoorAccessSubjectRegistry>()
            .As<IDoorAccessStateChangeSink>();
        builder.Register<ResourceRoomEnvironmentSettingsProvider>(Lifetime.Singleton)
            .As<IRoomEnvironmentSettingsProvider>();
        builder.Register<RoomEnvironmentEvaluator>(Lifetime.Singleton)
            .As<IRoomEnvironmentEvaluator>();
        builder.Register<RoomEnvironmentQuery>(Lifetime.Singleton)
            .As<IRoomEnvironmentQuery>();
        builder.Register<RoomEnvironmentExperienceService>(Lifetime.Singleton)
            .As<IRoomEnvironmentExperienceService>();
        builder.RegisterEntryPoint<RoomInspectionRuntime>(Lifetime.Singleton);
        builder.Register<RoomFacilityPolicyService>(Lifetime.Singleton)
            .As<IRoomFacilityPolicy>();

        builder.Register<CharacterBehaviorTreeRuntimeConfigurator>(Lifetime.Singleton)
            .As<ICharacterBehaviorTreeRuntimeConfigurator>();
        builder.Register<CharacterAiSchedulingService>(Lifetime.Singleton)
            .As<ICharacterAiSchedulingService>()
            .As<ICharacterAiDiagnosticsQuery>();
        builder.Register<CharacterMoodImpulseQuery>(Lifetime.Singleton)
            .As<ICharacterMoodImpulseQuery>();
        builder.Register<CharacterSocialMemoryFactory>(Lifetime.Singleton)
            .As<ICharacterSocialMemoryFactory>();
        builder.Register<CharacterFeedbackBubbleFactory>(Lifetime.Singleton)
            .As<ICharacterFeedbackBubbleFactory>();
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

        builder.Register<SocialReputationRuntimeProvider>(Lifetime.Singleton)
            .As<ISocialReputationRuntimeProvider>();
        builder.Register<SocialReputationBiasService>(Lifetime.Singleton)
            .As<ISocialReputationBiasService>();
        builder.Register<RegularCustomerRuntimeProvider>(Lifetime.Singleton)
            .As<IRegularCustomerRuntimeProvider>();

        RegularCustomerRuntime sceneRuntime = runtimeReferences.RegularCustomers;
        if (sceneRuntime != null)
        {
            builder.RegisterComponent(sceneRuntime);
            return;
        }

        builder.RegisterComponentOnNewGameObject<RegularCustomerRuntime>(
                Lifetime.Singleton,
                nameof(RegularCustomerRuntime))
            .UnderTransform(runtimeRoot);
    }
}
