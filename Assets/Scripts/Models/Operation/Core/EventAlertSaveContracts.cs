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
        public SettlementThreatAlertSaveData threatAlert = new();
        public SettlementLaborSaveData labor = new();
    }

    [Serializable]
    public sealed class SettlementThreatAlertSaveData
    {
        public int committedLevel;
        public int desiredLevel;
        public long alertEpochId;
        public long levelEnteredAbsoluteHour;
        public long downgradeStableSinceAbsoluteHour = -1L;
        public int reserveCoverageBand = 1;
        public float reserveCoverage = 1f;
        public long coverageStableSinceAbsoluteHour = -1L;
        public List<SettlementIncidentSaveData> incidents = new();
        public List<SettlementSuspendedWorkSaveData> suspendedWork = new();
    }

    [Serializable]
    public sealed class SettlementIncidentSaveData
    {
        public string incidentId = string.Empty;
        public bool active;
        public int requiredLevel;
        public long revision;
        public string sourceId = string.Empty;
        public string diagnostic = string.Empty;
    }

    [Serializable]
    public sealed class SettlementSuspendedWorkSaveData
    {
        public string characterId = string.Empty;
        public string workTypeId = string.Empty;
        public string targetBuildingId = string.Empty;
        public long alertEpochId;
        public long suspendedAtAbsoluteHour;
        public bool progressExternallyPersisted;
    }

    [Serializable]
    public sealed class SettlementLaborSaveData
    {
        public long actualLaborMilliWu;
        public long convertedProcessOutputMilliWu;
        public long domainAutomationMilliWu;
        public long lossMilliWu;
        public long essentialMaintenanceMilliWu;
        public long equipmentFacilityMaintenanceMilliWu;
        public List<SettlementLaborDailySaveData> dailyRecords = new();
        public List<SettlementLaborSequenceSaveData> contributionSequences = new();
    }

    [Serializable]
    public sealed class SettlementLaborDailySaveData
    {
        public int absoluteDay;
        public long actualLaborMilliWu;
        public long outputEquivalentMilliWu;
        public long realizedGrowthMilliWu;
        public long guaranteedGrowthMilliWu;
        public int productiveAdultCount;
        public float perCapitaNetWuIndex;
    }

    [Serializable]
    public sealed class SettlementLaborSequenceSaveData
    {
        public string operationId = string.Empty;
        public int channel;
        public long lastSequence;
    }

    [Serializable]
    public sealed class DungeonEventAlertRecordSaveData
    {
        public int id;
        public string title = string.Empty;
        public string detail = string.Empty;
        public EventAlertImportance importance;
        public string category = string.Empty;
        public string sourceId = string.Empty;
        public int count = 1;
        public bool dismissed;
        public List<DungeonEventAlertChoiceSaveData> choices = new();
    }

    [Serializable]
    public sealed class DungeonEventAlertChoiceSaveData
    {
        public string label = string.Empty;
        public string description = string.Empty;
        public string actionId = string.Empty;
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

                if (record.detail == null
                    || record.category == null
                    || record.sourceId == null)
                {
                    errors.Add($"Event-alert record {record.id} has null text fields.");
                }

                ValidateChoices(record, errors);
            }

            ValidateThreatAlert(payload.threatAlert, errors);
            ValidateLabor(payload.labor, errors);

            return errors;
        }

        private static void ValidateLabor(
            SettlementLaborSaveData labor,
            ICollection<string> errors)
        {
            if (labor == null
                || labor.dailyRecords == null
                || labor.contributionSequences == null
                || labor.actualLaborMilliWu < 0L
                || labor.convertedProcessOutputMilliWu < 0L
                || labor.domainAutomationMilliWu < 0L
                || labor.lossMilliWu < 0L
                || labor.essentialMaintenanceMilliWu < 0L
                || labor.equipmentFacilityMaintenanceMilliWu < 0L)
            {
                errors.Add("Settlement labor payload is missing or has negative totals.");
                return;
            }
            if (labor.dailyRecords.Count > 30)
            {
                errors.Add("Settlement labor payload exceeds the 30-day rolling window.");
            }

            int previousDay = 0;
            for (int index = 0; index < labor.dailyRecords.Count; index++)
            {
                SettlementLaborDailySaveData record = labor.dailyRecords[index];
                if (record == null
                    || record.absoluteDay <= previousDay
                    || record.actualLaborMilliWu < 0L
                    || record.outputEquivalentMilliWu < 0L
                    || record.realizedGrowthMilliWu < 0L
                    || record.guaranteedGrowthMilliWu < 0L
                    || record.productiveAdultCount < 0
                    || float.IsNaN(record.perCapitaNetWuIndex)
                    || float.IsInfinity(record.perCapitaNetWuIndex)
                    || record.perCapitaNetWuIndex < 0f)
                {
                    errors.Add($"Settlement labor daily record {index} is invalid or unordered.");
                }
                previousDay = record?.absoluteDay ?? previousDay;
            }

            HashSet<string> sequences = new(StringComparer.Ordinal);
            for (int index = 0; index < labor.contributionSequences.Count; index++)
            {
                SettlementLaborSequenceSaveData sequence =
                    labor.contributionSequences[index];
                string key = sequence == null
                    ? string.Empty
                    : $"{sequence.operationId}\n{sequence.channel}";
                if (sequence == null
                    || string.IsNullOrWhiteSpace(sequence.operationId)
                    || sequence.channel < 0
                    || sequence.channel > 5
                    || sequence.lastSequence < 0L
                    || !sequences.Add(key))
                {
                    errors.Add($"Settlement labor sequence {index} is invalid or duplicated.");
                }
            }
        }

        private static void ValidateThreatAlert(
            SettlementThreatAlertSaveData threatAlert,
            ICollection<string> errors)
        {
            if (threatAlert == null)
            {
                errors.Add("Event-alert payload has no settlement threat state.");
                return;
            }

            if (threatAlert.committedLevel < 0 || threatAlert.committedLevel > 2
                || threatAlert.desiredLevel < 0 || threatAlert.desiredLevel > 2
                || threatAlert.reserveCoverageBand < 0 || threatAlert.reserveCoverageBand > 3
                || float.IsNaN(threatAlert.reserveCoverage)
                || float.IsInfinity(threatAlert.reserveCoverage)
                || threatAlert.reserveCoverage < 0f
                || threatAlert.alertEpochId < 0L
                || threatAlert.levelEnteredAbsoluteHour < 0L
                || threatAlert.downgradeStableSinceAbsoluteHour < -1L
                || threatAlert.coverageStableSinceAbsoluteHour < -1L)
            {
                errors.Add("Settlement threat alert contains invalid levels, coverage or time values.");
            }

            if (threatAlert.incidents == null
                || threatAlert.suspendedWork == null)
            {
                errors.Add("Settlement threat alert has no incident or suspended-work list.");
                return;
            }

            HashSet<string> ids = new(StringComparer.Ordinal);
            for (int index = 0; index < threatAlert.incidents.Count; index++)
            {
                SettlementIncidentSaveData incident = threatAlert.incidents[index];
                if (incident == null
                    || string.IsNullOrWhiteSpace(incident.incidentId)
                    || !ids.Add(incident.incidentId)
                    || incident.requiredLevel < 1
                    || incident.requiredLevel > 2
                    || incident.revision < 0L
                    || incident.sourceId == null
                    || incident.diagnostic == null)
                {
                    errors.Add($"Settlement threat incident {index} is invalid or duplicated.");
                }
            }

            HashSet<string> suspendedCharacters = new(StringComparer.Ordinal);
            for (int index = 0; index < threatAlert.suspendedWork.Count; index++)
            {
                SettlementSuspendedWorkSaveData suspended =
                    threatAlert.suspendedWork[index];
                if (suspended == null
                    || string.IsNullOrWhiteSpace(suspended.characterId)
                    || !suspendedCharacters.Add(suspended.characterId)
                    || string.IsNullOrWhiteSpace(suspended.workTypeId)
                    || string.IsNullOrWhiteSpace(suspended.targetBuildingId)
                    || suspended.alertEpochId <= 0L
                    || suspended.alertEpochId > threatAlert.alertEpochId
                    || suspended.suspendedAtAbsoluteHour < 0L
                    || !suspended.progressExternallyPersisted)
                {
                    errors.Add(
                        $"Settlement suspended work {index} is invalid or duplicated.");
                }
            }
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

            if (record.choices.Count > 4)
            {
                errors.Add(
                    $"Event-alert record {record.id} exceeds the four-choice limit.");
            }

            for (int choiceIndex = 0; choiceIndex < record.choices.Count; choiceIndex++)
            {
                DungeonEventAlertChoiceSaveData choice = record.choices[choiceIndex];
                if (choice == null || string.IsNullOrWhiteSpace(choice.label))
                {
                    errors.Add(
                        $"Event-alert record {record.id} choice {choiceIndex} is invalid.");
                }
                else if (choice.description == null || choice.actionId == null)
                {
                    errors.Add(
                        $"Event-alert record {record.id} choice {choiceIndex} has a null description.");
                }
            }
        }
    }
}
