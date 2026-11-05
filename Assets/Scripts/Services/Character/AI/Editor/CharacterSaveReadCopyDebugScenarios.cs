#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

// 저장 DTO 값과 독립 소유권만 검사한다. 게임 상태를 복원하거나 모델 답을 주입하지 않는다.
public static class CharacterSaveReadCopyDebugScenarios
{
    private static readonly Type[] CopiedTypes =
    {
        typeof(global::CharacterAcquiredTraitAggregateState),
        typeof(global::CharacterAcquiredTraitEffectOverride),
        typeof(global::CharacterAcquiredTraitFormulaCapabilityEnvelope),
        typeof(global::CharacterAcquiredTraitFormulaParameter),
        typeof(global::CharacterAcquiredTraitInstanceState),
        typeof(global::CharacterAcquiredTraitModuleOfferState),
        typeof(global::CharacterAcquiredTraitPendingRequestState),
        typeof(global::CharacterAcquiredTraitPresentationOriginState),
        typeof(global::CharacterAcquiredTraitReactionState),
        typeof(global::CharacterAcquiredTraitRequestInvalidationState),
        typeof(global::CharacterAutomaticSkillActivationState),
        typeof(global::CharacterCarriedItemSaveData),
        typeof(global::CharacterCarryInventorySaveData),
        typeof(global::CharacterEventContext),
        typeof(global::CharacterExpeditionRecoveryState),
        typeof(global::CharacterGrowthState),
        typeof(global::CharacterManualSkillBuffState),
        typeof(global::CharacterManualSkillCooldownState),
        typeof(global::CharacterNarrativeFact),
        typeof(global::CharacterNarrativeLedger),
        typeof(global::CharacterNarrativeRequestOwnerBinding),
        typeof(global::CharacterRestAppliedFactData),
        typeof(global::CharacterRestObservationSaveData),
        typeof(global::CharacterSkillActivationCommitState),
        typeof(global::CharacterSkillCandidateRule),
        typeof(global::CharacterSkillDraft),
        typeof(global::CharacterSkillDrawbackEffectEnvelope),
        typeof(global::CharacterSkillFormulaCapabilityEnvelope),
        typeof(global::CharacterSkillFormulaParameter),
        typeof(global::CharacterSkillInstance),
        typeof(global::CharacterSkillModuleOfferState),
        typeof(global::CharacterSkillModuleSelection),
        typeof(global::CharacterSkillRequestContextSnapshot),
        typeof(global::CharacterSkillUseLimitState),
        typeof(global::CharacterSocialMemorySnapshot),
        typeof(global::CharacterStartingProficiencyExperience),
        typeof(global::CharacterStartingProfileState),
        typeof(global::CharacterVisitorExitOperationSaveData),
        typeof(global::DungeonCharacterConditionSaveData),
        typeof(global::DungeonCharacterMoodFactorSaveData),
        typeof(global::DungeonCharacterPersonaNarrativeSaveData),
        typeof(global::DungeonCharacterSaveData),
        typeof(global::DungeonCharacterWorkPrioritySaveData),
        typeof(global::DungeonCharacterWorldSaveData),
        typeof(global::GameplayExperienceAssessmentSnapshot),
        typeof(global::GameplayNarrativeEventContext),
        typeof(global::GameplayOutcomeEvidenceBindingSnapshot),
        typeof(global::GameplayOutcomeEvidenceFactBindingSnapshot),
        typeof(global::GameplayOutcomeEvidenceMetricBindingSnapshot),
        typeof(global::GameplayOutcomeEvidenceParticipantBindingSnapshot),
        typeof(global::GameplayOutcomeEvidenceProvenanceBindingSnapshot),
        typeof(global::GlobalFacilityReputationSnapshot),
        typeof(global::HaulDeliveryIntentSaveData),
        typeof(global::HaulDeliveryItemCommitmentSaveData),
        typeof(global::ItemInstanceComponentSaveData),
        typeof(global::ItemStateValueSaveData),
        typeof(global::NarrativeGenerationTrace),
        typeof(global::NarrativePublicContextMaterialSnapshot),
        typeof(global::NarrativePublicEntitySnapshot),
        typeof(global::NarrativePublicEventSnapshot),
        typeof(global::NarrativePublicFactSnapshot),
        typeof(global::NarrativePublicMotifSnapshot),
        typeof(global::RetailPurchaseRecoverySaveData),
        typeof(global::RetailStockComponentSnapshot),
        typeof(global::RetailStockComponentValueSnapshot),
        typeof(global::RetailStockLotSnapshot),
        typeof(global::SocialMemoryFloat),
        typeof(global::SocialRumorSnapshot),
        typeof(global::WarehouseHaulAdmissionSaveData),
        typeof(global::WorldCharacterProfile)
    };

    public static void VerifyForBatch()
    {
        var reachable = new HashSet<Type>();
        VisitContract(typeof(DungeonCharacterWorldSaveData), reachable);
        Require(reachable.SetEquals(CopiedTypes) && reachable.Count == 70
                && reachable.Sum(type => Fields(type).Length) == 672,
            "Character serialized field graph changed; review the exact copy contract.");
        var source = (DungeonCharacterWorldSaveData)Populate(typeof(DungeonCharacterWorldSaveData), "characters", 0);
        var copy = (DungeonCharacterWorldSaveData)((IDungeonSaveReadSnapshot)source).CopyForSaveRead();
        AssertCopy(typeof(DungeonCharacterWorldSaveData), source, copy, "characters", 0);
        Require(Serialize(source) == Serialize(copy), "Field-only character JSON changed during copy.");

        foreach (Type type in CopiedTypes)
        {
            object nullable = Populate(type, "nullable", 0);
            foreach (FieldInfo field in Fields(type))
            {
                if (!field.FieldType.IsValueType) field.SetValue(nullable, null);
                else if (field.FieldType == typeof(float))
                    field.SetValue(nullable, BitConverter.ToSingle(BitConverter.GetBytes(unchecked((int)0x7fc01234)), 0));
                else if (field.FieldType == typeof(double))
                    field.SetValue(nullable, BitConverter.Int64BitsToDouble(unchecked((long)0x8000000000000000)));
            }
            object nullableCopy = CopyExact(type, nullable);
            AssertCopy(type, nullable, nullableCopy, type.FullName + ".null", 0);
        }

        string json = Serialize(source);
        long parses = DungeonSaveJson.EditorSourceParseCount;
        long replays = DungeonSaveJson.EditorReplayCount;
        long copies = DungeonSaveJson.EditorSnapshotCopyCount;
        using (DungeonSaveJson.BeginReadScope())
        {
            var first = DungeonSaveJson.Deserialize<DungeonCharacterWorldSaveData>(json);
            first.actors[0].displayName = "mutated-first-read";
            first.actors.Clear();
            first.populationProfiles.Clear();
            var second = DungeonSaveJson.Deserialize<DungeonCharacterWorldSaveData>(json);
            Require(DungeonSaveJson.EditorSourceParseCount == parses + 1
                    && DungeonSaveJson.EditorReplayCount == replays + 1
                    && DungeonSaveJson.EditorSnapshotCopyCount == copies + 2,
                "Character payload did not use the operation-local exact snapshot path.");
            AssertCopy(typeof(DungeonCharacterWorldSaveData), source, second, "later-read", 0);
        }
        using (DungeonSaveJson.BeginReadScope())
        {
            var fresh = DungeonSaveJson.Deserialize<DungeonCharacterWorldSaveData>(json);
            Require(DungeonSaveJson.EditorSourceParseCount == parses + 2,
                "Character snapshot escaped its save operation lifetime.");
            AssertCopy(typeof(DungeonCharacterWorldSaveData), source, fresh, "new-operation", 0);
        }
        const string malformed = "{\"actors\":[{\"dataId\":\"not-an-int\"}]}";
        int rejected = 0;
        parses = DungeonSaveJson.EditorSourceParseCount;
        using (DungeonSaveJson.BeginReadScope())
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                try { DungeonSaveJson.Deserialize<DungeonCharacterWorldSaveData>(malformed); }
                catch (Newtonsoft.Json.JsonException) { rejected++; }
            }
        }
        Require(rejected == 2 && DungeonSaveJson.EditorSourceParseCount == parses + 2,
            "Malformed character data was cached or hidden by the copy path.");
    }

    private static FieldInfo[] Fields(Type type) => type.GetFields(BindingFlags.Instance | BindingFlags.Public)
        .Where(field => !field.IsInitOnly && !field.IsDefined(typeof(NonSerializedAttribute), true)
            && !field.IsDefined(typeof(Newtonsoft.Json.JsonIgnoreAttribute), true)).ToArray();

    private static string Serialize(object value)
    {
        Type codec = typeof(DungeonCharacterWorldSaveData).Assembly.GetType("DungeonCharacterWorldSaveCodec", true);
        MethodInfo method = codec.GetMethod("SerializeFields", BindingFlags.Static | BindingFlags.NonPublic,
            null, new[] { typeof(object) }, null);
        Require(method != null, "Authoritative character field codec not found.");
        return (string)method.Invoke(null, new[] { value });
    }

    private static object CopyExact(Type type, object source)
    {
        Type copier = typeof(DungeonCharacterWorldSaveData).Assembly.GetType("DungeonCharacterSaveReadCopy", true);
        MethodInfo method = copier.GetMethod("Copy", BindingFlags.Static | BindingFlags.NonPublic,
            null, new[] { type }, null);
        Require(method != null, "Missing typed character copy for " + type.FullName);
        return method.Invoke(null, new[] { source });
    }

    private static void VisitContract(Type type, HashSet<Type> seen)
    {
        if (type == typeof(string) || type.IsPrimitive || type.IsEnum) return;
        if (type.IsArray) { Require(type.GetArrayRank() == 1, "Unexpected DTO array rank."); VisitContract(type.GetElementType(), seen); return; }
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        { VisitContract(type.GetGenericArguments()[0], seen); return; }
        Require(type.IsClass && type.IsSealed && type.IsDefined(typeof(SerializableAttribute), false),
            "Unknown character save copy type: " + type.FullName);
        if (!seen.Add(type)) return;
        foreach (FieldInfo field in Fields(type)) VisitContract(field.FieldType, seen);
    }

    private static object Populate(Type type, string path, int depth)
    {
        Require(depth <= 24, "Unexpected recursive character DTO: " + path);
        if (type == typeof(string)) return "  qa:" + path + "  ";
        if (type == typeof(bool)) return true;
        if (type == typeof(int)) return -17;
        if (type == typeof(long)) return -41L;
        if (type == typeof(float)) return 1.25f;
        if (type == typeof(double)) return 0.10000000000000002d;
        if (type.IsEnum) return Enum.ToObject(type, -17);
        if (type.IsArray)
        {
            Require(type.GetArrayRank() == 1, "Unexpected DTO array rank.");
            Type element = type.GetElementType();
            Array array = Array.CreateInstance(element, 3);
            array.SetValue(Populate(element, path + "[0]", depth + 1), 0);
            array.SetValue(Populate(element, path + "[1]", depth + 1), 1);
            return array;
        }
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            IList list = (IList)Activator.CreateInstance(type);
            Type element = type.GetGenericArguments()[0];
            list.Add(Populate(element, path + "[0]", depth + 1));
            list.Add(Populate(element, path + "[1]", depth + 1));
            if (element.IsClass) list.Add(null);
            return list;
        }
        Require(CopiedTypes.Contains(type), "Unknown character fixture type: " + type.FullName);
        object instance = type == typeof(SocialMemoryFloat)
            ? new SocialMemoryFloat(null, 0f) : Activator.CreateInstance(type);
        foreach (FieldInfo field in Fields(type))
            field.SetValue(instance, Populate(field.FieldType, path + "." + field.Name, depth + 1));
        return instance;
    }

    private static void AssertCopy(Type type, object source, object copy, string path, int depth)
    {
        Require(depth <= 24, "Unexpected recursive character copy: " + path);
        if (source == null || copy == null)
        { Require(source == null && copy == null, "Null changed at " + path); return; }
        if (type == typeof(float))
        { Require(BitConverter.ToInt32(BitConverter.GetBytes((float)source), 0) == BitConverter.ToInt32(BitConverter.GetBytes((float)copy), 0), "Float bits changed at " + path); return; }
        if (type == typeof(double))
        { Require(BitConverter.DoubleToInt64Bits((double)source) == BitConverter.DoubleToInt64Bits((double)copy), "Double bits changed at " + path); return; }
        if (type == typeof(string) || type.IsPrimitive || type.IsEnum)
        { Require(source.Equals(copy), "Scalar changed at " + path); return; }
        Require(!ReferenceEquals(source, copy), "Mutable reference shared at " + path);
        if (type.IsArray || type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            IList left = (IList)source, right = (IList)copy;
            Require(left.Count == right.Count, "Collection length changed at " + path);
            Type element = type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0];
            for (int i = 0; i < left.Count; i++) AssertCopy(element, left[i], right[i], path + "[" + i + "]", depth + 1);
            return;
        }
        foreach (FieldInfo field in Fields(type))
            AssertCopy(field.FieldType, field.GetValue(source), field.GetValue(copy), path + "." + field.Name, depth + 1);
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
#endif
