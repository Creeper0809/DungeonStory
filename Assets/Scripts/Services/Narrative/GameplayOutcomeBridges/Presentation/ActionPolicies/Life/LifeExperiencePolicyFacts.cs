using System;
using System.Collections.Generic;

internal static class LifeExperiencePolicyFacts
{
    internal static void RequireRole(GameplayExperienceFacts facts, params string[] roles)
    {
        if (!facts.HasRole(roles))
            GameplayExperienceFacts.Fail("subject role:" + string.Join(",", roles));
    }

    internal static bool HasParticipantRole(GameplayExperienceFacts facts, string role)
    {
        foreach (GameplayOutcomeEvidenceParticipantBindingSnapshot participant in
                 facts.Binding.participants
                 ?? new List<GameplayOutcomeEvidenceParticipantBindingSnapshot>())
        {
            if (participant != null && participant.roleId == role)
                return true;
        }
        return false;
    }

    internal static bool Boolean(double value, string field)
    {
        if (value == 0d) return false;
        if (value == 1d) return true;
        GameplayExperienceFacts.Fail(field);
        return false;
    }

    internal static bool FactBoolean(string value, string field)
    {
        if (value == "0") return false;
        if (value == "1") return true;
        GameplayExperienceFacts.Fail(field);
        return false;
    }

    internal static T EnumNumber<T>(double value, string field) where T : struct, Enum
    {
        if (value != Math.Truncate(value)
            || value < int.MinValue || value > int.MaxValue
            || !Enum.IsDefined(typeof(T), (int)value))
        {
            GameplayExperienceFacts.Fail(field);
        }
        return (T)Enum.ToObject(typeof(T), (int)value);
    }

    internal static int WholeNonnegative(double value, string field)
    {
        if (value < 0d || value != Math.Truncate(value) || value > int.MaxValue)
            GameplayExperienceFacts.Fail(field);
        return (int)value;
    }

    internal static int WholePositive(double value, string field)
    {
        int result = WholeNonnegative(value, field);
        if (result == 0)
            GameplayExperienceFacts.Fail(field);
        return result;
    }

    internal static void Nonnegative(double value, string field)
    {
        if (value < 0d)
            GameplayExperienceFacts.Fail(field);
    }

    internal static void ConsecutiveRevisions(double before, double after, string field)
    {
        if (before < 0d || before != Math.Truncate(before)
            || after != before + 1d)
        {
            GameplayExperienceFacts.Fail(field);
        }
    }
}
