#!/usr/bin/env python3
"""Regression tests for the current gameplay-action-log composition verifier."""

from __future__ import annotations

import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


SCRIPT = Path(__file__).with_name("verify_composition_source.py")
COMMON_SERVICES = (
    "IGameplayOutcomeRegistry",
    "IGameplayOutcomeQuery",
    "IGameplayOutcomeMemoryCommands",
    "IGameplayOutcomeConsolidationService",
    "IGameplayOutcomeDiagnosticsQuery",
    "IGameplayOutcomePersistence",
    "IGameplayOutcomeRecorder",
    "IKoreanJosaFormatter",
)
RUNTIME_SERVICES = (
    "IGameplayOutcomePresentationQuery",
    "IGameplayOutcomeNarrativeEvidenceQuery",
    "IGameplayOutcomeEvidenceUseTransaction",
)
SURFACES = {
    "Assets/Scripts/Views/UI/CodexFeatureSurfacePresenter.cs": "GetGlobalPage",
    "Assets/Scripts/Views/UI/CharacterSummaryInfo.cs": "GetEntityPage",
    "Assets/Scripts/Views/UI/BuildingSummaryInfo.cs": "GetEntityPage",
    "Assets/Scripts/Services/Items/ItemPileInfoPanel.cs": "GetEntityPage",
    "Assets/Scripts/Services/Offense/OffenseExpeditionPanel.cs": "GetExpeditionPage",
}


class CompositionVerifierTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        (self.root / "Assets" / "Scripts").mkdir(parents=True)
        self.write_catalog()
        self.write_module_factory()
        self.write_registry()
        self.write_composition()
        self.write_ui()

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def write(self, relative: str, source: str) -> Path:
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(source, encoding="utf-8")
        return path

    def write_catalog(self) -> None:
        typed = [
            "GameplayActionLogDefinition.Typed<"
            f"TestAdapter{index:03d}, TestDescriptor{index:03d}, TestPolicy{index:03d}>"
            f"(nameof(TestAdapter{index:03d})),"
            for index in range(77)
        ]
        migrated = [
            "GameplayActionLogDefinition.Migrated<"
            f"MigratedPolicy{index:03d}>(MigratedProducerOutcomeKind.Kind{index:03d}),"
            for index in range(72)
        ]
        self.write(
            "Assets/Scripts/Services/Narrative/GameplayOutcomes/GameplayActionLogCatalog.cs",
            "public static class GameplayActionLogCatalog {\n"
            "public static object Definitions = new[] {\n"
            + "\n".join(typed + migrated)
            + "\n}; }\n",
        )
        enum_members = ["None = 0,"] + [
            f"Kind{index:03d} = {index + 1}," for index in range(72)
        ]
        self.write(
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/"
            "MigratedProducers/MigratedProducerOutcomeContracts.cs",
            "public enum MigratedProducerOutcomeKind {\n"
            + "\n".join(enum_members)
            + "\n}\n",
        )

    def write_module_factory(self) -> None:
        self.write(
            "Assets/Scripts/Services/Narrative/GameplayOutcomes/GameplayActionLogModule.cs",
            """
public sealed class GameplayActionLogDefinition {
 public static GameplayActionLogDefinition Typed<TAdapter,TDescriptor,TPolicy>(string group) {
  var policy = new TPolicy();
  return new GameplayActionLogDefinition(group, policy, builder => {
   builder.Register<TAdapter>(Lifetime.Singleton).AsSelf();
   builder.Register<TDescriptor>(Lifetime.Singleton).AsSelf();
   builder.Register(resolver => new GameplayActionLogModule<TPolicy>(resolver.Resolve<TAdapter>(),
    resolver.Resolve<TDescriptor>(), policy), Lifetime.Singleton).As<IGameplayActionLogModule>();
  });
 }
 public static GameplayActionLogDefinition Migrated<TPolicy>(MigratedProducerOutcomeKind kind) {
  var policy = new TPolicy();
  var definition = MigratedProducerOutcomeCatalog.Definitions.Single(value => value.Kind == kind);
  if (policy.OutcomeTypes.Count != 1 || policy.OutcomeTypes[0] != definition.OutcomeTypeId.Value)
   throw new System.Exception();
  return new GameplayActionLogDefinition("migrated", policy, builder =>
   builder.Register(resolver => new GameplayActionLogModule<TPolicy>(
    resolver.Resolve<MigratedProducerOutcomeAdapter>(),
    new MigratedProducerOutcomeDescriptor(definition, resolver.Resolve<IKoreanJosaFormatter>()), policy),
    Lifetime.Singleton).As<IGameplayActionLogModule>());
 }
}
public static class GameplayActionLogRegistration {
 public static void RegisterGameplayActionLogs(IContainerBuilder builder, string group) {
  var definitions = GameplayActionLogCatalog.Definitions.Where(value => value.Group == group).ToArray();
  foreach (var definition in definitions) definition.Register(builder);
 }
}
""",
        )

    def write_registry(self) -> None:
        self.write(
            "Assets/Scripts/Services/Narrative/GameplayOutcomes/GameplayOutcomeRegistry.cs",
            """
public sealed class GameplayOutcomeRegistry {
 public static GameplayOutcomeRegistry FromModules(IEnumerable<IGameplayActionLogModule> modules) {
  var frozen = modules.ToArray();
  foreach (var module in frozen)
   _ = new GameplayActionLogModule(module.Adapter, module.Descriptor, module.ExperienceObserver);
  return new GameplayOutcomeRegistry(frozen);
 }
}
""",
        )

    @staticmethod
    def service_registrations(services: tuple[str, ...]) -> str:
        return "\n".join(
            f"builder.Register<{service[1:]}>(Lifetime.Singleton).As<{service}>();"
            for service in services
        )

    def write_composition(self) -> None:
        common = self.service_registrations(COMMON_SERVICES).replace(
            "builder.Register<GameplayOutcomeRegistry>(Lifetime.Singleton)"
            ".As<IGameplayOutcomeRegistry>();\n",
            "",
        )
        group_calls = "\n".join(
            f"builder.RegisterGameplayActionLogs(nameof(TestAdapter{index:03d}));"
            for index in range(77)
        )
        self.write(
            "Assets/Scripts/Services/Infrastructure/Registration/"
            "DungeonFoundationRegistration.cs",
            "public static class Foundation {\n"
            " public static void RegisterDungeonFoundation(IContainerBuilder builder) {\n"
            + common
            + "\nbuilder.RegisterGameplayActionLogs(\"migrated\");\n"
            "builder.Register(resolver => GameplayOutcomeRegistry.FromModules("
            "resolver.Resolve<IEnumerable<IGameplayActionLogModule>>()), Lifetime.Singleton)"
            ".As<IGameplayOutcomeRegistry>();\n"
            " }\n}\n",
        )
        self.write(
            "Assets/Scripts/Services/Infrastructure/Registration/AllOutcomeRegistration.cs",
            "public static class AllOutcomes {\n"
            " public static void RegisterAllGameplayOutcomes(IContainerBuilder builder) {\n"
            + group_calls
            + "\n }\n}\n",
        )
        self.write(
            "Assets/Scripts/Services/Infrastructure/Registration/"
            "DungeonPresentationRegistration.cs",
            "public static class Presentation {\n"
            " public static void RegisterDungeonPresentation(IContainerBuilder builder) {\n"
            + self.service_registrations(RUNTIME_SERVICES)
            + "\n }\n}\n",
        )
        self.write(
            "Assets/Scripts/Services/Infrastructure/DungeonRuntimeLifetimeScope.cs",
            "public sealed class DungeonRuntimeLifetimeScope {\n"
            " protected override void Configure(IContainerBuilder builder) {\n"
            "  builder.RegisterDungeonFoundation();\n"
            "  builder.RegisterAllGameplayOutcomes();\n"
            "  builder.RegisterDungeonPresentation();\n"
            " }\n}\n",
        )
        self.write(
            "Assets/Scripts/Services/Infrastructure/DungeonPreparationLifetimeScope.cs",
            "public sealed class DungeonPreparationLifetimeScope {\n"
            " protected override void Configure(IContainerBuilder builder) {\n"
            "  builder.RegisterDungeonFoundation();\n"
            "  builder.Register<GameplayOutcomeNarrativeEvidenceQuery>(Lifetime.Singleton)"
            ".As<IGameplayOutcomeNarrativeEvidenceQuery>();\n"
            " }\n}\n",
        )

    def write_ui(self) -> None:
        for relative, method in SURFACES.items():
            self.write(
                relative,
                "private IGameplayOutcomePresentationQuery outcomes;\n"
                f"void Render() {{ outcomes.{method}(); }}\n",
            )
        self.write(
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Presentation/"
            "GameplayOutcomePresentationQuery.cs",
            "public Page GetExpeditionPage() { return query.GetForOperation(operationId); }\n",
        )

    def run_verifier(self) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            [sys.executable, str(SCRIPT), "--source-root", str(self.root)],
            capture_output=True,
            text=True,
            check=False,
        )

    def assert_failure(self, expected: str) -> None:
        result = self.run_verifier()
        self.assertNotEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertIn(expected, result.stdout)

    def test_valid_current_architecture_passes(self) -> None:
        result = self.run_verifier()
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertIn("77 typed + 72 migrated = 149 modules", result.stdout)

    def test_distinct_preparation_and_runtime_service_registrations_are_valid(self) -> None:
        preparation = self.root / (
            "Assets/Scripts/Services/Infrastructure/DungeonPreparationLifetimeScope.cs"
        )
        runtime = self.root / "Assets/Scripts/Services/Infrastructure/DungeonRuntimeLifetimeScope.cs"
        self.assertIn("IGameplayOutcomeNarrativeEvidenceQuery", preparation.read_text(encoding="utf-8"))
        self.assertIn("RegisterDungeonPresentation", runtime.read_text(encoding="utf-8"))
        result = self.run_verifier()
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)

    def test_missing_module_group_call_fails(self) -> None:
        path = self.root / (
            "Assets/Scripts/Services/Infrastructure/Registration/AllOutcomeRegistration.cs"
        )
        path.write_text(
            path.read_text(encoding="utf-8").replace(
                "builder.RegisterGameplayActionLogs(nameof(TestAdapter000));\n", ""
            ),
            encoding="utf-8",
        )
        self.assert_failure("action-log module group calls missing: TestAdapter000")

    def test_duplicate_module_group_call_fails(self) -> None:
        path = self.root / (
            "Assets/Scripts/Services/Infrastructure/Registration/AllOutcomeRegistration.cs"
        )
        source = path.read_text(encoding="utf-8")
        path.write_text(
            source.replace(
                "builder.RegisterGameplayActionLogs(nameof(TestAdapter000));",
                "builder.RegisterGameplayActionLogs(nameof(TestAdapter000));\n"
                "builder.RegisterGameplayActionLogs(nameof(TestAdapter000));",
            ),
            encoding="utf-8",
        )
        self.assert_failure("duplicate action-log module group calls: TestAdapter000")

    def test_unknown_module_group_call_fails(self) -> None:
        path = self.root / (
            "Assets/Scripts/Services/Infrastructure/Registration/AllOutcomeRegistration.cs"
        )
        source = path.read_text(encoding="utf-8")
        path.write_text(
            source.replace(
                "\n }",
                "\n builder.RegisterGameplayActionLogs(nameof(UnknownAdapter));\n }",
            ),
            encoding="utf-8",
        )
        self.assert_failure("unknown action-log module group calls: UnknownAdapter")

    def test_mismatched_module_identity_fails(self) -> None:
        path = self.root / (
            "Assets/Scripts/Services/Narrative/GameplayOutcomes/GameplayActionLogCatalog.cs"
        )
        path.write_text(
            path.read_text(encoding="utf-8").replace(
                "(nameof(TestAdapter000))", "(nameof(WrongAdapter))", 1
            ),
            encoding="utf-8",
        )
        self.assert_failure("typed module group differs from adapter")

    def test_missing_module_factory_fails(self) -> None:
        path = self.root / (
            "Assets/Scripts/Services/Narrative/GameplayOutcomes/GameplayActionLogModule.cs"
        )
        path.write_text(
            path.read_text(encoding="utf-8").replace(
                ".As<IGameplayActionLogModule>()", ".AsSelf()", 1
            ),
            encoding="utf-8",
        )
        self.assert_failure("adapter/descriptor/policy module factory is missing")

    def test_missing_from_modules_registry_route_fails(self) -> None:
        path = self.root / (
            "Assets/Scripts/Services/Infrastructure/Registration/"
            "DungeonFoundationRegistration.cs"
        )
        path.write_text(
            path.read_text(encoding="utf-8").replace(
                "GameplayOutcomeRegistry.FromModules(", "new GameplayOutcomeRegistry("
            ),
            encoding="utf-8",
        )
        self.assert_failure("module collection is not routed through FromModules")

    def test_unreachable_group_registration_extension_fails(self) -> None:
        path = self.root / "Assets/Scripts/Services/Infrastructure/DungeonRuntimeLifetimeScope.cs"
        path.write_text(
            path.read_text(encoding="utf-8").replace(
                "  builder.RegisterAllGameplayOutcomes();\n", ""
            ),
            encoding="utf-8",
        )
        self.assert_failure("expected one runtime-root execution, found 0")

    def test_same_scope_duplicate_service_fails(self) -> None:
        path = self.root / (
            "Assets/Scripts/Services/Infrastructure/Registration/"
            "DungeonPresentationRegistration.cs"
        )
        source = path.read_text(encoding="utf-8")
        duplicate = (
            "builder.Register<DuplicateEvidence>(Lifetime.Singleton)"
            ".As<IGameplayOutcomeNarrativeEvidenceQuery>();\n"
        )
        path.write_text(
            source.replace("\n }", "\n" + duplicate + " }"), encoding="utf-8"
        )
        self.assert_failure(
            "runtime scope IGameplayOutcomeNarrativeEvidenceQuery: expected 1 registration, found 2"
        )

    def test_missing_ui_query_call_fails(self) -> None:
        path = self.root / "Assets/Scripts/Services/Items/ItemPileInfoPanel.cs"
        path.write_text(
            "private IGameplayOutcomePresentationQuery outcomes;\n",
            encoding="utf-8",
        )
        self.assert_failure("GetEntityPage is not called")

    def test_missing_expedition_operation_route_fails(self) -> None:
        path = self.root / (
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Presentation/"
            "GameplayOutcomePresentationQuery.cs"
        )
        path.write_text(
            "public Page GetExpeditionPage() { return new Page(); }\n",
            encoding="utf-8",
        )
        self.assert_failure("operation query route is missing")

    def test_core_content_branch_fails(self) -> None:
        path = self.root / (
            "Assets/Scripts/Services/Narrative/GameplayOutcomes/GameplayOutcomeRegistry.cs"
        )
        path.write_text(
            path.read_text(encoding="utf-8")
            + '\nvar forbidden = new GameplayOutcomeTypeId("content.branch");\n',
            encoding="utf-8",
        )
        self.assert_failure("core declares a concrete outcome type literal")

    def test_diagnostic_leak_fails(self) -> None:
        path = self.root / "Assets/Scripts/Views/UI/BuildingSummaryInfo.cs"
        path.write_text(
            "private IGameplayOutcomePresentationQuery outcomes;\n"
            "void Render() { outcomes.GetEntityPage(); var leak = page.DiagnosticCode; }\n",
            encoding="utf-8",
        )
        self.assert_failure("internal diagnostic code leaks")


if __name__ == "__main__":
    unittest.main()
