using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Operation;
using UnityEngine;

public static class V21ContentEffectExecutionRegistry
{
    private static readonly IReadOnlyDictionary<V20ContentEffectKind, string>
        Owners = new Dictionary<V20ContentEffectKind, string>
        {
            [V20ContentEffectKind.Mood] = "character mood command / characters",
            [V20ContentEffectKind.Trauma] = "grief-trauma command / characters.psychosocial",
            [V20ContentEffectKind.SkillExperience] = "character progression command / characters",
            [V20ContentEffectKind.Health] = "body-health command / characters.health",
            [V20ContentEffectKind.Relationship] = "social-memory command / characters",
            [V20ContentEffectKind.FactionRapport] = "faction campaign command / factions.campaign",
            [V20ContentEffectKind.FactionGrievance] = "faction campaign command / factions.campaign",
            [V20ContentEffectKind.FactionObligation] = "faction campaign command / factions.campaign",
            [V20ContentEffectKind.Money] = "money account command / session",
            [V20ContentEffectKind.ItemGrant] = "physical item command / items.world-stacks",
            [V20ContentEffectKind.ItemConsume] = "atomic item consumption / items.world-stacks",
            [V20ContentEffectKind.WorldFlag] = "campaign command / society.events",
            [V20ContentEffectKind.WorkDelayDays] = "campaign work-delay command / society.events",
            [V20ContentEffectKind.Threat] = "milestone pressure command / run.milestones",
            [V20ContentEffectKind.DiseaseExposure] = "population-health command / characters.health",
            [V20ContentEffectKind.AmbitionProgress] = "narrative command / characters.narrative",
            [V20ContentEffectKind.MilestonePressure] = "milestone pressure command / run.milestones",
            [V20ContentEffectKind.RetirementSchedule] =
                "career retirement schedule / characters.career"
        };

    public static bool HasExecutionOwner(V20ContentEffectKind kind) =>
        kind != V20ContentEffectKind.None && Owners.ContainsKey(kind);

    public static string DescribeOwner(V20ContentEffectKind kind) =>
        Owners.TryGetValue(kind, out string owner) ? owner : string.Empty;
}

