public interface IFacilityEvolutionStateComponentFactory : IBuildingEvolutionStatePort
{
    FacilityEvolutionStateComponent GetOrAdd(BuildableObject facility);
}

public sealed class FacilityEvolutionStateComponentFactory : IFacilityEvolutionStateComponentFactory
{
    void IBuildingEvolutionStatePort.EnsureInitialized(
        IBuildingWorldEntryPort facility)
    {
        if (facility == null)
        {
            return;
        }

        if (facility is not BuildableObject buildableObject)
        {
            throw new System.ArgumentException(
                $"{nameof(IBuildingEvolutionStatePort)} only accepts {nameof(BuildableObject)} facilities.",
                nameof(facility));
        }

        GetOrAdd(buildableObject);
    }

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
