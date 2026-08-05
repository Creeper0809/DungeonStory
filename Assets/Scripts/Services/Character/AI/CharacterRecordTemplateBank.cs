using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class CharacterRecordTemplateBank
{
    private const int MaxLineLength = CharacterLogNarrativeService.MaxLineCharacters;

    private static readonly HashSet<string> KnownWorkLabelIds =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "work:operate", "work:restock", "work:repair", "work:clean",
            "work:research", "work:guard", "work:reception", "work:rescue",
            "work:rest", "work:craft", "work:haul", "work:hunt",
            "work:butcher", "work:draw-water", "work:cook", "work:treat",
            "work:refuel", "work:alchemy-research", "work:weapon-sales",
            "work:cleaning"
        };

    private static readonly HashSet<string> SpecializedWorkIds =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "work:research", "work:clean", "work:repair", "work:restock",
            "work:guard", "work:reception", "work:craft", "work:haul",
            "work:hunt", "work:butcher", "work:draw-water", "work:cook",
            "work:treat", "work:refuel"
        };

    private readonly ICharacterNarrativeTextQuery text;

    public CharacterRecordTemplateBank(ICharacterNarrativeTextQuery text)
    {
        this.text = text ?? throw new ArgumentNullException(nameof(text));
    }
    public bool TryBuildLine(CharacterLog characterLog, CharacterLogEntry entry, out string line)
    {
        line = string.Empty;
        CharacterActivityEvent activity = entry.Activity;
        if (!ShouldUseTemplate(entry))
        {
            return false;
        }

        string subject = BuildSubject(characterLog, activity);
        string actionId = activity.ActionId ?? string.Empty;
        string work = ResolveWorkLabel(actionId);
        string place = ResolvePlace(activity);
        string target = ResolveTarget(activity);
        IReadOnlyList<string> templates = ResolveTemplates(activity, actionId);
        if (templates == null || templates.Count == 0)
        {
            return false;
        }

        int index = Math.Abs(StableHash(
            entry.EntryId,
            activity.KindId,
            activity.ActionId,
            activity.OutcomeId,
            activity.TargetName,
            activity.FactText)) % templates.Count;
        line = RenderTemplate(templates[index], subject, work, place, target);
        if (line.Length > MaxLineLength)
        {
            line = RenderTemplate(
                PickShortTemplate(activity, actionId, entry.EntryId),
                subject,
                work,
                place,
                target);
        }

        if (string.IsNullOrWhiteSpace(line)
            || line.Length > MaxLineLength
            || string.Equals(line.Trim(), entry.DisplayLine?.Trim(), StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private static bool ShouldUseTemplate(CharacterLogEntry entry)
    {
        CharacterActivityEvent activity = entry.Activity;
        if (entry.EntryId <= 0
            || entry.Count != 1
            || activity == null
            || !activity.NarrativeEligible
            || !activity.VisibleToPlayer)
        {
            return false;
        }

        if (IsMajorNarrative(activity))
        {
            return false;
        }

        return activity.KindId == CharacterActivityKinds.Work
            || activity.KindId == CharacterActivityKinds.FacilityUse
            || activity.KindId == CharacterActivityKinds.Stock
            || activity.KindId == CharacterActivityKinds.Shopping
            || activity.KindId == CharacterActivityKinds.Health
            || activity.KindId == CharacterActivityKinds.Duty
            || activity.KindId == CharacterActivityKinds.Wait
            || activity.KindId == CharacterActivityKinds.Social
            || activity.KindId == CharacterActivityKinds.Lifecycle;
    }

    private static bool IsMajorNarrative(CharacterActivityEvent activity)
    {
        if (activity == null)
        {
            return false;
        }

        if (activity.KindId == CharacterActivityKinds.Combat)
        {
            return true;
        }

        if (activity.KindId == CharacterActivityKinds.Health
            && string.Equals(activity.OutcomeId, CharacterActivityOutcomes.Damaged, StringComparison.Ordinal))
        {
            return true;
        }

        string reason = activity.ReasonCode ?? string.Empty;
        string action = activity.ActionId ?? string.Empty;
        string fact = activity.FactText ?? string.Empty;
        return ContainsMajorKeyword(reason)
            || ContainsMajorKeyword(action)
            || ContainsMajorKeyword(fact);
    }

    private static bool ContainsMajorKeyword(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.IndexOf("boss", StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("truth", StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("ultimate", StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("death", StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("사망", StringComparison.Ordinal) >= 0
            || value.IndexOf("보스", StringComparison.Ordinal) >= 0
            || value.IndexOf("진실", StringComparison.Ordinal) >= 0
            || value.IndexOf("궁극", StringComparison.Ordinal) >= 0;
    }

    private IReadOnlyList<string> ResolveTemplates(
        CharacterActivityEvent activity,
        string actionId)
    {
        if (activity.KindId == CharacterActivityKinds.Work)
        {
            if (string.Equals(
                    activity.OutcomeId,
                    CharacterActivityOutcomes.Started,
                    StringComparison.Ordinal))
            {
                return GetTemplates(
                    SpecializedWorkIds.Contains(actionId)
                        ? "Template.WorkStarted." + actionId
                        : "Template.GenericStarted");
            }

            if (string.Equals(
                    activity.OutcomeId,
                    CharacterActivityOutcomes.Completed,
                    StringComparison.Ordinal)
                || string.Equals(
                    activity.OutcomeId,
                    CharacterActivityOutcomes.Returned,
                    StringComparison.Ordinal))
            {
                return GetTemplates(
                    SpecializedWorkIds.Contains(actionId)
                        ? "Template.WorkCompleted." + actionId
                        : "Template.GenericCompleted");
            }

            if (string.Equals(
                    activity.OutcomeId,
                    CharacterActivityOutcomes.Progress,
                    StringComparison.Ordinal)
                || string.Equals(
                    activity.OutcomeId,
                    CharacterActivityOutcomes.Changed,
                    StringComparison.Ordinal))
            {
                return GetTemplates("Template.GenericProgress");
            }

            if (string.Equals(
                    activity.OutcomeId,
                    CharacterActivityOutcomes.Failed,
                    StringComparison.Ordinal)
                || string.Equals(
                    activity.OutcomeId,
                    CharacterActivityOutcomes.Cancelled,
                    StringComparison.Ordinal))
            {
                return GetTemplates("Template.GenericFailed");
            }

            if (string.Equals(
                    activity.OutcomeId,
                    CharacterActivityOutcomes.Blocked,
                    StringComparison.Ordinal))
            {
                return GetTemplates("Template.GenericBlocked");
            }

            return GetTemplates("Template.GenericCompleted");
        }

        string key = activity.KindId switch
        {
            CharacterActivityKinds.FacilityUse => "Template.Facility",
            CharacterActivityKinds.Stock => "Template.Stock",
            CharacterActivityKinds.Shopping => "Template.Shopping",
            CharacterActivityKinds.Health => "Template.Health",
            CharacterActivityKinds.Duty => "Template.Duty",
            CharacterActivityKinds.Wait => "Template.Wait",
            CharacterActivityKinds.Social => "Template.Social",
            CharacterActivityKinds.Lifecycle => "Template.Lifecycle",
            _ => string.Empty
        };
        return key.Length == 0 ? null : GetTemplates(key);
    }
    private string PickShortTemplate(
        CharacterActivityEvent activity,
        string actionId,
        int entryId)
    {
        string key;
        if (activity.KindId == CharacterActivityKinds.Work
            && string.Equals(
                activity.OutcomeId,
                CharacterActivityOutcomes.Started,
                StringComparison.Ordinal))
        {
            key = "Template.GenericStarted";
        }
        else if (activity.KindId == CharacterActivityKinds.Work
            && (string.Equals(
                    activity.OutcomeId,
                    CharacterActivityOutcomes.Failed,
                    StringComparison.Ordinal)
                || string.Equals(
                    activity.OutcomeId,
                    CharacterActivityOutcomes.Blocked,
                    StringComparison.Ordinal)))
        {
            key = "Template.GenericFailed";
        }
        else
        {
            key = "Template.GenericCompleted";
        }

        IReadOnlyList<string> templates = GetTemplates(key);
        return templates[Math.Abs(entryId) % templates.Count];
    }

    private IReadOnlyList<string> GetTemplates(string key) =>
        text.GetVariants(key);
    private string RenderTemplate(
        string template,
        string subject,
        string work,
        string place,
        string target)
    {
        string safeWork = string.IsNullOrWhiteSpace(work)
            ? text.Get("Fallback.Work")
            : work;
        string safePlace = string.IsNullOrWhiteSpace(place)
            ? text.Get("Fallback.Place")
            : place;
        string safeTarget = string.IsNullOrWhiteSpace(target) ? safeWork : target;
        string result = template
            .Replace("{subject}", subject)
            .Replace("{work}", safeWork)
            .Replace("{workObject}", text.ApplyObjectParticle(safeWork))
            .Replace("{place}", safePlace)
            .Replace("{target}", safeTarget)
            .Replace("{targetObject}", text.ApplyObjectParticle(safeTarget));
        result = result.Replace("  ", " ").Trim();
        if (!result.EndsWith(".", StringComparison.Ordinal)
            && !result.EndsWith("!", StringComparison.Ordinal)
            && !result.EndsWith("?", StringComparison.Ordinal))
        {
            result += ".";
        }

        return result;
    }

    private string BuildSubject(CharacterLog characterLog, CharacterActivityEvent activity)
    {
        string name = !string.IsNullOrWhiteSpace(activity?.ActorName)
            ? activity.ActorName.Trim()
            : characterLog != null
                ? characterLog.name
                : text.Get("Fallback.Someone");
        return text.ApplySubjectParticle(name);
    }

    private string ResolveWorkLabel(string actionId)
    {
        if (string.IsNullOrWhiteSpace(actionId))
        {
            return text.Get("Fallback.Work");
        }

        if (KnownWorkLabelIds.Contains(actionId))
        {
            return text.Get("WorkLabel." + actionId);
        }

        if (WorkTypeCatalog.TryGet(actionId, out WorkTypeDefinition definition)
            && !string.IsNullOrWhiteSpace(definition.DisplayName))
        {
            return definition.DisplayName;
        }

        int index = actionId.LastIndexOf(':');
        return index >= 0 && index < actionId.Length - 1
            ? actionId.Substring(index + 1).Replace('-', ' ')
            : actionId;
    }

    private string ResolvePlace(CharacterActivityEvent activity)
    {
        if (!string.IsNullOrWhiteSpace(activity?.PlaceName))
        {
            return activity.PlaceName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(activity?.TargetName))
        {
            return activity.TargetName.Trim();
        }

        return text.Get("Fallback.Place");
    }

    private string ResolveTarget(CharacterActivityEvent activity)
    {
        if (!string.IsNullOrWhiteSpace(activity?.TargetName))
        {
            return activity.TargetName.Trim();
        }

        return ResolveWorkLabel(activity?.ActionId);
    }

    private static int StableHash(int entryId, params string[] values)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + entryId;
            if (values != null)
            {
                foreach (string value in values)
                {
                    if (value == null)
                    {
                        continue;
                    }

                    for (int i = 0; i < value.Length; i++)
                    {
                        hash = hash * 31 + value[i];
                    }
                }
            }

            return hash == int.MinValue ? 0 : hash;
        }
    }
}
