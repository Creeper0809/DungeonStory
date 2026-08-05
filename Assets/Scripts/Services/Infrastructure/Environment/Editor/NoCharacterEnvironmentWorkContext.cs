#if UNITY_EDITOR
public sealed class NoCharacterEnvironmentWorkContext :
    ICharacterEnvironmentWorkContext
{
    public static readonly NoCharacterEnvironmentWorkContext Instance = new();

    private NoCharacterEnvironmentWorkContext()
    {
    }

    public EnvironmentalExposureBand GetPhysiologicalBand(
        CharacterId characterId) => EnvironmentalExposureBand.Stable;

    public EnvironmentalExposureBand GetVisualBand(
        CharacterId characterId) => EnvironmentalExposureBand.Stable;

    public void SetWorkContext(
        CharacterId characterId,
        EnvironmentalWorkKind workKind)
    {
    }

    public void ClearWorkContext(CharacterId characterId)
    {
    }
}
#endif
