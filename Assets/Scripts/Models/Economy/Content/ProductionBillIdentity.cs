public static class ProductionBillIdentity
{
    public const string DestinationPrefix = "production:";
    public const string OutputDestinationPrefix = ProductionOutputDestinationId.Prefix;
    public const string StockSensorDestinationPrefix = "production-sensor:";
    public const string StockSensorItemId = "component:stock-sensor-panel";
    public const string GenericBillLifecycleSchema =
        "production-generic-bill-lifecycle@2";
}
