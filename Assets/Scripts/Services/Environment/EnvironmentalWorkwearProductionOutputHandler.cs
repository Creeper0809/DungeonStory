using System;

public sealed class EnvironmentalWorkwearProductionOutputHandler :
    IProductionOutputHandler
{
    public const string SlimePadItemId =
        "equipment:slime-warming-pad";
    public const string ColdSuitItemId =
        "equipment:cold-work-suit";
    public const string RuneSuitItemId =
        "equipment:rune-cold-suit";

    private readonly IEnvironmentalWorkwearRuntime workwear;

    public EnvironmentalWorkwearProductionOutputHandler(
        IEnvironmentalWorkwearRuntime workwear)
    {
        this.workwear = workwear
            ?? throw new ArgumentNullException(nameof(workwear));
    }

    public bool CanHandle(string itemId)
    {
        return string.Equals(
                itemId,
                SlimePadItemId,
                StringComparison.Ordinal)
            || string.Equals(
                itemId,
                ColdSuitItemId,
                StringComparison.Ordinal)
            || string.Equals(
                itemId,
                RuneSuitItemId,
                StringComparison.Ordinal);
    }

    public bool TryProduce(
        ProductionOutputContext context,
        out string failureReason)
    {
        string workwearId = context.ItemId switch
        {
            SlimePadItemId => "workwear:slime-warming-pad",
            ColdSuitItemId => "workwear:cold-work-suit",
            RuneSuitItemId => "workwear:rune-cold-suit",
            _ => string.Empty
        };
        if (workwearId.Length == 0)
        {
            failureReason =
                $"지원하지 않는 환경 작업복 출력입니다: {context.ItemId}";
            return false;
        }

        return workwear.TryAddStock(
            workwearId,
            context.Amount,
            out failureReason);
    }
}
