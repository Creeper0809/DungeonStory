using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DungeonStory.Narrative.Korean;

public static class CharacterSkillAcquiredOutcomeIds
{
    public const string ProducerId = "character.skill-acquisition";
    public static readonly GameplayOutcomeTypeId Acquired = new("character.skill-acquired");
    public static readonly GameplayEntityKindId CharacterKind = new("character");
    public static readonly GameplayRoleId OwnerRole = new("skill-owner");
    public static readonly GameplayMetricId BeforeRevision = new("skill.acquisition-revision-before");
    public static readonly GameplayMetricId AfterRevision = new("skill.acquisition-revision-after");
    public static readonly GameplayMetricUnitId RevisionUnit = new("revision");
    public static readonly GameplayOutcomeTagId AcquisitionTag = new("skill-acquisition");

    public static readonly GameplayOutcomeFactId SkillId = new("skill.id");
    public static readonly GameplayOutcomeFactId SkillKind = new("skill.kind");
    public static readonly GameplayOutcomeFactId UnlockLevel = new("skill.unlock-level");
    public static readonly GameplayOutcomeFactId DisplayName =
        new("skill.display-name");
    public static readonly GameplayOutcomeFactId CombinationId = new("skill.combination-id");
    public static readonly GameplayOutcomeFactId PresentationId = new("skill.presentation-id");
    public static readonly GameplayOutcomeFactId Mechanics =
        new("skill.mechanics-canonical");
    public static readonly GameplayOutcomeFactId Evidence = new("skill.evidence-canonical");

    public const string SkillAnchor = "skill-id-sha256";
    public const string PresentationAnchor = "skill-presentation-sha256";
    public const string MechanicsAnchor = "skill-mechanics-sha256";
    public const string EvidenceAnchor = "skill-evidence-sha256";

    public static readonly GameplayOutcomeFactId[] OrderedFacts =
    {
        SkillId, SkillKind, UnlockLevel, DisplayName, CombinationId,
        PresentationId, Mechanics, Evidence
    };
}

/// <summary>
/// Freezes the actual selected skill, never a draft offer or narrative paraphrase.
/// The caller owns the progression commit and supplies its before/after revision.
/// </summary>
public readonly struct CharacterSkillAcquiredOutcomeReceipt
{
    public CharacterSkillAcquiredOutcomeReceipt(
        string operationId,
        string characterPersistentId,
        string characterDisplayName,
        long beforeAcquisitionRevision,
        long afterAcquisitionRevision,
        int unlockLevel,
        CharacterSkillInstance selectedSkill,
        int absoluteDay)
    {
        OperationId = new GameplayOperationId(operationId);
        CharacterPersistentId = GameplayOutcomeStableIdSyntax.Require(
            characterPersistentId, nameof(characterPersistentId));
        CharacterDisplayName = EvolutionOutcomeNames.Snapshot(
            CharacterPersistentId, characterDisplayName);
        if (!GameplayOutcomeLedger.IsValidBoundedUtf16(
                CharacterDisplayName.DisplayText,
                GameplayOutcomeBufferLimits.MaximumDisplayTextUtf16Length,
                allowEmpty: false))
            throw new ArgumentException("A bounded Korean character name is required.", nameof(characterDisplayName));
        if (beforeAcquisitionRevision < 0
            || beforeAcquisitionRevision == long.MaxValue
            || afterAcquisitionRevision != beforeAcquisitionRevision + 1)
            throw new ArgumentOutOfRangeException(nameof(afterAcquisitionRevision));
        if (unlockLevel < 1)
            throw new ArgumentOutOfRangeException(nameof(unlockLevel));
        if (absoluteDay < 0)
            throw new ArgumentOutOfRangeException(nameof(absoluteDay));
        if (selectedSkill == null)
            throw new ArgumentNullException(nameof(selectedSkill));
        if (selectedSkill.kind != CharacterSkillKind.Active
            && selectedSkill.kind != CharacterSkillKind.Passive
            && selectedSkill.kind != CharacterSkillKind.Ultimate)
            throw new ArgumentException("Only a committed active, passive, or ultimate skill can be acquired.", nameof(selectedSkill));
        if (selectedSkill.formulaVersion <= 0)
            throw new ArgumentException("A formula skill is required.", nameof(selectedSkill));

        BeforeAcquisitionRevision = beforeAcquisitionRevision;
        AfterAcquisitionRevision = afterAcquisitionRevision;
        UnlockLevel = unlockLevel;
        AbsoluteDay = absoluteDay;
        SkillId = RequireStable(selectedSkill.id, nameof(selectedSkill.id));
        SkillDisplayName = RequireFact(selectedSkill.displayName, nameof(selectedSkill.displayName));
        SkillKind = selectedSkill.kind.ToString();
        RuleId = RequireStable(selectedSkill.ruleId, nameof(selectedSkill.ruleId));
        CombinationId = string.IsNullOrEmpty(selectedSkill.combinationId)
            ? "none"
            : RequireStable(selectedSkill.combinationId, nameof(selectedSkill.combinationId));
        PresentationId = RequireStable(selectedSkill.presentationId, nameof(selectedSkill.presentationId));
        FormulaVersion = selectedSkill.formulaVersion.ToString(CultureInfo.InvariantCulture);
        FormulaCatalog = RequireStable(selectedSkill.formulaCatalogSha256, nameof(selectedSkill.formulaCatalogSha256));
        ModuleCanonical = RequireFact(
            CanonicalModules(selectedSkill.modules), nameof(selectedSkill.modules));
        CapabilityCanonical = RequireFact(
            CanonicalCapabilities(selectedSkill.formulaCapabilities),
            nameof(selectedSkill.formulaCapabilities));
        EvidenceCanonical = RequireFact(
            CanonicalEvidence(selectedSkill.evidenceIds, selectedSkill.evidenceBindings),
            nameof(selectedSkill.evidenceIds));
        Mechanics = RequireFact(
            MechanicsCanonical(
                RuleId,
                CombinationId,
                FormulaVersion,
                FormulaCatalog,
                ModuleCanonical,
                CapabilityCanonical),
            "skillMechanics");
    }

    public GameplayOperationId OperationId { get; }
    public string CharacterPersistentId { get; }
    public KoreanNameSnapshot CharacterDisplayName { get; }
    public long BeforeAcquisitionRevision { get; }
    public long AfterAcquisitionRevision { get; }
    public int UnlockLevel { get; }
    public int AbsoluteDay { get; }
    public string SkillId { get; }
    public string SkillDisplayName { get; }
    public string SkillKind { get; }
    public string RuleId { get; }
    public string CombinationId { get; }
    public string PresentationId { get; }
    public string FormulaVersion { get; }
    public string FormulaCatalog { get; }
    public string ModuleCanonical { get; }
    public string CapabilityCanonical { get; }
    public string EvidenceCanonical { get; }
    public string Mechanics { get; }
    public GameplayResultKey ResultKey => new(
        CharacterSkillAcquiredOutcomeIds.ProducerId,
        OperationId,
        AfterAcquisitionRevision,
        0);

    internal static string MechanicsCanonical(
        string ruleId,
        string combinationId,
        string formulaVersion,
        string formulaCatalog,
        string modules,
        string capabilities)
    {
        StringBuilder value = new("skill-mechanics-v1;");
        AppendToken(value, ruleId);
        AppendToken(value, combinationId);
        AppendToken(value, formulaVersion);
        AppendToken(value, formulaCatalog);
        AppendToken(value, modules);
        AppendToken(value, capabilities);
        return value.ToString();
    }

    private static string CanonicalModules(IReadOnlyList<CharacterSkillModuleSelection> modules)
    {
        if (modules == null || modules.Count == 0 || modules.Any(value => value == null))
            throw new ArgumentException("Selected skill modules are missing.", nameof(modules));
        var values = modules.Select(value => (
                Id: RequireStable(value.moduleId, nameof(value.moduleId)),
                Variant: string.IsNullOrEmpty(value.variantId)
                    ? string.Empty
                    : RequireStable(value.variantId, nameof(value.variantId))))
            .OrderBy(value => value.Id, StringComparer.Ordinal)
            .ThenBy(value => value.Variant, StringComparer.Ordinal)
            .ToArray();
        StringBuilder result = new("modules-v1;");
        result.Append(values.Length.ToString(CultureInfo.InvariantCulture)).Append(';');
        foreach (var value in values)
        {
            AppendToken(result, value.Id);
            AppendToken(result, value.Variant);
        }
        return result.ToString();
    }

    private static string CanonicalCapabilities(
        IReadOnlyList<CharacterSkillFormulaCapabilityEnvelope> capabilities)
    {
        if (capabilities == null || capabilities.Count == 0
            || capabilities.Any(value => value == null))
            throw new ArgumentException("Selected skill capabilities are missing.", nameof(capabilities));
        string[] values = capabilities.Select(value =>
            {
                StringBuilder item = new();
                AppendToken(item, RequireStable(value.capabilityId, nameof(value.capabilityId)));
                AppendToken(item, RequireStable(value.formatterId, nameof(value.formatterId)));
                AppendToken(item, RequireStable(value.applicatorId, nameof(value.applicatorId)));
                if (value.parameters == null || value.parameters.Any(parameter => parameter == null))
                    throw new ArgumentException("Selected skill capability parameters are invalid.", nameof(capabilities));
                var parameters = value.parameters.Select(parameter => (
                        Id: RequireStable(parameter.parameterId, nameof(parameter.parameterId)),
                        parameter.units))
                    .OrderBy(parameter => parameter.Id, StringComparer.Ordinal)
                    .ThenBy(parameter => parameter.units)
                    .ToArray();
                item.Append(parameters.Length.ToString(CultureInfo.InvariantCulture)).Append(';');
                foreach (var parameter in parameters)
                {
                    AppendToken(item, parameter.Id);
                    AppendToken(item, parameter.units.ToString(CultureInfo.InvariantCulture));
                }
                return item.ToString();
            })
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        StringBuilder result = new("capabilities-v1;");
        result.Append(values.Length.ToString(CultureInfo.InvariantCulture)).Append(';');
        foreach (string value in values)
            AppendToken(result, value);
        return result.ToString();
    }

    private static string CanonicalEvidence(
        IReadOnlyList<string> evidenceIds,
        IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot> bindings)
    {
        if (evidenceIds == null || evidenceIds.Count == 0)
            throw new ArgumentException("Formula evidence is missing.", nameof(evidenceIds));
        string[] ids = evidenceIds.Select(value => RequireStable(value, nameof(evidenceIds)))
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
            throw new ArgumentException("Formula evidence contains duplicate IDs.", nameof(evidenceIds));
        if (bindings == null || bindings.Any(value => value == null))
            throw new ArgumentException("Exact evidence bindings are invalid.", nameof(bindings));
        var exact = bindings.Select(value => (
                Id: RequireStable(value.publicFactId, nameof(value.publicFactId)),
                Hash: GameplayOutcomeEvidenceBindingAuthority.ComputeCanonicalHash(value)))
            .OrderBy(value => value.Id, StringComparer.Ordinal).ToArray();
        if (exact.Select(value => value.Id).Distinct(StringComparer.Ordinal).Count() != exact.Length
            || exact.Any(value => !ids.Contains(value.Id, StringComparer.Ordinal)))
            throw new ArgumentException("Exact evidence bindings do not match the selected IDs.", nameof(bindings));
        StringBuilder result = new("evidence-v1;");
        result.Append(ids.Length.ToString(CultureInfo.InvariantCulture)).Append(';');
        foreach (string id in ids)
            AppendToken(result, id);
        result.Append(exact.Length.ToString(CultureInfo.InvariantCulture)).Append(';');
        foreach (var value in exact)
        {
            AppendToken(result, value.Id);
            AppendToken(result, value.Hash);
        }
        return result.ToString();
    }

    private static string RequireStable(string value, string field) =>
        GameplayOutcomeStableIdSyntax.Require(value, field);

    private static string RequireFact(string value, string field)
    {
        if (!GameplayOutcomeLedger.IsValidBoundedUtf16(
                value, GameplayOutcomeBufferLimits.MaximumFactValueUtf16Length,
                allowEmpty: false))
            throw new ArgumentException("A non-empty bounded immutable fact is required.", field);
        return value;
    }

    private static void AppendToken(StringBuilder target, string value)
    {
        target.Append(value.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':').Append(value).Append(';');
    }
}

public sealed class CharacterSkillAcquiredOutcomeAdapter :
    GameplayOutcomeAdapter<CharacterSkillAcquiredOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId => CharacterSkillAcquiredOutcomeIds.Acquired;

    public override OutcomePrepareResult TryGetRequirements(
        in CharacterSkillAcquiredOutcomeReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements)
    {
        requirements = new OutcomeWriteRequirements(
            receipt.ResultKey, OutcomeTypeId, receipt.AbsoluteDay,
            GameplayOutcomeStatus.Succeeded, currentWorldEpoch,
            receipt.AfterAcquisitionRevision,
            1, 2, 1, 1, 4, factCount: 8);
        bool valid = receipt.OperationId.IsValid
            && GameplayOutcomeStableIdSyntax.IsValid(receipt.CharacterPersistentId)
            && !string.IsNullOrWhiteSpace(receipt.CharacterDisplayName.DisplayText)
            && !string.IsNullOrWhiteSpace(receipt.CharacterDisplayName.DisplaySnapshotRevision)
            && receipt.CharacterDisplayName.Locale == "ko-KR"
            && receipt.BeforeAcquisitionRevision >= 0
            && receipt.AfterAcquisitionRevision == receipt.BeforeAcquisitionRevision + 1
            && receipt.UnlockLevel > 0
            && receipt.AbsoluteDay >= 0
            && GameplayOutcomeStableIdSyntax.IsValid(receipt.SkillId)
            && !string.IsNullOrWhiteSpace(receipt.SkillDisplayName)
            && (receipt.SkillKind == nameof(CharacterSkillKind.Active)
                || receipt.SkillKind == nameof(CharacterSkillKind.Passive)
                || receipt.SkillKind == nameof(CharacterSkillKind.Ultimate))
            && GameplayOutcomeStableIdSyntax.IsValid(receipt.RuleId)
            && GameplayOutcomeStableIdSyntax.IsValid(receipt.PresentationId)
            && GameplayOutcomeStableIdSyntax.IsValid(receipt.FormulaCatalog)
            && !string.IsNullOrWhiteSpace(receipt.ModuleCanonical)
            && !string.IsNullOrWhiteSpace(receipt.CapabilityCanonical)
            && !string.IsNullOrWhiteSpace(receipt.Mechanics)
            && !string.IsNullOrWhiteSpace(receipt.EvidenceCanonical);
        return valid ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(OutcomePrepareCode.InvalidReceipt,
                "character-skill-acquired-receipt-invalid");
    }

    public override OutcomePrepareResult TryWrite(
        in CharacterSkillAcquiredOutcomeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        GameplayEntityId character = new(
            CharacterSkillAcquiredOutcomeIds.CharacterKind,
            receipt.CharacterPersistentId);
        string mechanics = receipt.Mechanics;
        bool written = builder.AddParticipant(new GameplayOutcomeParticipant(
                character, CharacterSkillAcquiredOutcomeIds.OwnerRole,
                GameplayParticipationKind.Direct, true,
                receipt.CharacterDisplayName))
            && builder.AddSubject(new GameplayOutcomeSubjectLink(
                character, 0.9f, NarrativeMemoryTier.Core, true, false, 0))
            && builder.AddMetric(new GameplayOutcomeMetric(
                CharacterSkillAcquiredOutcomeIds.BeforeRevision,
                receipt.BeforeAcquisitionRevision,
                CharacterSkillAcquiredOutcomeIds.RevisionUnit, character))
            && builder.AddMetric(new GameplayOutcomeMetric(
                CharacterSkillAcquiredOutcomeIds.AfterRevision,
                receipt.AfterAcquisitionRevision,
                CharacterSkillAcquiredOutcomeIds.RevisionUnit, character))
            && builder.AddTag(CharacterSkillAcquiredOutcomeIds.AcquisitionTag)
            && builder.AddFact(new GameplayOutcomeFact(CharacterSkillAcquiredOutcomeIds.SkillId, receipt.SkillId))
            && builder.AddFact(new GameplayOutcomeFact(CharacterSkillAcquiredOutcomeIds.SkillKind, receipt.SkillKind))
            && builder.AddFact(new GameplayOutcomeFact(CharacterSkillAcquiredOutcomeIds.UnlockLevel, receipt.UnlockLevel.ToString(CultureInfo.InvariantCulture)))
            && builder.AddFact(new GameplayOutcomeFact(CharacterSkillAcquiredOutcomeIds.DisplayName, receipt.SkillDisplayName))
            && builder.AddFact(new GameplayOutcomeFact(CharacterSkillAcquiredOutcomeIds.CombinationId, receipt.CombinationId))
            && builder.AddFact(new GameplayOutcomeFact(CharacterSkillAcquiredOutcomeIds.PresentationId, receipt.PresentationId))
            && builder.AddFact(new GameplayOutcomeFact(CharacterSkillAcquiredOutcomeIds.Mechanics, mechanics))
            && builder.AddFact(new GameplayOutcomeFact(CharacterSkillAcquiredOutcomeIds.Evidence, receipt.EvidenceCanonical))
            && builder.AddAnchor(character, Hash(CharacterSkillAcquiredOutcomeIds.SkillAnchor, receipt.SkillId))
            && builder.AddAnchor(character, Hash(CharacterSkillAcquiredOutcomeIds.PresentationAnchor, receipt.PresentationId))
            && builder.AddAnchor(character, Hash(CharacterSkillAcquiredOutcomeIds.MechanicsAnchor, mechanics))
            && builder.AddAnchor(character, Hash(CharacterSkillAcquiredOutcomeIds.EvidenceAnchor, receipt.EvidenceCanonical));
        return written ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(OutcomePrepareCode.AdapterWriteFailed,
                "character-skill-acquired-write-failed");
    }

    private static NarrativeEvidenceReference Hash(string type, string value) =>
        new(type, NarrativeInferenceHash.ComputeSha256Utf8(value));
}

public sealed class CharacterSkillAcquiredOutcomeDescriptor : IGameplayOutcomeDescriptor
{
    private static readonly INarrativePerspectiveProjector Projector =
        new CharacterSkillAcquiredPerspectiveProjector();
    private static readonly IOutcomeMemoryPolicy Memory = new EvolutionOutcomeMemoryPolicy();
    private static readonly IOutcomePerceptionPolicy Perception = new EvolutionOutcomePerceptionPolicy();
    private static readonly IOutcomeMemoryConsolidator Consolidator =
        new EvolutionOutcomeConsolidator(false);

    public GameplayOutcomeTypeId OutcomeTypeId => CharacterSkillAcquiredOutcomeIds.Acquired;
    public INarrativePerspectiveProjector PerspectiveProjector => Projector;
    public IOutcomeMemoryPolicy MemoryPolicy => Memory;
    public IOutcomePerceptionPolicy PerceptionPolicy => Perception;
    public IOutcomeMemoryConsolidator MemoryConsolidator => Consolidator;
    public bool IsKnownRole(GameplayRoleId roleId) =>
        roleId.Equals(CharacterSkillAcquiredOutcomeIds.OwnerRole);
    public bool IsKnownMetric(GameplayMetricId metricId, GameplayMetricUnitId unitId) =>
        unitId.Equals(CharacterSkillAcquiredOutcomeIds.RevisionUnit)
        && (metricId.Equals(CharacterSkillAcquiredOutcomeIds.BeforeRevision)
            || metricId.Equals(CharacterSkillAcquiredOutcomeIds.AfterRevision));

    public OutcomeValidationResult Validate(in GameplayOutcomeReadView outcome)
    {
        if (outcome.OutcomeTypeId != OutcomeTypeId
            || outcome.ParticipantCount != 1 || outcome.SubjectCount != 1
            || outcome.MetricCount != 2 || outcome.TagCount != 1
            || outcome.AnchorCount != 4 || outcome.FactCount != 8)
            return OutcomeValidationResult.Reject("character-skill-acquired-shape-invalid");
        GameplayOutcomeParticipant participant = outcome.GetParticipant(0);
        if (!participant.RoleId.Equals(CharacterSkillAcquiredOutcomeIds.OwnerRole)
            || !participant.EntityId.Kind.Equals(CharacterSkillAcquiredOutcomeIds.CharacterKind)
            || outcome.GetSubject(0).SubjectId != participant.EntityId
            || !outcome.GetTag(0).Equals(CharacterSkillAcquiredOutcomeIds.AcquisitionTag)
            || !outcome.GetMetric(0).MetricId.Equals(CharacterSkillAcquiredOutcomeIds.BeforeRevision)
            || !outcome.GetMetric(1).MetricId.Equals(CharacterSkillAcquiredOutcomeIds.AfterRevision)
            || !outcome.GetMetric(0).UnitId.Equals(CharacterSkillAcquiredOutcomeIds.RevisionUnit)
            || !outcome.GetMetric(1).UnitId.Equals(CharacterSkillAcquiredOutcomeIds.RevisionUnit)
            || outcome.GetMetric(0).DefinitionOrInstanceId != participant.EntityId
            || outcome.GetMetric(1).DefinitionOrInstanceId != participant.EntityId
            || outcome.GetMetric(0).Value < 0
            || outcome.GetMetric(0).Value != Math.Truncate(outcome.GetMetric(0).Value)
            || outcome.GetMetric(1).Value != outcome.GetMetric(0).Value + 1)
            return OutcomeValidationResult.Reject("character-skill-acquired-owner-invalid");
        for (int index = 0; index < CharacterSkillAcquiredOutcomeIds.OrderedFacts.Length; index++)
        {
            if (!outcome.GetFact(index).FactId.Equals(CharacterSkillAcquiredOutcomeIds.OrderedFacts[index]))
                return OutcomeValidationResult.Reject("character-skill-acquired-facts-invalid");
        }
        if (!int.TryParse(outcome.GetFact(2).Value, NumberStyles.None,
                CultureInfo.InvariantCulture, out int unlockLevel)
            || unlockLevel < 1
            || string.IsNullOrWhiteSpace(outcome.GetFact(3).Value)
            || !(outcome.GetFact(1).Value == nameof(CharacterSkillKind.Active)
                || outcome.GetFact(1).Value == nameof(CharacterSkillKind.Passive)
                || outcome.GetFact(1).Value == nameof(CharacterSkillKind.Ultimate))
            || !outcome.GetFact(6).Value.StartsWith(
                "skill-mechanics-v1;", StringComparison.Ordinal)
            || !outcome.GetFact(7).Value.StartsWith(
                "evidence-v1;", StringComparison.Ordinal))
            return OutcomeValidationResult.Reject("character-skill-acquired-canonical-facts-invalid");
        if (!HasHash(outcome.GetAnchor(0), CharacterSkillAcquiredOutcomeIds.SkillAnchor, outcome.GetFact(0).Value)
            || !HasHash(outcome.GetAnchor(1), CharacterSkillAcquiredOutcomeIds.PresentationAnchor, outcome.GetFact(5).Value)
            || !HasHash(outcome.GetAnchor(2), CharacterSkillAcquiredOutcomeIds.MechanicsAnchor, outcome.GetFact(6).Value)
            || !HasHash(outcome.GetAnchor(3), CharacterSkillAcquiredOutcomeIds.EvidenceAnchor, outcome.GetFact(7).Value))
            return OutcomeValidationResult.Reject("character-skill-acquired-anchors-invalid");
        for (int index = 0; index < outcome.AnchorCount; index++)
        {
            if (outcome.GetAnchorSubject(index) != participant.EntityId)
                return OutcomeValidationResult.Reject("character-skill-acquired-anchor-owner-invalid");
        }
        return OutcomeValidationResult.Accepted;
    }

    private static bool HasHash(NarrativeEvidenceReference value, string type, string fact) =>
        value.AnchorTypeId == type
        && value.AnchorId == NarrativeInferenceHash.ComputeSha256Utf8(fact);
}

internal sealed class CharacterSkillAcquiredPerspectiveProjector :
    INarrativePerspectiveProjector
{
    public NarrativeView Project(
        in GameplayOutcomeReadView outcome,
        NarrativePerspectiveContext perspective) => new(
        outcome.OutcomeId,
        perspective.Kind,
        outcome.GetParticipant(0).DisplayName.DisplayText
            + ": 기술 「" + outcome.GetFact(3).Value + "」 획득을 확정했다.",
        "character-skill-acquired-v1",
        true);
}
