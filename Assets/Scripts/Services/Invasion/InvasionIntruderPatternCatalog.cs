using System;
using System.Collections.Generic;

public static class InvasionIntruderPatternIds
{
    public const string Hunter = "invasion:pattern:hunter";
    public const string Ambusher = "invasion:pattern:ambusher";
    public const string Breaker = "invasion:pattern:breaker";
    public const string Plunderer = "invasion:pattern:plunderer";
    public const string Straggler = "invasion:pattern:straggler";
    public const string Executioner = "invasion:pattern:executioner";
}

public enum InvasionIntruderTargetPreference
{
    Owner,
    DefenseFacility,
    ValuableFacility
}

public sealed class InvasionIntruderPatternDefinition
{
    public InvasionIntruderPatternDefinition(
        string id,
        string title,
        string detail,
        InvasionIntruderTargetPreference targetPreference,
        float directOwnerFocus,
        float facilityDiversionFocus,
        int maxFacilityDamageCount)
    {
        this.id = id?.Trim() ?? string.Empty;
        this.title = title?.Trim() ?? string.Empty;
        this.detail = detail?.Trim() ?? string.Empty;
        this.targetPreference = targetPreference;
        this.directOwnerFocus = UnityEngine.Mathf.Clamp01(directOwnerFocus);
        this.facilityDiversionFocus = UnityEngine.Mathf.Clamp01(facilityDiversionFocus);
        this.maxFacilityDamageCount = UnityEngine.Mathf.Max(0, maxFacilityDamageCount);
    }

    public string id { get; }
    public string title { get; }
    public string detail { get; }
    public InvasionIntruderTargetPreference targetPreference { get; }
    public float directOwnerFocus { get; }
    public float facilityDiversionFocus { get; }
    public int maxFacilityDamageCount { get; }
}

public static class InvasionIntruderPatternCatalog
{
    private static Dictionary<string, InvasionIntruderPatternDefinition> definitions = BuildDefinitions();

    public static IReadOnlyCollection<InvasionIntruderPatternDefinition> All => definitions.Values;
    public static InvasionIntruderPatternDefinition Default => Get(InvasionIntruderPatternIds.Hunter);

    public static InvasionIntruderPatternDefinition Get(string id)
    {
        return !string.IsNullOrWhiteSpace(id)
            && definitions.TryGetValue(id, out InvasionIntruderPatternDefinition definition)
                ? definition
                : null;
    }

    public static InvasionIntruderPatternDefinition Resolve(string id)
    {
        return Get(id) ?? Default;
    }

    public static void Register(InvasionIntruderPatternDefinition definition, bool replace = false)
    {
        Validate(definition);
        if (!replace && definitions.ContainsKey(definition.id))
        {
            throw new InvalidOperationException($"Intruder pattern '{definition.id}' is already registered.");
        }

        definitions[definition.id] = definition;
    }

    public static void ResetToBuiltIns()
    {
        definitions = BuildDefinitions();
    }

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeCatalog()
    {
        ResetToBuiltIns();
    }

    private static Dictionary<string, InvasionIntruderPatternDefinition> BuildDefinitions()
    {
        InvasionIntruderPatternDefinition[] builtIns =
        {
            Definition(
                InvasionIntruderPatternIds.Hunter,
                "추적자",
                "탐색을 짧게 끝내고 사장을 추적합니다.",
                InvasionIntruderTargetPreference.Owner,
                0.65f,
                0f,
                1),
            Definition(
                InvasionIntruderPatternIds.Ambusher,
                "급습자",
                "거의 즉시 사장에게 향합니다. 긴 함정 동선이 유효합니다.",
                InvasionIntruderTargetPreference.Owner,
                0.35f,
                0f,
                0),
            Definition(
                InvasionIntruderPatternIds.Breaker,
                "파괴자",
                "초반에는 길목의 방어 시설부터 찾아 파괴합니다.",
                InvasionIntruderTargetPreference.DefenseFacility,
                0.9f,
                0.7f,
                1),
            Definition(
                InvasionIntruderPatternIds.Plunderer,
                "약탈자",
                "초반에는 값비싼 운영 시설을 찾아 훼손합니다.",
                InvasionIntruderTargetPreference.ValuableFacility,
                0.95f,
                0.75f,
                1),
            Definition(
                InvasionIntruderPatternIds.Straggler,
                "낙오자",
                "오래 헤매지만 함정 동선에 반복해서 노출됩니다.",
                InvasionIntruderTargetPreference.Owner,
                0.98f,
                0f,
                1),
            Definition(
                InvasionIntruderPatternIds.Executioner,
                "집행자",
                "다른 목표를 무시하고 곧장 사장을 노립니다.",
                InvasionIntruderTargetPreference.Owner,
                0f,
                0f,
                0)
        };

        Dictionary<string, InvasionIntruderPatternDefinition> result =
            new Dictionary<string, InvasionIntruderPatternDefinition>(StringComparer.Ordinal);
        foreach (InvasionIntruderPatternDefinition definition in builtIns)
        {
            Validate(definition);
            result.Add(definition.id, definition);
        }

        return result;
    }

    private static InvasionIntruderPatternDefinition Definition(
        string id,
        string title,
        string detail,
        InvasionIntruderTargetPreference targetPreference,
        float directOwnerFocus,
        float facilityDiversionFocus,
        int maxFacilityDamageCount)
    {
        return new InvasionIntruderPatternDefinition(
            id,
            title,
            detail,
            targetPreference,
            directOwnerFocus,
            facilityDiversionFocus,
            maxFacilityDamageCount);
    }

    private static void Validate(InvasionIntruderPatternDefinition definition)
    {
        if (definition == null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        if (string.IsNullOrWhiteSpace(definition.id)
            || string.IsNullOrWhiteSpace(definition.title)
            || string.IsNullOrWhiteSpace(definition.detail))
        {
            throw new InvalidOperationException("Intruder patterns require an id, title, and detail.");
        }

        if (definition.targetPreference != InvasionIntruderTargetPreference.Owner
            && definition.facilityDiversionFocus <= 0f)
        {
            throw new InvalidOperationException(
                $"Intruder pattern '{definition.id}' requires a facility diversion focus threshold.");
        }
    }
}
