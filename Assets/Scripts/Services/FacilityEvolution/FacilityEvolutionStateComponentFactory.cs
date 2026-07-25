public interface IFacilityEvolutionStateComponentFactory
{
    FacilityEvolutionStateComponent GetOrAdd(BuildableObject facility);
}

public sealed class FacilityEvolutionStateComponentFactory : IFacilityEvolutionStateComponentFactory
{
    public FacilityEvolutionStateComponent GetOrAdd(BuildableObject facility)
    {
        if (facility == null)
        {
            return null;
        }

        FacilityEvolutionStateComponent state = facility.GetComponent<FacilityEvolutionStateComponent>();
        if (state == null)
        {
            state = facility.gameObject.AddComponent<FacilityEvolutionStateComponent>();
        }

        state.InitializeIfNeeded(facility);
        return state;
    }
}
