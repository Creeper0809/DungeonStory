using System;

public readonly struct OffenseExpeditionExperienceNodeSnapshot
{
    public OffenseExpeditionExperienceNodeSnapshot(
        OffenseRouteNodeKind kind,
        float dangerMultiplier,
        string id)
    {
        Kind = kind;
        DangerMultiplier = dangerMultiplier;
        Id = id ?? string.Empty;
    }

    public OffenseRouteNodeKind Kind { get; }
    public float DangerMultiplier { get; }
    public string Id { get; }
}

public static class OffenseExpeditionExperienceCalculation
{
    public static int CalculateNodeExperience(
        OffenseExpeditionExperienceNodeSnapshot node,
        int stage)
    {
        int normalizedStage = ClampStage(stage);
        return node.Kind switch
        {
            OffenseRouteNodeKind.Event => 35 + normalizedStage * 10,
            OffenseRouteNodeKind.Camp => 35 + normalizedStage * 10,
            OffenseRouteNodeKind.Cache => 35 + normalizedStage * 10,
            OffenseRouteNodeKind.Battle => IsEliteBattleNode(node)
                ? 100 + normalizedStage * 25
                : 80 + normalizedStage * 20,
            OffenseRouteNodeKind.Boss => 140 + normalizedStage * 30,
            _ => 0
        };
    }

    public static int CalculateSuccessfulReturnExperience(int stage)
    {
        return 60 + ClampStage(stage) * 20;
    }

    private static int ClampStage(int stage)
    {
        return stage < 1 ? 1 : stage > 6 ? 6 : stage;
    }

    private static bool IsEliteBattleNode(
        OffenseExpeditionExperienceNodeSnapshot node)
    {
        return node.Kind == OffenseRouteNodeKind.Battle
            && (node.DangerMultiplier >= 0.95f
                || (!string.IsNullOrWhiteSpace(node.Id)
                    && node.Id.IndexOf(
                        "elite",
                        StringComparison.OrdinalIgnoreCase) >= 0));
    }
}
