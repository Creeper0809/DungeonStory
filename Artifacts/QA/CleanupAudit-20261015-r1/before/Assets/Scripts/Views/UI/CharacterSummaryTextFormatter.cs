using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>
/// Pure text projection for the character summary screen. This type owns no UI or
/// runtime state, so presenters and editor scenarios can share the same wording.
/// </summary>
public static class CharacterSummaryTextFormatter
{
    public static string FormatDeprivation(DeprivationKind kind)
    {
        return kind switch
        {
            DeprivationKind.Hunger => CharacterSummaryUiTextQuery.Get("CharacterSummary.Deprivation.Hunger"),
            DeprivationKind.Thirst => CharacterSummaryUiTextQuery.Get("CharacterSummary.Deprivation.Thirst"),
            DeprivationKind.Bladder => CharacterSummaryUiTextQuery.Get("CharacterSummary.Deprivation.Bladder"),
            DeprivationKind.Contamination => CharacterSummaryUiTextQuery.Get("CharacterSummary.Deprivation.Contamination"),
            DeprivationKind.Exhaustion => CharacterSummaryUiTextQuery.Get("CharacterSummary.Deprivation.Exhaustion"),
            DeprivationKind.MentalInstability => CharacterSummaryUiTextQuery.Get("CharacterSummary.Deprivation.MentalInstability"),
            _ => kind.ToString()
        };
    }

    public static string FormatBurdenState(float burden)
    {
        if (burden >= 100f) return CharacterSummaryUiTextQuery.Get("CharacterSummary.Burden.Critical");
        if (burden >= 70f) return CharacterSummaryUiTextQuery.Get("CharacterSummary.Burden.Danger");
        if (burden >= 40f) return CharacterSummaryUiTextQuery.Get("CharacterSummary.Burden.Unhealthy");
        if (burden > 0.1f) return CharacterSummaryUiTextQuery.Get("CharacterSummary.Burden.Accumulating");
        return CharacterSummaryUiTextQuery.Get("CharacterSummary.Burden.Stable");
    }

    public static string FormatBreakdown(CharacterBreakdownKind kind)
    {
        return kind switch
        {
            CharacterBreakdownKind.DesperateRelief => CharacterSummaryUiTextQuery.Get("CharacterSummary.Breakdown.DesperateRelief"),
            CharacterBreakdownKind.DesperateDrink => CharacterSummaryUiTextQuery.Get("CharacterSummary.Breakdown.DesperateDrink"),
            CharacterBreakdownKind.DesperateEat => CharacterSummaryUiTextQuery.Get("CharacterSummary.Breakdown.DesperateEat"),
            CharacterBreakdownKind.Collapse => CharacterSummaryUiTextQuery.Get("CharacterSummary.Breakdown.Collapse"),
            CharacterBreakdownKind.ViolentImpulse => CharacterSummaryUiTextQuery.Get("CharacterSummary.Breakdown.ViolentImpulse"),
            _ => CharacterSummaryUiTextQuery.Get("CharacterSummary.Common.None")
        };
    }

    public static string FormatMoodFactors(CharacterMoodSnapshot snapshot)
    {
        if (snapshot == null || snapshot.Factors == null || snapshot.Factors.Count == 0)
        {
            return CharacterSummaryUiTextQuery.Get(
                "CharacterSummary.Mood.NoFactors");
        }

        StringBuilder builder = new StringBuilder();
        AppendMoodFactorGroup(
            builder,
            snapshot.Factors,
            CharacterMoodFactorKind.Need,
            CharacterSummaryUiTextQuery.Get("CharacterSummary.Mood.NeedHeading"));
        AppendMoodFactorGroup(
            builder,
            snapshot.Factors,
            CharacterMoodFactorKind.Interaction,
            CharacterSummaryUiTextQuery.Get("CharacterSummary.Mood.InteractionHeading"));
        return builder.ToString().TrimEnd();
    }

    public static string FormatLogText(CharacterActor character, int maxLines = 8)
    {
        return FormatLogText(character != null ? character.LogComponent : null, maxLines);
    }

    public static string FormatLogText(CharacterLog characterLog, int maxLines = 8)
    {
        IReadOnlyList<string> entries = characterLog != null ? characterLog.Entries : null;
        if (entries == null || entries.Count == 0)
        {
            return CharacterSummaryUiTextQuery.Get(
                "CharacterSummary.Log.Empty");
        }

        int start = Mathf.Max(0, entries.Count - Mathf.Max(1, maxLines));
        List<string> rows = new List<string>();
        for (int i = entries.Count - 1; i >= start; i--)
        {
            rows.Add(CharacterSummaryUiTextQuery.Get(
                "CharacterSummary.Log.Entry",
                entries[i]));
        }

        return string.Join("\n\n", rows);
    }

    public static string FormatSurvivalHealthState(SurvivalHealthState state)
    {
        return state switch
        {
            SurvivalHealthState.Thirsty => CharacterSummaryUiTextQuery.Get("CharacterSummary.Health.Thirsty"),
            SurvivalHealthState.Hungry => CharacterSummaryUiTextQuery.Get("CharacterSummary.Health.Hungry"),
            SurvivalHealthState.Exposed => CharacterSummaryUiTextQuery.Get("CharacterSummary.Health.Exposed"),
            SurvivalHealthState.Sick => CharacterSummaryUiTextQuery.Get("CharacterSummary.Health.Sick"),
            SurvivalHealthState.Infected => CharacterSummaryUiTextQuery.Get("CharacterSummary.Health.Infected"),
            SurvivalHealthState.Recovering => CharacterSummaryUiTextQuery.Get("CharacterSummary.Health.Recovering"),
            _ => CharacterSummaryUiTextQuery.Get("CharacterSummary.Health.Healthy")
        };
    }

    public static string FormatRemainingTime(float remainingSeconds)
    {
        int seconds = Mathf.Max(1, Mathf.CeilToInt(remainingSeconds));
        if (seconds < 60)
        {
            return CharacterSummaryUiTextQuery.Get(
                "CharacterSummary.Time.Seconds",
                seconds);
        }

        int minutes = seconds / 60;
        int remainder = seconds % 60;
        return remainder > 0
            ? CharacterSummaryUiTextQuery.Get(
                "CharacterSummary.Time.MinutesSeconds",
                minutes,
                remainder)
            : CharacterSummaryUiTextQuery.Get(
                "CharacterSummary.Time.Minutes",
                minutes);
    }

    public static string FormatRole(CharacterRole role)
    {
        return role == CharacterRole.Owner
            ? CharacterSummaryUiTextQuery.Get("CharacterSummary.Role.Owner")
            : CharacterSummaryUiTextQuery.Get("CharacterSummary.Role.Regular");
    }

    public static string FormatLifecycle(CharacterLifecycleState state)
    {
        return state switch
        {
            CharacterLifecycleState.SpawningOutside => CharacterSummaryUiTextQuery.Get("CharacterSummary.Lifecycle.SpawningOutside"),
            CharacterLifecycleState.EnteringDungeon => CharacterSummaryUiTextQuery.Get("CharacterSummary.Lifecycle.EnteringDungeon"),
            CharacterLifecycleState.Active => CharacterSummaryUiTextQuery.Get("CharacterSummary.Lifecycle.Active"),
            CharacterLifecycleState.ExitingDungeon => CharacterSummaryUiTextQuery.Get("CharacterSummary.Lifecycle.ExitingDungeon"),
            CharacterLifecycleState.OnExpedition => CharacterSummaryUiTextQuery.Get("CharacterSummary.Lifecycle.OnExpedition"),
            CharacterLifecycleState.PreparingExpedition => CharacterSummaryUiTextQuery.Get("CharacterSummary.Lifecycle.PreparingExpedition"),
            CharacterLifecycleState.DepartingExpedition => CharacterSummaryUiTextQuery.Get("CharacterSummary.Lifecycle.DepartingExpedition"),
            CharacterLifecycleState.ReturningExpedition => CharacterSummaryUiTextQuery.Get("CharacterSummary.Lifecycle.ReturningExpedition"),
            CharacterLifecycleState.Downed => CharacterSummaryUiTextQuery.Get("CharacterSummary.Lifecycle.Downed"),
            CharacterLifecycleState.Despawned => CharacterSummaryUiTextQuery.Get("CharacterSummary.Lifecycle.Despawned"),
            _ => CharacterSummaryUiTextQuery.Get("CharacterSummary.Lifecycle.Waiting")
        };
    }

    public static string FormatCaptivityStatus(CaptivityStatus status)
    {
        return status switch
        {
            CaptivityStatus.AwaitingCapture => CharacterSummaryUiTextQuery.Get("CharacterSummary.Captivity.AwaitingCapture"),
            CaptivityStatus.Stabilizing => CharacterSummaryUiTextQuery.Get("CharacterSummary.Captivity.Stabilizing"),
            CaptivityStatus.AwaitingEscort => CharacterSummaryUiTextQuery.Get("CharacterSummary.Captivity.AwaitingEscort"),
            CaptivityStatus.Escorting => CharacterSummaryUiTextQuery.Get("CharacterSummary.Captivity.Escorting"),
            CaptivityStatus.Confined => CharacterSummaryUiTextQuery.Get("CharacterSummary.Captivity.Confined"),
            CaptivityStatus.Labor => CharacterSummaryUiTextQuery.Get("CharacterSummary.Captivity.Labor"),
            CaptivityStatus.Interaction => CharacterSummaryUiTextQuery.Get("CharacterSummary.Captivity.Interaction"),
            CaptivityStatus.Performer => CharacterSummaryUiTextQuery.Get("CharacterSummary.Captivity.Performer"),
            CaptivityStatus.EscapeAttempt => CharacterSummaryUiTextQuery.Get("CharacterSummary.Captivity.EscapeAttempt"),
            CaptivityStatus.Ransom => CharacterSummaryUiTextQuery.Get("CharacterSummary.Captivity.Ransom"),
            _ => status.ToString()
        };
    }

    public static string FormatWeight(float weight)
    {
        return CharacterSummaryUiTextQuery.Get(
            "CharacterSummary.Weight.Kilograms",
            Mathf.Max(0f, weight));
    }

    public static string FormatSigned(int value)
    {
        return value > 0 ? "+" + value : value.ToString();
    }

    public static string Fallback(string text, string fallback)
    {
        return string.IsNullOrWhiteSpace(text) ? fallback : text;
    }

    private static void AppendMoodFactorGroup(
        StringBuilder builder,
        IReadOnlyList<CharacterMoodFactorSnapshot> factors,
        CharacterMoodFactorKind kind,
        string heading)
    {
        bool hasGroup = factors.Any(factor => factor != null && factor.Kind == kind);
        if (!hasGroup)
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.AppendLine();
        }

        builder.AppendLine(heading);
        foreach (CharacterMoodFactorSnapshot factor in factors)
        {
            if (factor == null || factor.Kind != kind)
            {
                continue;
            }

            string signedValue = factor.Value >= 0f
                ? $"+{Mathf.RoundToInt(factor.Value)}"
                : $"{Mathf.RoundToInt(factor.Value)}";
            builder.AppendLine(kind == CharacterMoodFactorKind.Interaction
                ? CharacterSummaryUiTextQuery.Get(
                    "CharacterSummary.Mood.InteractionRow",
                    factor.Label,
                    signedValue,
                    FormatRemainingTime(factor.RemainingSeconds))
                : CharacterSummaryUiTextQuery.Get(
                    "CharacterSummary.Mood.FactorRow",
                    factor.Label,
                    signedValue));
        }
    }
}
