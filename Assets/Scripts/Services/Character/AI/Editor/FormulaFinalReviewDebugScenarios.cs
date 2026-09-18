#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

// Consumer-boundary regressions: a successful formula replay alone is not activation evidence.
public static class FormulaFinalReviewDebugScenarios
{
    public static bool RunArchivedV5RestoreScenario()
    {
        CharacterSkillSystemSettingsSO settings = EditorCharacterSkillSettingsFactory.CreateTransientDefaults();
        try
        {
            // Values/cost copied from the immutable Phase74 r2 C# resolution, row 1.
            // Expectations are not calculated by the new formula under test.
            CharacterSkillInstance old = new CharacterSkillInstance
            {
                id = "skill:qa:archive-v5", ruleId = "qa:archive-v5", displayName = "방어 기억",
                kind = CharacterSkillKind.Active, trigger = CharacterSkillTrigger.ManualCombat,
                target = CharacterSkillTarget.Self, targetingMode = CharacterSkillTargetingMode.Self,
                effectArea = CharacterSkillEffectArea.Single, areaSize = 1,
                formulaVersion = 5,
                formulaCatalogSha256 = "adb5e6fea3a46e2e33e1e03bb89120a923811d7099f3b9c5b6fea99397b76333",
                calculatedCost = 5, positiveCost = 5, narrativeBudget = 5,
                presentationId = "presentation:qa:archive-v5", mechanicalDescription = "방어 15%, 2턴",
                narrativeFlavor = "옛 전투의 방어 경험이 남아 있다.",
                evidenceIds = new List<string> { "qa:archive:combat-completed" },
                modules = new List<CharacterSkillModuleSelection> { new CharacterSkillModuleSelection { moduleId = "guard" } },
                formulaCapabilities = new List<CharacterSkillFormulaCapabilityEnvelope>
                {
                    new CharacterSkillFormulaCapabilityEnvelope
                    {
                        capabilityId = "guard", formatterId = "guard", applicatorId = "guard",
                        parameters = new List<CharacterSkillFormulaParameter>
                        {
                            new CharacterSkillFormulaParameter { parameterId = "count", units = 1 },
                            new CharacterSkillFormulaParameter { parameterId = "duration", units = 2 },
                            new CharacterSkillFormulaParameter { parameterId = "magnitude", units = 150 },
                            new CharacterSkillFormulaParameter { parameterId = "targetCount", units = 1 }
                        }
                    }
                }
            };
            string before = JsonUtility.ToJson(old);
            CharacterSkillInstance restored = JsonUtility.FromJson<CharacterSkillInstance>(before);
            CharacterSkillFormulaGeneration.ValidateRestoredFormulaSkill(restored, settings);
            Require(JsonUtility.ToJson(restored) == before, "Restoring an archived skill mutated frozen values.");
            CharacterSkillInstance fakeAbsentEffect = restored.Clone();
            fakeAbsentEffect.drawbackEffect ??= new CharacterSkillDrawbackEffectEnvelope();
            fakeAbsentEffect.drawbackEffect.value = 2f;
            RequireRejected(() => CharacterSkillFormulaGeneration.ValidateRestoredFormulaSkill(fakeAbsentEffect, settings),
                "A nondefault anonymous stat effect was accepted as serialized absence.");
            CharacterSkillInstance cooldownOnly = old.Clone();
            cooldownOnly.drawbackId = "character-skill:cooldown:+1";
            cooldownOnly.cooldownTurns = 1;
            cooldownOnly.narrativeBudget = 8;
            cooldownOnly.drawbackCredit = 1;
            cooldownOnly.calculatedCost = 4;
            cooldownOnly.drawbackEvidenceQualified = true;
            string cooldownJson = JsonUtility.ToJson(cooldownOnly);
            cooldownOnly = JsonUtility.FromJson<CharacterSkillInstance>(cooldownJson);
            CharacterSkillFormulaGeneration.ValidateRestoredFormulaSkill(cooldownOnly, settings);
            Require(JsonUtility.ToJson(cooldownOnly) == cooldownJson,
                "Cooldown-only serialized absence changed frozen data.");
            Require(Math.Abs(Convert.ToDouble(CharacterSkillFormulaGeneration.RequireParameter(restored,
                        "guard", "magnitude", settings)) - .15d) < .000001d,
                "Archived application reinterpreted the stored v5 guard magnitude.");
            Require(Convert.ToDouble(CharacterSkillFormulaGeneration.RequireParameter(restored,
                        "guard", "duration", settings)) == 2d,
                "Archived application reinterpreted the stored v5 duration.");
            restored.formulaCapabilities[0].parameters.Single(value => value.parameterId == "magnitude").units = 151;
            RequireRejected(() => CharacterSkillFormulaGeneration.ValidateRestoredFormulaSkill(restored, settings),
                "Off-grid archived numbers were accepted.");
            restored = old.Clone();
            restored.formulaVersion = 4;
            RequireRejected(() => CharacterSkillFormulaGeneration.ValidateRestoredFormulaSkill(restored, settings),
                "A catalog hash attached to the wrong formula version was accepted.");
            CharacterSkillInstance legacy = new CharacterSkillInstance { formulaVersion = 0 };
            string legacyBefore = JsonUtility.ToJson(legacy);
            CharacterSkillFormulaGeneration.ValidateRestoredFormulaSkill(legacy, settings);
            Require(JsonUtility.ToJson(legacy) == legacyBefore, "Legacy v0 was changed.");
            // Exact row-1 observations from the archived v2/r8, v3/Phase70-r3 and v4/Phase73-r1 resolutions.
            (int Version, string Hash)[] historical =
            {
                (2, "5d165ab9872c014af6b3d32a0154a214f113c5c819714029593e82fb1a9efe83"),
                (3, "fe24f7022b216b457cef56fe1c7bad2b4015c6b6785d46d40252e8c662ee3b60"),
                (4, "3b80dc59ad54d3f6952e293889c8e77262d7fa64200d7e92f966ca0f7b40d4e4")
            };
            foreach ((int version, string hash) in historical)
            {
                CharacterSkillInstance archived = old.Clone();
                archived.formulaVersion = version;
                archived.formulaCatalogSha256 = hash;
                archived.positiveCost = archived.calculatedCost = 4;
                archived.formulaCapabilities[0].parameters.Single(value => value.parameterId == "magnitude").units = 450;
                string encoded = JsonUtility.ToJson(archived);
                archived = JsonUtility.FromJson<CharacterSkillInstance>(encoded);
                CharacterSkillFormulaGeneration.ValidateRestoredFormulaSkill(archived, settings);
                Require(JsonUtility.ToJson(archived) == encoded
                    && Math.Abs(CharacterSkillFormulaGeneration.RequireParameter(archived,
                        "guard", "magnitude", settings) - .45f) < .000001f,
                    "Archived v" + version + " changed its exact numeric/application values.");
            }
            CharacterSkillInstance burden = old.Clone();
            burden.narrativeBudget = 8;
            burden.drawbackCredit = 1;
            burden.calculatedCost = 4;
            burden.drawbackEvidenceQualified = true;
            burden.drawbackId = "character-skill:drawback:combat-power:+1";
            burden.drawbackEffect = new CharacterSkillDrawbackEffectEnvelope
            {
                drawbackModuleId = "character-skill:drawback:combat-power",
                effectId = "effect:character:combat-power:multiply",
                targetId = "character:combat-power", operation = GameplayEffectOperation.Multiply,
                value = .97f, severityUnits = 1, displayName = "전투 후유증"
            };
            CharacterSkillFormulaGeneration.ValidateRestoredFormulaSkill(burden, settings);
            foreach (float nonFinite in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                CharacterSkillInstance invalid = burden.Clone();
                invalid.drawbackEffect.value = nonFinite;
                RequireRejected(() => CharacterSkillFormulaGeneration.ValidateRestoredFormulaSkill(invalid, settings),
                    "Non-finite archived stat effects must be rejected.");
            }
            burden.formulaVersion = settings.RequireFormulaPolicy().FormulaVersion;
            burden.formulaCatalogSha256 = settings.formulaPolicy.RequireCatalogSha256();
            CharacterSkillFormulaGeneration.ValidateRestoredFormulaSkill(burden, settings);
            CharacterSkillInstance unsupportedCurrentContext = burden.Clone();
            unsupportedCurrentContext.kind = CharacterSkillKind.Passive;
            unsupportedCurrentContext.trigger = CharacterSkillTrigger.BattleCompleted;
            RequireRejected(() => CharacterSkillFormulaGeneration.ValidateRestoredFormulaSkill(unsupportedCurrentContext, settings),
                "A current saved skill bypassed the runtime-context eligibility gate.");
            foreach (float nonFinite in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                CharacterSkillInstance invalid = burden.Clone();
                invalid.drawbackEffect.value = nonFinite;
                RequireRejected(() => CharacterSkillFormulaGeneration.ValidateRestoredFormulaSkill(invalid, settings),
                    "Non-finite current stat effects must be rejected.");
            }
            Debug.Log("FormulaFinalReview: archived v2-v5 numeric/application preservation, off-grid/hash-version rejection and v0 preservation passed.");
            return true;
        }
        finally { UnityEngine.Object.DestroyImmediate(settings); }
    }

    public static bool RunEffectiveBoundsAndManualActivation()
    {
        CharacterSkillSystemSettingsSO settings = EditorCharacterSkillSettingsFactory.CreateTransientDefaults();
        Type fixtureType = typeof(CharacterProgressionDebugScenarios).GetNestedType("ActorFixture", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("The existing progression test fixture is missing.");
        using IDisposable fixture = (IDisposable)Activator.CreateInstance(fixtureType,
            new object[] { 8976, "공식 검증자", "human", false, "character:qa-formula-final" });
        CharacterActor actor = (CharacterActor)fixtureType.GetProperty("Actor").GetValue(fixture);
        try
        {
            Require(settings.RequireFormulaPolicy().FormulaVersion >= 6, "Current formula must be v6 or newer.");
            NarrativeFormulaCapabilityDescriptor reposition = settings.RequireFormulaDescriptor(settings.FindModule("reposition"));
            NarrativeFormulaCapabilityDescriptor revenue = settings.RequireFormulaDescriptor(settings.FindModule("revenue"));
            Require(reposition.RequireRange("magnitude").ToDecimal(reposition.RequireRange("magnitude").MaximumUnits) == 2m,
                "Reposition charges for ranks beyond the three-rank runtime formation.");
            Require(revenue.RequireRange("magnitude").ToDecimal(revenue.RequireRange("magnitude").MaximumUnits) == .15m,
                "Revenue charges above the unchanged 15% worker revenue premium.");
            NarrativeFormulaCapabilityDescriptor guard = settings.RequireFormulaDescriptor(settings.FindModule("guard"));
            NarrativeFormulaCapabilityDescriptor protect = settings.RequireFormulaDescriptor(settings.FindModule("protect"));
            Require(guard.ForbiddenSynergies.Contains("protect") || protect.ForbiddenSynergies.Contains("guard")
                    || guard.ConflictGroups.Intersect(protect.ConflictGroups).Any(),
                "Equivalent guard/protect status applications are not declared incompatible.");

            actor.Progression.RecordNarrative(CharacterNarrativeDomain.Work, "work:repair", "facility:qa",
                CharacterActivityOutcomes.Completed, day: 1);
            actor.Progression.RecordNarrative(CharacterNarrativeDomain.Work, "work:repair-failure", "facility:qa",
                CharacterActivityOutcomes.Failed, day: 2);
            CharacterSkillDraft duplicateDraft = new CharacterSkillDraft
            {
                kind = CharacterSkillKind.Active, requestKey = "qa:formula-final:duplicate",
                rules = Enumerable.Range(0, 3).Select(index => new CharacterSkillCandidateRule
                {
                    ruleId = "qa:formula-final:duplicate:" + index,
                    trigger = CharacterSkillTrigger.ManualCombat,
                    target = CharacterSkillTarget.Self,
                    targetingMode = CharacterSkillTargetingMode.Self,
                    effectArea = CharacterSkillEffectArea.Single, areaSize = 1,
                    allowedModuleIds = new List<string> { "guard", "protect" }
                }).ToList()
            };
            CharacterSkillFormulaGeneration.InitializeModuleSelection(actor.Progression, duplicateDraft, settings);
            NarrativeFormulaModuleSelectionRequest duplicateRequest =
                CharacterSkillFormulaGeneration.BuildModuleSelectionRequest(duplicateDraft, settings);
            Require(!NarrativeFormulaModuleSelectionValidator.TryValidate(duplicateRequest,
                    new NarrativeFormulaModuleSelectionChoice(duplicateRequest.SelectionId,
                        new[] { "guard", "protect" }, Array.Empty<string>(),
                        new[] { duplicateRequest.EvidenceFactIds[0] }), out _, out _),
                "C# accepted a double-charge guard/protect combination.");
            CharacterSkillDraft draft = new CharacterSkillDraft
            {
                kind = CharacterSkillKind.Active, requestKey = "qa:formula-final:manual",
                rules = Enumerable.Range(0, 3).Select(index => new CharacterSkillCandidateRule
                {
                    ruleId = "qa:formula-final:manual:" + index,
                    trigger = CharacterSkillTrigger.ManualWork,
                    target = CharacterSkillTarget.Self,
                    targetingMode = CharacterSkillTargetingMode.Self,
                    effectArea = CharacterSkillEffectArea.Single, areaSize = 1,
                    manualDurationHours = 24, manualCooldownDays = 1,
                    allowedModuleIds = new List<string> { "repair" }
                }).ToList()
            };
            CharacterSkillFormulaGeneration.InitializeModuleSelection(actor.Progression, draft, settings);
            MethodInfo exportRule = typeof(FormulaPresentationPilotExporter).GetMethod(
                "SkillRuleJson", BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo importRule = typeof(FormulaPresentationPilotExporter).GetMethod(
                "SkillRuleFromJson", BindingFlags.NonPublic | BindingFlags.Static);
            var ruleJson = (Newtonsoft.Json.Linq.JObject)exportRule.Invoke(null, new object[] { draft.rules[0] });
            var parsedRule = (CharacterSkillCandidateRule)importRule.Invoke(null, new object[] { ruleJson });
            Require(parsedRule.manualDurationHours == 24 && parsedRule.manualCooldownDays == 1,
                "Exported manual rule did not preserve the live timing contract.");
            foreach (string timeKey in new[] { "manualDurationHours", "manualCooldownDays" })
            {
                var invalidRule = (Newtonsoft.Json.Linq.JObject)ruleJson.DeepClone();
                invalidRule[timeKey] = 0;
                bool timingRejected = false;
                try { importRule.Invoke(null, new object[] { invalidRule }); }
                catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException)
                { timingRejected = true; }
                Require(timingRejected, "The exported rule parser accepted zero " + timeKey + ".");
            }
            NarrativeFormulaModuleSelectionRequest request = CharacterSkillFormulaGeneration.BuildModuleSelectionRequest(draft, settings);
            CharacterSkillInstance skill = CharacterSkillFormulaGeneration.ResolveModuleSelection(draft,
                new NarrativeFormulaModuleSelectionChoice(request.SelectionId, new[] { "repair" },
                    Array.Empty<string>(), new[] { request.EvidenceFactIds[0] }), settings);
            skill.displayName = "도구 점검";
            skill.narrativeFlavor = "시설 수리 경험에서 비롯된 작업 능력이다.";
            Require(skill.IsReady && skill.manualDurationHours == 24 && skill.manualCooldownDays >= 1,
                "C# resolved a manual skill without actual usable duration/cooldown.");
            CharacterSkillFormulaGeneration.ValidateRestoredFormulaSkill(skill, settings);
            CharacterSkillInstance tampered = skill.Clone();
            tampered.formulaCatalogSha256 = new string('0', 64);
            bool rejected = false;
            try { CharacterSkillFormulaGeneration.ValidateRestoredFormulaSkill(tampered, settings); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "Unknown formula authorities must not be accepted on restore.");
            actor.Progression.GrowthState.activeSkills.Add(skill);
            string frozen = JsonUtility.ToJson(skill);
            skill = JsonUtility.FromJson<CharacterSkillInstance>(frozen);
            CharacterSkillFormulaGeneration.ValidateRestoredFormulaSkill(skill, settings);
            actor.Progression.GrowthState.activeSkills.Clear();
            actor.Progression.GrowthState.activeSkills.Add(skill);
            Require(CharacterSkillDrawbackEffectSourceProjection.Project(actor.Progression).Count == 0,
                "Serialized absent stat burden was projected as a gameplay effect.");
            Require(CharacterManualSkillRuntime.TryActivateForExpedition(actor, skill.id, null,
                    new[] { actor }, _ => 0, 100, out string firstMessage),
                "Resolved formula skill could not activate in expedition: " + firstMessage);
            CharacterSkillInstance buff = CharacterManualSkillRuntime.GetActiveBuffSkills(actor, 100).Single(value => value.id == skill.id);
            Require(JsonUtility.ToJson(buff) == frozen, "Activation changed the frozen numeric envelope.");
            Require(!CharacterManualSkillRuntime.TryActivateForExpedition(actor, skill.id, null,
                    new[] { actor }, _ => 0, 101, out _), "Cooldown did not block immediate reuse.");
            Require(CharacterManualSkillRuntime.GetActiveBuffSkills(actor, 123).Any(value => value.id == skill.id)
                    && !CharacterManualSkillRuntime.GetActiveBuffSkills(actor, 124).Any(value => value.id == skill.id),
                "Manual duration did not expire in game hours.");
            CharacterSkillUseLimitState restoredLimits = actor.Progression.GrowthState.useLimits.Clone();
            Require(restoredLimits.manualSkillCooldowns.Single(value => value.skillId == skill.id).readyAbsoluteHour
                    == 100 + skill.manualCooldownDays * 24L,
                "Cooldown clone changed the frozen day/hour conversion.");
            Debug.Log("FormulaFinalReview: effective caps, equivalent effects, post-selection manual activation, cooldown and expiry passed.");
            return true;
        }
        finally { UnityEngine.Object.DestroyImmediate(settings); }
    }

    public static bool RunContextAndWorkSpeedScenario()
    {
        Type fixtureType = typeof(CharacterProgressionDebugScenarios).GetNestedType("ActorFixture", BindingFlags.NonPublic);
        using IDisposable fixture = (IDisposable)Activator.CreateInstance(fixtureType,
            new object[] { 8977, "속도 검증자", "human", false, "character:qa-formula-speed" });
        CharacterActor actor = (CharacterActor)fixtureType.GetProperty("Actor").GetValue(fixture);
        CharacterSkillSystemSettingsSO settings = actor.Progression.SkillSettings;
        foreach (CharacterSkillTrigger trigger in new[] { CharacterSkillTrigger.BattleStarted,
            CharacterSkillTrigger.DamageTaken, CharacterSkillTrigger.EnemyDefeated,
            CharacterSkillTrigger.BattleCompleted, CharacterSkillTrigger.InvasionStarted })
        {
            foreach (string id in new[] { "guard", "protect", "cleanse" })
                Require(!CharacterSkillFormulaRuntimeContextPolicy.ConsumesAllAppliedAxes(
                    CharacterSkillKind.Passive, trigger, CharacterUltimateDomain.None, settings.FindModule(id)),
                    "An outside-combat passive could charge for unused axes: " + trigger + "/" + id);
            Require(CharacterSkillFormulaRuntimeContextPolicy.ConsumesAllAppliedAxes(
                CharacterSkillKind.Passive, trigger, CharacterUltimateDomain.None, settings.FindModule("heal")),
                "The portable passive heal capability was accidentally removed.");
        }
        Require(!CharacterSkillFormulaRuntimeContextPolicy.ConsumesAllAppliedAxes(
            CharacterSkillKind.Ultimate, CharacterSkillTrigger.InvasionStarted,
            CharacterUltimateDomain.Defense, settings.FindModule("damage")),
            "Defense damage consumes no formula count axis and must fail closed.");
        foreach (string id in new[] { "dot", "conditional_amplify" })
            Require(CharacterSkillFormulaRuntimeContextPolicy.ConsumesAllAppliedAxes(
                CharacterSkillKind.Ultimate, CharacterSkillTrigger.InvasionStarted,
                CharacterUltimateDomain.Defense, settings.FindModule(id)),
                "Supported defense formula capability was removed: " + id);
        actor.Progression.GrowthState.passiveSkills.Clear();
        actor.Progression.RecordNarrative(CharacterNarrativeDomain.Work, "work:repair", "facility:qa",
            CharacterActivityOutcomes.Completed, day: 1);
        CharacterSkillInstance Resolve(CharacterSkillKind kind, CharacterSkillTrigger trigger)
        {
            CharacterSkillDraft draft = new CharacterSkillDraft
            {
                kind = kind, requestKey = "qa:speed:" + kind + ":" + trigger,
                requestedUltimateDomain = kind == CharacterSkillKind.Ultimate ? CharacterUltimateDomain.Management : CharacterUltimateDomain.None,
                rules = Enumerable.Range(0, kind == CharacterSkillKind.Active ? 3 : 1).Select(index => new CharacterSkillCandidateRule
                {
                    ruleId = "qa:speed:" + kind + ":" + trigger + ":" + index,
                    trigger = trigger, target = CharacterSkillTarget.Self,
                    targetingMode = CharacterSkillTargetingMode.Self, effectArea = CharacterSkillEffectArea.Single,
                    areaSize = 1, manualDurationHours = trigger == CharacterSkillTrigger.ManualWork ? 24 : 0,
                    manualCooldownDays = trigger == CharacterSkillTrigger.ManualWork ? 1 : 0,
                    ultimateDomain = kind == CharacterSkillKind.Ultimate ? CharacterUltimateDomain.Management : CharacterUltimateDomain.None,
                    allowedModuleIds = new List<string> { "work_speed" }
                }).ToList()
            };
            CharacterSkillFormulaGeneration.InitializeModuleSelection(actor.Progression, draft, settings);
            NarrativeFormulaModuleSelectionRequest request = CharacterSkillFormulaGeneration.BuildModuleSelectionRequest(draft, settings);
            CharacterSkillInstance result = CharacterSkillFormulaGeneration.ResolveModuleSelection(draft,
                new NarrativeFormulaModuleSelectionChoice(request.SelectionId, new[] { "work_speed" },
                    Array.Empty<string>(), new[] { request.EvidenceFactIds[0] }), settings);
            result.displayName = "작업 보조";
            result.narrativeFlavor = "일을 마친 경험을 다음 작업에 살린다.";
            return result;
        }
        void AssertWork(float bonus, string label)
        {
            CharacterSkillRuntimeEffects.BeginWork(actor, null, BuiltInWorkTypeIds.Operate, "qa:speed:" + label);
            Require(Mathf.Approximately(CharacterSkillRuntimeEffects.GetWorkSpeedMultiplier(actor),
                CharacterSkillWorkSpeedAuthority.ResolveFromAuthoredBonus(bonus)), "Incorrect work-speed contribution: " + label);
            CharacterSkillRuntimeEffects.EndWork(actor);
        }
        float Magnitude(CharacterSkillInstance skill) => CharacterSkillFormulaGeneration.RequireParameter(skill, "work_speed", "magnitude", settings);
        CharacterSkillInstance manual = Resolve(CharacterSkillKind.Active, CharacterSkillTrigger.ManualWork);
        actor.Progression.GrowthState.activeSkills.Add(manual);
        Require(actor.TryGetAbility(out AbilityWork work) && CharacterAiEditorTestDependencies.GameCalendar != null,
            "Fixture lacks the injected game clock.");
        Require(CharacterManualSkillRuntime.TryActivateForExpedition(actor, manual.id, null, new[] { actor },
            _ => 0, CharacterAiEditorTestDependencies.GameCalendar.AbsoluteHour, out string message),
            "Manual work-speed activation failed: " + message);
        AssertWork(Magnitude(manual), "v6-manual-once");
        actor.Progression.GrowthState.useLimits.manualSkillBuffs.Clear();
        actor.Progression.GrowthState.activeSkills.Clear();
        CharacterSkillInstance ultimate = Resolve(CharacterSkillKind.Ultimate, CharacterSkillTrigger.OperatingDayStarted);
        actor.Progression.GrowthState.ultimate = ultimate;
        actor.Progression.GrowthState.useLimits.managementOperatingDay = 0;
        AssertWork(Magnitude(ultimate), "v6-ultimate-once");
        CharacterSkillInstance archived = ultimate.Clone();
        archived.formulaVersion = 5;
        archived.formulaCatalogSha256 = "adb5e6fea3a46e2e33e1e03bb89120a923811d7099f3b9c5b6fea99397b76333";
        CharacterSkillFormulaGeneration.ValidateRestoredFormulaSkill(archived, settings);
        actor.Progression.GrowthState.ultimate = archived;
        AssertWork(2f * Magnitude(archived), "v5-ultimate-preserved");
        actor.Progression.GrowthState.ultimate = null;
        CharacterSkillInstance start = Resolve(CharacterSkillKind.Passive, CharacterSkillTrigger.WorkStarted);
        CharacterSkillInstance complete = Resolve(CharacterSkillKind.Passive, CharacterSkillTrigger.WorkCompleted);
        actor.Progression.GrowthState.passiveSkills.AddRange(new[] { start, complete });
        AssertWork(Magnitude(start) + Magnitude(complete), "distinct-passives-once-each");
        Debug.Log("FormulaFinalReview: context fail-closed and real work-speed v6 single/v5 legacy/passive contributions passed.");
        return true;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void RequireRejected(Action action, string message)
    {
        bool rejected = false;
        try { action(); }
        catch (ArgumentException) { rejected = true; }
        catch (InvalidOperationException) { rejected = true; }
        Require(rejected, message);
    }
}
#endif
