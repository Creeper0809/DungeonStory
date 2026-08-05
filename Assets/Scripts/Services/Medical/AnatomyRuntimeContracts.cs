using System.Collections.Generic;

/// <summary>
/// Application-facing anatomy ports. Actor components remain outside the
/// immutable DungeonStory.Medical definition assembly.
/// </summary>
public interface IAnatomyEffectRuntime
{
    AnatomyActionAxisSnapshot GetActionAxes(CharacterActor actor);
    AnatomyActionAxisSnapshot GetActionAxes(string characterId);
    AnatomyActivityFactorSnapshot GetActivityFactor(
        CharacterActor actor,
        AnatomyActivityId activity);
}

public interface IAnatomyHealthRuntime
{
    AnatomyHealthSnapshot GetAnatomySnapshot(CharacterActor actor);
    AnatomyHealthSnapshot GetAnatomySnapshot(string characterId);
    bool TryDamageNode(
        CharacterActor actor,
        string nodeId,
        float damage,
        float bleeding,
        string reason);
    bool TryHealNode(
        CharacterActor actor,
        string nodeId,
        float health,
        float infectionReduction);
    PartRecoveryPolicy GetRecoveryPolicy(
        CharacterActor actor,
        string nodeId);
    bool CanRecoverNaturally(
        CharacterActor actor,
        string nodeId);
    bool TryMaintainNode(
        CharacterActor actor,
        string nodeId,
        float durability,
        float contaminationReduction,
        out DomainFailure failure);
    bool TryRemoveNode(
        CharacterActor actor,
        string nodeId,
        out AnatomyNodeHealthState removedNode,
        out DomainFailure failure);
    bool TryInstallPart(
        CharacterActor actor,
        string nodeId,
        string partInstanceId,
        SurgicalPartKind partKind,
        float efficiency,
        out DomainFailure failure);
    bool TryReplaceNodePart(
        CharacterActor actor,
        string nodeId,
        string partInstanceId,
        SurgicalPartKind partKind,
        float efficiency,
        out AnatomyNodeHealthState replacedNode,
        out DomainFailure failure);
    bool TryAddNodeBurden(
        CharacterActor actor,
        string nodeId,
        float rejection,
        float mutation,
        float infection,
        out DomainFailure failure);
    bool TryReduceNodeBurden(
        CharacterActor actor,
        string nodeId,
        float rejection,
        float mutation,
        float infection,
        out DomainFailure failure);
}

public interface IWildlifeAnatomyHealthRuntime
{
    AnatomyHealthSnapshot GetAnatomySnapshot(WildlifeActor actor);
    bool TryHealNode(
        WildlifeActor actor,
        string nodeId,
        float health,
        float infectionReduction);
    bool TryRemoveNode(
        WildlifeActor actor,
        string nodeId,
        out AnatomyNodeHealthState removedNode,
        out DomainFailure failure);
    bool TryInstallPart(
        WildlifeActor actor,
        string nodeId,
        string partInstanceId,
        SurgicalPartKind partKind,
        float efficiency,
        out DomainFailure failure);
    bool TryAddNodeBurden(
        WildlifeActor actor,
        string nodeId,
        float rejection,
        float mutation,
        float infection,
        out DomainFailure failure);
    bool TryReduceNodeBurden(
        WildlifeActor actor,
        string nodeId,
        float rejection,
        float mutation,
        float infection,
        out DomainFailure failure);
    IReadOnlyList<WildlifeAnatomyState> Capture();
}
