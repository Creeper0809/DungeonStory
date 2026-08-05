using System;
using System.Collections.Generic;
using DungeonStory.Operation;

namespace DungeonStory.Infrastructure
{
    public interface IEventAlertSaveService
    {
        DungeonEventAlertSaveData Capture();
        EventAlertRestoreCandidate PrepareRestore(
            DungeonEventAlertSaveData source);
        void PublishRestore(EventAlertRestoreCandidate candidate);
    }

    [Serializable]
    public sealed class DungeonEventAlertSaveData
    {
        public List<DungeonEventAlertRecordSaveData> records = new();
    }

    [Serializable]
    public sealed class DungeonEventAlertRecordSaveData
    {
        public int id;
        public string title = string.Empty;
        public string detail = string.Empty;
        public EventAlertImportance importance;
        public string category = string.Empty;
        public int count = 1;
        public bool dismissed;
        public List<DungeonEventAlertChoiceSaveData> choices = new();
    }

    [Serializable]
    public sealed class DungeonEventAlertChoiceSaveData
    {
        public string label = string.Empty;
        public string description = string.Empty;
    }
}

namespace DungeonStory.Operation
{
    using DungeonStory.Infrastructure;

    public static class EventAlertPayloadValidation
    {
        public const int MaxSavedRecords = 80;

        public static IReadOnlyList<string> Validate(
            DungeonEventAlertSaveData payload)
        {
            List<string> errors = new();
            if (payload?.records == null)
            {
                errors.Add("Event-alert payload has no record list.");
                return errors;
            }

            if (payload.records.Count > MaxSavedRecords)
            {
                errors.Add(
                    $"Event-alert payload exceeds the {MaxSavedRecords}-record limit.");
            }

            HashSet<int> seenIds = new();
            for (int recordIndex = 0; recordIndex < payload.records.Count; recordIndex++)
            {
                DungeonEventAlertRecordSaveData record = payload.records[recordIndex];
                if (record == null)
                {
                    errors.Add($"Event-alert payload record {recordIndex} is null.");
                    continue;
                }

                if (record.id <= 0
                    || record.id == int.MaxValue
                    || !seenIds.Add(record.id))
                {
                    errors.Add(
                        $"Event-alert payload contains invalid or duplicate record ID {record.id}.");
                }

                if (string.IsNullOrWhiteSpace(record.title))
                {
                    errors.Add($"Event-alert record {record.id} has no title.");
                }

                if (!Enum.IsDefined(typeof(EventAlertImportance), record.importance))
                {
                    errors.Add(
                        $"Event-alert record {record.id} has invalid importance {record.importance}.");
                }

                if (record.count < 1)
                {
                    errors.Add(
                        $"Event-alert record {record.id} has invalid count {record.count}.");
                }

                if (record.detail == null || record.category == null)
                {
                    errors.Add($"Event-alert record {record.id} has null text fields.");
                }

                ValidateChoices(record, errors);
            }

            return errors;
        }

        private static void ValidateChoices(
            DungeonEventAlertRecordSaveData record,
            ICollection<string> errors)
        {
            if (record.choices == null)
            {
                errors.Add($"Event-alert record {record.id} has no choice list.");
                return;
            }

            if (record.choices.Count > 3)
            {
                errors.Add(
                    $"Event-alert record {record.id} exceeds the three-choice limit.");
            }

            for (int choiceIndex = 0; choiceIndex < record.choices.Count; choiceIndex++)
            {
                DungeonEventAlertChoiceSaveData choice = record.choices[choiceIndex];
                if (choice == null || string.IsNullOrWhiteSpace(choice.label))
                {
                    errors.Add(
                        $"Event-alert record {record.id} choice {choiceIndex} is invalid.");
                }
                else if (choice.description == null)
                {
                    errors.Add(
                        $"Event-alert record {record.id} choice {choiceIndex} has a null description.");
                }
            }
        }
    }
}
