"""현재 호출 규격과 공개 사실을 보존하는 작성 지시 경계 검사."""
from copy import deepcopy
import importlib.util
from pathlib import Path
import unittest

from build_aligned_language_training import SYSTEM


SOURCE = Path(__file__).with_name('r80_game_bridge.py')
SPEC = importlib.util.spec_from_file_location('r80_writing_contract_source', SOURCE)
bridge = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(bridge)


class WritingContractTests(unittest.TestCase):
    def test_only_legacy_system_bound_changes_and_all_public_content_is_exact(self):
        messages = [dict(role='system', content=SYSTEM),
                    dict(role='system', content='현재 호출의 24자 상한과 참조 규칙'),
                    dict(role='user', content=bridge.json.dumps(dict(
                        sourceEvent=dict(message='피해 0.00012: 출혈', outcome='outcome:damaged'),
                        callerBackground=SYSTEM, exactReferences=['F01', 'M02']), ensure_ascii=False))]
        schema = dict(type='object', additionalProperties=False, required=['line'],
                      properties=dict(line=dict(type='string', minLength=1, maxLength=24)))
        before_messages, before_schema = deepcopy(messages), deepcopy(schema)
        actual = bridge.bubble_writing_messages(messages, schema)
        self.assertEqual(actual[1:], before_messages[1:])
        self.assertEqual(messages, before_messages)
        self.assertEqual(schema, before_schema)
        self.assertNotIn('80자 이내의 4~10어절', actual[0]['content'])
        self.assertIn('24자 이내', actual[0]['content'])
        self.assertEqual(bridge.bubble_writing_messages(actual, schema), actual)

    def test_writing_bound_comes_from_the_caller_instead_of_a_fixed_global_limit(self):
        for maximum in (12, 24, 80):
            with self.subTest(maximum=maximum):
                schema = dict(properties=dict(line=dict(type='string', maxLength=maximum)))
                messages = [dict(role='system', content=SYSTEM), dict(role='user', content='원문')]
                actual = bridge.bubble_writing_messages(messages, schema)
                self.assertIn(str(maximum) + '자 이내', actual[0]['content'])
                self.assertEqual(actual[1:], messages[1:])

    def test_missing_or_ambiguous_caller_bound_fails_without_mutating_input(self):
        invalid = [dict(properties=dict(line=dict(type='string'))),
                   dict(properties=dict(line=dict(type='string', maxLength=True))),
                   dict(properties=dict(line=dict(type='string', maxLength=0))),
                   dict(properties=dict(line=dict(type='string', maxLength=24), extra=dict(type='string')))]
        messages = [dict(role='system', content=SYSTEM), dict(role='user', content='원문')]
        before = deepcopy(messages)
        for schema in invalid:
            with self.subTest(schema=schema), self.assertRaisesRegex(ValueError, 'caller-bounded'):
                bridge.bubble_writing_messages(messages, schema)
        self.assertEqual(messages, before)


if __name__ == '__main__':
    unittest.main()
