public static class CharacterAiJobGiverRegistry
{
    private static readonly ICharacterAiJobGiverCatalog Catalog = new CharacterAiJobGiverCatalog();

    public static CharacterAiJobGiver ExitDungeon => Catalog.ExitDungeon;
    public static CharacterAiJobGiver GetFood => Catalog.GetFood;
    public static CharacterAiJobGiver Rest => Catalog.Rest;
    public static CharacterAiJobGiver Toilet => Catalog.Toilet;
    public static CharacterAiJobGiver Hygiene => Catalog.Hygiene;
    public static CharacterAiJobGiver Work => Catalog.Work;
    public static CharacterAiJobGiver Shopping => Catalog.Shopping;
    public static CharacterAiJobGiver LookAround => Catalog.LookAround;
    public static CharacterAiJobGiver Wait => Catalog.Wait;

    public static CharacterAiJobGiver Get(CharacterAiBranch branch)
    {
        return Catalog.Get(branch);
    }
}
