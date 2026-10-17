using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Operation;
using UnityEngine;

public enum ContentResolutionDisposition
{
    Terminal = 0,
    AcceptedPending = 1
}

public sealed class ContentResolutionRequest
{
    public string ActionId { get; set; } = string.Empty;
    public ContentResolutionRequestKind Kind { get; set; }
    public string InstanceId { get; set; } = string.Empty;
    public string FactionId { get; set; } = string.Empty;
    public string ChoiceId { get; set; } = string.Empty;
    public string ContractId { get; set; } = string.Empty;
    public bool ContractSucceeded { get; set; }
    public int AbsoluteDay { get; set; }
    public V20DailyEventContext DailyContext { get; set; }
    public RunMilestoneEvaluationSnapshot Requirements { get; set; }
    public string DefinitionId { get; set; } = string.Empty;
    public IReadOnlyList<string> ParticipantCharacterIds { get; set; } =
        Array.Empty<string>();
}

public sealed class ContentResolutionResult
{
    public string ActionId { get; internal set; } = string.Empty;
    public IReadOnlyList<V20ResolvedEventResult> Resolutions { get; internal set; } =
        Array.Empty<V20ResolvedEventResult>();
    public ContentResolutionDisposition Disposition { get; internal set; }
}

public interface IContentResolutionService
{
    bool TryExecute(
        ContentResolutionRequest request,
        out ContentResolutionResult result,
        out DomainFailure failure);
}

public interface IRetirementScheduleContentQuery
{
    bool CanScheduleRetirement(CharacterId characterId);
}

