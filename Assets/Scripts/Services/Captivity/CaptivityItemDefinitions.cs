using System;

public static class CaptivityItemDefinitions
{
    public const string RestraintsItemId = "captivity:restraints";
    public const string ExtractedBloodItemId = "captivity:extracted-blood";
    public const string MemoryResidueItemId = "captivity:memory-residue";

    public static bool TryGetDefinition(
        string itemId,
        out DungeonItemDefinition definition)
    {
        string normalized = itemId?.Trim() ?? string.Empty;
        if (string.Equals(normalized, RestraintsItemId, StringComparison.Ordinal))
        {
            definition = new DungeonItemDefinition(
                RestraintsItemId,
                "구속구",
                "포로를 감방으로 호송할 때 쓰는 사슬과 결박구입니다.",
                StockCategory.General,
                18,
                null,
                2.5f,
                10);
            return true;
        }

        if (string.Equals(normalized, ExtractedBloodItemId, StringComparison.Ordinal))
        {
            definition = new DungeonItemDefinition(
                ExtractedBloodItemId,
                "추출 혈액",
                "의식과 연금 작업에 쓰이는 위험한 부산물입니다.",
                StockCategory.Biological,
                22,
                null,
                0.5f,
                20);
            return true;
        }

        if (string.Equals(normalized, MemoryResidueItemId, StringComparison.Ordinal))
        {
            definition = new DungeonItemDefinition(
                MemoryResidueItemId,
                "기억 잔재",
                "심문에서 뽑아낸 불완전한 기억의 응결물입니다.",
                StockCategory.Knowledge,
                35,
                null,
                0.2f,
                20);
            return true;
        }

        definition = null;
        return false;
    }
}
