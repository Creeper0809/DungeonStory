using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public interface IDefenseUiTextQuery
{
    string Get(string key, params object[] arguments);
    string Get(DomainFailure failure);
}

public interface IDomainFailureLocalizer
{
    string Localize(DomainFailure failure);
    string Localize(InfrastructureStatus status);
    string Localize(
        CharacterConsumablesFailureCode code,
        IReadOnlyList<string> parameters);
}

/// <summary>
/// Presentation adapter for localization-neutral domain failures.
/// </summary>
public sealed class DomainFailureLocalizer : IDomainFailureLocalizer
    , IDefenseUiTextQuery
{
    public const string TableName = "DomainFailures";
    public const string DefenseTableName = "DefenseUI";

    public string Localize(DomainFailure failure)
    {
        if (!failure.IsFailure)
        {
            return string.Empty;
        }

        object[] arguments = failure.Parameters.ToArray()
            .Cast<object>()
            .ToArray();
        return LocalizeEntry(
            TableName,
            failure.Code.ToString(),
            arguments,
            DomainFailureLocalizationFormatContract.GetFailureArgumentCount(
                failure.Code));
    }

    public string Localize(InfrastructureStatus status)
    {
        if (!status.IsBlocked)
        {
            return string.Empty;
        }

        return LocalizeEntry(
            "InfrastructureStatus" + status.Code,
            status.Parameters ?? Array.Empty<string>());
    }

    public string Localize(
        CharacterConsumablesFailureCode code,
        IReadOnlyList<string> parameters)
    {
        if (code == CharacterConsumablesFailureCode.None)
        {
            return string.Empty;
        }

        return LocalizeEntry(code.ToString(), parameters ?? Array.Empty<string>());
    }

    public string Get(string key, params object[] arguments)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException(
                "Defense localization key is required.",
                nameof(key));
        }

        return LocalizeEntry(
            DefenseTableName,
            key,
            arguments ?? Array.Empty<object>());
    }

    public string Get(DomainFailure failure) => Localize(failure);

    private static string LocalizeEntry(
        string key,
        IEnumerable<string> parameters)
    {
        object[] arguments = parameters.Cast<object>().ToArray();
        return LocalizeEntry(TableName, key, arguments);
    }

    private static string LocalizeEntry(
        string tableName,
        string key,
        IReadOnlyList<object> arguments,
        int? expectedArgumentCount = null)
    {
        LocalizationSettings.InitializationOperation.WaitForCompletion();
        string template = new LocalizedString(
                tableName,
                key)
            .GetLocalizedString();
        if (string.IsNullOrWhiteSpace(template))
        {
            throw new InvalidOperationException(
                $"Missing localized failure entry '{key}' "
                + $"in String Table '{tableName}'.");
        }
        DomainFailureLocalizationFormatContract.ValidateArguments(
            tableName,
            key,
            template,
            arguments,
            expectedArgumentCount);
        return string.Format(
            CultureInfo.CurrentCulture,
            template,
            arguments.ToArray());
    }
}

public static class DomainFailureLocalizationFormatContract
{
    private static readonly Regex PlaceholderPattern = new(
        @"(?<!\{)\{(?<index>\d+)(?:,[^}:]+)?(?::[^}]+)?\}(?!\})",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly char[] MojibakeMarkers =
    {
        '\uFFFD',
        '\u63F4',
        '\u5A9B',
        '\u8A1B',
        '\u5AC4',
        '\uF9CD',
        '\u6028',
        '\u936E',
        '\u6D79',
        '\u8A98'
    };

    private static readonly IReadOnlyDictionary<FailureCode, int>
        FailureArgumentCounts = new Dictionary<FailureCode, int>
        {
            // Explicit non-zero arities. Every other FailureCode has arity zero.
            [FailureCode.AgeTreatmentAnatomyUnavailable] = 3,
            [FailureCode.AgeTreatmentCharacterMissing] = 1,
            [FailureCode.AgeTreatmentCooldownActive] = 2,
            [FailureCode.AgeTreatmentDefinitionMissing] = 1,
            [FailureCode.AgeTreatmentProcedureMismatch] = 2,
            [FailureCode.AgeTreatmentSupplyUnavailable] = 3,
            [FailureCode.AgeTreatmentTooYoung] = 2,
            [FailureCode.ChildSafetyApprenticeshipDisabled] = 1,
            [FailureCode.ChildSafetyCharacterPermissionRequired] = 1,
            [FailureCode.ChildSafetyLifeStateUnavailable] = 1,
            [FailureCode.ChildSafetyProtectiveEquipmentRequired] = 1,
            [FailureCode.ChildSafetySupervisorTooFar] = 1,
            [FailureCode.ChildSafetySupervisorUnavailable] = 1,
            [FailureCode.ChildSafetyWorkConfirmationRequired] = 1,
            [FailureCode.ChildSafetyWorkForbidden] = 1,
            [FailureCode.CropTreatmentDefinitionMissing] = 1,
            [FailureCode.CropTreatmentKindUnsupported] = 2,
            [FailureCode.CropTreatmentPlotMissing] = 1,
            [FailureCode.CropTreatmentSupplyUnavailable] = 3,
            [FailureCode.PopulationHealthCharacterMissing] = 1,
            [FailureCode.TemporalStasisFacilityMissing] = 1,
            [FailureCode.TemporalStasisPowerInsufficient] = 2,
            [FailureCode.VaccineDefinitionMissing] = 1,
            [FailureCode.VaccineDiseaseMismatch] = 2,
            [FailureCode.VaccineDoseUnavailable] = 3,
[FailureCode.RequiredResearchUnavailable] = 2,
            [FailureCode.ModuleSlotMissing] = 1,
            [FailureCode.ModuleSlotEmpty] = 1,
            [FailureCode.RumorMitigationAlreadyUsed] = 1,
            [FailureCode.InsufficientRenown] = 2,
            [FailureCode.InsufficientGold] = 2,
            [FailureCode.InsufficientDread] = 2,
            [FailureCode.InsufficientScoutingLabor] = 2,
            [FailureCode.ExpeditionSiteExpired] = 1,
            [FailureCode.OffenseTargetUnknown] = 1,
            [FailureCode.ServiceProcessContractMissing] = 2,
            [FailureCode.ServiceStageNotAllowed] = 1,
            [FailureCode.ServiceFeatureMissing] = 1,
            [FailureCode.ServiceSupportUnpowered] = 1,
            [FailureCode.EnvironmentThermostatUnsupported] = 2,
            [FailureCode.EnvironmentEvacuationCellUnavailable] = 1,
            [FailureCode.EnvironmentWorkwearDefinitionMissing] = 1,
            [FailureCode.EnvironmentWorkwearSpeciesIncompatible] = 2,
            [FailureCode.EnvironmentWorkwearResearchLocked] = 1,
            [FailureCode.EnvironmentWorkwearStockMissing] = 1,
            [FailureCode.EnvironmentWorkwearTransferFailed] = 1,
            [FailureCode.EnvironmentWorkwearInstanceIdMissing] = 1,
            [FailureCode.EnvironmentWorkwearLockerUnreachable] = 2,
            [FailureCode.EnvironmentWorkwearNotEquipped] = 1,
            [FailureCode.EnvironmentWorkwearPhysicalItemMissing] = 1,
            [FailureCode.EnvironmentWorkwearProductionContextInvalid] = 2,
            [FailureCode.EnvironmentWorkwearOutputSpawnFailed] = 2,
            [FailureCode.EnvironmentColdWorkCooldownActive] = 1,
            [FailureCode.EnvironmentExposureCritical] = 7,
            [FailureCode.SurgeryProcedureMissing] = 1,
            [FailureCode.SurgerySubjectMaintenanceOnly] = 1,
            [FailureCode.SurgeryPreferredDoctorInvalid] = 1,
            [FailureCode.SurgerySubjectAlreadyScheduled] = 1,
            [FailureCode.SurgeryFacilityMissing] = 1,
            [FailureCode.SurgeryFacilityUnavailable] = 1,
            [FailureCode.SurgeryOrderMissing] = 1,
            [FailureCode.SurgeryAnatomyFamilyUnsupported] = 1,
            [FailureCode.SurgerySpeciesUnsupported] = 1,
            [FailureCode.SurgeryResearchIncomplete] = 1,
            [FailureCode.SurgeryCorpseMissing] = 1,
            [FailureCode.SurgeryNodeAlreadyExtracted] = 1,
            [FailureCode.SurgeryLivingSubjectUnavailable] = 1,
            [FailureCode.SurgeryWildlifeSubjectUnavailable] = 1,
            [FailureCode.SurgeryTargetNodeUnavailable] = 1,
            [FailureCode.SurgeryOperatorStatInsufficient] = 3,
            [FailureCode.SurgeryOperatorSkillInsufficient] = 2,
            [FailureCode.SurgeryPreferredDoctorOnly] = 1,
            [FailureCode.SurgeryDoctorAlreadyAssigned] = 1,
            [FailureCode.SurgeryReservedDoctorMismatch] = 1,
            [FailureCode.SurgeryMaterialUnavailable] = 1,
            [FailureCode.SurgeryEffectHandlerMissing] = 1,
            [FailureCode.SurgeryEffectFailed] = 1,
            [FailureCode.SurgeryTransportOrderMissing] = 1,
            [FailureCode.SurgeryTransportCarrierMismatch] = 1,
            [FailureCode.SurgeryTransportUnavailable] = 1,
            [FailureCode.SurgeryExtractionAlreadyRecorded] = 2,
            [FailureCode.SurgeryEnvironmentUnsafe] = 1,
            [FailureCode.SurgeryOutcomeFailed] = 1,
            [FailureCode.CharacterMedicalOrderUnavailable] = 1,
            [FailureCode.CharacterMedicalOrderMissing] = 1,
            [FailureCode.CharacterMedicalOrderCreationFailed] = 1,
            [FailureCode.CharacterMedicalNoTreatableInjury] = 1,
            [FailureCode.CharacterMedicalAmbulatoryTreatmentUnsupported] = 1,
            [FailureCode.CharacterMedicalFacilityUnavailable] = 1,
            [FailureCode.CharacterMedicalFacilityReserved] = 2,
            [FailureCode.CharacterMedicalStabilizationRequired] = 1,
            [FailureCode.CharacterMedicalBedUnavailable] = 1,
            [FailureCode.CharacterMedicalDestinationUnavailable] = 1,
            [FailureCode.CharacterMedicalReservationMismatch] = 2,
            [FailureCode.CharacterMedicalProjectionPositionInvalid] = 3,
            [FailureCode.CharacterMedicalProjectionGridOccupied] = 2,
            [FailureCode.CharacterSpeciesStateUnavailable] = 1,
            [FailureCode.CharacterSpeciesRechargeUnsupported] = 1,
            [FailureCode.CharacterSpeciesRepairUnsupported] = 1,
            [FailureCode.SurvivalWorkUnsupported] = 1,
            [FailureCode.SurvivalWaterSourceUnsupported] = 1,
            [FailureCode.SurvivalWaterFrozen] = 1,
            [FailureCode.SurvivalOutputUnavailable] = 1,
            [FailureCode.SurvivalCookingUnsupported] = 1,
            [FailureCode.SurvivalFoodStockMissing] = 1,
            [FailureCode.SurvivalFuelStockMissing] = 1,
            [FailureCode.SurvivalTreatmentUnsupported] = 1,
            [FailureCode.SurvivalTreatmentTargetMissing] = 1,
            [FailureCode.SurvivalRefuelUnsupported] = 1,
            [FailureCode.ItemReservationRequestInvalid] = 1,
            [FailureCode.ItemReservationStackMissing] = 1,
            [FailureCode.ItemReservationStackForbidden] = 1,
            [FailureCode.ItemReservationSignatureMismatch] = 1,
            [FailureCode.ItemReservationQuantityUnavailable] = 1,
            [FailureCode.ItemReservationOperationConflict] = 1,
            [FailureCode.ItemReservationLeaseMissing] = 1,
            [FailureCode.ItemReservationLeaseExpired] = 1,
            [FailureCode.ItemReservationSliceInvalid] = 2,
            [FailureCode.ItemReservationRestoreConflict] = 2,
            [FailureCode.WarehouseMassAdmissionRequestInvalid] = 2,
            [FailureCode.WarehouseMassAdmissionOwnerUnavailable] = 2,
            [FailureCode.WarehouseMassCapacityUnavailable] = 3,
            [FailureCode.WarehouseMassAdmissionTokenMissing] = 1,
            [FailureCode.WarehouseMassAdmissionTokenExpired] = 2,
            [FailureCode.WarehouseMassAdmissionRevisionMismatch] = 3,
            [FailureCode.WarehouseMassAdmissionFingerprintMismatch] = 2,
            [FailureCode.WarehouseMassAdmissionCommitConflict] = 2,
            [FailureCode.WarehouseMassAdmissionTokenTerminal] = 2,
            [FailureCode.ItemAggregationIncompatible] = 1,
            [FailureCode.ItemAggregationDestinationMissing] = 1,
        };

    public static int GetFailureArgumentCount(FailureCode code)
    {
        if (code == FailureCode.None)
        {
            return 0;
        }
        return FailureArgumentCounts.TryGetValue(code, out int count)
            ? count
            : 0;
    }

    public static IReadOnlyList<int> GetPlaceholderIndices(string template)
    {
        if (template == null)
        {
            throw new ArgumentNullException(nameof(template));
        }

        return PlaceholderPattern.Matches(template)
            .Cast<Match>()
            .Select(match => int.Parse(
                match.Groups["index"].Value,
                CultureInfo.InvariantCulture))
            .Distinct()
            .OrderBy(index => index)
            .ToArray();
    }

    public static void ValidateTemplatePair(
        string key,
        string korean,
        string english)
    {
        ValidateTemplate(key, "ko", korean);
        ValidateTemplate(key, "en", english);
        int[] koreanIndices = GetPlaceholderIndices(korean).ToArray();
        int[] englishIndices = GetPlaceholderIndices(english).ToArray();
        if (!koreanIndices.SequenceEqual(englishIndices))
        {
            throw new InvalidOperationException(
                $"DomainFailures placeholder mismatch for '{key}': "
                + $"ko=[{string.Join(",", koreanIndices)}], "
                + $"en=[{string.Join(",", englishIndices)}].");
        }

        if (Enum.TryParse(key, out FailureCode failureCode)
            && failureCode != FailureCode.None)
        {
            ValidateFailureTemplate(failureCode, "ko", korean);
            ValidateFailureTemplate(failureCode, "en", english);
        }
    }

    public static void ValidateFailureTemplate(
        FailureCode code,
        string locale,
        string template)
    {
        ValidateTemplate(code.ToString(), locale, template);
        int expected = GetFailureArgumentCount(code);
        int[] actual = GetPlaceholderIndices(template).ToArray();
        int[] required = Enumerable.Range(0, expected).ToArray();
        if (!actual.SequenceEqual(required))
        {
            throw new InvalidOperationException(
                $"DomainFailures {locale} entry '{code}' does not match its "
                + $"parameter arity {expected}. Placeholders=[{string.Join(",", actual)}].");
        }
    }

    public static void ValidateTemplate(
        string key,
        string locale,
        string template)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            throw new InvalidOperationException(
                $"DomainFailures {locale} entry '{key}' is blank.");
        }
        ValidateNoMojibake(key, locale, template);
        IReadOnlyList<int> indices = GetPlaceholderIndices(template);
        int argumentCount = indices.Count == 0 ? 0 : indices.Max() + 1;
        object[] probes = Enumerable.Range(0, argumentCount)
            .Select(index => (object)index)
            .ToArray();
        try
        {
            _ = string.Format(CultureInfo.InvariantCulture, template, probes);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException(
                $"DomainFailures {locale} entry '{key}' has an invalid composite format.",
                exception);
        }
    }

    public static void ValidateNoMojibake(
        string key,
        string locale,
        string value)
    {
        if (value.IndexOfAny(MojibakeMarkers) >= 0)
        {
            throw new InvalidOperationException(
                $"DomainFailures {locale} entry '{key}' contains mojibake.");
        }
    }

    public static void ValidateArguments(
        string tableName,
        string key,
        string template,
        IReadOnlyList<object> arguments,
        int? expectedArgumentCount = null)
    {
        ValidateTemplate(key, tableName, template);
        IReadOnlyList<int> indices = GetPlaceholderIndices(template);
        int expected = expectedArgumentCount
            ?? (indices.Count == 0 ? 0 : indices.Max() + 1);
        int actual = arguments?.Count ?? 0;
        if (actual != expected)
        {
            throw new InvalidOperationException(
                $"Localization arguments for '{tableName}/{key}' do not match "
                + $"the authored contract. Expected {expected}, received {actual}.");
        }

        for (int index = 0; index < expected; index++)
        {
            if (!indices.Contains(index))
            {
                throw new InvalidOperationException(
                    $"Localization entry '{tableName}/{key}' skips placeholder {{{index}}}.");
            }
        }
    }
}
