using System;

/// <summary>Applies node experience through character progression and reports actual transitions, without narrative policy.</summary>
public sealed class OffenseExpeditionExperienceRules
{
    public void AwardNodeExperience(OffenseExpeditionRun expedition, OffenseRouteNode node,
        Action<OffenseNodeExperienceData> observed)
    {
        if (observed == null) throw new ArgumentNullException(nameof(observed));
        if (expedition == null || node == null) return;
        int experience = CalculateNodeExperience(node, expedition.Target?.campaignOrder ?? 1);
        if (experience <= 0) return;
        foreach (OffenseExpeditionMemberState member in expedition.MemberStates)
        {
            CharacterActor actor = member?.Actor;
            if (actor == null || actor.IsDead || actor.Progression == null) continue;
            var progression = actor.Progression;
            int levelBefore = progression.Level, experienceBefore = progression.CurrentExperience;
            progression.AddExperience(experience);
            observed(new OffenseNodeExperienceData
            {
                characterId = CharacterPersistentIdentity.Require(actor).Value,
                displayName = actor.BuildingDisplayName,
                requestedAmount = experience, levelBefore = levelBefore, levelAfter = progression.Level,
                experienceBefore = experienceBefore, experienceAfter = progression.CurrentExperience
            });
        }
    }

    public int CalculateNodeExperience(OffenseRouteNode node, int stage)
    {
        if (node == null)
        {
            return 0;
        }

        return OffenseExpeditionExperienceCalculation.CalculateNodeExperience(
            new OffenseExpeditionExperienceNodeSnapshot(
                node.Kind,
                node.DangerMultiplier,
                node.Id),
            stage);
    }

    public int CalculateSuccessfulReturnExperience(
        OffenseExpeditionRun expedition)
    {
        return CalculateSuccessfulReturnExperience(
            expedition?.Target?.campaignOrder ?? 1);
    }

    public int CalculateSuccessfulReturnExperience(int stage)
    {
        return OffenseExpeditionExperienceCalculation
            .CalculateSuccessfulReturnExperience(stage);
    }
}
