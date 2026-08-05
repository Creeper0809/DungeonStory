using System;
using UnityEngine;

[Serializable]
[BuildingAbilityDisplayName("서비스 허브")]
public sealed class BuildingServiceHubAbility : BuildingAbility
{
    [InspectorName("서비스 종류")]
    public ServiceCategory serviceCategory;

    [InspectorName("서비스 허브 태그")]
    public string serviceHubTag = string.Empty;

    [InspectorName("지원 공정 ID")]
    public string[] supportedProcessIds = Array.Empty<string>();

    [Min(1), InspectorName("기본 용량")]
    public int baseCapacity = 1;

    [InspectorName("허용 운영 모드")]
    public ServiceOperationModeMask allowedModes =
        ServiceOperationModeMask.Direct;

    [InspectorName("내부 직원 간이 이용")]
    public bool allowInternalStaffDirectUse = true;

    [InspectorName("결제 정책")]
    public ServicePaymentPolicy paymentPolicy =
        ServicePaymentPolicy.PayAfterCompletion;

    [Min(0), InspectorName("간이 운영 기본 가격")]
    public int directPrice = 5;

    [Range(-100f, 100f), InspectorName("간이 운영 만족도")]
    public float directSatisfaction = 45f;

    [InspectorName("관리형 필수 기능 태그")]
    public string[] managedRequiredFeatureTags = Array.Empty<string>();

    [InspectorName("자동화 필수 기능 태그")]
    public string[] automatedRequiredFeatureTags = Array.Empty<string>();

    public string ServiceHubTag => serviceHubTag?.Trim() ?? string.Empty;
    public int BaseCapacity => Mathf.Max(1, baseCapacity);
    public bool IsValid => ServiceHubTag.Length > 0;

    public bool Allows(ServiceOperationMode mode)
    {
        ServiceOperationModeMask flag = mode switch
        {
            ServiceOperationMode.Direct => ServiceOperationModeMask.Direct,
            ServiceOperationMode.Managed => ServiceOperationModeMask.Managed,
            ServiceOperationMode.Automated => ServiceOperationModeMask.Automated,
            _ => ServiceOperationModeMask.None
        };
        return (allowedModes & flag) != 0;
    }

    public bool SupportsProcess(string processId)
    {
        if (string.IsNullOrWhiteSpace(processId)
            || supportedProcessIds == null)
        {
            return false;
        }

        string normalized = processId.Trim();
        for (int index = 0; index < supportedProcessIds.Length; index++)
        {
            if (string.Equals(
                    supportedProcessIds[index]?.Trim(),
                    normalized,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

[Serializable]
[BuildingAbilityDisplayName("서비스 보조 시설")]
public sealed class BuildingServiceSupportAbility : BuildingAbility
{
    [InspectorName("보조 시설 ID")]
    public string supportId = string.Empty;

    [InspectorName("기능 태그")]
    public string[] featureTags = Array.Empty<string>();

    [InspectorName("호환 서비스 허브 태그")]
    public string[] compatibleHubTags = Array.Empty<string>();

    [InspectorName("보정 유형")]
    public ServiceSupportModifierType modifierType;

    [Min(0), InspectorName("추가 용량")]
    public int capacity;

    [InspectorName("전력 필요")]
    public bool requiresPower;

    [Min(0f), InspectorName("깨끗한 물")]
    public float cleanWaterPerUse;

    [Min(0f), InspectorName("폐수")]
    public float wastewaterPerUse;

    [InspectorName("물통 대체 허용")]
    public bool allowsManualWaterFallback;

    [Min(0.01f), InspectorName("작업 속도 배율")]
    public float workSpeedMultiplier = 1f;

    [InspectorName("만족도 보정")]
    public float satisfactionModifier;

    [InspectorName("수입 보정")]
    public int revenueModifier;

    public string SupportId => supportId?.Trim() ?? string.Empty;
    public bool IsValid =>
        SupportId.Length > 0
        && featureTags != null
        && featureTags.Length > 0;

    public bool Provides(string featureTag)
    {
        if (string.IsNullOrWhiteSpace(featureTag) || featureTags == null)
        {
            return false;
        }

        string normalized = featureTag.Trim();
        for (int index = 0; index < featureTags.Length; index++)
        {
            if (string.Equals(
                    featureTags[index]?.Trim(),
                    normalized,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public bool SupportsHub(string hubTag)
    {
        if (string.IsNullOrWhiteSpace(hubTag)
            || compatibleHubTags == null)
        {
            return false;
        }

        string normalized = hubTag.Trim();
        for (int index = 0; index < compatibleHubTags.Length; index++)
        {
            if (string.Equals(
                    compatibleHubTags[index]?.Trim(),
                    normalized,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

public static class ServiceRoomAbilityAccessors
{
    public static BuildingServiceHubAbility GetServiceHubAbility(
        this BuildingSO building)
    {
        BuildingServiceHubAbility ability =
            building?.GetAbility<BuildingServiceHubAbility>();
        return ability != null && ability.IsValid ? ability : null;
    }

    public static BuildingServiceSupportAbility GetServiceSupportAbility(
        this BuildingSO building)
    {
        BuildingServiceSupportAbility ability =
            building?.GetAbility<BuildingServiceSupportAbility>();
        return ability != null && ability.IsValid ? ability : null;
    }

    public static BuildingServiceHubAbility GetServiceHubAbility(
        this BuildableObject building) =>
        building?.BuildingData.GetServiceHubAbility();

    public static BuildingServiceSupportAbility GetServiceSupportAbility(
        this BuildableObject building) =>
        building?.BuildingData.GetServiceSupportAbility();
}
