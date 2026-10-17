public interface IProductionPreparedOutputRoutingPersistence
{
    ProductionPreparedOutputRoutingSaveData Capture();
    ProductionPreparedOutputRoutingSaveData BuildRestoreCandidate(
        ProductionPreparedOutputRoutingSaveData source);
    void Restore(ProductionPreparedOutputRoutingSaveData candidate);
}
