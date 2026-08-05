using System.Collections.Generic;
using DungeonStory.Operation;

public static class EventAlertPresentationText
{
    public static string ToDetailText(this EventAlertRecord record)
    {
        if (record == null)
        {
            return string.Empty;
        }

        List<string> lines = new List<string>
        {
            record.Title,
            $"중요도: {GetImportanceName(record.Importance)}"
        };

        if (!string.IsNullOrWhiteSpace(record.Category))
        {
            lines.Add($"분류: {record.Category}");
        }

        if (record.Count > 1)
        {
            lines.Add($"반복: {record.Count}");
        }

        if (!string.IsNullOrWhiteSpace(record.Detail))
        {
            lines.Add(string.Empty);
            lines.Add(record.Detail);
        }

        if (record.Choices.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("선택지:");
            foreach (EventAlertChoice choice in record.Choices)
            {
                lines.Add(string.IsNullOrWhiteSpace(choice.Description)
                    ? $"- {choice.Label}"
                    : $"- {choice.Label}: {choice.Description}");
            }
        }

        return string.Join("\n", lines);
    }

    private static string GetImportanceName(EventAlertImportance importance)
    {
        return importance switch
        {
            EventAlertImportance.Low => "낮음",
            EventAlertImportance.Medium => "중간",
            EventAlertImportance.High => "높음",
            _ => importance.ToString()
        };
    }
}
