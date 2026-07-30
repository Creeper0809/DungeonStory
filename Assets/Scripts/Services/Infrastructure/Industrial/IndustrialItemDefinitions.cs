public static class IndustrialItemDefinitions
{
    public const string SludgeId = "industrial:sludge";

    public static bool TryGetDefinition(
        string itemId,
        out DungeonItemDefinition definition)
    {
        if (string.Equals(
                itemId?.Trim(),
                SludgeId,
                System.StringComparison.Ordinal))
        {
            definition = new DungeonItemDefinition(
                SludgeId,
                "오수 슬러지",
                "폐수 처리에서 남은 침전물. 퇴비, 저급 연료, 독성 연금 재료로 가공할 수 있습니다.",
                StockCategory.General,
                2,
                null,
                1.2f,
                50);
            return true;
        }

        definition = null;
        return false;
    }
}
