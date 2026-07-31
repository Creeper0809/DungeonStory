using System;
using UnityEngine;

[Flags]
public enum ServiceProcessStageMask
{
    None = 0,
    Reception = 1 << 0,
    Waiting = 1 << 1,
    Service = 1 << 2,
    Payment = 1 << 3,
    Cleanup = 1 << 4
}

[Serializable]
public sealed class ServiceModeProcessContract
{
    public ServiceOperationMode mode;
    public ServiceProcessStageMask activeStages =
        ServiceProcessStageMask.Service | ServiceProcessStageMask.Payment;
    [Min(0f)] public float receptionSeconds;
    [Min(0f)] public float waitingSeconds;
    [Min(0f)] public float serviceSeconds = 1f;
    [Min(0f)] public float paymentSeconds;
    [Min(0f)] public float cleanupSeconds;
    [Min(0)] public int basePrice = 5;
    public float satisfaction = 45f;
    public string[] requiredFeatureTags = Array.Empty<string>();
}

[CreateAssetMenu(
    menuName = "DungeonStory/Service Rooms/Service Process",
    fileName = "ServiceProcess")]
public sealed class ServiceProcessSO : ScriptableObject
{
    [SerializeField] private string processId = string.Empty;
    [SerializeField] private ServiceCategory serviceCategory;
    [SerializeField] private string ownerHubTag = string.Empty;
    [SerializeField] private ServiceModeProcessContract[] modeContracts =
        Array.Empty<ServiceModeProcessContract>();

    [Header("Physical input")]
    [SerializeField] private string physicalInputItemId = string.Empty;
    [Min(0), SerializeField] private int physicalInputQuantity;
    [SerializeField] private StockCategory physicalInputCategory;

    [Header("Work and utilities")]
    [SerializeField] private string workTypeId = string.Empty;
    [Min(0f), SerializeField] private float cleanWater;
    [Min(0f), SerializeField] private float wastewater;
    [SerializeField] private bool allowsManualWaterFallback;

    [Header("Completion")]
    [SerializeField] private ServicePaymentPolicy paymentPolicy =
        ServicePaymentPolicy.PayAfterCompletion;
    [SerializeField] private bool requiresCleanup;

    public string ProcessId => processId?.Trim() ?? string.Empty;
    public ServiceCategory ServiceCategory => serviceCategory;
    public string OwnerHubTag => ownerHubTag?.Trim() ?? string.Empty;
    public string PhysicalInputItemId =>
        physicalInputItemId?.Trim() ?? string.Empty;
    public int PhysicalInputQuantity => Mathf.Max(0, physicalInputQuantity);
    public StockCategory PhysicalInputCategory => physicalInputCategory;
    public bool TryGetWorkTypeId(out WorkTypeId value)
    {
        if (string.IsNullOrWhiteSpace(workTypeId))
        {
            value = default;
            return false;
        }

        value = new WorkTypeId(workTypeId);
        return true;
    }
    public float CleanWater => Mathf.Max(0f, cleanWater);
    public float Wastewater => Mathf.Max(0f, wastewater);
    public bool AllowsManualWaterFallback => allowsManualWaterFallback;
    public ServicePaymentPolicy PaymentPolicy => paymentPolicy;
    public bool RequiresCleanup => requiresCleanup;
    public bool IsValid =>
        ProcessId.Length > 0
        && OwnerHubTag.Length > 0;

    public bool TryGetContract(
        ServiceOperationMode mode,
        out ServiceModeProcessContract contract)
    {
        if (modeContracts != null)
        {
            for (int index = 0; index < modeContracts.Length; index++)
            {
                ServiceModeProcessContract candidate = modeContracts[index];
                if (candidate != null && candidate.mode == mode)
                {
                    contract = candidate;
                    return true;
                }
            }
        }

        contract = null;
        return false;
    }

#if UNITY_EDITOR
    public void Configure(
        string id,
        ServiceCategory category,
        string hubTag,
        ServiceModeProcessContract[] contracts,
        string inputItemId,
        int inputQuantity,
        StockCategory inputCategory,
        string requiredWorkTypeId,
        float water,
        float waste,
        bool manualWaterFallback,
        ServicePaymentPolicy completionPaymentPolicy,
        bool cleanup)
    {
        processId = id?.Trim() ?? string.Empty;
        serviceCategory = category;
        ownerHubTag = hubTag?.Trim() ?? string.Empty;
        modeContracts = contracts ?? Array.Empty<ServiceModeProcessContract>();
        physicalInputItemId = inputItemId?.Trim() ?? string.Empty;
        physicalInputQuantity = Mathf.Max(0, inputQuantity);
        physicalInputCategory = inputCategory;
        workTypeId = requiredWorkTypeId?.Trim() ?? string.Empty;
        cleanWater = Mathf.Max(0f, water);
        wastewater = Mathf.Max(0f, waste);
        allowsManualWaterFallback = manualWaterFallback;
        paymentPolicy = completionPaymentPolicy;
        requiresCleanup = cleanup;
    }
#endif
}
