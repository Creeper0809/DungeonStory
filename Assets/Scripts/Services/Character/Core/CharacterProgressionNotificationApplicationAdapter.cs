using System;
using DungeonStory.Foundation;
using DungeonStory.Operation;

public sealed class CharacterProgressionNotificationApplicationAdapter
{
    private readonly IGameEventBus gameEventBus;

    public CharacterProgressionNotificationApplicationAdapter(
        IGameEventBus gameEventBus)
    {
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
    }

    public void NotifyActiveDraftReady(
        CharacterActor actor,
        int unlockLevel,
        Action openGrowthTab)
    {
        gameEventBus.RaiseAlert(
            "기술 선택 가능",
            $"{actor?.Identity?.DisplayName ?? "인물"}의 Lv.{unlockLevel} 액티브 후보가 준비되었습니다.",
            EventAlertImportance.Medium,
            "성장",
            new[]
            {
                new EventAlertChoice(
                    "성장 탭 열기",
                    "후보 3개를 확인합니다.",
                    openGrowthTab ?? throw new ArgumentNullException(
                        nameof(openGrowthTab)))
            });
    }

    public void NotifySkillUnlocked(
        CharacterSkillInstance skill,
        bool isUltimate)
    {
        if (skill == null)
        {
            throw new ArgumentNullException(nameof(skill));
        }

        gameEventBus.RaiseAlert(
            skill.displayName,
            $"{skill.narrativeReason}\n{skill.description}",
            isUltimate
                ? EventAlertImportance.High
                : EventAlertImportance.Medium,
            "성장");
    }

    public void ShowGrowth(CharacterActor actor)
    {
        if (actor == null)
        {
            throw new ArgumentNullException(nameof(actor));
        }

        gameEventBus.ShowInfo(actor);
        gameEventBus.Publish(new CharacterGrowthTabRequestedEvent(actor));
    }
}

public static class CharacterProgressionConstructionApplicationAdapter
{
    public static void ConstructCharacterProgression(
        this CharacterProgression progression,
        ICharacterSkillGenerationService generationService,
        ICharacterSkillSystemSettingsProvider settingsProvider,
        IGameEventBus gameEventBus,
        CharacterProgressionProfileProjector profileProjector)
    {
        if (progression == null)
        {
            throw new ArgumentNullException(nameof(progression));
        }

        progression.ConstructCharacterProgression(
            generationService,
            settingsProvider,
            new CharacterProgressionNotificationApplicationAdapter(gameEventBus),
            profileProjector);
    }
}

public readonly struct CharacterGrowthTabRequestedEvent
{
    public CharacterGrowthTabRequestedEvent(CharacterActor actor)
    {
        Actor = actor;
    }

    public CharacterActor Actor { get; }
}
