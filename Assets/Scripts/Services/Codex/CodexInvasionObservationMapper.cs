using System.Collections.Generic;

public static class CodexInvasionObservationMapper
{
    public static IEnumerable<string> FromEffectTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            yield break;
        }

        string normalized = tag.Trim();
        if (normalized.Contains("감속") || normalized.Contains("속박"))
        {
            yield return "약점: 감속";
        }

        if (normalized.Contains("경비"))
        {
            yield return "약점: 근접 교전";
        }

        if (normalized.Contains("부식"))
        {
            yield return "약점: 방어력 감소";
        }

        if (normalized.Contains("축전"))
        {
            yield return "약점: 축전 연계";
        }

        if (normalized.Contains("연소"))
        {
            yield return "약점: 지속 피해";
        }
    }

    public static string NormalizeObservation(string observation)
    {
        if (string.IsNullOrWhiteSpace(observation))
        {
            return string.Empty;
        }

        string normalized = observation.Trim();
        if (normalized.Contains("감속"))
        {
            return "약점: 감속";
        }

        if (normalized.Contains("경비"))
        {
            return "약점: 근접 교전";
        }

        if (normalized.Contains("부식"))
        {
            return "약점: 방어력 감소";
        }

        if (normalized.Contains("축전"))
        {
            return "약점: 축전 연계";
        }

        if (normalized.Contains("연소"))
        {
            return "약점: 지속 피해";
        }

        if (normalized.Contains("직접 피해"))
        {
            return "약점: 직접 피해";
        }

        return normalized.Replace("관찰:", "관찰:");
    }
}
