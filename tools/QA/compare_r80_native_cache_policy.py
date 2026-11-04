"""Compare cache256/cache0/cache256 on exact retained R80 native phases, never TRAIN."""
from concurrent.futures import ThreadPoolExecutor
from copy import deepcopy
import argparse
import ctypes
import hashlib
import json
import os
from pathlib import Path
import secrets
import socket
import subprocess
import sys
import threading
import time

ROOT = Path(__file__).resolve().parents[2]
QA = ROOT / 'Artifacts/QA/R80PrototypePolish-20261028-r1/r54-cache-diagnosis-r1'
BUNDLE = ROOT / 'Assets/StreamingAssets/R80/DungeonStoryLlm'
OUTPUT = QA / 'actual-native-aba-r1'
MODEL_SHA = '2894ec5261b3a8352a375eb6319bbc8dd8c68c9b7b64fba255d51ce1bc2eb9b2'
GIB = 1024 ** 3
LIMIT_SECONDS = 1200 - 180
sys.path[:0] = [str(BUNDLE), str(BUNDLE / 'recipe'), str(ROOT / 'Tools/AI')]

import psutil
from jsonschema import Draft7Validator
import r80_game_bridge as bridge
import general_prototype_auxiliary_grammar as general
import behavior_auxiliary_grammar as behavior


class MemoryStatus(ctypes.Structure):
    _fields_ = [('length', ctypes.c_ulong), ('load', ctypes.c_ulong)] + [
        (name, ctypes.c_ulonglong) for name in ('totalPhysical', 'availablePhysical', 'totalPageFile',
        'availablePageFile', 'totalVirtual', 'availableVirtual', 'availableExtendedVirtual')]


def memory_headroom():
    value = MemoryStatus()
    value.length = ctypes.sizeof(value)
    require(ctypes.windll.kernel32.GlobalMemoryStatusEx(ctypes.byref(value)), 'Actual Windows memory observation required')
    return value.availablePhysical, value.availablePageFile


def read(path):
    return json.loads(path.read_bytes())


def sha(path):
    digest = hashlib.sha256()
    with path.open('rb') as source:
        for data in iter(lambda: source.read(8 * 1024 * 1024), b''):
            digest.update(data)
    return digest.hexdigest()


def require(condition, message):
    if not condition:
        raise ValueError(message)


def prepare_cases():
    pins = read(QA / 'native-source-pins.json')
    require(all(sha(ROOT / name) == pin for name, pin in pins.items()),
            'Original model host or exact native decoder source changed')
    bridge.install_game_candidate_schema()
    cases = []
    for meta in read(QA / 'analysis.json')['cases']:
        path = (ROOT / meta['source']).resolve()
        require(path.is_relative_to(ROOT / 'Artifacts/QA') and sha(path) == meta['sourceSha256'],
                'Only the retained, exact original QA inputs may be replayed')
        value = read(path)
        if meta['stage'] == 'selection':
            _, decoder, _ = bridge.decoder_for(value['schema'], lambda _: deepcopy(value['nativeSchema']), 'selection')
        elif meta['stage'] == 'social':
            decoder, _ = general.native_constraint(value['nativeSchema'])
        elif meta['stage'] == 'bubble':
            decoder, _ = behavior.native_constraint(value['nativeSchema'])
        else:
            raise ValueError('Unknown retained native phase')
        require(hashlib.sha256(decoder['grammar'].encode()).hexdigest() == meta['grammarSha256']
                and hashlib.sha256(value['renderedPrompt'].encode()).hexdigest() == meta['promptSha256'],
                'Original prompt or native grammar bytes changed')
        tokens = read(path.with_name(path.name.replace('-input.json', '-tokenize-http.json')))['tokens']
        require(len(tokens) == meta['promptTokens'], 'Original full prompt token count differs')
        Draft7Validator.check_schema(value['nativeSchema'])
        cases.append(dict(meta=meta, value=value, decoder=decoder, tokens=tokens))
    require([row['meta']['case'] for row in cases] == ['long-selection', 'failed-rumor', 'short-bubble'],
            'Exactly the three declared original native phases are required')
    return cases


def require_idle_resource_window():
    for process in psutil.process_iter(['name', 'cmdline']):
        name = (process.info['name'] or '').lower()
        if name in ('unity.exe', 'llama-server.exe'):
            raise RuntimeError('Existing Unity/native process must finish; do not close user applications')
        if name == 'python.exe' and any('train_local_runtime_language_r4.py' in part
                                        for part in process.info['cmdline'] or []):
            raise RuntimeError('Current owned learner is still live; no overlapping GPU benchmark')
    physical, commit = memory_headroom()
    require(physical >= 8 * GIB and commit >= 2 * GIB,
            'Keep the original 8GiB startup RAM and 2GiB commit reserves')
    gpu = subprocess.run(['nvidia-smi', '--query-gpu=memory.free', '--format=csv,noheader,nounits'],
                         capture_output=True, text=True, check=True, creationflags=subprocess.CREATE_NO_WINDOW)
    values = gpu.stdout.splitlines()
    require(len(values) == 1 and values[0].strip().isdigit() and int(values[0]) >= 7168,
            'Keep the original 7GiB free VRAM startup reserve')


def phase(engine, case, folder, slot, start_at=None):
    folder.mkdir(exist_ok=False)
    if start_at is not None:
        delay = start_at - time.monotonic()
        if delay > 0:
            time.sleep(delay)
    started = time.monotonic()
    meta, value = case['meta'], case['value']
    observation = dict(case=meta['case'], slot=slot, source=meta['source'], sourceSha256=meta['sourceSha256'],
                       promptSha256=meta['promptSha256'], grammarSha256=meta['grammarSha256'],
                       originalOutputLimit=meta['originalOutputLimit'], outputRepair=False,
                       onlyOneNativePhaseNotCompleteSkillGeneration=True)
    try:
        body, raw = engine.http('/completion', dict(prompt=value['renderedPrompt'], **case['decoder'],
            n_predict=meta['originalOutputLimit'], temperature=0., samplers=['temperature'], seed=0,
            stream=False, cache_prompt=True, id_slot=slot, return_tokens=True, repeat_penalty=1.,
            presence_penalty=0., frequency_penalty=0., ignore_eos=False,
            response_fields=['content', 'tokens', 'stop', 'stop_type', 'truncated', 'timings']), timeout=60)
        with (folder / 'native-http.json').open('xb') as target:
            target.write(raw)
        result = json.loads(bridge.validated_response(body))
        Draft7Validator(value['nativeSchema']).validate(result)
        Draft7Validator(value['schema']).validate(result)
        observation.update(status='ORIGINAL_NATIVE_SCHEMA_VALIDATED_NOT_GAME_APPLIED',
                           nativeTimings=body['timings'], responseSha256=hashlib.sha256(raw).hexdigest(),
                           result=result)
    except BaseException as error:
        observation.update(status='NATIVE_REPLAY_FAILED_NO_REPAIR', errorType=type(error).__name__, error=str(error))
        raise
    finally:
        observation['seconds'] = time.monotonic() - started
        if meta['stage'] in ('social', 'bubble'):
            # The longer diagnostic transport wait does not relax the actual game deadline.
            observation['meetsOriginalEightSecondServiceDeadline'] = observation['seconds'] <= 8
        bridge.write(folder / 'observation.json', observation)
    return observation


def execute():
    require(os.name == 'nt', 'This benchmark uses the actual Windows R80 binary')
    require_idle_resource_window()
    cases = prepare_cases()
    require(sha(BUNDLE / 'DungeonStory-R80-8B-Q4_K_M.gguf') == MODEL_SHA,
            'Only the original independently hashed R80 model may be loaded')
    require(not OUTPUT.exists(), 'No automatic rerun or overwrite of the native diagnostic')
    OUTPUT.mkdir()
    started_all = time.monotonic()
    observations = []
    for ordinal, cache_mib in enumerate((256, 0, 256)):
        require_idle_resource_window()
        require(time.monotonic() - started_all < LIMIT_SECONDS, 'Own bounded native comparison window expired')
        folder = OUTPUT / ('%02d-cache-%d' % (ordinal, cache_mib))
        folder.mkdir()
        with socket.socket() as reservation:
            reservation.bind(('127.0.0.1', 0))
            port = reservation.getsockname()[1]
        key = secrets.token_hex(32)
        argv = [str(BUNDLE / 'llama-server.exe'), '--model', str(BUNDLE / 'DungeonStory-R80-8B-Q4_K_M.gguf'),
                '--host', '127.0.0.1', '--port', str(port), '--api-key', key, '--ctx-size', '30720',
                '--parallel', '4', '--threads', '6', '--threads-batch', '6', '--gpu-layers', '99',
                '--reasoning', 'off', '--reasoning-budget', '0', '--cache-prompt', '--cache-ram', str(cache_mib),
                '--batch-size', '256', '--ubatch-size', '128', '--no-warmup', '--no-webui', '--kv-unified',
                '--no-context-shift', '--load-mode', 'none', '--jinja', '--chat-template-kwargs', '{"enable_thinking":false}']
        stop, pressure = threading.Event(), threading.Event()
        physical, commit = memory_headroom()
        minimum_ram, minimum_commit = [physical], [commit]
        stop_reason = [None]
        completed_arm = False
        arm_failure = None
        child = None
        with (folder / 'engine.log').open('xb') as log:
            try:
                child = subprocess.Popen(argv, cwd=BUNDLE, stdin=subprocess.DEVNULL, stdout=log,
                                         stderr=subprocess.STDOUT, creationflags=subprocess.CREATE_NO_WINDOW)
                # Keep the ephemeral API key out of all receipts and output.
                bridge.write(folder / 'process.json', dict(ownedPid=child.pid, cacheRamMiB=cache_mib,
                    modelSha256=MODEL_SHA, sourcePinsSha256=sha(QA / 'native-source-pins.json'),
                    originalGameDeadlinesChanged=False, noModelOrEnvironmentCopies=True))

                def monitor():
                    while not stop.wait(.25):
                        available, available_commit = memory_headroom()
                        minimum_ram[0] = min(minimum_ram[0], available)
                        minimum_commit[0] = min(minimum_commit[0], available_commit)
                        if available < 2 * GIB or available_commit < 2 * GIB or time.monotonic() - started_all >= LIMIT_SECONDS:
                            stop_reason[0] = ('RAM_OR_COMMIT_RESERVE' if min(available, available_commit) < 2 * GIB
                                              else 'OWN_BOUNDED_WINDOW_FINAL_RESERVE')
                            pressure.set()
                            if child.poll() is None:
                                child.terminate()
                            return

                watcher = threading.Thread(target=monitor, daemon=True)
                watcher.start()
                engine = bridge.Engine('http://127.0.0.1:' + str(port), key, context_size=12288)
                deadline = time.monotonic() + 120
                while True:
                    require(child.poll() is None and not pressure.is_set(), 'Own native startup failed or hit its resource guard')
                    try:
                        engine.http('/health', timeout=2)
                        break
                    except (OSError, bridge.InferenceTransportError):
                        if time.monotonic() >= deadline:
                            raise
                        time.sleep(.3)
                for case in cases:
                    tokens, _ = engine.http('/tokenize', dict(content=case['value']['renderedPrompt'],
                        add_special=False, parse_special=True), timeout=10)
                    require(tokens['tokens'] == case['tokens'], 'All original prompt token IDs must remain byte-for-byte identical')
                # Same fixed priming order in all three arms, with all raw outputs retained.
                for index, (case, slot) in enumerate(((cases[0], 3), (cases[1], 1), (cases[2], 0), (cases[0], 2))):
                    phase(engine, case, folder / ('prime-%02d' % index), slot)
                for cycle in range(2):
                    when = time.monotonic() + .1
                    with ThreadPoolExecutor(max_workers=3) as executor:
                        futures = [executor.submit(phase, engine, case, folder / ('burst-%d-%s' % (cycle, case['meta']['case'])),
                                                   case['meta']['slot'], when + (0 if index == 0 else .617))
                                   for index, case in enumerate(cases)]
                        for future in futures:
                            observations.append(dict(arm=ordinal, cacheRamMiB=cache_mib, cycle=cycle, **future.result()))
                    require(not pressure.is_set(), 'Native comparison reached its unchanged RAM/window guard')
                completed_arm = True
            except BaseException as error:
                arm_failure = dict(errorType=type(error).__name__, error=str(error))
                raise
            finally:
                stop.set()
                if 'watcher' in locals():
                    watcher.join(timeout=2)
                if child is not None and child.poll() is None:
                    child.terminate()
                    try:
                        child.wait(timeout=10)
                    except subprocess.TimeoutExpired:
                        child.kill()
                        child.wait(timeout=10)
                bridge.write(folder / 'terminal.json', dict(status='ARM_COMPLETED' if completed_arm else 'ARM_FAILED_NO_RETRY',
                    ownedPid=child.pid if child is not None else None,
                    exitCode=child.returncode if child is not None else None, resourceGuardTriggered=pressure.is_set(),
                    stopReason=stop_reason[0], failure=arm_failure,
                    minimumAvailableRamBytes=minimum_ram[0], minimumAvailableCommitBytes=minimum_commit[0],
                    userApplicationsTerminated=0))
    prepare_cases()
    bridge.write(OUTPUT / 'comparison.json', dict(status='EXACT_NATIVE_CACHE_ABA_COMPLETED_NOT_GAME_ADOPTED',
        observations=observations, modelSha256=MODEL_SHA, controlledArrivalDelaysSeconds=[0, .617, .617],
        originalInputAndDecoderBytesPreserved=True, originalGameTimeoutSeconds=8,
        diagnosticTransportTimeoutSeconds=60, gameDeadlinesChanged=False,
        renderedGamePlayOrFullSkillSpeedClaimed=False, gameFactValidatorsExecuted=False,
        gameStateApplied=False, outputRepair=False, qualityAccepted=False, policyAdopted=False))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--preflight-only', action='store_true')
    options = parser.parse_args()
    if options.preflight_only:
        cases = prepare_cases()
        print(json.dumps(dict(status='ORIGINAL_THREE_NATIVE_PHASES_AND_GRAMMARS_EXACT_NOT_GPU_EXECUTION',
                              cases=[row['meta'] for row in cases], gpuStarted=False)))
    else:
        execute()
