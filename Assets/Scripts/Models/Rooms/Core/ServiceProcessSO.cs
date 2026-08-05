using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
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
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ServiceProcessSO : ScriptableObject
{
    [SerializeField] private string processId = string.Empty;
    [SerializeField] private ServiceCategory serviceCategory;
    [SerializeField] private string ownerHubTag = string.Empty;
    [SerializeField] private ServiceModeProcessContract[] modeContracts =
        Array.Empty<ServiceModeProcessContract>();

    [Header("Work and utilities")]
    [SerializeField] private string workTypeId = string.Empty;
    [Min(0f), SerializeField] private float cleanWater;
    [Min(0f), SerializeField] private float wastewater;
    [SerializeField] private bool allowsManualWaterFallback;

    [Header("Completion")]
    [SerializeField] private ServicePaymentPolicy paymentPolicy =
        ServicePaymentPolicy.PayAfterCompletion;
    [SerializeField] private bool requiresCleanup;

    public string ProcessId => processId ?? string.Empty;
    public ServiceCategory ServiceCategory => serviceCategory;
    public string OwnerHubTag => ownerHubTag ?? string.Empty;
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

    public IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = new();
        if (!IsCanonicalRequired(processId))
        {
            errors.Add("Process ID is empty or non-canonical.");
        }
        if (!Enum.IsDefined(typeof(ServiceCategory), serviceCategory))
        {
            errors.Add($"Service category '{serviceCategory}' is undefined.");
        }
        if (!IsCanonicalRequired(ownerHubTag))
        {
            errors.Add("Owner hub tag is empty or non-canonical.");
        }
        if (modeContracts == null || modeContracts.Length == 0)
        {
            errors.Add("At least one mode contract is required.");
        }
        else
        {
            HashSet<ServiceOperationMode> modes = new();
            for (int index = 0; index < modeContracts.Length; index++)
            {
                ValidateContract(modeContracts[index], index, modes, errors);
            }
        }

        if (!string.IsNullOrEmpty(workTypeId)
            && (!IsCanonicalRequired(workTypeId)
                || !WorkTypeCatalog.TryGet(workTypeId, out _)))
        {
            errors.Add($"Work type '{workTypeId}' is missing or non-canonical.");
        }
        if (!IsFiniteNonNegative(cleanWater)
            || !IsFiniteNonNegative(wastewater))
        {
            errors.Add("Water values must be finite and non-negative.");
        }
        if (!Enum.IsDefined(typeof(ServicePaymentPolicy), paymentPolicy))
        {
            errors.Add($"Payment policy '{paymentPolicy}' is undefined.");
        }
        return errors;
    }

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
        workTypeId = requiredWorkTypeId?.Trim() ?? string.Empty;
        cleanWater = Mathf.Max(0f, water);
        wastewater = Mathf.Max(0f, waste);
        allowsManualWaterFallback = manualWaterFallback;
        paymentPolicy = completionPaymentPolicy;
        requiresCleanup = cleanup;
    }
#endif

    private static void ValidateContract(
        ServiceModeProcessContract contract,
        int index,
        ISet<ServiceOperationMode> modes,
        ICollection<string> errors)
    {
        if (contract == null)
        {
            errors.Add($"Mode contract {index} is missing.");
            return;
        }
        if (!Enum.IsDefined(typeof(ServiceOperationMode), contract.mode)
            || !modes.Add(contract.mode))
        {
            errors.Add($"Mode contract {index} has an undefined or duplicate mode.");
        }
        const ServiceProcessStageMask allStages =
            ServiceProcessStageMask.Reception
            | ServiceProcessStageMask.Waiting
            | ServiceProcessStageMask.Service
            | ServiceProcessStageMask.Payment
            | ServiceProcessStageMask.Cleanup;
        if (contract.activeStages == ServiceProcessStageMask.None
            || (contract.activeStages & ~allStages) != 0)
        {
            errors.Add($"Mode contract {index} has an invalid stage mask.");
        }
        if (!IsFiniteNonNegative(contract.receptionSeconds)
            || !IsFiniteNonNegative(contract.waitingSeconds)
            || !IsFiniteNonNegative(contract.serviceSeconds)
            || !IsFiniteNonNegative(contract.paymentSeconds)
            || !IsFiniteNonNegative(contract.cleanupSeconds)
            || contract.basePrice < 0
            || float.IsNaN(contract.satisfaction)
            || float.IsInfinity(contract.satisfaction)
            || contract.satisfaction < -100f
            || contract.satisfaction > 100f)
        {
            errors.Add($"Mode contract {index} has invalid numeric values.");
        }
        if (contract.requiredFeatureTags == null)
        {
            errors.Add($"Mode contract {index} has a null feature-tag list.");
            return;
        }

        HashSet<string> featureTags = new(StringComparer.Ordinal);
        foreach (string tag in contract.requiredFeatureTags)
        {
            if (!IsCanonicalRequired(tag) || !featureTags.Add(tag))
            {
                errors.Add($"Mode contract {index} has an invalid feature tag.");
            }
        }
    }

    private static bool IsCanonicalRequired(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool IsFiniteNonNegative(float value) =>
        !float.IsNaN(value)
        && !float.IsInfinity(value)
        && value >= 0f;
}
