using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DungeonStory.Foundation.Facts;

internal static class V20CampaignIdentity
{
    public const string SocietyChoiceOwnerId = "society.events";
    internal const string GraveVisitLifeEventId = "life-event:grave-visit";
    internal const string StoryCompressedLifeEventId = "life-event:story-compressed";
    internal const string ApprenticeMistakeLifeEventId = "life-event:apprentice-mistake";
    internal const string LastLessonLifeEventId = "life-event:last-lesson";
    internal const string QuietPromotionLifeEventId = "life-event:quiet-promotion";

    internal static string EffectOwnerId(
        string instanceId,
        EndlessCrisisAxis axis) =>
        $"{instanceId}:axis:{(int)axis}";

    internal static string ObservedLifeOccurrenceInstanceId(
        ObservedLifeEventSourceKind sourceKind,
        string sourceOperationId) =>
        $"observed-life:{(int)sourceKind}:"
        + Convert.ToBase64String(Encoding.UTF8.GetBytes(sourceOperationId));

    internal static string CharacterRecurrenceKey(
        string definitionId,
        string characterId) =>
        $"{definitionId}:character:{Normalize(characterId)}";

    internal static string CommittedDecisionKey(
        V20CommittedRunChoiceSaveData choice) =>
        $"{(int)choice.kind}:{choice.ownerId}:{choice.instanceId}";

    internal static bool IsCanonicalRequiredId(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
    internal static string Normalize(string value) => value?.Trim() ?? string.Empty;
}
