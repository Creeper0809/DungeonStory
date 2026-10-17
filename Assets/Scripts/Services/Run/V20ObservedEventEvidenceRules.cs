using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DungeonStory.Foundation.Facts;
using static V20CampaignIdentity;
using static V20ContentDefinitionRules;
using static V20ObservedEventEvidenceRules;

internal static class V20ObservedEventEvidenceRules
{

    internal static string FormatObservedIncidentResponseOperationId(
        string instanceId,
        string choiceId) => string.Equals(
            choiceId,
            "replace",
            StringComparison.Ordinal)
        ? $"consumable-operation:wim040-response:{instanceId}"
        : $"society-incident-response:{instanceId}:{choiceId}";

    internal static bool IsExpectedObservedIncidentResponseReceipt(
        V20ActiveEventSaveData occurrence,
        string externalOperationId,
        string receiptId)
    {
        ObservedIncidentResponseSaveData response =
            occurrence?.observedIncidentResponse;
        if (response == null
            || !IsCanonicalRequiredId(externalOperationId)
            || !IsCanonicalRequiredId(receiptId))
        {
            return false;
        }
        if (string.Equals(response.choiceId, "replace", StringComparison.Ordinal))
        {
            string prefix = $"physical-meal-consumed:{response.operationId}:";
            return string.Equals(
                    externalOperationId,
                    response.operationId,
                    StringComparison.Ordinal)
                && receiptId.StartsWith(prefix, StringComparison.Ordinal)
                && new ItemStackId(receiptId.Substring(prefix.Length)).IsValid;
        }
        if (string.Equals(
                response.choiceId,
                "emergency-care",
                StringComparison.Ordinal))
        {
            return string.Equals(
                receiptId,
                $"medical-stabilized:{externalOperationId}",
                StringComparison.Ordinal);
        }
        return string.Equals(response.choiceId, "transfer", StringComparison.Ordinal)
            && TryClassifyObservedIncidentTransferExternalOperationId(
                occurrence,
                externalOperationId,
                out _)
            && new CharacterId(
                    occurrence.observedMealIncident?.targetCharacterId)
                is { IsValid: true } customerId
            && string.Equals(
                receiptId,
                FormatObservedIncidentTransferReceiptId(
                    externalOperationId,
                    customerId),
                StringComparison.Ordinal);
    }

    internal static bool
        TryClassifyObservedIncidentTransferExternalOperationId(
            V20ActiveEventSaveData occurrence,
            string externalOperationId,
            out bool medicalOrder)
    {
        medicalOrder = false;
        ObservedIncidentResponseSaveData response =
            occurrence?.observedIncidentResponse;
        if (response == null
            || !string.Equals(
                response.choiceId,
                "transfer",
                StringComparison.Ordinal)
            || !IsCanonicalRequiredId(externalOperationId))
        {
            return false;
        }
        if (TryParseObservedIncidentTransferOperationId(
                response.operationId,
                externalOperationId,
                out _,
                out _))
        {
            return true;
        }
        if (occurrence.hasObservedMealIncident != true
            || occurrence.observedMealIncident?.incidentKind
                != ServiceIncidentKind.MedicalCollapse)
        {
            return false;
        }

        const string medicalOrderPrefix = "medical:";
        if (!externalOperationId.StartsWith(
                medicalOrderPrefix,
                StringComparison.Ordinal))
        {
            return false;
        }
        string suffix = externalOperationId.Substring(
            medicalOrderPrefix.Length);
        medicalOrder = int.TryParse(
                suffix,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int sequence)
            && sequence > 0
            && string.Equals(
                suffix,
                sequence.ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal);
        return medicalOrder;
    }

    internal static string FormatObservedIncidentTransferOperationId(
        string responseOperationId,
        BuildingInstanceId facilityId,
        CoreGridCell destination)
    {
        if (!IsCanonicalRequiredId(responseOperationId)
            || !facilityId.IsValid)
        {
            throw new ArgumentException(
                "A canonical response operation and isolation facility are required.");
        }
        return responseOperationId
            + ":destination:"
            + destination.X.ToString(CultureInfo.InvariantCulture)
            + ":"
            + destination.Y.ToString(CultureInfo.InvariantCulture)
            + ":"
            + facilityId.Value;
    }

    internal static bool TryParseObservedIncidentTransferOperationId(
        string responseOperationId,
        string externalOperationId,
        out BuildingInstanceId facilityId,
        out CoreGridCell destination)
    {
        facilityId = default;
        destination = default;
        if (!IsCanonicalRequiredId(responseOperationId)
            || !IsCanonicalRequiredId(externalOperationId))
        {
            return false;
        }

        string prefix = responseOperationId + ":destination:";
        if (!externalOperationId.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }
        string payload = externalOperationId.Substring(prefix.Length);
        int firstSeparator = payload.IndexOf(':');
        int secondSeparator = firstSeparator < 0
            ? -1
            : payload.IndexOf(':', firstSeparator + 1);
        if (firstSeparator <= 0
            || secondSeparator <= firstSeparator + 1
            || secondSeparator >= payload.Length - 1
            || !int.TryParse(
                payload.Substring(0, firstSeparator),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int x)
            || !int.TryParse(
                payload.Substring(
                    firstSeparator + 1,
                    secondSeparator - firstSeparator - 1),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int y))
        {
            return false;
        }

        BuildingInstanceId parsedFacility = new(
            payload.Substring(secondSeparator + 1));
        CoreGridCell parsedDestination = new(x, y);
        if (!parsedFacility.IsValid
            || !string.Equals(
                externalOperationId,
                FormatObservedIncidentTransferOperationId(
                    responseOperationId,
                    parsedFacility,
                    parsedDestination),
                StringComparison.Ordinal))
        {
            return false;
        }

        facilityId = parsedFacility;
        destination = parsedDestination;
        return true;
    }

    internal static string FormatObservedIncidentTransferReceiptId(
        string externalOperationId,
        CharacterId customerId)
    {
        if (!IsCanonicalRequiredId(externalOperationId)
            || !customerId.IsValid)
        {
            throw new ArgumentException(
                "A canonical transfer operation and customer are required.");
        }
        return $"character-arrived:{externalOperationId}:{customerId.Value}";
    }

    internal static string ObservedIncidentResponseFailureResolutionId(
        ObservedIncidentResponsePhase phase) => phase switch
        {
            ObservedIncidentResponsePhase.Failed => "response-failed",
            ObservedIncidentResponsePhase.Cancelled => "response-cancelled",
            _ => throw new ArgumentOutOfRangeException(
                nameof(phase),
                phase,
                "Only failed or cancelled incident responses are terminal failures.")
        };

    internal static string FormatObservedIncidentResponseFailureNarrative(
        ObservedIncidentResponseSaveData response)
    {
        if (response == null
            || response.phase is not (
                ObservedIncidentResponsePhase.Failed
                or ObservedIncidentResponsePhase.Cancelled)
            || !IsCanonicalRequiredId(response.choiceId)
            || !IsCanonicalRequiredId(response.operationId)
            || string.IsNullOrWhiteSpace(response.failureReason))
        {
            throw new InvalidOperationException(
                "A terminal incident-response failure requires exact provenance.");
        }
        string externalOperationId = string.IsNullOrEmpty(
                response.externalOperationId)
            ? "없음"
            : response.externalOperationId;
        return $"선택={response.choiceId}; operation={response.operationId}; "
            + $"external-operation={externalOperationId}; "
            + $"failure={response.failureReason}";
    }

    internal static bool TryBuildObservedMealEvidence(
        ObservedMealIncidentSnapshot source,
        string definitionId,
        string occurrenceInstanceId,
        out ServiceIncidentKind incidentKind,
        out ObservedMealIncidentEvidenceSaveData evidence)
    {
        incidentKind = default;
        evidence = null;
        bool facilityValid = source.FacilityInstanceId.IsValid;
        if (!source.OperationId.IsValid
            || !source.TargetCharacterId.IsValid
            || !source.ItemDefinitionId.IsValid
            || !source.ItemStackId.IsValid
            || source.AbsoluteDay < 1
            || source.ObservedDead
            || !source.ObservedDowned
                && !source.ObservedContaminated
                && !source.ObservedPolicyViolation
            || source.FieldMeal == facilityValid)
        {
            return false;
        }

        incidentKind = source.ObservedDowned
            ? ServiceIncidentKind.MedicalCollapse
            : source.ObservedContaminated
                ? ServiceIncidentKind.Contamination
                : ServiceIncidentKind.ForbiddenMeal;
        string location = source.FieldMeal
            ? $"야전 식사 ({source.Location.X}, {source.Location.Y})"
            : $"시설 {source.FacilityInstanceId.Value} "
                + $"({source.Location.X}, {source.Location.Y})";
        string observedCause = incidentKind switch
        {
            ServiceIncidentKind.MedicalCollapse => "식사 직후 확인된 쓰러짐",
            ServiceIncidentKind.Contamination => "실제로 섭취한 식사의 오염 확인",
            _ => "실제 식사 정책 위반 확인"
        };
        observedCause += $" · 대상 {source.TargetCharacterId.Value}"
            + $" · 식사 {source.ItemDefinitionId.Value}"
            + $" · 스택 {source.ItemStackId.Value}"
            + $" · 장소 {location}";
        evidence = new ObservedMealIncidentEvidenceSaveData
        {
            sourceKind = ObservedIncidentSourceKind.Meal,
            operationId = source.OperationId.Value,
            occurrenceInstanceId = occurrenceInstanceId ?? string.Empty,
            definitionId = definitionId ?? string.Empty,
            incidentKind = incidentKind,
            targetCharacterId = source.TargetCharacterId.Value,
            itemDefinitionId = source.ItemDefinitionId.Value,
            itemStackId = source.ItemStackId.Value,
            facilityInstanceId = source.FieldMeal
                ? string.Empty
                : source.FacilityInstanceId.Value,
            fieldMeal = source.FieldMeal,
            locationX = source.Location.X,
            locationY = source.Location.Y,
            absoluteDay = source.AbsoluteDay,
            observedPolicyViolation = source.ObservedPolicyViolation,
            observedDowned = source.ObservedDowned,
            observedDead = source.ObservedDead,
            observedCause = observedCause
        };
        return true;
    }

    internal static bool TryBuildObservedShopliftingEvidence(
        ObservedShopliftingIncidentSnapshot source,
        string definitionId,
        string occurrenceInstanceId,
        out ObservedMealIncidentEvidenceSaveData evidence)
    {
        evidence = null;
        bool itemInstanceValid = string.IsNullOrEmpty(source.ItemInstanceId)
            || IsCanonicalRequiredId(source.ItemInstanceId);
        bool sourceStackValid = string.IsNullOrEmpty(source.SourceStackId)
            || IsCanonicalRequiredId(source.SourceStackId);
        bool componentFingerprintValid = string.IsNullOrEmpty(
                source.ComponentFingerprint)
            || string.Equals(
                source.ComponentFingerprint,
                source.ComponentFingerprint.Trim(),
                StringComparison.Ordinal);
        if (!IsCanonicalRequiredId(source.OperationId)
            || !IsCanonicalRequiredId(source.SourceOperationId)
            || !source.TargetCharacterId.IsValid
            || !source.ItemDefinitionId.IsValid
            || source.SaleItemId < 0
            || !itemInstanceValid
            || !sourceStackValid
            || source.Quantity != 1
            || source.UnitMassGrams <= 0L
            || !componentFingerprintValid
            || !source.FacilityInstanceId.IsValid
            || source.AbsoluteDay < 1
            || source.LossValue < 0)
        {
            return false;
        }

        string lotIdentity = !string.IsNullOrEmpty(source.ItemInstanceId)
            ? source.ItemInstanceId
            : !string.IsNullOrEmpty(source.SourceStackId)
                ? source.SourceStackId
                : source.SourceOperationId;
        evidence = new ObservedMealIncidentEvidenceSaveData
        {
            sourceKind = ObservedIncidentSourceKind.Shoplifting,
            operationId = source.OperationId,
            sourceOperationId = source.SourceOperationId,
            occurrenceInstanceId = occurrenceInstanceId ?? string.Empty,
            definitionId = definitionId ?? string.Empty,
            incidentKind = ServiceIncidentKind.Theft,
            targetCharacterId = source.TargetCharacterId.Value,
            itemDefinitionId = source.ItemDefinitionId.Value,
            itemStackId = string.Empty,
            saleItemId = source.SaleItemId,
            itemInstanceId = source.ItemInstanceId,
            sourceStackId = source.SourceStackId,
            quantity = source.Quantity,
            unitMassGrams = source.UnitMassGrams,
            componentFingerprint = source.ComponentFingerprint,
            lossValue = source.LossValue,
            facilityInstanceId = source.FacilityInstanceId.Value,
            fieldMeal = false,
            locationX = source.Location.X,
            locationY = source.Location.Y,
            absoluteDay = source.AbsoluteDay,
            observedPolicyViolation = false,
            observedDowned = false,
            observedDead = false,
            observedCause = "던전 상점 물품의 실제 절도 커밋"
                + $" · 대상 {source.TargetCharacterId.Value}"
                + $" · 품목 {source.ItemDefinitionId.Value}"
                + $" · lot {lotIdentity}"
                + $" · 수량 {source.Quantity}"
                + $" · 질량 {source.UnitMassGrams}g"
                + $" · 장소 시설 {source.FacilityInstanceId.Value} "
                + $"({source.Location.X}, {source.Location.Y})"
        };
        return true;
    }

    internal static bool TryBuildObservedBrawlEvidence(
        ObservedBrawlIncidentSnapshot source,
        string definitionId,
        string occurrenceInstanceId,
        out ObservedMealIncidentEvidenceSaveData evidence)
    {
        evidence = null;
        if (!IsCanonicalRequiredId(source.AttackOperationId)
            || !source.TargetCharacterId.IsValid
            || !source.OtherParticipantCharacterId.IsValid
            || source.TargetCharacterId.Equals(
                source.OtherParticipantCharacterId)
            || !float.IsFinite(source.ActualDamage)
            || source.ActualDamage <= 0f
            || !source.FacilityInstanceId.IsValid
            || source.AbsoluteDay < 1)
        {
            return false;
        }

        evidence = new ObservedMealIncidentEvidenceSaveData
        {
            sourceKind = ObservedIncidentSourceKind.Combat,
            operationId = source.AttackOperationId,
            sourceOperationId = source.AttackOperationId,
            occurrenceInstanceId = occurrenceInstanceId ?? string.Empty,
            definitionId = definitionId ?? string.Empty,
            incidentKind = ServiceIncidentKind.Brawl,
            targetCharacterId = source.TargetCharacterId.Value,
            otherParticipantCharacterId =
                source.OtherParticipantCharacterId.Value,
            customerWasAttacker = source.CustomerWasAttacker,
            actualDamage = source.ActualDamage,
            facilityInstanceId = source.FacilityInstanceId.Value,
            fieldMeal = false,
            locationX = source.Location.X,
            locationY = source.Location.Y,
            absoluteDay = source.AbsoluteDay,
            observedPolicyViolation = false,
            observedDowned = false,
            observedDead = false,
            observedCause = "방문 서비스 시설에서 확인된 실제 전투 피해"
                + " · 공격자 "
                + (source.CustomerWasAttacker
                    ? source.TargetCharacterId.Value
                    : source.OtherParticipantCharacterId.Value)
                + " · 피해자 "
                + (source.CustomerWasAttacker
                    ? source.OtherParticipantCharacterId.Value
                    : source.TargetCharacterId.Value)
                + " · 피해 "
                + source.ActualDamage.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture)
                + $" · 장소 시설 {source.FacilityInstanceId.Value} "
                + $"({source.Location.X}, {source.Location.Y})"
        };
        return true;
    }

    internal static bool TryBuildObservedCulturalConflictEvidence(
        ObservedCulturalConflictIncidentSnapshot source,
        string definitionId,
        string occurrenceInstanceId,
        out ObservedMealIncidentEvidenceSaveData evidence)
    {
        evidence = null;
        bool customerIsParticipant = source.CustomerCharacterId.Equals(
                source.InstigatorCharacterId)
            || source.CustomerCharacterId.Equals(source.TargetCharacterId);
        if (!IsCanonicalRequiredId(source.OperationId)
            || !source.InstigatorCharacterId.IsValid
            || !source.TargetCharacterId.IsValid
            || source.InstigatorCharacterId.Equals(source.TargetCharacterId)
            || !source.CustomerCharacterId.IsValid
            || !customerIsParticipant
            || !source.InstigatorCultureId.IsValid
            || !source.TargetCultureId.IsValid
            || source.InstigatorCultureId.Equals(source.TargetCultureId)
            || !source.FacilityInstanceId.IsValid
            || source.AbsoluteDay < 1)
        {
            return false;
        }

        CharacterId otherParticipant = source.CustomerCharacterId.Equals(
                source.InstigatorCharacterId)
            ? source.TargetCharacterId
            : source.InstigatorCharacterId;
        evidence = new ObservedMealIncidentEvidenceSaveData
        {
            sourceKind = ObservedIncidentSourceKind.SocialConflict,
            operationId = source.OperationId,
            sourceOperationId = source.OperationId,
            occurrenceInstanceId = occurrenceInstanceId ?? string.Empty,
            definitionId = definitionId ?? string.Empty,
            incidentKind = ServiceIncidentKind.CulturalInsult,
            targetCharacterId = source.CustomerCharacterId.Value,
            otherParticipantCharacterId = otherParticipant.Value,
            instigatorCharacterId = source.InstigatorCharacterId.Value,
            instigatorCultureId = source.InstigatorCultureId.Value,
            targetCultureId = source.TargetCultureId.Value,
            facilityInstanceId = source.FacilityInstanceId.Value,
            fieldMeal = false,
            locationX = source.Location.X,
            locationY = source.Location.Y,
            absoluteDay = source.AbsoluteDay,
            observedCause = "실제 사회 갈등 효과 커밋"
                + $" · 행위자 {source.InstigatorCharacterId.Value}"
                + $" ({source.InstigatorCultureId.Value})"
                + $" · 상대 {source.TargetCharacterId.Value}"
                + $" ({source.TargetCultureId.Value})"
                + $" · 장소 시설 {source.FacilityInstanceId.Value} "
                + $"({source.Location.X}, {source.Location.Y})"
        };
        return true;
    }

    internal static bool SameObservedMealPayload(
        ObservedMealIncidentEvidenceSaveData left,
        ObservedMealIncidentEvidenceSaveData right) =>
        left != null
        && right != null
        && string.Equals(left.operationId, right.operationId, StringComparison.Ordinal)
        && string.Equals(left.sourceOperationId, right.sourceOperationId, StringComparison.Ordinal)
        && left.incidentKind == right.incidentKind
        && string.Equals(left.targetCharacterId, right.targetCharacterId, StringComparison.Ordinal)
        && string.Equals(left.itemDefinitionId, right.itemDefinitionId, StringComparison.Ordinal)
        && string.Equals(left.itemStackId, right.itemStackId, StringComparison.Ordinal)
        && left.sourceKind == right.sourceKind
        && left.saleItemId == right.saleItemId
        && string.Equals(left.itemInstanceId, right.itemInstanceId, StringComparison.Ordinal)
        && string.Equals(left.sourceStackId, right.sourceStackId, StringComparison.Ordinal)
        && left.quantity == right.quantity
        && left.unitMassGrams == right.unitMassGrams
        && string.Equals(left.componentFingerprint, right.componentFingerprint, StringComparison.Ordinal)
        && left.lossValue == right.lossValue
        && string.Equals(
            left.otherParticipantCharacterId,
            right.otherParticipantCharacterId,
            StringComparison.Ordinal)
        && string.Equals(
            left.instigatorCharacterId,
            right.instigatorCharacterId,
            StringComparison.Ordinal)
        && string.Equals(
            left.instigatorCultureId,
            right.instigatorCultureId,
            StringComparison.Ordinal)
        && string.Equals(
            left.targetCultureId,
            right.targetCultureId,
            StringComparison.Ordinal)
        && left.customerWasAttacker == right.customerWasAttacker
        && left.actualDamage.Equals(right.actualDamage)
        && string.Equals(left.facilityInstanceId, right.facilityInstanceId, StringComparison.Ordinal)
        && left.fieldMeal == right.fieldMeal
        && left.locationX == right.locationX
        && left.locationY == right.locationY
        && left.absoluteDay == right.absoluteDay
        && left.observedPolicyViolation == right.observedPolicyViolation
        && left.observedDowned == right.observedDowned
        && left.observedDead == right.observedDead;

    internal static bool SameObservedMealEvidence(
        ObservedMealIncidentEvidenceSaveData left,
        ObservedMealIncidentEvidenceSaveData right) =>
        SameObservedMealPayload(left, right)
        && string.Equals(
            left.occurrenceInstanceId,
            right.occurrenceInstanceId,
            StringComparison.Ordinal)
        && string.Equals(
            left.definitionId,
            right.definitionId,
            StringComparison.Ordinal)
        && string.Equals(
            left.observedCause,
            right.observedCause,
            StringComparison.Ordinal);

    internal static bool IsObservedLifeEventDefinition(string definitionId) =>
        string.Equals(
            definitionId,
            GraveVisitLifeEventId,
            StringComparison.Ordinal)
        || string.Equals(
            definitionId,
            StoryCompressedLifeEventId,
            StringComparison.Ordinal)
        || string.Equals(
            definitionId,
            ApprenticeMistakeLifeEventId,
            StringComparison.Ordinal)
        || string.Equals(
            definitionId,
            LastLessonLifeEventId,
            StringComparison.Ordinal)
        || string.Equals(
            definitionId,
            QuietPromotionLifeEventId,
            StringComparison.Ordinal);

    internal static void RequireValidObservedIncidentResponse(
        V20ActiveEventSaveData occurrence)
    {
        ObservedIncidentResponseSaveData response =
            occurrence?.observedIncidentResponse;
        if (response == null)
        {
            throw new InvalidOperationException(
                $"Service incident '{occurrence?.instanceId}' has no response owner state.");
        }
        if (!Enum.IsDefined(
                typeof(ObservedIncidentResponsePhase),
                response.phase))
        {
            throw new InvalidOperationException(
                $"Service incident '{occurrence.instanceId}' has an unknown response phase.");
        }

        bool emptyChoice = string.IsNullOrEmpty(response.choiceId);
        bool emptyOperation = string.IsNullOrEmpty(response.operationId);
        bool emptyExternal = string.IsNullOrEmpty(
            response.externalOperationId);
        bool emptyReceipt = string.IsNullOrEmpty(response.receiptId);
        bool emptyFailure = string.IsNullOrEmpty(response.failureReason);
        if (response.phase == ObservedIncidentResponsePhase.None)
        {
            if (!emptyChoice || !emptyOperation || !emptyExternal
                || !emptyReceipt || !emptyFailure)
            {
                throw new InvalidOperationException(
                    $"Service incident '{occurrence.instanceId}' has a non-empty idle response owner.");
            }
            return;
        }

        ServiceIncidentKind kind = occurrence.observedMealIncident.incidentKind;
        bool supportedChoice = kind == ServiceIncidentKind.ForbiddenMeal
                && string.Equals(
                    response.choiceId,
                    "replace",
                    StringComparison.Ordinal)
            || kind == ServiceIncidentKind.MedicalCollapse
                && (string.Equals(
                        response.choiceId,
                        "emergency-care",
                        StringComparison.Ordinal)
                    || string.Equals(
                        response.choiceId,
                        "transfer",
                        StringComparison.Ordinal));
        string expectedOperation = supportedChoice
            ? FormatObservedIncidentResponseOperationId(
                occurrence.instanceId,
                response.choiceId)
            : string.Empty;
        bool identityValid = supportedChoice
            && !emptyChoice
            && !emptyOperation
            && string.Equals(
                response.operationId,
                expectedOperation,
                StringComparison.Ordinal)
            && string.Equals(
                occurrence.selectedChoiceId,
                response.choiceId,
                StringComparison.Ordinal);
        bool externalIdentityValid = emptyExternal
            || string.Equals(response.choiceId, "replace", StringComparison.Ordinal)
                && string.Equals(
                    response.externalOperationId,
                    response.operationId,
                    StringComparison.Ordinal)
            || string.Equals(
                    response.choiceId,
                    "emergency-care",
                    StringComparison.Ordinal)
                && IsCanonicalRequiredId(response.externalOperationId)
            || string.Equals(response.choiceId, "transfer", StringComparison.Ordinal)
                && TryClassifyObservedIncidentTransferExternalOperationId(
                    occurrence,
                    response.externalOperationId,
                    out _);
        bool receiptIdentityValid = emptyReceipt
            || IsExpectedObservedIncidentResponseReceipt(
                occurrence,
                response.externalOperationId,
                response.receiptId);
        bool shapeValid = response.phase switch
        {
            ObservedIncidentResponsePhase.Accepted =>
                !occurrence.resolved
                && emptyExternal && emptyReceipt && emptyFailure,
            ObservedIncidentResponsePhase.ExternalOperationStarted =>
                !occurrence.resolved
                && !emptyExternal && emptyReceipt && emptyFailure,
            ObservedIncidentResponsePhase.ReceiptCommitted =>
                !occurrence.resolved
                && !emptyExternal && !emptyReceipt && emptyFailure,
            ObservedIncidentResponsePhase.Failed =>
                occurrence.resolved
                && emptyReceipt && !emptyFailure
                && string.Equals(
                    occurrence.resolutionId,
                    ObservedIncidentResponseFailureResolutionId(
                        response.phase),
                    StringComparison.Ordinal)
                && occurrence.terminalEffectKinds.Count == 0
                && string.Equals(
                    occurrence.terminalActionLabel,
                    "현장 대응 실패",
                    StringComparison.Ordinal)
                && string.Equals(
                    occurrence.terminalOutcomeNarrative,
                    FormatObservedIncidentResponseFailureNarrative(response),
                    StringComparison.Ordinal),
            ObservedIncidentResponsePhase.Cancelled =>
                occurrence.resolved
                && emptyReceipt && !emptyFailure
                && string.Equals(
                    occurrence.resolutionId,
                    ObservedIncidentResponseFailureResolutionId(
                        response.phase),
                    StringComparison.Ordinal)
                && occurrence.terminalEffectKinds.Count == 0
                && string.Equals(
                    occurrence.terminalActionLabel,
                    "현장 대응 취소",
                    StringComparison.Ordinal)
                && string.Equals(
                    occurrence.terminalOutcomeNarrative,
                    FormatObservedIncidentResponseFailureNarrative(response),
                    StringComparison.Ordinal),
            ObservedIncidentResponsePhase.EffectsPublished =>
                occurrence.resolved
                && !emptyExternal && !emptyReceipt && emptyFailure
                && string.Equals(
                    occurrence.resolutionId,
                    response.choiceId,
                    StringComparison.Ordinal),
            _ => false
        };
        if (!identityValid
            || !externalIdentityValid
            || !receiptIdentityValid
            || !shapeValid
            || !emptyExternal
                && !IsCanonicalRequiredId(response.externalOperationId)
            || !emptyReceipt && !IsCanonicalRequiredId(response.receiptId)
            || !emptyFailure
                && !string.Equals(
                    response.failureReason,
                    response.failureReason.Trim(),
                    StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Service incident '{occurrence.instanceId}' has an invalid response owner state.");
        }
    }

    internal static bool HasObservedIncidentResponsePayload(
        ObservedIncidentResponseSaveData response) =>
        response != null
        && (response.phase != ObservedIncidentResponsePhase.None
            || !string.IsNullOrEmpty(response.choiceId)
            || !string.IsNullOrEmpty(response.operationId)
            || !string.IsNullOrEmpty(response.externalOperationId)
            || !string.IsNullOrEmpty(response.receiptId)
            || !string.IsNullOrEmpty(response.failureReason));

    internal static bool HasObservedMealIncidentPayload(
        ObservedMealIncidentEvidenceSaveData evidence) =>
        evidence != null
        && (!string.IsNullOrEmpty(evidence.operationId)
            || !string.IsNullOrEmpty(evidence.sourceOperationId)
            || !string.IsNullOrEmpty(evidence.occurrenceInstanceId)
            || !string.IsNullOrEmpty(evidence.definitionId)
            || evidence.incidentKind != default
            || !string.IsNullOrEmpty(evidence.targetCharacterId)
            || !string.IsNullOrEmpty(evidence.itemDefinitionId)
            || !string.IsNullOrEmpty(evidence.itemStackId)
            || evidence.sourceKind != ObservedIncidentSourceKind.Meal
            || evidence.saleItemId != 0
            || !string.IsNullOrEmpty(evidence.itemInstanceId)
            || !string.IsNullOrEmpty(evidence.sourceStackId)
            || evidence.quantity != 0
            || evidence.unitMassGrams != 0L
            || !string.IsNullOrEmpty(evidence.componentFingerprint)
            || evidence.lossValue != 0
            || !string.IsNullOrEmpty(
                evidence.otherParticipantCharacterId)
            || !string.IsNullOrEmpty(evidence.instigatorCharacterId)
            || !string.IsNullOrEmpty(evidence.instigatorCultureId)
            || !string.IsNullOrEmpty(evidence.targetCultureId)
            || evidence.customerWasAttacker
            || evidence.actualDamage != 0f
            || !string.IsNullOrEmpty(evidence.facilityInstanceId)
            || evidence.fieldMeal
            || evidence.locationX != 0
            || evidence.locationY != 0
            || evidence.absoluteDay != 0
            || evidence.observedPolicyViolation
            || evidence.observedDowned
            || evidence.observedDead
            || !string.IsNullOrEmpty(evidence.observedCause));
}
