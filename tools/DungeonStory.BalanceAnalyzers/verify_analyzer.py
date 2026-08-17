#!/usr/bin/env python3
"""Reproducibly build and verify the committed V27 Roslyn analyzer.

The compiler and reference assembly packages are content-hash pinned.  The same
script is used locally and by GitHub Actions so DSB006 proves that the deployed
Unity analyzer binary was produced by the reviewed source and toolchain.
"""

from __future__ import annotations

import argparse
import hashlib
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import urllib.request
import zipfile


ROOT = Path(__file__).resolve().parents[2]
TOOL_DIR = Path(__file__).resolve().parent
SOURCE = TOOL_DIR / "DungeonStoryBalanceAnalyzer.cs"
COMMITTED = ROOT / "Assets/Analyzers/DungeonStory.BalanceAnalyzers.dll"
POSITIVE = TOOL_DIR / "Tests/Positive.cs"
NEGATIVE = TOOL_DIR / "Tests/Negative.cs"

COMPILER_URL = (
    "https://api.nuget.org/v3-flatcontainer/microsoft.net.compilers.toolset/4.3.1/"
    "microsoft.net.compilers.toolset.4.3.1.nupkg"
)
COMPILER_SHA256 = "ccbb7f75ba7271f5fad020e8b1b4eeffe5c56da5dd3a17797c2943aacf29ef78"
RUNTIME_URL = (
    "https://api.nuget.org/v3-flatcontainer/"
    "microsoft.netcore.app.runtime.win-x64/6.0.21/"
    "microsoft.netcore.app.runtime.win-x64.6.0.21.nupkg"
)
RUNTIME_SHA256 = "99cd57e2ec803781258b8127e3a17d1563b5f322e4e183fc59311b0f2b4745ee"

FRAMEWORK_REFERENCES = (
    "System.Private.CoreLib.dll",
    "System.Runtime.dll",
    "netstandard.dll",
    "System.Collections.dll",
    "System.Collections.Immutable.dll",
    "System.Linq.dll",
    "System.Linq.Expressions.dll",
    "System.Runtime.Extensions.dll",
    "System.Threading.dll",
    "System.Threading.Tasks.dll",
    "System.Memory.dll",
)


def fail(message: str) -> None:
    raise RuntimeError(message)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def download_pinned(url: str, expected: str, destination: Path) -> None:
    if destination.is_file() and sha256(destination) == expected:
        return
    request = urllib.request.Request(url, headers={"User-Agent": "DungeonStory-V27-CI/1"})
    with urllib.request.urlopen(request, timeout=120) as response:
        payload = response.read()
    actual = hashlib.sha256(payload).hexdigest()
    if actual != expected:
        fail(f"pinned package digest mismatch for {url}: expected={expected} actual={actual}")
    destination.write_bytes(payload)


def extract_pinned(package: Path, destination: Path) -> None:
    if destination.is_dir():
        return
    destination.mkdir(parents=True)
    root = destination.resolve()
    with zipfile.ZipFile(package) as archive:
        for member in archive.infolist():
            target = (destination / member.filename).resolve()
            if root != target and root not in target.parents:
                fail(f"unsafe path in pinned NuGet package: {member.filename}")
        archive.extractall(destination)


def resolve_dotnet(explicit: str) -> str:
    if explicit:
        path = Path(explicit)
        if path.is_file():
            return str(path)
        found = shutil.which(explicit)
        if found:
            return found
        fail(f"dotnet executable not found: {explicit}")
    found = shutil.which("dotnet")
    if found:
        return found
    unity = Path(
        r"C:\Program Files\Unity\Hub\Editor\6000.3.8f1\Editor\Data\NetCoreRuntime\dotnet.exe"
    )
    if unity.is_file():
        return str(unity)
    fail("dotnet 6 runtime is required for the pinned analyzer toolchain")


def prepare_toolchain(cache: Path) -> tuple[Path, Path]:
    cache.mkdir(parents=True, exist_ok=True)
    compiler_package = cache / "microsoft.net.compilers.toolset.4.3.1.nupkg"
    runtime_package = cache / "microsoft.netcore.app.runtime.win-x64.6.0.21.nupkg"
    download_pinned(COMPILER_URL, COMPILER_SHA256, compiler_package)
    download_pinned(RUNTIME_URL, RUNTIME_SHA256, runtime_package)
    compiler_root = cache / "compiler-4.3.1"
    runtime_root = cache / "runtime-win-x64-6.0.21"
    extract_pinned(compiler_package, compiler_root)
    extract_pinned(runtime_package, runtime_root)
    compiler = compiler_root / "tasks/net6.0/bincore"
    runtime = runtime_root / "runtimes/win-x64/lib/net6.0"
    required = (
        compiler / "csc.dll",
        compiler / "Microsoft.CodeAnalysis.dll",
        compiler / "Microsoft.CodeAnalysis.CSharp.dll",
        *(runtime / name for name in FRAMEWORK_REFERENCES),
    )
    missing = [str(path) for path in required if not path.is_file()]
    if missing:
        fail(f"pinned analyzer toolchain is incomplete: {missing}")
    return compiler, runtime


def run_compiler(dotnet: str, arguments: list[str], expect_success: bool) -> str:
    process = subprocess.run(
        [dotnet, *arguments],
        cwd=ROOT,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
        encoding="utf-8",
        errors="replace",
        check=False,
    )
    if (process.returncode == 0) != expect_success:
        expected = "success" if expect_success else "failure"
        fail(f"Roslyn compilation expected {expected}, exit={process.returncode}:\n{process.stdout}")
    return process.stdout


def compiler_arguments(
    compiler: Path,
    runtime: Path,
    output: Path,
    source: Path,
    analyzer: Path | None = None,
) -> list[str]:
    arguments = [
        str(compiler / "csc.dll"),
        "/nologo",
        "/nostdlib+",
        "/target:library",
        "/langversion:latest",
        "/optimize+",
        "/deterministic+",
        f"/out:{output}",
        f"/reference:{compiler / 'Microsoft.CodeAnalysis.dll'}",
        f"/reference:{compiler / 'Microsoft.CodeAnalysis.CSharp.dll'}",
    ]
    arguments.extend(f"/reference:{runtime / name}" for name in FRAMEWORK_REFERENCES)
    if analyzer is not None:
        arguments.append(f"/analyzer:{analyzer}")
    arguments.append(source.relative_to(ROOT).as_posix())
    return arguments


def build(dotnet: str, compiler: Path, runtime: Path, output: Path) -> None:
    output.parent.mkdir(parents=True, exist_ok=True)
    run_compiler(
        dotnet,
        compiler_arguments(compiler, runtime, output, SOURCE),
        expect_success=True,
    )


def verify(dotnet: str, compiler: Path, runtime: Path) -> None:
    if not COMMITTED.is_file():
        fail("committed Unity analyzer DLL is missing")
    with tempfile.TemporaryDirectory(prefix="dungeonstory-balance-analyzer-") as raw:
        temporary = Path(raw)
        rebuilt = temporary / "DungeonStory.BalanceAnalyzers.dll"
        build(dotnet, compiler, runtime, rebuilt)
        committed_hash = sha256(COMMITTED)
        rebuilt_hash = sha256(rebuilt)
        if committed_hash != rebuilt_hash:
            fail(
                "DSB006 analyzer binary drift: "
                f"committed={committed_hash} rebuilt={rebuilt_hash}"
            )

        positive_output = temporary / "positive.dll"
        run_compiler(
            dotnet,
            compiler_arguments(
                compiler, runtime, positive_output, POSITIVE, analyzer=COMMITTED
            ),
            expect_success=True,
        )
        negative_output = temporary / "negative.dll"
        diagnostics = run_compiler(
            dotnet,
            compiler_arguments(
                compiler, runtime, negative_output, NEGATIVE, analyzer=COMMITTED
            ),
            expect_success=False,
        )
        for diagnostic in (
            "DSB001",
            "DSB002",
            "DSB003",
            "DSB004",
            "DSB005",
            "DSB007",
            "DSB008",
        ):
            if diagnostic not in diagnostics:
                fail(f"negative analyzer fixture did not emit {diagnostic}:\n{diagnostics}")

    print(
        "RESULT=PASS; analyzerRules=DSB001-DSB008; "
        f"sourceHash={sha256(SOURCE)}; binaryHash={sha256(COMMITTED)}; "
        f"compilerPackage={COMPILER_SHA256}; runtimePackage={RUNTIME_SHA256}"
    )


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--dotnet", default="")
    parser.add_argument("--output", default="")
    parser.add_argument(
        "--cache",
        default=str(Path(tempfile.gettempdir()) / "dungeonstory-v27-analyzer-toolchain"),
    )
    arguments = parser.parse_args()
    dotnet = resolve_dotnet(arguments.dotnet)
    compiler, runtime = prepare_toolchain(Path(arguments.cache))
    if arguments.output:
        output = Path(arguments.output)
        if not output.is_absolute():
            output = ROOT / output
        build(dotnet, compiler, runtime, output)
        print(f"RESULT=PASS; output={output}; sha256={sha256(output)}")
    else:
        verify(dotnet, compiler, runtime)
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception as error:
        print(f"RESULT=FAIL; {error}", file=sys.stderr)
        sys.exit(1)
