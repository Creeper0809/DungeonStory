using System;
using System.Collections.Generic;

/// <summary>
/// Primitive-only persisted identity for an exact gameplay-outcome evidence row.
/// This type lives in DungeonStory.Evolution so evolution save models never depend
/// on the default Assembly-CSharp presentation/runtime assembly.
/// </summary>
[Serializable]
public sealed class GameplayOutcomeEvidenceBindingSnapshot
{
    public string publicFactId = string.Empty;
    public string outcomeRunId = string.Empty;
    public long outcomeSequence;
    public string outcomeTypeId = string.Empty;
    public string subjectKindId = string.Empty;
    public string subjectId = string.Empty;
    public int anchorRevision;
    public int status;
    public float subjectSalience;
    public int influenceUseCount;
    public int influenceRevision;
    public string canonicalFactText = string.Empty;
    public List<string> roleIds = new();
    public List<string> metricIds = new();
    public List<string> metricReferenceIds = new();
    public List<string> factIds = new();
    public List<string> semanticTags = new();

    public GameplayOutcomeEvidenceBindingSnapshot Clone() => new()
    {
        publicFactId = publicFactId ?? string.Empty,
        outcomeRunId = outcomeRunId ?? string.Empty,
        outcomeSequence = outcomeSequence,
        outcomeTypeId = outcomeTypeId ?? string.Empty,
        subjectKindId = subjectKindId ?? string.Empty,
        subjectId = subjectId ?? string.Empty,
        anchorRevision = anchorRevision,
        status = status,
        subjectSalience = subjectSalience,
        influenceUseCount = influenceUseCount,
        influenceRevision = influenceRevision,
        canonicalFactText = canonicalFactText ?? string.Empty,
        roleIds = roleIds == null ? new List<string>() : new List<string>(roleIds),
        metricIds = metricIds == null ? new List<string>() : new List<string>(metricIds),
        metricReferenceIds = metricReferenceIds == null
            ? new List<string>() : new List<string>(metricReferenceIds),
        factIds = factIds == null ? new List<string>() : new List<string>(factIds),
        semanticTags = semanticTags == null
            ? new List<string>() : new List<string>(semanticTags)
    };
}
