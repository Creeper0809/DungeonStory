using System;
using System.Linq;
using C = GameplayExperienceCategory;
using static WorldExperiencePolicyFacts;

public sealed class CharacterDiseaseAnatomyBurdenAppliedExperiencePolicy : GameplayActionExperiencePolicy
{
    public CharacterDiseaseAnatomyBurdenAppliedExperiencePolicy() : base("character.disease-anatomy-burden-applied", "환자 실제 감염부담 증가만 경미. 건강·영구손상 추정 금지.") { }
    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts f)
    {
        if (!f.HasRole("patient")) return f.Technical();
        double before = Nonnegative(f.Number("disease.node-infection-before", "burden"));
        double after = Nonnegative(f.Number("disease.node-infection-after", "burden"));
        if (after < before) GameplayExperienceFacts.Fail("infection burden decreased in applied-burden receipt");
        return f.Judge(after > before ? C.Mild : C.Routine, "infection-burden-change", "실제 국소 감염 부담. 체력·장기 결손 근거는 없으므로 중상으로 확대하지 않음.");
    }
}

public sealed class CharacterMedicalProcedureCompletedExperiencePolicy : GameplayActionExperiencePolicy
{
    public CharacterMedicalProcedureCompletedExperiencePolicy() : base("character.medical-procedure-completed", "환자의 실제 회복/출혈 안정 평가. 임상의 성취와 환자 체험을 분리.") { }
    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts f)
    {
        double before = Nonnegative(f.Number("medical.health-before", "points", "patient"));
        double after = Nonnegative(f.Number("medical.health-after", "points", "patient"));
        string clinical = f.Fact("medical.clinical-result");
        if (!new[] { "bleeding-stabilized", "no-bleeding-change", "no-observable-change", "additional-treatment-required", "recovered" }.Contains(clinical))
            GameplayExperienceFacts.Fail("medical clinical result");
        if (f.HasRole("clinician")) return f.Action(C.Mild, "환자를 진료한 경험. 환자의 위기 점수를 의료진에게 전이하지 않음.");
        if (!f.HasRole("patient")) return f.Technical();
        if (after < before) return f.Damage(before, after, before - after, "의료 처치 중 실제 건강 악화.");
        double relief = 0;
        foreach (string id in new[] { "blood-loss", "part-deficit", "anatomy-deficit", "anatomy-bleeding", "surface-bleeding" })
            relief += Math.Max(0, Nonnegative(f.Number("medical." + id + "-before", "points")) - Nonnegative(f.Number("medical." + id + "-after", "points")));
        bool improved = after > before || relief > 0;
        return f.Judge(!improved ? C.Routine : clinical == "bleeding-stabilized" || after - before >= Math.Max(1, before * .5) ? C.Meaningful : C.Mild,
            "observed-clinical-improvement", "실제 회복/출혈 안정만 평가. recovered라는 문자열만으로 죽음에서 소생했다고 추정하지 않음.");
    }
}

public sealed class MedicalSurgeryResolvedExperiencePolicy : GameplayActionExperiencePolicy
{
    public MedicalSurgeryResolvedExperiencePolicy() : base("medical.surgery-resolved", "생존 환자 수술: 치명 .95/중대합병증 .80/경미 .30, 영구 신체변화 .95, 삽입강화 .80, 치료 .55, 유지 .10. 시체는 개인 경험 아님.") { }
    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts f)
    {
        if (!f.HasRole("patient")) return f.Technical();
        string[] procedure = f.Fact("surgery.procedure").Split('|');
        if (procedure.Length != 5 || !procedure[2].StartsWith("subject-kind=", StringComparison.Ordinal)
            || !procedure[3].StartsWith("doctor=", StringComparison.Ordinal) || !procedure[4].StartsWith("target=", StringComparison.Ordinal))
            GameplayExperienceFacts.Fail("surgery canonical procedure");
        bool validKind = Enum.TryParse(procedure[1], out SurgicalProcedureKind kind) && Enum.IsDefined(typeof(SurgicalProcedureKind), kind);
        bool validSubject = Enum.TryParse(procedure[2].Substring(13), out SurgicalSubjectKind subject) && Enum.IsDefined(typeof(SurgicalSubjectKind), subject);
        if (!validKind || !validSubject)
            GameplayExperienceFacts.Fail("surgery procedure/subject enum");
        var severity = f.EnumFact<SurgeryFailureSeverity>("surgery.severity");
        bool changed = f.Fact("surgery.anatomy-before") != f.Fact("surgery.anatomy-after");
        if (subject == SurgicalSubjectKind.HumanoidCorpse || subject == SurgicalSubjectKind.WildlifeCorpse)
            return f.Technical("corpse-has-no-current-experience");
        if (severity != SurgeryFailureSeverity.None)
            return f.Judge(severity == SurgeryFailureSeverity.Fatal ? C.Transformative : severity == SurgeryFailureSeverity.Major ? C.Intense : C.Mild,
                "confirmed-surgery-complication", "명시된 실제 수술 합병증 등급으로 판단.");
        if (!f.Success || !changed) return f.Technical("no-confirmed-surgery-change");
        var level = kind switch
        {
            SurgicalProcedureKind.Amputate or SurgicalProcedureKind.ExtractOrgan or SurgicalProcedureKind.TransplantOrgan or SurgicalProcedureKind.InstallProsthetic => C.Transformative,
            SurgicalProcedureKind.InstallImplant or SurgicalProcedureKind.ArcaneModification or SurgicalProcedureKind.SpeciesAugmentation => C.Intense,
            SurgicalProcedureKind.Maintenance => C.Routine,
            _ => C.Meaningful
        };
        return f.Judge(level, "confirmed-surgery-anatomy-change", "수술 성공과 해부 상태 변화가 함께 확인됨: " + kind);
    }
}

