#!/usr/bin/env python3
"""Regression tests for the Phase80 static composition verifier."""

from __future__ import annotations

import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


SCRIPT = Path(__file__).with_name("verify_composition_source.py")
SERVICES = (
    "IGameplayOutcomeRegistry", "IGameplayOutcomeQuery",
    "IGameplayOutcomeMemoryCommands", "IGameplayOutcomeConsolidationService",
    "IGameplayOutcomeDiagnosticsQuery", "IGameplayOutcomePersistence",
    "IGameplayOutcomeRecorder", "IGameplayOutcomePresentationQuery",
    "IGameplayOutcomeNarrativeEvidenceQuery",
    "IGameplayOutcomeEvidenceUseTransaction", "IKoreanJosaFormatter",
)
SURFACES = {
    "Assets/Scripts/Views/UI/CodexFeatureSurfacePresenter.cs": "GetGlobalPage",
    "Assets/Scripts/Views/UI/CharacterSummaryInfo.cs": "GetEntityPage",
    "Assets/Scripts/Views/UI/BuildingSummaryInfo.cs": "GetEntityPage",
    "Assets/Scripts/Services/Items/ItemPileInfoPanel.cs": "GetEntityPage",
    "Assets/Scripts/Services/Offense/OffenseExpeditionPanel.cs": "GetOperationPage",
}


class CompositionVerifierTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        source_root = self.root / "Assets" / "Scripts"
        source_root.mkdir(parents=True)
        registrations = "\n".join(
            f"builder.Register<{service[1:]}>(Lifetime.Singleton).As<{service}>();"
            for service in SERVICES
        )
        (source_root / "BridgeRegistration.cs").write_text(
            "internal static void RegisterTestGameplayOutcomeBridges(object builder) {\n"
            "builder.Register<TestAdapter>(Lifetime.Singleton)"
            ".As<IGameplayOutcomeAdapterRegistration>();\n"
            "builder.Register<TestDescriptor>(Lifetime.Singleton)"
            ".As<IGameplayOutcomeDescriptor>();\n"
            "}\n",
            encoding="utf-8",
        )
        (source_root / "Composition.cs").write_text(
            "public void Install() { RegisterTestGameplayOutcomeBridges(builder);\n"
            + registrations + "\n}\n",
            encoding="utf-8",
        )
        for relative, method in SURFACES.items():
            path = self.root / relative
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(
                "private IGameplayOutcomePresentationQuery outcomes;\n"
                f"void Render() {{ outcomes.{method}(); }}\n",
                encoding="utf-8",
            )

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def run_verifier(self) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            [sys.executable, str(SCRIPT), "--source-root", str(self.root)],
            capture_output=True,
            text=True,
            check=False,
        )

    def test_valid_fixture_passes(self) -> None:
        result = self.run_verifier()
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)

    def test_missing_ui_query_call_fails(self) -> None:
        path = self.root / "Assets/Scripts/Services/Items/ItemPileInfoPanel.cs"
        path.write_text(
            "private IGameplayOutcomePresentationQuery outcomes;\n",
            encoding="utf-8",
        )
        result = self.run_verifier()
        self.assertNotEqual(0, result.returncode)
        self.assertIn("GetEntityPage is not called", result.stdout)

    def test_diagnostic_leak_fails(self) -> None:
        path = self.root / "Assets/Scripts/Views/UI/BuildingSummaryInfo.cs"
        path.write_text(
            "private IGameplayOutcomePresentationQuery outcomes;\n"
            "void Render() { outcomes.GetEntityPage(); var leak = page.DiagnosticCode; }\n",
            encoding="utf-8",
        )
        result = self.run_verifier()
        self.assertNotEqual(0, result.returncode)
        self.assertIn("internal diagnostic code leaks", result.stdout)


if __name__ == "__main__":
    unittest.main()
