using System;
using System.Collections.Generic;

/// <summary>Controlled policy fixtures; never natural gameplay or training examples.</summary>
public sealed class GameplayExperiencePolicyCase
{
    public GameplayExperiencePolicyCase(string name, GameplayOutcomeEvidenceBindingSnapshot fact,
        GameplayExperienceCategory expected)
    { Name = name; Fact = fact; Expected = expected; }
    public string Name { get; }
    public GameplayOutcomeEvidenceBindingSnapshot Fact { get; }
    public GameplayExperienceCategory Expected { get; }
}

public static class GameplayExperienceCaseFactory
{
    public static GameplayOutcomeEvidenceBindingSnapshot Create(string type, string role = "actor",
        GameplayOutcomeStatus status = GameplayOutcomeStatus.Succeeded, string kind = "character", string id = "subject")
    {
        var value = new GameplayOutcomeEvidenceBindingSnapshot
        {
            schemaVersion = 4, sourceKind = "exact", publicFactId = "policy-fixture:" + type,
            outcomeRunId = "policy-fixture", outcomeSequence = 1, outcomeTypeId = type,
            subjectKindId = kind, subjectId = id, status = (int)status, perspectiveKind = 1,
            firstDay = 1, lastDay = 1, subjectSalience = .99f
        };
        Participant(value, role, kind, id);
        return value;
    }

    public static void Participant(GameplayOutcomeEvidenceBindingSnapshot value, string role,
        string kind = "character", string id = "subject") => value.participants.Add(new()
    {
        entityKindId = kind, entityId = id, roleId = role, participationKind = 1,
        displayText = id, locale = "ko-KR"
    });

    public static void Metric(GameplayOutcomeEvidenceBindingSnapshot value, string id, double number,
        string unit, string referenceKind = null, string referenceId = null) => value.metrics.Add(new()
    {
        metricId = id, value = number, minimum = number, maximum = number, sampleCount = 1,
        unitId = unit, referenceKindId = referenceKind ?? value.subjectKindId,
        referenceId = referenceId ?? value.subjectId
    });

    public static void Fact(GameplayOutcomeEvidenceBindingSnapshot value, string id, string text) =>
        value.facts.Add(new() { factId = id, value = text });

    public static GameplayExperiencePolicyCase Case(string name, GameplayOutcomeEvidenceBindingSnapshot value,
        GameplayExperienceCategory expected) => new(name, value, expected);
}
