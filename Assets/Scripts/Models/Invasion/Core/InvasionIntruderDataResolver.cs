public interface IInvasionIntruderDataProvider<TData>
    where TData : class
{
    TData GetRequiredIntruderData(TData configuredData);
}
