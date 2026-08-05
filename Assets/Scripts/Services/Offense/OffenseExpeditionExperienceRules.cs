/// <summary>
/// Applies the named expedition experience calculation to character progression.
/// The expedition runtime coordinates journey state; it does not write character
/// progression records itself.
/// </summary>
public sealed class OffenseExpeditionExperienceRules
{
    public void AwardNodeExperience(
        OffenseExpeditionRun expedition,
        OffenseRouteNode node)
    {
        if (expedition == null || node == null)
        {
            return;
        }

        int stage = expedition.Target?.campaignOrder ?? 1;
        int experience = CalculateNodeExperience(node, stage);
        if (experience <= 0)
        {
            return;
        }

        foreach (OffenseExpeditionMemberState member in expedition.MemberStates)
        {
            CharacterActor actor = member?.Actor;
            if (actor == null || actor.IsDead)
            {
                continue;
            }

            actor.Progression?.AddExperience(experience);
            actor.Progression?.RecordNarrative(
                CharacterNarrativeDomain.Expedition,
                $"node:{node.Kind}",
                expedition.Target?.id ?? string.Empty,
                "resolved",
                experience);
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
