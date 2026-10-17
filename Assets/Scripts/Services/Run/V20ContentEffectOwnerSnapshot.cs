using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Operation;
using UnityEngine;


internal sealed class V20CharacterEffectOwnerSnapshot
{
    internal V20CharacterEffectOwnerSnapshot(CharacterActor actor)
    {
        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        Stats = actor.Stats?.CaptureMoodDeliveryTransactionState();
        Progression = actor.Progression?.CapturePersistentState();
        Social = actor.SocialMemory?.CaptureSnapshot();
    }

    internal CharacterActor Actor { get; }
    internal CharacterMoodDeliveryTransactionSnapshot Stats { get; }
    internal CharacterProgressionSnapshot Progression { get; }
    internal CharacterSocialMemorySnapshot Social { get; }
}

internal sealed class V20ContentEffectOwnerSnapshot
{
    internal SeasonalEventWorldSaveData Seasonal;
    internal SocietyEventWorldSaveData Society;
    internal FactionCampaignWorldSaveData Factions;
    internal RunMilestoneWorldSaveData Milestones;
    internal DungeonPhysicalItemSaveData Items;
    internal int MoneyBalance;
    internal CharacterCareerWorldSaveData Careers;
    internal DungeonCharacterBodyHealthSaveData BodyHealth;
    internal CharacterPsychosocialWorldSaveData Psychosocial;
    internal PopulationHealthWorldSaveData PopulationHealth;
    internal CharacterNarrativeWorldSaveData Narrative;
    internal V20CharacterEffectOwnerSnapshot[] Characters =
        Array.Empty<V20CharacterEffectOwnerSnapshot>();
}
