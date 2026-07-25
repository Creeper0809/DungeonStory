public interface IFacilityEvolutionRecordComponentFactory
{
    FacilityEvolutionRecordComponent GetOrAdd(BuildableObject facility);
}

public sealed class FacilityEvolutionRecordComponentFactory : IFacilityEvolutionRecordComponentFactory
{
    public FacilityEvolutionRecordComponent GetOrAdd(BuildableObject facility)
    {
        if (facility == null)
        {
            return null;
        }

        FacilityEvolutionRecordComponent component = facility.GetComponent<FacilityEvolutionRecordComponent>();
        if (component == null)
        {
            component = facility.gameObject.AddComponent<FacilityEvolutionRecordComponent>();
        }

        return component;
    }
}
