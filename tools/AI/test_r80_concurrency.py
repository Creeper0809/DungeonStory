"""CPU-only request-lane checks for the R80 game bridge.

The native transport is replaced only at the HTTP boundary. These tests do not
exercise a model and do not make any narrative-quality claim.
"""
from concurrent.futures import ThreadPoolExecutor
import importlib.util
import json
from pathlib import Path
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from tempfile import TemporaryDirectory
import threading
import time
import unittest
from unittest.mock import patch

from jsonschema.exceptions import ValidationError


BRIDGE_PATH = Path(__file__).with_name('r80_game_bridge.py').resolve()
BRIDGE_SPEC = importlib.util.spec_from_file_location('current_tools_r80_game_bridge', BRIDGE_PATH)
bridge = importlib.util.module_from_spec(BRIDGE_SPEC)
BRIDGE_SPEC.loader.exec_module(bridge)


class RequestEngineTests(unittest.TestCase):
    def test_content_rejection_and_transient_transport_failures_stay_distinct(self):
        self.assertFalse(bridge.retryable_failure(ValueError('invalid selected effect evidence')))
        self.assertFalse(bridge.retryable_failure(ValidationError('schema rejected')))
        self.assertTrue(bridge.retryable_failure(TimeoutError('native response timeout')))
        self.assertTrue(bridge.retryable_failure(ConnectionError('native endpoint unavailable')))
        self.assertTrue(bridge.retryable_failure(bridge.InferenceTransportError('native HTTP 503')))

    def test_native_completion_preserves_original_response_bytes(self):
        raw = b'{ "content": "original", "stop": true, "stop_type": "eos", "truncated": false }'
        received = []

        class Native(BaseHTTPRequestHandler):
            def do_POST(self):
                received.append((self.path, self.headers['Authorization'],
                    self.rfile.read(int(self.headers['Content-Length']))))
                self.send_response(200)
                self.send_header('Content-Length', str(len(raw)))
                self.end_headers()
                self.wfile.write(raw)

            def log_message(self, *args):
                pass

        with ThreadingHTTPServer(('127.0.0.1', 0), Native) as server:
            worker = threading.Thread(target=server.serve_forever, daemon=True)
            worker.start()
            try:
                engine = bridge.Engine('http://127.0.0.1:' + str(server.server_port),
                    'unit-key', caller_connected=lambda: True)
                value, original = engine.http('/completion', {'prompt': 'exact'}, timeout=3)
                self.assertEqual(value, json.loads(raw))
                self.assertEqual(original, raw)
                self.assertEqual(received, [('/completion', 'Bearer unit-key', b'{"prompt":"exact"}')])
            finally:
                server.shutdown()
                worker.join(timeout=2)

    def test_caller_abort_closes_native_socket_before_generation_finishes(self):
        ready, native_closed, connected = threading.Event(), threading.Event(), threading.Event()
        connected.set()

        class Native(BaseHTTPRequestHandler):
            def do_POST(self):
                self.rfile.read(int(self.headers['Content-Length']))
                self.connection.settimeout(3)
                ready.set()
                if self.rfile.read(1) == b'':
                    native_closed.set()

            def log_message(self, *args):
                pass

        with ThreadingHTTPServer(('127.0.0.1', 0), Native) as server:
            worker = threading.Thread(target=server.serve_forever, daemon=True)
            worker.start()
            try:
                engine = bridge.Engine('http://127.0.0.1:' + str(server.server_port),
                    'unit-key', caller_connected=connected.is_set)
                with ThreadPoolExecutor(max_workers=1) as jobs:
                    result = jobs.submit(engine.http, '/completion', {'prompt': 'bounded'}, 3)
                    self.assertTrue(ready.wait(2), 'Native request did not arrive')
                    connected.clear()
                    with self.assertRaises(ConnectionAbortedError):
                        result.result(timeout=2)
                    self.assertTrue(native_closed.wait(2), 'Cancelled native socket remained open')
            finally:
                server.shutdown()
                worker.join(timeout=2)

    @staticmethod
    def schema(value):
        return {
            'type': 'object',
            'additionalProperties': False,
            'required': ['value'],
            'properties': {'value': {'type': 'string', 'enum': [value]}},
        }

    @staticmethod
    def messages(profile):
        return [
            {'role': 'system', 'content': 'Return the bounded test value.'},
            {'role': 'user', 'content': profile},
        ]

    @staticmethod
    def fake_transport(engine, value, completion_barrier=None, captured=None):
        def http(path, body=None, timeout=45):
            engine.require_connected()
            if path == '/apply-template':
                return {'prompt': 'prompt:' + engine.profile}, b'{"prompt":"bounded"}'
            if path == '/tokenize':
                return {'tokens': [1, 2]}, b'{"tokens":[1,2]}'
            if path != '/completion':
                raise AssertionError('Unexpected native path: ' + path)
            if captured is not None:
                captured.append(body)
            if completion_barrier is not None:
                completion_barrier.wait(timeout=5)
            response = {
                'content': json.dumps({'value': value}, separators=(',', ':')),
                'tokens': [1],
                'stop': True,
                'stop_type': 'eos',
                'truncated': False,
            }
            return response, json.dumps(response, separators=(',', ':')).encode('utf8')
        return http

    def test_shared_kv_lanes_reserve_speech_short_and_two_background_workers(self):
        base = bridge.Engine('http://127.0.0.1:1', 'unit-key', 10240)
        lanes = bridge.RequestLanes(base, 4)
        deadline = time.monotonic() + 2
        with lanes.acquire('CharacterSkillModuleSelection', lambda: True) as first:
            with lanes.acquire('AcquiredTraitModuleSelection', lambda: time.monotonic() < deadline) as second:
                with lanes.acquire('EvolutionHistoryLegacyV2', lambda: True) as ordinary:
                    with lanes.acquire('BubbleLine', lambda: True) as speech:
                        self.assertEqual([first.slot, second.slot, ordinary.slot, speech.slot], [2, 3, 1, 0])
                        self.assertEqual([e.context_size for e in (first, second, ordinary, speech)], [10240] * 4)
                        checks = iter([True, False])
                        with self.assertRaises(ConnectionAbortedError):
                            with lanes.acquire('Persona', lambda: next(checks, False)):
                                self.fail('Busy ordinary worker was bypassed')
        with self.assertRaisesRegex(RuntimeError, 'bounded failure'):
            with lanes.acquire('CharacterSkillModuleSelection', lambda: True):
                raise RuntimeError('bounded failure')
        checks = iter([True, False])
        with self.assertRaises(ConnectionAbortedError):
            with lanes.acquire('AcquiredTraitModuleSelection', lambda: next(checks, False)) as disconnected:
                disconnected.require_connected()
        with lanes.acquire('CharacterSkillModuleSelection', lambda: True) as restored_first:
            with lanes.acquire('AcquiredTraitModuleSelection', lambda: True) as restored_second:
                self.assertEqual({restored_first.slot, restored_second.slot}, {2, 3})
        with lanes.acquire('Persona', lambda: True) as restored_ordinary:
            self.assertEqual(restored_ordinary.slot, 1)
        with lanes.acquire('BubbleLine', lambda: True) as restored_speech:
            self.assertEqual(restored_speech.slot, 0)

    def test_three_lanes_keep_speech_and_context_capacity_independent(self):
        base = bridge.Engine('http://127.0.0.1:1', 'unit-key', 10240)
        speech = base.for_request('BubbleLine', 3, lambda: True)
        short = base.for_request('SocialRumor', 3, lambda: True)
        history = base.for_request('EvolutionHistoryLegacyV2', 3, lambda: True)
        background = base.for_request('CharacterSkillModuleSelection', 3, lambda: True)
        self.assertEqual([background.slot, short.slot, speech.slot], [2, 1, 0])
        self.assertEqual(history.slot, 1)
        self.assertEqual(base.for_request('EvolutionHistoryLegacyV2', 4, lambda: True).slot, 1)
        self.assertEqual([e.context_size for e in (background, short, speech)], [10240] * 3)
        self.assertEqual(base.profile, None)

    def test_three_lane_pool_waits_for_bulk_without_blocking_speech_or_short(self):
        base = bridge.Engine('http://127.0.0.1:1', 'unit-key', 12288)
        lanes = bridge.RequestLanes(base, 3)
        with lanes.acquire('CharacterSkillModuleSelection', lambda: True) as bulk:
            with lanes.acquire('CharacterRecord', lambda: True) as short:
                with lanes.acquire('BubbleLine', lambda: True) as speech:
                    self.assertEqual([bulk.slot, short.slot, speech.slot], [2, 1, 0])
                    self.assertEqual([value.context_size for value in (bulk, short, speech)], [12288] * 3)
                    attempts = iter([True, False])
                    with self.assertRaises(ConnectionAbortedError):
                        with lanes.acquire('AcquiredTraitModuleSelection', lambda: next(attempts, False)):
                            self.fail('A second bulk request entered an occupied three-lane pool')
        with lanes.acquire('AcquiredTraitModuleSelection', lambda: True) as resumed:
            self.assertEqual(resumed.slot, 2)

    def test_request_factory_keeps_profile_slot_context_and_opener_isolated(self):
        self.assertEqual(Path(bridge.__file__).resolve(), BRIDGE_PATH)
        expected_interactive = {
            'Persona', 'MacroGoal', 'MoodImpulse', 'SocialRumor',
            'CharacterRecord', 'BubbleLine', 'EvolutionHistoryLegacyV2',
        }
        self.assertEqual(set(bridge.INTERACTIVE_PROFILES), expected_interactive)

        base = bridge.Engine('http://127.0.0.1:1', 'key', 12288,
                             profile='health-base', slot=0)
        connected = lambda: True
        children = [base.for_request(profile, 2, connected)
                    for profile in sorted(expected_interactive)]
        background = base.for_request('CharacterSkillModuleSelection', 2, connected)

        self.assertEqual(base.profile, 'health-base')
        self.assertEqual(base.slot, 0)
        self.assertEqual(base.context_size, 12288)
        for child in children:
            self.assertEqual(child.slot, 0)
            self.assertEqual(child.context_size, 12288)
            self.assertIs(child.caller_connected, connected)
            self.assertIsNot(child.opener, base.opener)
        self.assertEqual(background.slot, 1)
        self.assertEqual(background.context_size, 12288)
        self.assertIsNot(background.opener, base.opener)
        self.assertEqual(len({id(child.opener) for child in children + [background]}),
                         len(children) + 1)

        for profile in expected_interactive | {'CharacterSkillModuleSelection'}:
            child = base.for_request(profile, 1, connected)
            self.assertEqual(child.slot, 0)
            self.assertEqual(child.context_size, 12288)

    def test_concurrent_request_engines_keep_profile_and_native_slot_isolated(self):
        base = bridge.Engine('http://127.0.0.1:1', 'key', 12288,
                             profile='health-base', slot=0)
        interactive = base.for_request('BubbleLine', 2, lambda: True)
        background = base.for_request('CharacterSkillModuleSelection', 2, lambda: True)
        barrier = threading.Barrier(2)
        bodies = {'BubbleLine': [], 'CharacterSkillModuleSelection': []}
        interactive.http = self.fake_transport(
            interactive, 'BubbleLine', barrier, bodies['BubbleLine'])
        background.http = self.fake_transport(
            background, 'CharacterSkillModuleSelection', barrier,
            bodies['CharacterSkillModuleSelection'])

        with TemporaryDirectory() as temporary:
            root = Path(temporary)
            folders = {profile: root / profile for profile in bodies}
            for folder in folders.values():
                folder.mkdir()
            with ThreadPoolExecutor(max_workers=2) as workers:
                interactive_result = workers.submit(
                    interactive.call, self.messages('BubbleLine'),
                    self.schema('BubbleLine'), lambda native: native,
                    folders['BubbleLine'], 'selection', 32)
                background_result = workers.submit(
                    background.call, self.messages('CharacterSkillModuleSelection'),
                    self.schema('CharacterSkillModuleSelection'), lambda native: native,
                    folders['CharacterSkillModuleSelection'], 'selection', 32)
                self.assertEqual(interactive_result.result(timeout=10),
                                 {'value': 'BubbleLine'})
                self.assertEqual(background_result.result(timeout=10),
                                 {'value': 'CharacterSkillModuleSelection'})

        self.assertEqual(base.profile, 'health-base')
        self.assertEqual(interactive.profile, 'BubbleLine')
        self.assertEqual(background.profile, 'CharacterSkillModuleSelection')
        for profile, expected_slot in [('BubbleLine', 0),
                                       ('CharacterSkillModuleSelection', 1)]:
            self.assertEqual(len(bodies[profile]), 1)
            body = bodies[profile][0]
            self.assertIs(body['cache_prompt'], True)
            self.assertEqual(body['id_slot'], expected_slot)
            self.assertEqual(body['n_predict'], 32)

    def test_call_rejects_a_native_value_outside_the_original_schema(self):
        engine = bridge.Engine('http://127.0.0.1:1', 'key', 12288).for_request(
            'BubbleLine', 2, lambda: True)
        engine.http = self.fake_transport(engine, 'not-allowed')
        with TemporaryDirectory() as temporary:
            folder = Path(temporary) / 'request'
            folder.mkdir()
            with self.assertRaises(ValidationError):
                engine.call(self.messages('BubbleLine'), self.schema('allowed'),
                            lambda native: native, folder, 'selection', 32)

    def test_disconnected_caller_stops_before_native_http(self):
        checks = []

        def disconnected():
            checks.append(True)
            return False

        engine = bridge.Engine('http://127.0.0.1:1', 'key', 12288).for_request(
            'BubbleLine', 2, disconnected)
        with TemporaryDirectory() as temporary:
            folder = Path(temporary) / 'request'
            folder.mkdir()
            with patch.object(engine.opener, 'open') as native_open:
                with self.assertRaises(ConnectionAbortedError):
                    engine.call(self.messages('BubbleLine'), self.schema('BubbleLine'),
                                lambda native: native, folder, 'selection', 32)
                native_open.assert_not_called()
        self.assertTrue(checks)


if __name__ == '__main__':
    unittest.main()
