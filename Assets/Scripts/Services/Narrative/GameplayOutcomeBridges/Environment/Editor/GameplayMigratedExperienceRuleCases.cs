using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Controlled source-only cases for the 72 migrated-producer experience rules.</summary>
public static class GameplayMigratedExperienceRuleCases
{
    public static IEnumerable<GameplayExperiencePolicyCase> Create()
    {
        var cases = new List<GameplayExperiencePolicyCase>();

        Add(cases, "combat.character-downed", GameplayExperienceCategory.Intense,
            role: "target");
        Add(cases, "combat.character-recovered", GameplayExperienceCategory.Meaningful,
            role: "target");
        Add(cases, "character.death", GameplayExperienceCategory.Transformative,
            role: "target");
        Add(cases, "combat.character-killed", GameplayExperienceCategory.Transformative,
            role: "target");
        Add(cases, "combat.attack-resolved", GameplayExperienceCategory.Mild,
            configure: AttackDetail);
        Add(cases, "character.health-threshold-crossed", GameplayExperienceCategory.Meaningful,
            role: "target", configure: value => Fact(value, "detail",
                "beforeRatio=0.5;afterRatio=0.25"));

        AddTechnical(cases, "defense.facility-state-changed", "facility", "facility");
        AddTechnical(cases, "defense.facility-triggered", "facility", "facility");
        Add(cases, "defense.front-collapsed", GameplayExperienceCategory.Intense,
            role: "defender");
        Add(cases, "invasion.dungeon-breached", GameplayExperienceCategory.Meaningful,
            role: "intruder");
        Add(cases, "invasion.facility-damaged", GameplayExperienceCategory.Meaningful,
            role: "intruder");
        Add(cases, "invasion.resolved", GameplayExperienceCategory.Meaningful,
            role: "defender", status: GameplayOutcomeStatus.Failed);
        Add(cases, "invasion.started", GameplayExperienceCategory.Meaningful,
            role: "intruder");

        Add(cases, "external.visitor-reception-applied", GameplayExperienceCategory.Routine);
        Add(cases, "faction.route-arrived", GameplayExperienceCategory.Mild,
            role: "faction", kind: "faction");
        Add(cases, "faction.route-cargo-delivered", GameplayExperienceCategory.Mild,
            role: "route", kind: "route");
        Add(cases, "faction.route-settled", GameplayExperienceCategory.Mild,
            role: "faction", kind: "faction");
        Add(cases, "faction.trust-changed", GameplayExperienceCategory.Meaningful,
            role: "faction", kind: "faction", configure: value => Trust(value, 40d, 60d));

        AddTechnical(cases, "society.festival-celebrated", "operation", "operation");
        Add(cases, "offense.battle-command-resolved", GameplayExperienceCategory.Meaningful,
            role: "target", configure: value => HealthDamage(value, 100d, 80d));
        Add(cases, "offense.expedition-arrived", GameplayExperienceCategory.Meaningful,
            role: "arrival");
        Add(cases, "offense.expedition-node-resolved", GameplayExperienceCategory.Meaningful,
            role: "member");
        Add(cases, "offense.expedition-resolved", GameplayExperienceCategory.Meaningful,
            role: "member", status: GameplayOutcomeStatus.Failed);

        AddTechnical(cases, "run.result-ready", "operation", "operation");
        AddTechnical(cases, "run.variable-activated", "operation", "operation");
        AddTechnical(cases, "run.variable-expired", "operation", "operation");
        Add(cases, "run.campaign-event-resolved", GameplayExperienceCategory.Meaningful);

        Add(cases, "character.mentorship-awarded", GameplayExperienceCategory.Routine);
        Add(cases, "character.age-condition-changed", GameplayExperienceCategory.Routine);
        Add(cases, "survival.meal-consumed", GameplayExperienceCategory.Routine,
            configure: value => Nourishment(value, "hunger-before", "hunger-after", 70d, 80d));
        Add(cases, "survival.substance-consumed", GameplayExperienceCategory.Routine);
        Add(cases, "survival.detox-treated", GameplayExperienceCategory.Routine);
        Add(cases, "character.life-stage-changed", GameplayExperienceCategory.Routine);
        Add(cases, "survival.primitive-completed", GameplayExperienceCategory.Routine);
        Add(cases, "character.proficiency-awarded", GameplayExperienceCategory.Meaningful,
            configure: value => Proficiency(value, 1d, 2d));
        Add(cases, "survival.taboo-incident", GameplayExperienceCategory.Transformative,
            configure: CorpseConsumption);
        Add(cases, "survival.water-consumed", GameplayExperienceCategory.Routine,
            configure: value => Nourishment(value, "thirst-before", "thirst-after", 70d, 80d));
        Add(cases, "survival.meal-missed", GameplayExperienceCategory.Routine);
        Add(cases, "character.funeral-observed", GameplayExperienceCategory.Intense,
            role: "witness");
        Add(cases, "character.last-lesson-observed", GameplayExperienceCategory.Meaningful,
            role: "target");
        AddTechnical(cases, "character.lineage-compression-observed", "target", "character");
        Add(cases, "character.production-loss-observed", GameplayExperienceCategory.Mild,
            configure: value => Metric(value, "mass", 500d, "gram"));
        Add(cases, "survival.meal-incident-observed", GameplayExperienceCategory.Routine);
        Add(cases, "character.quiet-promotion-observed", GameplayExperienceCategory.Meaningful,
            configure: value => Proficiency(value, 2d, 3d));
        Add(cases, "run.owner-ended", GameplayExperienceCategory.Transformative);
        Add(cases, "character.rest-identity-applied", GameplayExperienceCategory.Routine,
            configure: value =>
            {
                Metric(value, "sleep-before", 70d, "point");
                Metric(value, "sleep-after", 90d, "point");
            });

        AddTechnical(cases, "economy.auto-procurement-resolved", "operation", "operation");
        Add(cases, "economy.retail-purchase-committed", GameplayExperienceCategory.Routine,
            role: "customer");
        AddTechnical(cases, "service.facility-crime", "facility", "facility");
        AddTechnical(cases, "economy.facility-purchased", "owner", "run");
        Add(cases, "service.facility-visited", GameplayExperienceCategory.Routine,
            configure: value => FacilityVisit(value, 100d, 100d, 50d, 50d));
        AddTechnical(cases, "meta.upgrade-purchased", "operation", "operation");
        AddTechnical(cases, "service.operating-day-resolved", "operation", "operation");
        AddTechnical(cases, "inventory.item-transformed", "operation", "operation");
        Add(cases, "service.regular-customer-recruited", GameplayExperienceCategory.Meaningful,
            role: "customer");
        Add(cases, "service.regular-customer-visited", GameplayExperienceCategory.Routine,
            role: "customer");
        AddTechnical(cases, "service.mode-changed", "facility", "facility");
        AddTechnical(cases, "inventory.stock-supplied", "operation", "operation");
        Add(cases, "work.order-terminal", GameplayExperienceCategory.Routine,
            role: "facility", kind: "facility",
            configure: value => Fact(value, "terminal-kind", "completed"));
        Add(cases, "economy.regional-supply-contract", GameplayExperienceCategory.Meaningful,
            role: "faction", kind: "faction",
            configure: value => Fact(value, "regional-contract-status-after", "Completed"));
        Add(cases, "service.regular-customer-recruit-delivered",
            GameplayExperienceCategory.Meaningful);
        Add(cases, "world.seasonal-wildlife-arrived", GameplayExperienceCategory.Mild,
            role: "target", kind: "wildlife",
            configure: value => Metric(value, "count", 3d, "count"));
        AddTechnical(cases, "world.seasonal-drift-cargo-resolved", "item", "item-instance");
        AddTechnical(cases, "equipment.apparel-work-order-completed", "facility", "facility");
        Add(cases, "equipment.combat-craft-completed", GameplayExperienceCategory.Routine);
        Add(cases, "work.repair-resolved", GameplayExperienceCategory.Routine);
        Add(cases, "world.resource-cycle-resolved", GameplayExperienceCategory.Routine);
        AddTechnical(cases, "economy.animal-husbandry-command-resolved", "facility", "facility");
        Add(cases, "economy.animal-husbandry-work-completed", GameplayExperienceCategory.Routine);
        AddTechnical(cases, "economy.stock-policy-sale-completed", "operation", "operation");
        AddTechnical(cases, "economy.quality-rejected-sale-completed", "operation", "operation");
        Add(cases, "survival.foul-water-consequence", GameplayExperienceCategory.Mild,
            configure: value => FoulWater(value, 100d, 100d, 0d, 5d));

        AddBoundaryCases(cases);
        RequireNormalCoverage(cases);
        return cases;
    }

    private static void AddBoundaryCases(List<GameplayExperiencePolicyCase> cases)
    {
        Add(cases, "combat.character-killed", GameplayExperienceCategory.Intense,
            role: "actor", name: "boundary:killed-actor");
        Add(cases, "character.health-threshold-crossed",
            GameplayExperienceCategory.Transformative, role: "target",
            name: "boundary:health-zero",
            configure: value => Fact(value, "detail", "beforeRatio=0.2;afterRatio=0"));
        Add(cases, "character.health-threshold-crossed",
            GameplayExperienceCategory.Intense, role: "target",
            name: "boundary:health-downed",
            configure: value => Fact(value, "detail", "beforeDowned=False;afterDowned=True"));
        Add(cases, "combat.attack-resolved", GameplayExperienceCategory.NotExperience,
            status: GameplayOutcomeStatus.Failed, name: "boundary:attack-failed");
        Add(cases, "combat.attack-resolved", GameplayExperienceCategory.Unassessed,
            name: "boundary:attack-detail-missing");

        Add(cases, "survival.taboo-incident", GameplayExperienceCategory.Mild,
            name: "boundary:humanoid-butchery-minimal");
        Add(cases, "survival.taboo-incident", GameplayExperienceCategory.NotExperience,
            role: "target", name: "boundary:corpse-target");
        Add(cases, "survival.taboo-incident", GameplayExperienceCategory.Unassessed,
            name: "boundary:corpse-physical-missing",
            configure: value =>
            {
                Metric(value, "quantity", 1d, "count");
                Metric(value, "input-mass", 100d, "gram");
                Fact(value, "reason-code", "extreme-hunger");
                Fact(value, "detail",
                    "action=corpse-consumption;sameSpecies=True;deathReason=combat");
            });
        Add(cases, "survival.taboo-incident", GameplayExperienceCategory.Unassessed,
            name: "boundary:corpse-quantity-missing",
            configure: value =>
            {
                Metric(value, "input-mass", 100d, "gram");
                Fact(value, "reason-code", "extreme-hunger");
                Fact(value, "physical-commit-id", "physical-commit:corpse");
                Fact(value, "detail",
                    "action=corpse-consumption;sameSpecies=True;deathReason=combat");
            });

        Add(cases, "faction.trust-changed", GameplayExperienceCategory.Routine,
            role: "faction", kind: "faction", name: "boundary:trust-zero",
            configure: value => Trust(value, 50d, 50d));
        Add(cases, "faction.trust-changed", GameplayExperienceCategory.Mild,
            role: "faction", kind: "faction", name: "boundary:trust-nineteen",
            configure: value => Trust(value, 50d, 69d));
        Add(cases, "faction.trust-changed", GameplayExperienceCategory.Meaningful,
            role: "faction", kind: "faction", name: "boundary:trust-twenty",
            configure: value => Trust(value, 50d, 70d));
        Add(cases, "faction.trust-changed", GameplayExperienceCategory.NotExperience,
            role: "witness", kind: "faction", name: "boundary:trust-witness");

        Add(cases, "character.funeral-observed", GameplayExperienceCategory.NotExperience,
            role: "target", name: "boundary:funeral-deceased");
        Add(cases, "run.owner-ended", GameplayExperienceCategory.Transformative,
            status: GameplayOutcomeStatus.Failed, name: "boundary:owner-run-failed");
        Add(cases, "offense.expedition-resolved", GameplayExperienceCategory.NotExperience,
            role: "operation", kind: "operation", name: "boundary:expedition-operation");

        Add(cases, "service.facility-visited", GameplayExperienceCategory.Meaningful,
            status: GameplayOutcomeStatus.Failed, name: "boundary:facility-failed-with-delta",
            configure: value => FacilityVisit(value, 100d, 100d, 40d, 65d));
        Add(cases, "service.facility-visited", GameplayExperienceCategory.NotExperience,
            status: GameplayOutcomeStatus.Cancelled, name: "boundary:facility-cancelled-no-delta",
            configure: value => FacilityVisit(value, 100d, 100d, 40d, 40d));
        Add(cases, "service.facility-visited", GameplayExperienceCategory.Unassessed,
            name: "boundary:facility-mood-missing",
            configure: value =>
            {
                Metric(value, "health-before", 100d, "point");
                Metric(value, "health-after", 100d, "point");
                Metric(value, "mood-before", 40d, "point");
            });

        Add(cases, "survival.meal-consumed", GameplayExperienceCategory.Meaningful,
            name: "boundary:meal-major-relief",
            configure: value => Nourishment(value, "hunger-before", "hunger-after", 10d, 90d));
        Add(cases, "survival.water-consumed", GameplayExperienceCategory.Meaningful,
            name: "boundary:water-major-relief",
            configure: value => Nourishment(value, "thirst-before", "thirst-after", 10d, 90d));
        Add(cases, "survival.water-consumed", GameplayExperienceCategory.Meaningful,
            name: "boundary:water-health-loss",
            configure: value =>
            {
                Metric(value, "health-before", 100d, "point");
                Metric(value, "health-after", 80d, "point");
                Metric(value, "thirst-before", 80d, "point");
                Metric(value, "thirst-after", 90d, "point");
            });
        Add(cases, "survival.meal-consumed", GameplayExperienceCategory.Routine,
            name: "boundary:meal-ordinary-forty-point-refill",
            configure: value => Nourishment(value, "hunger-before", "hunger-after", 40d, 80d));
        Add(cases, "character.rest-identity-applied", GameplayExperienceCategory.Meaningful,
            name: "boundary:rest-critical-to-sufficient",
            configure: value =>
            {
                Metric(value, "sleep-before", 10d, "point");
                Metric(value, "sleep-after", 90d, "point");
            });
        Add(cases, "character.rest-identity-applied", GameplayExperienceCategory.Routine,
            name: "boundary:rest-ordinary-forty-point-refill",
            configure: value =>
            {
                Metric(value, "sleep-before", 40d, "point");
                Metric(value, "sleep-after", 80d, "point");
            });

        Add(cases, "character.proficiency-awarded", GameplayExperienceCategory.Routine,
            name: "boundary:proficiency-no-rank",
            configure: value => Proficiency(value, 2d, 2d));
        Add(cases, "character.quiet-promotion-observed", GameplayExperienceCategory.Routine,
            name: "boundary:quiet-promotion-no-rank",
            configure: value => Proficiency(value, 2d, 2d));
        Add(cases, "character.quiet-promotion-observed", GameplayExperienceCategory.Unassessed,
            name: "boundary:quiet-promotion-wrong-unit",
            configure: value =>
            {
                Proficiency(value, 2d, 3d);
                value.metrics.Single(metric => metric.metricId == "rank-before").unitId = "count";
            });
        Add(cases, "character.proficiency-awarded", GameplayExperienceCategory.Unassessed,
            name: "boundary:proficiency-wrong-reference",
            configure: value =>
            {
                Proficiency(value, 2d, 3d);
                var metric = value.metrics.Single(item => item.metricId == "current-experience-before");
                metric.referenceKindId = value.subjectKindId;
                metric.referenceId = value.subjectId;
            });
        Add(cases, "character.proficiency-awarded", GameplayExperienceCategory.Unassessed,
            name: "boundary:proficiency-unknown-rank",
            configure: value => Proficiency(value, 2d, 987d));
        Add(cases, "character.quiet-promotion-observed", GameplayExperienceCategory.Unassessed,
            name: "boundary:quiet-promotion-fractional-rank",
            configure: value => Proficiency(value, 2d, 2.5d));
        Add(cases, "work.order-terminal", GameplayExperienceCategory.NotExperience,
            role: "facility", kind: "facility", name: "boundary:work-cancelled",
            configure: value => Fact(value, "terminal-kind", "cancelled"));
        Add(cases, "work.order-terminal", GameplayExperienceCategory.Unassessed,
            role: "facility", kind: "facility", name: "boundary:work-terminal-missing");
        Add(cases, "economy.regional-supply-contract", GameplayExperienceCategory.Mild,
            role: "faction", kind: "faction", name: "boundary:contract-accepted",
            configure: value => Fact(value, "regional-contract-status-after", "Accepted"));
        Add(cases, "survival.foul-water-consequence", GameplayExperienceCategory.NotExperience,
            status: GameplayOutcomeStatus.Failed, name: "boundary:foul-water-not-applied");
    }

    private static void RequireNormalCoverage(
        IReadOnlyList<GameplayExperiencePolicyCase> cases)
    {
        string[] actual = cases
            .Where(value => value.Name.StartsWith("normal:", StringComparison.Ordinal))
            .Select(value => value.Fact.outcomeTypeId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] expected = MigratedProducerOutcomeCatalog.Definitions
            .Select(value => value.OutcomeTypeId.Value)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (actual.Length != 72 || !actual.SequenceEqual(expected, StringComparer.Ordinal))
            throw new InvalidOperationException("Migrated policy cases must cover all 72 catalog types once.");
    }

    private static void Add(
        ICollection<GameplayExperiencePolicyCase> cases,
        string type,
        GameplayExperienceCategory expected,
        string role = "actor",
        GameplayOutcomeStatus status = GameplayOutcomeStatus.Succeeded,
        string kind = "character",
        Action<GameplayOutcomeEvidenceBindingSnapshot> configure = null,
        string name = null)
    {
        GameplayOutcomeEvidenceBindingSnapshot value =
            GameplayExperienceCaseFactory.Create(type, role, status, kind);
        configure?.Invoke(value);
        cases.Add(GameplayExperienceCaseFactory.Case(
            name ?? "normal:" + type, value, expected));
    }

    private static void AddTechnical(
        ICollection<GameplayExperiencePolicyCase> cases,
        string type,
        string role,
        string kind) => Add(cases, type, GameplayExperienceCategory.NotExperience,
            role, kind: kind);

    private static void Metric(
        GameplayOutcomeEvidenceBindingSnapshot value,
        string id,
        double number,
        string unit) => GameplayExperienceCaseFactory.Metric(value, id, number, unit);

    private static void Fact(
        GameplayOutcomeEvidenceBindingSnapshot value,
        string id,
        string text) => GameplayExperienceCaseFactory.Fact(value, id, text);

    private static void AttackDetail(GameplayOutcomeEvidenceBindingSnapshot value) =>
        Fact(value, "detail",
            "executed=True;hit=True;coverBlocked=False;bodyPart=Torso;damage=10");

    private static void HealthDamage(
        GameplayOutcomeEvidenceBindingSnapshot value,
        double before,
        double after)
    {
        Metric(value, "health-before", before, "point");
        Metric(value, "health-after", after, "point");
        Metric(value, "damage", before - after, "point");
    }

    private static void Trust(
        GameplayOutcomeEvidenceBindingSnapshot value,
        double before,
        double after)
    {
        Metric(value, "trust-before", before, "point");
        Metric(value, "trust-after", after, "point");
    }

    private static void Nourishment(
        GameplayOutcomeEvidenceBindingSnapshot value,
        string needBeforeId,
        string needAfterId,
        double needBefore,
        double needAfter)
    {
        Metric(value, "health-before", 80d, "point");
        Metric(value, "health-after", 80d, "point");
        Metric(value, needBeforeId, needBefore, "point");
        Metric(value, needAfterId, needAfter, "point");
    }

    private static void Proficiency(
        GameplayOutcomeEvidenceBindingSnapshot value,
        double rankBefore,
        double rankAfter)
    {
        Metric(value, "current-experience-before", 100d, "milli-experience");
        Metric(value, "current-experience-after", 150d, "milli-experience");
        Metric(value, "lifetime-experience-before", 1000d, "milli-experience");
        Metric(value, "lifetime-experience-after", 1050d, "milli-experience");
        Metric(value, "rank-before", rankBefore, "enum");
        Metric(value, "rank-after", rankAfter, "enum");
        if (value.outcomeTypeId == "character.proficiency-awarded")
        {
            GameplayExperienceCaseFactory.Participant(value, "source", "proficiency", "fixture-proficiency");
            foreach (var metric in value.metrics)
            {
                metric.referenceKindId = "proficiency";
                metric.referenceId = "fixture-proficiency";
            }
        }
    }

    private static void CorpseConsumption(GameplayOutcomeEvidenceBindingSnapshot value)
    {
        Metric(value, "quantity", 1d, "count");
        Metric(value, "input-mass", 300d, "gram");
        Fact(value, "reason-code", "extreme-hunger");
        Fact(value, "physical-commit-id", "physical-commit:corpse");
        Fact(value, "detail",
            "action=corpse-consumption;sameSpecies=True;deathReason=combat");
    }

    private static void FacilityVisit(
        GameplayOutcomeEvidenceBindingSnapshot value,
        double healthBefore,
        double healthAfter,
        double moodBefore,
        double moodAfter)
    {
        Metric(value, "health-before", healthBefore, "point");
        Metric(value, "health-after", healthAfter, "point");
        Metric(value, "mood-before", moodBefore, "point");
        Metric(value, "mood-after", moodAfter, "point");
    }

    private static void FoulWater(
        GameplayOutcomeEvidenceBindingSnapshot value,
        double healthBefore,
        double healthAfter,
        double infectionBefore,
        double infectionAfter)
    {
        Metric(value, "health-before", healthBefore, "point");
        Metric(value, "health-after", healthAfter, "point");
        Metric(value, "infection-before", infectionBefore, "point");
        Metric(value, "infection-after", infectionAfter, "point");
    }
}
