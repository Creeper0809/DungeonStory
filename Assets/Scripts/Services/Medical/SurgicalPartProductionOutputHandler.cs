using System;

public sealed class SurgicalPartProductionOutputHandler :
    IProductionOutputHandler
{
    public static readonly string ProstheticArmOutputId =
        SurgeryItemDefinitions.GetProstheticItemId("arm:left");
    public static readonly string ProstheticLegOutputId =
        SurgeryItemDefinitions.GetProstheticItemId("leg:left");
    public static readonly string ArtificialEyeOutputId =
        SurgeryItemDefinitions.GetProstheticItemId("eye:left");

    private readonly ISurgicalPartRuntime parts;
    private readonly IItemDefinitionCatalog itemCatalog;

    public SurgicalPartProductionOutputHandler(
        ISurgicalPartRuntime parts,
        IItemDefinitionCatalog itemCatalog)
    {
        this.parts = parts ?? throw new ArgumentNullException(nameof(parts));
        this.itemCatalog = itemCatalog
            ?? throw new ArgumentNullException(nameof(itemCatalog));
    }

    public bool CanHandle(string itemId)
    {
        return string.Equals(itemId, ProstheticArmOutputId, StringComparison.Ordinal)
            || string.Equals(itemId, ProstheticLegOutputId, StringComparison.Ordinal)
            || string.Equals(itemId, ArtificialEyeOutputId, StringComparison.Ordinal);
    }

    public bool TryProduce(
        ProductionOutputContext context,
        out string failureReason)
    {
        if (!CanHandle(context.ItemId))
        {
            failureReason = FailureCode.SurgeryPartUnavailable.ToString();
            return false;
        }

        ResolveDefinition(
            context.ItemId,
            out string nodeId,
            out SurgicalPartKind kind);
        string displayName = itemCatalog
            .GetRequired(new ItemDefinitionId(context.ItemId))
            .DisplayName;
        for (int index = 0; index < context.Amount; index++)
        {
            if (!parts.TryCreateCraftedPart(
                    nodeId,
                    displayName,
                    kind,
                    ResolveQuality(context.Worker),
                    context.Facility.centerPos,
                    out _,
                    out DomainFailure failure))
            {
                failureReason = failure.Code.ToString();
                return false;
            }
        }

        failureReason = string.Empty;
        return true;
    }

    private static void ResolveDefinition(
        string itemId,
        out string nodeId,
        out SurgicalPartKind kind)
    {
        kind = SurgicalPartKind.Prosthetic;
        if (string.Equals(
                itemId,
                ProstheticLegOutputId,
                StringComparison.Ordinal))
        {
            nodeId = "leg:left";
            return;
        }

        if (string.Equals(
                itemId,
                ArtificialEyeOutputId,
                StringComparison.Ordinal))
        {
            nodeId = "eye:left";
            kind = SurgicalPartKind.Implant;
            return;
        }

        nodeId = "arm:left";
    }

    private static float ResolveQuality(CharacterActor worker)
    {
        CharacterStats stats = worker != null
            ? worker.GetComponent<CharacterStats>()
            : null;
        float medical = stats != null
            ? stats.GetCharacterStat(CharacterStatType.Medical)
            : 0f;
        float dexterity = stats != null
            ? stats.GetCharacterStat(CharacterStatType.Dexterity)
            : 0f;
        return UnityEngine.Mathf.Clamp(
            0.7f + medical * 0.012f + dexterity * 0.008f,
            0.7f,
            1.25f);
    }
}
