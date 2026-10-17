using System;
using System.Collections.Generic;
using C = GameplayExperienceCategory;

/// <summary>
/// Hand-authored policy witnesses for every world experience rule. Expected
/// categories are intentionally stated at each call site rather than derived
/// from the production rule table.
/// </summary>
public static class GameplayWorldExperienceRuleCases
{
    public static IReadOnlyList<GameplayExperiencePolicyCase> Create()
    {
        var cases = new List<GameplayExperiencePolicyCase>();

        AddDisease(cases);
        AddGolem(cases);
        AddSimpleActions(cases);
        AddCropPlan(cases);
        AddDiseaseExposure(cases);
        AddFire(cases);
        AddRoomAndSpecies(cases);
        AddWildlife(cases);
        AddMedical(cases);
        AddCombat(cases);
        AddTechnicalRules(cases);
        AddSurgery(cases);
        AddDefense(cases);
        AddVandalism(cases);
        AddEvacuation(cases);
        AddTrade(cases);
        AddResearch(cases);
        AddService(cases);

        return cases;
    }

    private static void AddDisease(ICollection<GameplayExperiencePolicyCase> cases)
    {
        const string type = "character.disease-anatomy-burden-applied";
        var increased = New(type, "patient");
        Metric(increased, "disease.node-infection-before", 2, "burden");
        Metric(increased, "disease.node-infection-after", 3, "burden");
        Add(cases, "disease burden increased", increased, C.Mild);

        var unchanged = New(type, "patient");
        Metric(unchanged, "disease.node-infection-before", 2, "burden");
        Metric(unchanged, "disease.node-infection-after", 2, "burden");
        Add(cases, "disease burden unchanged", unchanged, C.Routine);

        var decreased = New(type, "patient");
        Metric(decreased, "disease.node-infection-before", 3, "burden");
        Metric(decreased, "disease.node-infection-after", 2, "burden");
        Add(cases, "disease applied receipt has impossible decrease", decreased, C.Unassessed);

        var wrongRole = New(type, "actor");
        Add(cases, "disease definition is not patient experience", wrongRole, C.NotExperience);
    }

    private static void AddGolem(ICollection<GameplayExperiencePolicyCase> cases)
    {
        Add(cases, "golem wear", Golem("wear"), C.Routine);
        Add(cases, "golem recharge", Golem("recharge"), C.Routine);
        Add(cases, "golem discharge", Golem("discharge"), C.Meaningful);
        Add(cases, "golem unknown episode", Golem("unsupported"), C.Unassessed);
    }

    private static void AddSimpleActions(ICollection<GameplayExperiencePolicyCase> cases)
    {
        Simple(cases, "certified seed completed", "environment.certified-seed-completed", C.Mild,
            "facility", "facility", "facility-1");
        Simple(cases, "environment control changed", "environment.control-changed", C.Routine,
            "facility", "facility", "facility-1");
        Simple(cases, "crop irrigation supplied", "environment.crop-irrigation-supplied", C.Routine,
            "facility", "facility", "plot-1");
        Simple(cases, "crop manual water refilled", "environment.crop-manual-water-refilled", C.Routine);
        Simple(cases, "crop selection changed", "environment.crop-selection-changed", C.Routine,
            "facility", "facility", "plot-1");
        Simple(cases, "crop treatment applied", "environment.crop-treatment-applied", C.Routine);
        Simple(cases, "fire ignition created", "environment.fire-ignition", C.Mild,
            "facility", "facility", "facility-1");
        Simple(cases, "fire water consumed", "environment.fire-water-consumed", C.Routine);
        Simple(cases, "golden harvest scheduled", "environment.golden-harvest-scheduled", C.Routine);

        // The production factory records a committed gale relocation as Failed even though
        // the physical item relocation happened. Preserve both pieces of source evidence.
        var gale = New("environment.harpy-gale-relocation", "actor", GameplayOutcomeStatus.Failed);
        GameplayExperienceCaseFactory.Participant(gale, "item", "item-stack", "item-stack-1");
        Metric(gale, "quantity", 2, "count", "item-stack", "item-stack-1");
        Metric(gale, "source-x", 1, "cell", "item-stack", "item-stack-1");
        Metric(gale, "destination-x", 2, "cell", "item-stack", "item-stack-1");
        Metric(gale, "source-y", 3, "cell", "item-stack", "item-stack-1");
        Metric(gale, "destination-y", 3, "cell", "item-stack", "item-stack-1");
        Add(cases, "harpy gale committed relocation", gale, C.Mild);

        Simple(cases, "wildlife command", "environment.wildlife-command", C.Routine,
            "wildlife", "wildlife", "wildlife-1");
        Simple(cases, "wildlife food raid completed", "environment.wildlife-food-raid-completed", C.Mild,
            "wildlife", "wildlife", "wildlife-1");
        Simple(cases, "production completed", "production.completed", C.Routine,
            "facility", "facility", "facility-1");
        Simple(cases, "defense facility service result", "defense.facility-service-result", C.Routine,
            "facility", "facility", "facility-1");
        Simple(cases, "building demolished", "infrastructure.building-demolished", C.Mild,
            "facility", "facility", "facility-1");
        Simple(cases, "dungeon space expanded", "infrastructure.dungeon-space-expanded", C.Meaningful,
            "facility", "facility", "dungeon-1");
        Simple(cases, "research knowledge completed", "research.knowledge-completed", C.Meaningful,
            "research-subject", "research-project", "research-1");
        Simple(cases, "facility synthesis completed", "facility.synthesis-completed", C.Meaningful,
            "facility", "facility", "facility-1");
        Simple(cases, "truth revealed", "offense.truth-revealed", C.Meaningful);

        Simple(cases, "failed action has no completed consequence",
            "environment.certified-seed-completed", C.NotExperience,
            "facility", "facility", "facility-1", GameplayOutcomeStatus.Failed);
    }

    private static void AddCropPlan(ICollection<GameplayExperiencePolicyCase> cases)
    {
        const string type = "environment.crop-plan-terminal";
        var output = New(type, "crop", GameplayOutcomeStatus.Succeeded, "crop-definition", "crop-1");
        Metric(output, "output-quantity", 4, "count");
        Add(cases, "crop plan produced output", output, C.Routine);

        var zero = New(type, "crop", GameplayOutcomeStatus.Failed, "crop-definition", "crop-1");
        Metric(zero, "output-quantity", 0, "count");
        Add(cases, "crop plan ended without output", zero, C.NotExperience);

        var negative = New(type, "crop", GameplayOutcomeStatus.Failed, "crop-definition", "crop-1");
        Metric(negative, "output-quantity", -1, "count");
        Add(cases, "crop plan negative output is malformed", negative, C.Unassessed);
    }

    private static void AddDiseaseExposure(ICollection<GameplayExperiencePolicyCase> cases)
    {
        const string type = "environment.disease-route-exposure";
        var exposure = New(type, "actor");
        GameplayExperienceCaseFactory.Participant(exposure, "disease", "disease-definition", "disease-1");
        Metric(exposure, "exposure-hours", 2, "hour", "disease-definition", "disease-1");
        Metric(exposure, "environment-coefficient", .5, "ratio", "disease-definition", "disease-1");
        Add(cases, "disease route exposure", exposure, C.Routine);

        var missing = New(type, "actor");
        GameplayExperienceCaseFactory.Participant(missing, "disease", "disease-definition", "disease-1");
        Metric(missing, "exposure-hours", 2, "hour", "disease-definition", "disease-1");
        Add(cases, "disease route missing coefficient", missing, C.Unassessed);
    }

    private static void AddFire(ICollection<GameplayExperiencePolicyCase> cases)
    {
        Add(cases, "fire damaged character but status zero is not death",
            FireDamage(10, 0, "character"), C.Intense);
        Add(cases, "fire disabled non-character target",
            FireDamage(10, 0, "facility"), C.Meaningful);
        Add(cases, "fire damaged still-combustible target",
            FireDamage(10, 1, "character"), C.Mild);
        Add(cases, "fire combustible enum malformed", FireDamage(10, 2, "character"), C.Unassessed);

        var fuel = New("environment.fire-fuel-loss", "item", GameplayOutcomeStatus.Failed,
            "item-stack", "fuel-stack");
        Metric(fuel, "quantity", 3, "count");
        Add(cases, "committed fire fuel loss despite failed operation status", fuel, C.Mild);
        var noFuel = New("environment.fire-fuel-loss", "item", GameplayOutcomeStatus.Failed,
            "item-stack", "fuel-stack");
        Metric(noFuel, "quantity", 0, "count");
        Add(cases, "fire fuel loss zero", noFuel, C.NotExperience);

        Add(cases, "partial fire suppression", Suppression(1, .4, true), C.Mild);
        Add(cases, "complete fire suppression", Suppression(1, 0, true), C.Meaningful);
        Add(cases, "unchanged fire intensity", Suppression(1, 1, true), C.NotExperience);
        Add(cases, "suppression without actor role", Suppression(1, .4, false), C.NotExperience);
        Add(cases, "suppression increased intensity", Suppression(.4, 1, true), C.Unassessed);

        var accident = New("environment.process-accident", "actor", GameplayOutcomeStatus.Failed);
        Metric(accident, "damage", 2, "point");
        Add(cases, "process accident applied damage", accident, C.Mild);
        var harmless = New("environment.process-accident", "actor", GameplayOutcomeStatus.Failed);
        Metric(harmless, "damage", 0, "point");
        Add(cases, "process accident zero damage", harmless, C.Routine);
    }

    private static void AddRoomAndSpecies(ICollection<GameplayExperiencePolicyCase> cases)
    {
        var condition = New("environment.room-condition-changed", "actor");
        GameplayExperienceCaseFactory.Participant(condition, "room", "room", "room-1");
        Metric(condition, "previous-cleanliness", 40, "point", "room", "room-1");
        Metric(condition, "current-cleanliness", 60, "point", "room", "room-1");
        Add(cases, "room cleanliness changed", condition, C.Routine);

        Add(cases, "room mood unchanged", RoomMood(0, 0), C.Routine);
        Add(cases, "room mood boundary below ten", RoomMood(9.99, 0), C.Mild);
        Add(cases, "room mood boundary ten", RoomMood(10, 0), C.Meaningful);
        Add(cases, "room mood negative ten", RoomMood(-10, 0), C.Meaningful);

        var incident = New("environment.species-incident-triggered", "actor", GameplayOutcomeStatus.Failed);
        Fact(incident, "incident-id", "harpy-gale-commotion");
        Add(cases, "species incident actor", incident, C.Mild);
        var missing = New("environment.species-incident-triggered", "actor", GameplayOutcomeStatus.Failed);
        Add(cases, "species incident missing incident id", missing, C.Unassessed);
    }

    private static void AddWildlife(ICollection<GameplayExperiencePolicyCase> cases)
    {
        Add(cases, "wildlife health loss ten percent", Wildlife("wildlife", 100, 90, 10), C.Meaningful);
        Add(cases, "wildlife health loss fifty percent", Wildlife("wildlife", 100, 50, 50), C.Intense);
        Add(cases, "wildlife health reaches zero", Wildlife("wildlife", 10, 0, 10), C.Transformative);
        Add(cases, "wildlife health unchanged", Wildlife("wildlife", 100, 100, 0), C.Routine);
        Add(cases, "hunter successful hit", Wildlife("actor", 100, 90, 10), C.Mild);
        Add(cases, "hunter missed", Wildlife("actor", 100, 100, 0), C.Routine);
        Add(cases, "wildlife inconsistent health delta", Wildlife("wildlife", 100, 95, 10), C.Unassessed);
    }

    private static void AddMedical(ICollection<GameplayExperiencePolicyCase> cases)
    {
        Add(cases, "patient bleeding stabilized", Medical("patient", 80, 80,
            "bleeding-stabilized", 8, 0), C.Meaningful);
        Add(cases, "patient small recovery", Medical("patient", 80, 90,
            "recovered", 0, 0), C.Mild);
        Add(cases, "patient major health recovery", Medical("patient", 50, 80,
            "recovered", 0, 0), C.Meaningful);
        Add(cases, "patient no observable change", Medical("patient", 80, 80,
            "no-observable-change", 0, 0), C.Routine);
        Add(cases, "patient worsened during care", Medical("patient", 100, 50,
            "no-observable-change", 0, 0), C.Intense);

        var clinician = Medical("clinician", 80, 90, "recovered", 0, 0);
        Add(cases, "physician experience does not inherit patient recovery", clinician, C.Mild);

        var invalid = Medical("patient", 80, 80, "miracle", 0, 0);
        Add(cases, "medical clinical result malformed", invalid, C.Unassessed);
    }

    private static void AddCombat(ICollection<GameplayExperiencePolicyCase> cases)
    {
        var victim = New("combat.damage-resolved", "victim");
        Metric(victim, "health.damage", 7, "health-point");
        Add(cases, "combat victim absolute damage", victim, C.Mild);

        var attacker = New("combat.damage-resolved", "attacker");
        GameplayExperienceCaseFactory.Participant(attacker, "victim", "character", "victim-1");
        Metric(attacker, "health.damage", 7, "health-point", "character", "victim-1");
        Add(cases, "combat attacker does not inherit victim severity", attacker, C.Mild);
    }

    private static void AddTechnicalRules(ICollection<GameplayExperiencePolicyCase> cases)
    {
        foreach (string type in new[]
        {
            "character.work-identity-applied",
            "infrastructure.autonomous-transition",
            "infrastructure.command-applied",
            "production.command-applied",
            "work.emergency-suspension-recorded",
            "policy.status-changed",
            "policy.value-changed"
        })
        {
            TechnicalStatus(cases, type, GameplayOutcomeStatus.Succeeded);
            TechnicalStatus(cases, type, GameplayOutcomeStatus.PartiallySucceeded);
            TechnicalStatus(cases, type, GameplayOutcomeStatus.Failed);
            TechnicalStatus(cases, type, GameplayOutcomeStatus.Blocked);
            TechnicalStatus(cases, type, GameplayOutcomeStatus.Cancelled);
        }
    }

    private static void AddSurgery(ICollection<GameplayExperiencePolicyCase> cases)
    {
        Add(cases, "surgery suture", Surgery(SurgicalProcedureKind.Suture), C.Meaningful);
        Add(cases, "surgery transfusion", Surgery(SurgicalProcedureKind.Transfusion), C.Meaningful);
        Add(cases, "surgery remove foreign body", Surgery(SurgicalProcedureKind.RemoveForeignBody), C.Meaningful);
        Add(cases, "surgery heal organ", Surgery(SurgicalProcedureKind.HealOrgan), C.Meaningful);
        Add(cases, "surgery amputate", Surgery(SurgicalProcedureKind.Amputate), C.Transformative);
        Add(cases, "surgery extract organ", Surgery(SurgicalProcedureKind.ExtractOrgan), C.Transformative);
        Add(cases, "surgery transplant organ", Surgery(SurgicalProcedureKind.TransplantOrgan), C.Transformative);
        Add(cases, "surgery install prosthetic", Surgery(SurgicalProcedureKind.InstallProsthetic), C.Transformative);
        Add(cases, "surgery install implant", Surgery(SurgicalProcedureKind.InstallImplant), C.Intense);
        Add(cases, "surgery arcane modification", Surgery(SurgicalProcedureKind.ArcaneModification), C.Intense);
        Add(cases, "surgery rehabilitation", Surgery(SurgicalProcedureKind.Rehabilitation), C.Meaningful);
        Add(cases, "surgery maintenance", Surgery(SurgicalProcedureKind.Maintenance), C.Routine);
        Add(cases, "surgery species stabilization", Surgery(SurgicalProcedureKind.SpeciesStabilization), C.Meaningful);
        Add(cases, "surgery species augmentation", Surgery(SurgicalProcedureKind.SpeciesAugmentation), C.Intense);

        Add(cases, "surgery minor complication",
            Surgery(SurgicalProcedureKind.Suture, SurgeryFailureSeverity.Minor), C.Mild);
        Add(cases, "surgery major complication",
            Surgery(SurgicalProcedureKind.Suture, SurgeryFailureSeverity.Major), C.Intense);
        Add(cases, "surgery fatal complication",
            Surgery(SurgicalProcedureKind.Suture, SurgeryFailureSeverity.Fatal), C.Transformative);
        Add(cases, "surgery corpse subject",
            Surgery(SurgicalProcedureKind.Suture, SurgeryFailureSeverity.None,
                SurgicalSubjectKind.HumanoidCorpse), C.NotExperience);
        Add(cases, "surgery unchanged anatomy",
            Surgery(SurgicalProcedureKind.Suture, SurgeryFailureSeverity.None,
                SurgicalSubjectKind.Character, GameplayOutcomeStatus.Succeeded, false), C.NotExperience);
        Add(cases, "surgery failed without complication",
            Surgery(SurgicalProcedureKind.Suture, SurgeryFailureSeverity.None,
                SurgicalSubjectKind.Character, GameplayOutcomeStatus.Failed), C.NotExperience);

        var malformed = Surgery(SurgicalProcedureKind.Suture);
        malformed.facts.Find(value => value.factId == "surgery.procedure").value = "not-canonical";
        Add(cases, "surgery malformed canonical procedure", malformed, C.Unassessed);
    }

    private static void AddDefense(ICollection<GameplayExperiencePolicyCase> cases)
    {
        Add(cases, "defense damage ten percent", Defense(100, 90, 10, 0, 0, 0), C.Meaningful);
        Add(cases, "defense damage fifty percent", Defense(100, 50, 50, 0, 0, 0), C.Intense);
        Add(cases, "defense damage reaches zero", Defense(10, 0, 10, 0, 0, 0), C.Transformative);
        Add(cases, "defense status applied without damage", Defense(100, 100, 0, 0, 1, 0), C.Mild);
        Add(cases, "defense delay applied without damage", Defense(100, 100, 0, 0, 0, 2), C.Mild);
        Add(cases, "defense no personal effect", Defense(100, 100, 0, 0, 0, 0), C.Routine);
        Add(cases, "defense inconsistent health delta", Defense(100, 95, 10, 0, 0, 0), C.Unassessed);
    }

    private static void AddVandalism(ICollection<GameplayExperiencePolicyCase> cases)
    {
        Add(cases, "vandal committed structural damage", Vandalism("vandal", 100, 80), C.Meaningful);
        Add(cases, "facility partially vandalized", Vandalism("damaged-facility", 100, 80), C.Mild);
        Add(cases, "facility destroyed by vandalism", Vandalism("damaged-facility", 100, 0), C.Meaningful);
        Add(cases, "vandalism with no structural change", Vandalism("damaged-facility", 100, 100), C.NotExperience);
        Add(cases, "vandalism impossible structural increase", Vandalism("damaged-facility", 100, 110), C.Unassessed);
    }

    private static void AddEvacuation(ICollection<GameplayExperiencePolicyCase> cases)
    {
        Add(cases, "evacuee arrived under invasion threat",
            Evacuation(ResidentEvacuationThreatKind.Invasion, ResidentEvacuationTerminalKind.Arrived), C.Meaningful);
        Add(cases, "evacuation no capacity under fire threat",
            Evacuation(ResidentEvacuationThreatKind.Fire, ResidentEvacuationTerminalKind.NoCapacity), C.Meaningful);
        Add(cases, "evacuation request replaced administratively",
            Evacuation(ResidentEvacuationThreatKind.InvasionAndFire,
                ResidentEvacuationTerminalKind.ReplacedByRequest), C.NotExperience);
        var invalid = Evacuation(
            ResidentEvacuationThreatKind.Invasion,
            ResidentEvacuationTerminalKind.ReplacedByRequest);
        invalid.facts.Find(value => value.factId == "evacuation.terminal-kind").value = "99";
        Add(cases, "evacuation terminal enum malformed", invalid, C.Unassessed);
    }

    private static void AddTrade(ICollection<GameplayExperiencePolicyCase> cases)
    {
        Trade(cases, 1, "trade facility crime", C.Mild);
        Trade(cases, 2, "trade facility revenue", C.Routine);
        Trade(cases, 3, "trade facility shop purchased", C.Routine);
        Trade(cases, 4, "trade facility stock consumed", C.Routine);
        Trade(cases, 5, "trade facility visit", C.Routine);
        Trade(cases, 6, "trade grand project physical input", C.NotExperience);
        Trade(cases, 7, "trade guest request delivery", C.Routine);
        Trade(cases, 8, "trade item extraction", C.NotExperience);
        Trade(cases, 9, "trade meta upgrade purchased", C.Meaningful);
        Trade(cases, 10, "trade packaged lot tare output", C.NotExperience);
        Trade(cases, 11, "trade physical item batch disposition", C.NotExperience);
        Trade(cases, 12, "trade physical item disposition", C.NotExperience);
        Trade(cases, 13, "trade exact source publication", C.NotExperience);
        Trade(cases, 14, "trade physical item relocation", C.NotExperience);
        Trade(cases, 15, "trade source publication", C.NotExperience);
        Trade(cases, 16, "trade physical item transform", C.NotExperience);
        Trade(cases, 17, "trade regional supply delivery transfer", C.NotExperience);
        Trade(cases, 18, "trade reserved retail stock transfer", C.NotExperience);
        Trade(cases, 19, "trade resource stock policy sale transfer", C.NotExperience);
        Trade(cases, 20, "trade stock supply", C.Routine);
        Trade(cases, 21, "trade warehouse mass admission", C.NotExperience);
        Trade(cases, 22, "trade world resource renewable debit", C.NotExperience);
        Trade(cases, 23, "trade waste policy command", C.NotExperience);

        Trade(cases, 2, "partial trade facility revenue", C.Routine,
            GameplayOutcomeStatus.PartiallySucceeded);
        Trade(cases, 1, "failed trade is not committed experience", C.NotExperience,
            GameplayOutcomeStatus.Failed);
        Trade(cases, 24, "trade unknown result enum", C.Unassessed);
    }

    private static void AddResearch(ICollection<GameplayExperiencePolicyCase> cases)
    {
        Add(cases, "research completed", Research("true", 5), C.Meaningful);
        Add(cases, "research progress", Research("false", 5), C.Routine);
        Add(cases, "research zero progress", Research("false", 0), C.NotExperience);
        Add(cases, "research invalid completion flag", Research("yes", 5), C.Unassessed);
        Add(cases, "research negative progress", Research("false", -1), C.Unassessed);
    }

    private static void AddService(ICollection<GameplayExperiencePolicyCase> cases)
    {
        Add(cases, "cancelled service with actual provided time",
            Service(30, GameplayOutcomeStatus.Cancelled), C.Routine);
        Add(cases, "cancelled service without provided time",
            Service(0, GameplayOutcomeStatus.Cancelled), C.NotExperience);
        Add(cases, "service negative provided time",
            Service(-1, GameplayOutcomeStatus.Failed), C.Unassessed);
    }

    private static GameplayOutcomeEvidenceBindingSnapshot Golem(string episode)
    {
        var value = New("character.golem-upkeep-episode", "actor");
        Fact(value, "episode-kind", episode);
        Fact(value, "state-before", "before");
        Fact(value, "state-after", "after");
        return value;
    }

    private static GameplayOutcomeEvidenceBindingSnapshot FireDamage(double damage, int status, string kind)
    {
        var value = New("environment.fire-damage", "target", GameplayOutcomeStatus.Failed,
            kind, kind + "-target");
        Metric(value, "damage", damage, "point");
        Metric(value, "status", status, "enum");
        return value;
    }

    private static GameplayOutcomeEvidenceBindingSnapshot Suppression(double before, double after, bool actor)
    {
        var value = actor
            ? New("environment.fire-suppression", "actor")
            : New("environment.fire-suppression", "fire", GameplayOutcomeStatus.Succeeded,
                "fire", "fire-1");
        if (actor)
            GameplayExperienceCaseFactory.Participant(value, "fire", "fire", "fire-1");
        else
            GameplayExperienceCaseFactory.Participant(value, "actor", "character", "actor-1");
        Metric(value, "intensity-before", before, "ratio", "fire", "fire-1");
        Metric(value, "intensity-after", after, "ratio", "fire", "fire-1");
        return value;
    }

    private static GameplayOutcomeEvidenceBindingSnapshot RoomMood(double impression, double cleanliness)
    {
        var value = New("environment.room-experience-applied", "actor");
        Metric(value, "impression-mood", impression, "point");
        Metric(value, "cleanliness-mood", cleanliness, "point");
        return value;
    }

    private static GameplayOutcomeEvidenceBindingSnapshot Wildlife(
        string role, double before, double after, double damage)
    {
        string kind = role == "wildlife" ? "wildlife" : "character";
        string id = role == "wildlife" ? "wildlife-1" : "subject";
        GameplayOutcomeStatus status = damage > 0
            ? GameplayOutcomeStatus.Succeeded
            : GameplayOutcomeStatus.Failed;
        var value = New("environment.wildlife-action", role, status, kind, id);
        if (role != "wildlife")
            GameplayExperienceCaseFactory.Participant(value, "wildlife", "wildlife", "wildlife-1");
        Fact(value, "event-kind", "hunt-attack");
        Metric(value, "health-before", before, "point", "wildlife", "wildlife-1");
        Metric(value, "health-after", after, "point", "wildlife", "wildlife-1");
        Metric(value, "damage", damage, "point", "wildlife", "wildlife-1");
        return value;
    }

    private static GameplayOutcomeEvidenceBindingSnapshot Medical(
        string subjectRole, double before, double after, string clinical,
        double bleedingBefore, double bleedingAfter)
    {
        var value = New("character.medical-procedure-completed", subjectRole);
        if (subjectRole == "clinician")
            GameplayExperienceCaseFactory.Participant(value, "patient", "character", "patient-1");
        else
            GameplayExperienceCaseFactory.Participant(value, "clinician", "character", "clinician-1");
        string patient = subjectRole == "patient" ? "subject" : "patient-1";
        Metric(value, "medical.health-before", before, "points", "character", patient);
        Metric(value, "medical.health-after", after, "points", "character", patient);
        Fact(value, "medical.clinical-result", clinical);
        foreach (string id in new[] { "blood-loss", "part-deficit", "anatomy-deficit", "anatomy-bleeding", "surface-bleeding" })
        {
            double oldValue = id == "anatomy-bleeding" ? bleedingBefore : 0;
            double newValue = id == "anatomy-bleeding" ? bleedingAfter : 0;
            Metric(value, "medical." + id + "-before", oldValue, "points", "character", patient);
            Metric(value, "medical." + id + "-after", newValue, "points", "character", patient);
        }
        return value;
    }

    private static GameplayOutcomeEvidenceBindingSnapshot Surgery(
        SurgicalProcedureKind kind,
        SurgeryFailureSeverity severity = SurgeryFailureSeverity.None,
        SurgicalSubjectKind subject = SurgicalSubjectKind.Character,
        GameplayOutcomeStatus status = GameplayOutcomeStatus.Succeeded,
        bool changed = true)
    {
        var value = New("medical.surgery-resolved", "patient", status,
            "surgical-subject", "patient-1");
        Fact(value, "surgery.procedure", "procedure-1|" + kind + "|subject-kind=" + subject
            + "|doctor=doctor-1|target=target-1");
        Fact(value, "surgery.severity", severity.ToString());
        Fact(value, "surgery.anatomy-before", "anatomy-before");
        Fact(value, "surgery.anatomy-after", changed ? "anatomy-after" : "anatomy-before");
        return value;
    }

    private static GameplayOutcomeEvidenceBindingSnapshot Defense(
        double before, double after, double damage, double statusesBefore,
        double statusesAfter, double delay)
    {
        var value = New("defense.facility-effect-resolved", "target");
        GameplayExperienceCaseFactory.Participant(
            value, "source-facility", "facility", "facility-1");
        Metric(value, "defense-effect.health-before", before, "points");
        Metric(value, "defense-effect.health-after", after, "points");
        Metric(value, "defense-effect.damage", damage, "points");
        Metric(value, "defense-effect.status-count-before", statusesBefore, "count");
        Metric(value, "defense-effect.status-count-after", statusesAfter, "count");
        Metric(value, "defense-effect.movement-delay", delay, "seconds");
        return value;
    }

    private static GameplayOutcomeEvidenceBindingSnapshot Vandalism(
        string role, double before, double after)
    {
        string kind = role == "vandal" ? "character" : "facility";
        string id = role == "vandal" ? "subject" : "facility-1";
        var value = New("infrastructure.facility-vandalized", role,
            GameplayOutcomeStatus.Succeeded, kind, id);
        if (role == "vandal")
            GameplayExperienceCaseFactory.Participant(
                value, "damaged-facility", "facility", "facility-1");
        else
            GameplayExperienceCaseFactory.Participant(
                value, "vandal", "character", "vandal-1");
        Fact(value, "vandalism.structural-hit-points-before", before.ToString(
            System.Globalization.CultureInfo.InvariantCulture));
        Fact(value, "vandalism.structural-hit-points-after", after.ToString(
            System.Globalization.CultureInfo.InvariantCulture));
        return value;
    }

    private static GameplayOutcomeEvidenceBindingSnapshot Evacuation(
        ResidentEvacuationThreatKind threat, ResidentEvacuationTerminalKind terminal)
    {
        GameplayOutcomeStatus status = terminal switch
        {
            ResidentEvacuationTerminalKind.Arrived => GameplayOutcomeStatus.Succeeded,
            ResidentEvacuationTerminalKind.NoCapacity or ResidentEvacuationTerminalKind.Unreachable =>
                GameplayOutcomeStatus.Blocked,
            _ => GameplayOutcomeStatus.Cancelled
        };
        var value = New("invasion.resident-evacuation-terminal", "evacuee", status);
        Fact(value, "evacuation.threat-kind", threat.ToString());
        Fact(value, "evacuation.terminal-kind", terminal.ToString());
        return value;
    }

    private static GameplayOutcomeEvidenceBindingSnapshot Research(string completed, double delta)
    {
        var value = New("research.work-applied", "research-subject",
            GameplayOutcomeStatus.Succeeded, "research-project", "research-1");
        Fact(value, "research.completed", completed);
        Metric(value, "research.work-delta", delta, "work-unit");
        return value;
    }

    private static GameplayOutcomeEvidenceBindingSnapshot Service(
        double seconds, GameplayOutcomeStatus status)
    {
        var value = New("service.session-terminal", "session", status,
            "service-session", "session-1");
        Metric(value, "provided-service-seconds", seconds, "second");
        return value;
    }

    private static void Trade(ICollection<GameplayExperiencePolicyCase> cases,
        int code, string name, C expected,
        GameplayOutcomeStatus status = GameplayOutcomeStatus.Succeeded)
    {
        var value = New("trade-inventory.result", "actor", status);
        Metric(value, "result.kind", code, "enum");
        Add(cases, name, value, expected);
    }

    private static void TechnicalStatus(ICollection<GameplayExperiencePolicyCase> cases,
        string type, GameplayOutcomeStatus status) => Add(cases,
        type + " " + status + " remains technical", New(type, "actor", status), C.NotExperience);

    private static void Simple(ICollection<GameplayExperiencePolicyCase> cases,
        string name, string type, C expected, string role = "actor",
        string kind = "character", string id = "subject",
        GameplayOutcomeStatus status = GameplayOutcomeStatus.Succeeded) =>
        Add(cases, name, New(type, role, status, kind, id), expected);

    private static GameplayOutcomeEvidenceBindingSnapshot New(string type, string role,
        GameplayOutcomeStatus status = GameplayOutcomeStatus.Succeeded,
        string kind = "character", string id = "subject") =>
        GameplayExperienceCaseFactory.Create(type, role, status, kind, id);

    private static void Metric(GameplayOutcomeEvidenceBindingSnapshot value,
        string id, double number, string unit, string referenceKind = null,
        string referenceId = null) => GameplayExperienceCaseFactory.Metric(
        value, id, number, unit, referenceKind, referenceId);

    private static void Fact(GameplayOutcomeEvidenceBindingSnapshot value,
        string id, string text) => GameplayExperienceCaseFactory.Fact(value, id, text);

    private static void Add(ICollection<GameplayExperiencePolicyCase> cases,
        string name, GameplayOutcomeEvidenceBindingSnapshot value, C expected) =>
        cases.Add(GameplayExperienceCaseFactory.Case(name, value, expected));
}
