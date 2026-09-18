"""Capture real, digest-bound Unity/Python evidence; never authorize training.

Run with the NarrativeAI venv. Requires the official Unity CLI and an idle Editor.
No MCP, scene loading, Play, asset editing, model inference or source generation.
Every run/output is create-only. A timeout is terminal, NOT permission to retry
against an Editor whose previous command may still be running.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import re
import shutil
import subprocess
import sys
import time
from pathlib import Path


V25 = "V25NarrativeInferenceDebugScenarios"
PROGRESSION = "CharacterProgressionDebugScenarios"
FACILITY = "FacilityEvolutionDebugScenarios"
FOCUSED = [(V25, name) for name in (
    "VerifyPublicNarrativeContinuityContract", "VerifyChoiceCanonicalizationAndGrammar",
    "VerifyAcquiredTraitExactResponseContract", "VerifyContextAwareScheduling",
    "VerifyTypedEquipmentEvidence", "VerifyEquipmentChoiceFallbackAudits",
    "VerifyMechanicalNarrativeSeparation", "VerifyMultiPerspectiveIdentity",
    "VerifyMissingHostFailsClosed", "VerifyCorruptModelFailsClosed",
    "VerifyBundledLlamaCppBackendContract", "VerifyCharacterSkillPublicSemanticsContract",
    "VerifyCanonicalCatalogJsonParity", "VerifyIndependentCatalogExportBytes",
)] + [(PROGRESSION, name) for name in (
    "VerifyPermanentChoiceAndPersistence", "VerifyModuleValidation",
    "VerifyAcquiredTraitExperienceScore", "VerifyAcquiredTraitMilestoneLifecycle",
    "VerifyAuthoredAcquiredTraitEffectProjection", "VerifyAcquiredTraitAutomaticProducerResume",
    "VerifyAcquiredTraitWholeRootSaveRoundTrip", "VerifyMemoryErasureSealAtomicity",
)] + [(FACILITY, "VerifyLlmProposalFiltersIdsAndOrdersCandidates")]


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def digest(path):
    return "sha256:" + hashlib.sha256(Path(path).read_bytes()).hexdigest()


def write_json(path, value):
    with Path(path).open("x", encoding="utf-8", newline="\n") as stream:
        json.dump(value, stream, ensure_ascii=False, indent=2, sort_keys=True, allow_nan=False)
        stream.write("\n")


def cs(value):
    return json.dumps(str(value), ensure_ascii=True)


def check_version(value):
    require(re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9_-]{0,99}", value), "Unsafe version")
    return value


def unwrap(payload):
    """Unity CLI has transport, command, and eval success envelopes."""
    require(payload.get("success") is True, f"Unity transport failure: {payload}")
    data = payload["data"]
    require(data.get("success", True) is True, f"Unity command failure: {data}")
    if "state" in data:
        require(data["state"] == "completed", f"Unity job not completed: {data}")
    result = data.get("result", data)
    if isinstance(result, str):
        result = json.loads(result)
    if isinstance(result, dict) and "success" in result:
        require(result["success"] is True, f"Unity eval failure: {result}")
        return result.get("result", result)
    return result


def canonical(value):
    # The Unity canonical DOM permits signed Int64 numbers only.
    def visit(item):
        if isinstance(item, float):
            raise ValueError("Canonical catalog must not contain floating point values")
        if type(item) is int and not -(2**63) <= item < 2**63:
            raise ValueError("Canonical catalog number outside Int64")
        if isinstance(item, dict):
            for child in item.values():
                visit(child)
        elif isinstance(item, list):
            for child in item:
                visit(child)
    visit(value)
    return json.dumps(value, ensure_ascii=False, sort_keys=True,
                      separators=(",", ":"), allow_nan=False).encode("utf-8")


def verify_identity(directory, baseline):
    manifest = json.loads((directory / "manifest.json").read_text(encoding="utf-8"))
    for field in ("inputDigest", "source", "uncommittedSourceHash"):
        require(manifest[field] == baseline[field], f"Source changed during preflight: {field}")
    require(digest(directory / "catalog.json") == baseline["catalogBytesSha256"],
            "Catalog changed during preflight")


class Capture:
    def __init__(self, project, ai, version):
        self.project, self.ai = project.resolve(), ai.resolve()
        self.version = check_version(version)
        self.cli = shutil.which("unity")
        require(self.cli, "Official Unity CLI unavailable")
        self.qa = self.project / "Artifacts/QA/NarrativeTrainingPreflight" / version
        self.exports = self.project / "Artifacts/Exports/NarrativeMechanicCatalog"
        self.bundle = self.project / "Artifacts/Exports/NarrativeTrainingPreflight" / version
        require(not self.bundle.exists(), "Bundle already exists")
        for suffix in ("-baseline", ""):
            require(not (self.exports / (version + suffix)).exists(), "Export already exists")
        self.qa.mkdir(parents=True, exist_ok=False)
        self.index = 0
        self.gates = []

    def command(self, name, code=None):
        arguments = [self.cli, "command", name, "--project-path", str(self.project), "--format", "json"]
        if code is not None:
            arguments += ["--detach", "--code", code]
        payload = self.raw_command(arguments)
        if code is None:
            return unwrap(payload)
        require(payload.get("success") is True, f"Unity dispatch failure: {payload}")
        job = payload["data"]
        require(job.get("detached") is True and job.get("jobId"), "Missing detached job handle")
        # Use the official handle; no resubmission after timeout or uncertain state.
        for _ in range(4):
            payload = self.raw_command([self.cli, "job", "wait", job["jobId"],
                "--timeout", "40", "--project-path", str(self.project), "--format", "json"],
                allow_failure=True)
            if payload.get("success") is True:
                return unwrap(payload)
            status = self.raw_command([self.cli, "job", "status", job["jobId"],
                "--project-path", str(self.project), "--format", "json"])
            require(status.get("success") is True, f"Cannot establish job state: {status}")
            if status["data"]["state"] not in ("queued", "running"):
                return unwrap(status)
            print(f"Waiting on existing Unity job {job['jobId']} (not resubmitting)", flush=True)
        raise RuntimeError(f"Unity job still running: {job['jobId']}; completion unknown, stop")

    def raw_command(self, arguments, allow_failure=False):
        self.index += 1
        prefix = self.qa / f"command-{self.index:03d}"
        write_json(prefix.with_suffix(".request.json"), {"arguments": arguments, "cwd": str(self.project)})
        started = time.time()
        try:
            run = subprocess.run(arguments, cwd=self.project, capture_output=True, timeout=55)
        except subprocess.TimeoutExpired as exc:
            write_json(prefix.with_suffix(".timeout.json"), {
                "status": "TIMEOUT_COMMAND_COMPLETION_UNKNOWN", "elapsedSeconds": time.time() - started,
                "stdout": (exc.stdout or b"").decode("utf-8", errors="replace"),
                "stderr": (exc.stderr or b"").decode("utf-8", errors="replace")})
            raise RuntimeError("Unity command timed out: stop; verify completion before another run") from exc
        with prefix.with_suffix(".stdout.json").open("xb") as stream:
            stream.write(run.stdout)
        with prefix.with_suffix(".stderr.txt").open("xb") as stream:
            stream.write(run.stderr)
        write_json(prefix.with_suffix(".exit.json"), {
            "exitCode": run.returncode, "elapsedSeconds": time.time() - started})
        require(run.returncode == 0 or allow_failure, f"Unity CLI failed; see {prefix}")
        return json.loads(run.stdout.decode("utf-8-sig"))

    def ready(self):
        result = self.command("eval", '''return new {
            compiling = UnityEditor.EditorApplication.isCompiling,
            updating = UnityEditor.EditorApplication.isUpdating,
            playing = UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode,
            failed = UnityEditor.EditorUtility.scriptCompilationFailed,
            unityVersion = UnityEngine.Application.unityVersion,
            assembly = typeof(NarrativeMechanicCatalogExporter).Assembly.Location,
            assemblyTicks = System.IO.File.GetLastWriteTimeUtc(typeof(NarrativeMechanicCatalogExporter).Assembly.Location).Ticks
        };''')
        require(all(result.get(key) is False for key in ("compiling", "updating", "playing", "failed")),
                f"Editor not ready: {result}")
        sources = list((self.project / "Assets/Scripts").rglob("*.cs"))
        latest = max(sources, key=lambda path: path.stat().st_mtime_ns)
        # CLR DateTime ticks start in year 1; compare in nanoseconds without rounding down.
        assembly_ns = (int(result["assemblyTicks"]) - 621355968000000000) * 100
        require(assembly_ns >= latest.stat().st_mtime_ns, "Source newer than Editor assembly; recompile first")
        result["latestScript"] = latest.relative_to(self.project).as_posix()
        result["latestScriptMtimeNs"] = latest.stat().st_mtime_ns
        return result

    def export(self, version, evidence="null"):
        code = f'''var r = NarrativeMechanicCatalogExporter.Export(
            new NarrativeMechanicCatalogUnityAssetSource(),
            new NarrativeMechanicCatalogExportRequest({cs(version)}, false, {evidence}));
            return new {{ directory = r.DirectoryPath, inputDigest = r.InputDigest,
                catalogHash = r.CatalogHash, trainingEligible = r.TrainingEligible }};'''
        result = self.command("eval", code)
        require(result["trainingEligible"] is False, "Unexpected training eligibility")
        path = Path(result["directory"]).resolve()
        require(path == (self.exports / version).resolve(), "Unexpected export destination")
        return path

    def gate(self, gate_id, report):
        report.update({"inputDigest": self.identity["inputDigest"], "catalogHash": self.identity["catalogHash"],
                       "humanApprovalClaimed": False})
        path = self.qa / (gate_id + ".json")
        write_json(path, report)
        require(report["passed"] is True, f"Gate failed: {gate_id}; raw result preserved")
        self.gates.append({"gateId": gate_id, "artifactPath": path.relative_to(self.project).as_posix(),
                           "artifactHash": digest(path), "inputDigest": self.identity["inputDigest"],
                           "passed": True, "detail": "Actual execution; see hashed artifact and raw command records."})

    def execute(self):
        environment = self.ready()
        self.command("recompile_status")
        focused_sources = {relative: digest(self.project / relative) for relative in (
            "Assets/Scripts/Services/Character/AI/Editor/V25NarrativeInferenceDebugScenarios.cs",
            "Assets/Scripts/Services/Character/AI/Editor/CharacterProgressionDebugScenarios.cs",
            "Assets/Scripts/Services/FacilityEvolution/Editor/FacilityEvolutionDebugScenarios.cs")}
        write_json(self.qa / "environment.json", {"editor": environment, "python": sys.version,
            "executable": sys.executable, "captureToolHash": digest(__file__),
            "focusedSourceHashes": focused_sources,
            "window": "UNITY_VERIFY", "gameBalanceImpact": "none", "humanApprovalClaimed": False})
        unit_args = [sys.executable, "-B", "-X", "utf8", "-m", "unittest", "discover",
            "-s", "Tools/Documentation", "-p", "test_capture_v25_narrative_training_preflight.py", "-v"]
        unit = subprocess.run(unit_args, cwd=self.project, capture_output=True, timeout=30)
        write_json(self.qa / "offline-tests.json", {"arguments": unit_args, "exitCode": unit.returncode,
            "stdout": unit.stdout.decode("utf-8"), "stderr": unit.stderr.decode("utf-8")})
        require(unit.returncode == 0, "Preflight offline regressions failed")
        baseline = self.export(self.version + "-baseline")
        self.identity = json.loads((baseline / "manifest.json").read_text(encoding="utf-8"))
        self.identity["catalogBytesSha256"] = digest(baseline / "catalog.json")
        self.identity["catalogHash"] = json.loads((baseline / "catalog.json").read_text(encoding="utf-8"))["catalogHash"]
        write_json(self.qa / "identity.json", self.identity)

        comparisons = []
        for name in ("catalog.json", "packet_parity_cases.json"):
            raw = (baseline / name).read_bytes()
            encoded = canonical(json.loads(raw))
            comparisons.append({"file": name, "byteCount": len(raw), "sha256": digest(baseline / name),
                                "matched": encoded == raw})
        self.gate("canonical-json-python-parity", {
            "passed": all(row["matched"] for row in comparisons), "checks": comparisons,
            "scope": "Actual Unity canonical full catalog and parity bytes vs Python stdlib canonical encoding"})

        # Two new providers and independent source captures, not one DOM serialized twice.
        byte_result = self.command("eval", '''var r = NarrativeMechanicCatalogExporter.CompareIndependentExports(
            () => new NarrativeMechanicCatalogUnityAssetSource()); r.RequireByteIdentical();
            return new { passed = r.IsByteIdentical, firstInputDigest = r.FirstInputDigest,
                secondInputDigest = r.SecondInputDigest, byteCount = r.FirstBytes.Length,
                firstSha256 = NarrativeMechanicCatalogCanonicalJson.Sha256Prefixed(r.FirstBytes),
                secondSha256 = NarrativeMechanicCatalogCanonicalJson.Sha256Prefixed(r.SecondBytes) };''')
        require(byte_result["firstInputDigest"] == self.identity["inputDigest"] == byte_result["secondInputDigest"],
                "Independent captures differ from evidence input")
        self.gate("catalog-byte-determinism", byte_result)

        sys.path.insert(0, str(self.ai / "tools/v25_narrative_training"))
        from mechanic_catalog import load_catalog
        from mechanic_profiles import ContractError, validate_unity_response
        from unity_mechanics import validate_acquired_packet
        catalog = load_catalog(baseline / "catalog.json", require_training_eligible=False)
        packet = json.loads((baseline / "packet_parity_cases.json").read_text(encoding="utf-8"))
        require(not packet["blockers"] and packet["cases"], "Packet parity blocked or empty")
        outcomes = []
        for case in packet["cases"]:
            error = ""
            try:
                if case["profileId"] == "AcquiredTrait":
                    validate_acquired_packet(catalog, case["request"])
                if case["responseJson"]:
                    validate_unity_response(case["profileId"], catalog, case["request"], json.loads(case["responseJson"]))
                accepted = True
            except (ContractError, KeyError, TypeError, ValueError) as exc:
                accepted, error = False, str(exc)
            outcomes.append({"caseId": case["caseId"], "profileId": case["profileId"],
                "expectedAccepted": case["expected"]["accepted"], "actualAccepted": accepted,
                "matched": accepted == case["expected"]["accepted"], "error": error})
        self.gate("packet-parity", {"passed": all(row["matched"] for row in outcomes),
            "caseCount": len(outcomes), "outcomes": outcomes,
            "consumerSources": {path.name: digest(path) for path in sorted(
                (self.ai / "tools/v25_narrative_training").glob("*.py"))}})

        results = []
        for owner, method in FOCUSED:
            print(f"Unity focused: {owner}.{method}", flush=True)
            # Exact known type/method only; never enumerate AppDomain assemblies/types.
            result = self.command("eval", f'''var m = typeof({owner}).GetMethod({cs(method)},
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                if (m == null) throw new System.InvalidOperationException("Focused method missing");
                var r = m.Invoke(null, null);
                if (r is bool && !(bool)r) throw new System.InvalidOperationException("Focused check returned false");
                return new {{ passed = true, owner = {cs(owner)}, method = {cs(method)} }};''')
            results.append(result)
        self.gate("v25-focused-unity-regression", {"passed": all(row["passed"] for row in results),
            "caseCount": len(results), "failureCount": 0, "cases": results,
            "countUnit": "named focused methods (nested assertions not inflated into test counts)"})

        self.ready()
        require(all(digest(self.project / relative) == expected for relative, expected in focused_sources.items()),
                "Focused test sources changed during verification")
        gate_code = ",".join("new NarrativeMechanicCatalogEvidenceGate(" + ",".join((
            cs(g["gateId"]), "true", cs(g["inputDigest"]), cs(g["artifactPath"]),
            cs(g["artifactHash"]), cs(g["detail"]))) + ")" for g in self.gates)
        evidence = f"new NarrativeMechanicCatalogTestEvidence({cs(self.identity['inputDigest'])}, new NarrativeMechanicCatalogEvidenceGate[] {{ {gate_code} }})"
        final = self.export(self.version, evidence)
        verify_identity(final, self.identity)
        policy = json.loads((final / "policy.json").read_text(encoding="utf-8"))
        write_json(self.qa / "final-policy.json", policy)
        from verify_v25_narrative_mechanic_handoff import verify
        verification = verify(self.project, final, self.ai, False)
        require(verification["evidenceGateCount"] == 4, "Evidence not attached")
        write_json(self.qa / "handoff-verification.json", verification)
        write_json(self.qa / "collection-status.json", {
            "status": "NOT_COLLECTED", "sampleCount": 0, "trainingEligible": False,
            "humanApprovalClaimed": False, "scope": "Existing controlled Editor fixture export only",
            "blockers": ["No natural-play/save/replay collection session executed or supplied to this tool",
                         "Existing calibration/evaluation must not be re-labelled as training inputs",
                         "Independent family/seed/save provenance and editorial quality review still required"],
            "modelQualityGatePassed": False})
        shutil.copyfile(Path(__file__).with_name("V25_NARRATIVE_TRAINING_PREFLIGHT.md"),
                        self.qa / "collection-and-operation-notes.md")
        write_json(self.qa / "completion.json", {"status": "PASS", "exportDirectory": str(final),
            "inputDigest": self.identity["inputDigest"], "catalogHash": self.identity["catalogHash"],
            "evidenceGateCount": len(self.gates), "trainingEligible": False, "humanApprovalClaimed": False})
        # Portable bundle preserves project-relative artifact references used by the C# verifier.
        self.bundle.mkdir(parents=True, exist_ok=False)
        for source in (final, self.qa):
            shutil.copytree(source, self.bundle / "project" / source.relative_to(self.project))
        with (self.bundle / "README.md").open("x", encoding="utf-8", newline="\n") as stream:
            stream.write("# V25 training preflight evidence\n\nFour actual execution gates attached. "
                "Not training approval or model quality approval. No new natural-play samples.\n\n"
                "`project/` preserves export/evidence relative paths. Source provenance is checked against "
                "the original Unity checkout; source files are not copied. Existing controlled fixtures remain "
                "non-training. See QA completion, collection status and raw command records.\n")
        files = {path.relative_to(self.bundle).as_posix(): digest(path)
                 for path in sorted(self.bundle.rglob("*")) if path.is_file()}
        write_json(self.bundle / "delivery_manifest.json", {"schemaVersion": 1, "files": files,
            "inputDigest": self.identity["inputDigest"], "catalogHash": self.identity["catalogHash"],
            "trainingEligible": False, "humanApprovalClaimed": False})
        return {"export": str(final), "bundle": str(self.bundle), "evidence": str(self.qa),
                "deliveryManifestHash": digest(self.bundle / "delivery_manifest.json")}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project-root", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--narrative-ai-root", type=Path, required=True)
    parser.add_argument("--version", required=True)
    args = parser.parse_args()
    capture = None
    try:
        capture = Capture(args.project_root, args.narrative_ai_root, args.version)
        print(json.dumps(capture.execute(), ensure_ascii=False, indent=2))
        return 0
    except Exception as exc:
        if capture is not None:
            write_json(capture.qa / "failure.json", {"status": "FAIL", "error": str(exc),
                "exceptionType": type(exc).__name__, "trainingEligible": False,
                "humanApprovalClaimed": False, "retryAutomatically": False})
        print(f"PREFLIGHT FAIL: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
