using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;

public struct InvasionThreatWarningEvent
{
    public InvasionThreatSnapshot snapshot;

    public InvasionThreatWarningEvent(InvasionThreatSnapshot snapshot)
    {
        this.snapshot = snapshot;
    }
}

public struct InvasionCandidateEvent
{
    public InvasionThreatSnapshot snapshot;

    public InvasionCandidateEvent(InvasionThreatSnapshot snapshot)
    {
        this.snapshot = snapshot;
    }
}

public struct InvasionStartedEvent
{
    public InvasionThreatSnapshot snapshot;

    public InvasionStartedEvent(InvasionThreatSnapshot snapshot)
    {
        this.snapshot = snapshot;
    }
}

public struct InvasionResolvedEvent
{
    public bool defended;
    public float residualRisk;

    public InvasionResolvedEvent(bool defended, float residualRisk)
    {
        this.defended = defended;
        this.residualRisk = Mathf.Max(0f, residualRisk);
    }

}

public static class InvasionThreatCalculator
{
    public static float CalculateRisePerSecond(InvasionThreatSettings settings, InvasionThreatFactors factors)
    {
        return CalculateRisePerSecond(settings, factors, 1f);
    }

    public static float CalculateRisePerSecond(InvasionThreatSettings settings, InvasionThreatFactors factors, float runMultiplier)
    {
        if (settings == null)
        {
            return 0f;
        }

        float raw = settings.baseRisePerSecond
            + (factors.dungeonValue * settings.dungeonValueRiseWeight)
            + (factors.reputation * settings.reputationRiseWeight)
            + (factors.time * settings.timeRiseWeight)
            + (factors.risk * settings.riskRiseWeight);

        return Mathf.Max(0f, raw * settings.GetDifficultyMultiplier() * Mathf.Max(0.05f, runMultiplier));
    }

    public static string BuildWarningDetail(InvasionThreatSnapshot snapshot)
    {
        List<string> reasons = new List<string>();
        InvasionThreatFactors factors = snapshot.factors;

        if (factors.dungeonValue >= 3f)
        {
            reasons.Add("던전 가치 상승");
        }

        if (factors.reputation >= 2f)
        {
            reasons.Add("소문 증가");
        }

        if (factors.time >= 1f)
        {
            reasons.Add("마지막 침입 이후 시간 경과");
        }

        if (factors.risk >= 1f)
        {
            reasons.Add("취약한 운영 흔적");
        }

        string reasonText = reasons.Count > 0
            ? string.Join(", ", reasons)
            : "주변 정찰 활동 증가";

        return $"모험가들의 소문이 늘고 있습니다.\n징후: {reasonText}";
    }

    public static string BuildCandidateDetail(InvasionThreatSnapshot snapshot)
    {
        return "수상한 정찰대가 던전 근처에서 목격되었습니다.\n침입이 임박한 것 같습니다.";
    }
}

public readonly struct BossInvasionStartedEvent
{
    public CharacterActor Intruder { get; }
    public InvasionThreatSnapshot Snapshot { get; }

    public BossInvasionStartedEvent(CharacterActor intruder, InvasionThreatSnapshot snapshot)
    {
        Intruder = intruder;
        Snapshot = snapshot;
    }
}
