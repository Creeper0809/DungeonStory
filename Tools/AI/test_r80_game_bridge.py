"""Boundary checks for the new transport, using the original public schemas."""
from copy import deepcopy
import hashlib
import json
from pathlib import Path
from tempfile import TemporaryDirectory
import unittest
from unittest.mock import patch

import r80_game_bridge as bridge


class CallerBindingTests(unittest.TestCase):
    @staticmethod
    def fixed_facility_schema():
        schema = json.loads((Path(__file__).resolve().parent / 'fixtures/facility_singleton_schema.json').read_bytes())
        schema['properties']['selectionId']['enum'] = ['presentation:facility:fixture']
        for field, value in [('positiveModuleIds', 'facility:fixture'), ('evidenceFactIds', 'facility-fact:fixture')]:
            node = schema['properties'][field]
            node['minItems'] = node['maxItems'] = 1
            node['items']['enum'] = [value]
        schema['properties']['drawbackModuleIds']['enum'] = [[]]
        schema['properties']['drawbackModuleIds']['maxItems'] = 0
        return schema

    def test_fixed_facility_cannot_choose_from_multiple_effects_or_evidence(self):
        schema = self.fixed_facility_schema()
        expected = dict(selectionId='presentation:facility:fixture', positiveModuleIds=['facility:fixture'],
                        drawbackModuleIds=[], evidenceFactIds=['facility-fact:fixture'])
        before = deepcopy(schema)
        self.assertEqual(bridge.fixed_facility_fields(schema), expected)
        self.assertEqual(schema, before)
        for field in ('positiveModuleIds', 'evidenceFactIds'):
            changed = deepcopy(schema)
            changed['properties'][field]['items']['enum'].append('second-choice')
            self.assertIsNone(bridge.fixed_facility_fields(changed))
        changed = deepcopy(schema)
        changed['properties']['evidenceFactIds']['minItems'] = 0
        self.assertIsNone(bridge.fixed_facility_fields(changed))

    def test_fixed_facility_grammar_keeps_all_original_fields_and_bounds(self):
        from test_r80_name_grammar import regex_for
        schema = self.fixed_facility_schema()
        original = deepcopy(schema)
        native, decoder, constraint = bridge.decoder_for(schema, lambda value: value, 'fixed-facility')
        rules = dict(line.split(' ::= ', 1) for line in decoder['grammar'].splitlines() if line)
        pattern = regex_for(rules, 'root')
        result = dict(bridge.fixed_facility_fields(schema), displayName='맑은 터전',
                      narrativeFlavor='이 시설은 청소 속도가 빨라진다.')
        def accepts(value):
            return bool(pattern.fullmatch(json.dumps(value, ensure_ascii=False, separators=(',', ':'))))
        self.assertTrue(accepts(result))
        for field in ('selectionId', 'positiveModuleIds', 'drawbackModuleIds', 'evidenceFactIds'):
            modified = deepcopy(result)
            modified[field] = 'other' if field == 'selectionId' else ['other']
            self.assertFalse(accepts(modified), field)
        for field, text in [('displayName', '가' * 33), ('narrativeFlavor', '가' * 181),
                            ('narrativeFlavor', '효과가 3배가 된다.')]:
            self.assertFalse(accepts(dict(result, **{field: text})), field)
        self.assertEqual(native, original)
        self.assertEqual(schema, original)
        self.assertFalse(constraint['outputRepair'])

    def test_fixed_facility_generates_full_response_once_without_rewriting_it(self):
        schema = self.fixed_facility_schema()
        row = dict(profile='FacilityEvolutionModuleSelection', wire=dict(
            messages=[dict(role='system', content='caller contract'), dict(role='user', content='public facts')],
            response_format=dict(json_schema=dict(schema=schema))))
        original = deepcopy(row)
        result = dict(bridge.fixed_facility_fields(schema), displayName='맑은 터전',
                      narrativeFlavor='이 시설은 청소 속도가 빨라진다.')
        class Engine:
            calls = []
            def call(self, messages, original_schema, transform, folder, stage, limit):
                self.calls.append((deepcopy(messages), deepcopy(original_schema), stage))
                return result
        engine = Engine()
        with TemporaryDirectory() as directory, patch.object(bridge.r80_request, 'validate_request') as validate:
            actual = bridge.generate_fixed_facility(object(), engine, row, Path(directory))
            validate.assert_called_once()
        self.assertIs(actual, result)
        self.assertEqual(row, original)
        self.assertEqual(len(engine.calls), 1)
        self.assertEqual(engine.calls[0][0][:2], original['wire']['messages'])
        self.assertEqual(engine.calls[0][1], original['wire']['response_format']['json_schema']['schema'])
        self.assertEqual(engine.calls[0][2], 'fixed-facility')

    def test_active_runtime_points_to_the_current_published_manifest(self):
        root = Path(__file__).resolve().parents[2] / 'Assets' / 'StreamingAssets'
        active = json.loads((root / 'narrative-runtime.json').read_bytes())
        bundle = root / active['relativeRoot'] / 'DungeonStoryLlm'
        self.assertEqual(active['manifestSha256'], hashlib.sha256((bundle / 'manifest.json').read_bytes()).hexdigest())
        manifest = json.loads((bundle / 'manifest.json').read_bytes())
        entry = next(value for value in manifest['supportFiles'] if value['file'] == 'r80_game_bridge.py')
        self.assertEqual(entry['sha256'], hashlib.sha256((bundle / entry['file']).read_bytes()).hexdigest())

    def test_trait_number_exception_uses_only_the_exact_authoritative_name(self):
        from live_module_selection_contract import has_unbound_authored_number as invalid
        for name in ['로웬 4', '다인 ５', 'Arin (2)']:
            self.assertFalse(invalid(name + '는 손이 빨라진다.', name))
            self.assertFalse(invalid('이름은 ' + name + '다.', name))
            for value in [name + '2', 'A' + name, name + '의 힘 3', name + '의 힘 ３',
                          name + '의 힘 %', name + '의 힘 ％', '다른 7의 힘']:
                self.assertTrue(invalid(value, name), (name, value))
        self.assertTrue(invalid('로웬 4는 힘이 난다.', None))
    def test_authored_stage_messages_preserve_caller_prefix_and_isolate_inputs(self):
        wire = dict(messages=[dict(role='system', content='original system', metadata=dict(tags=['x'])),
                              dict(role='user', content='원래 요청과 공개 사실')],
                    response_format=dict(json_schema=dict(schema=dict(type='object'))))
        before = deepcopy(wire)
        stage = dict(previousCandidateChoices=[dict(selectionId='S0', evidenceFactIds=['E0'])],
                     previousCandidateNames=['첫 이름'])
        expected_stage = deepcopy(stage)
        selection = bridge.authored_stage_messages(wire, '선택 단계 지시', stage)
        prose = bridge.authored_stage_messages(wire, '설명 단계 지시', stage)
        self.assertEqual(selection[:2], before['messages'])
        self.assertEqual(prose[:2], before['messages'])
        self.assertEqual(selection[:2], prose[:2])
        self.assertEqual(selection[2], dict(role='system', content='선택 단계 지시'))
        self.assertEqual(prose[2], dict(role='system', content='설명 단계 지시'))
        self.assertEqual(json.loads(selection[3]['content']), expected_stage)
        self.assertEqual(json.loads(prose[3]['content']), expected_stage)
        self.assertEqual(wire, before)
        selection[0]['metadata']['tags'].append('mutated')
        stage['previousCandidateChoices'][0]['evidenceFactIds'].append('E1')
        stage['previousCandidateNames'].append('다음 이름')
        self.assertEqual(wire, before)
        self.assertEqual(prose[:2], before['messages'])
        self.assertEqual(json.loads(selection[3]['content']), expected_stage)

    def test_authored_three_candidates_keep_stage_order_and_prior_choices(self):
        def candidate_schema(index):
            return dict(type='object', additionalProperties=False,
                required=['selectionId', 'positiveModuleIds', 'drawbackModuleIds',
                          'evidenceFactIds', 'displayName', 'narrativeFlavor'],
                properties=dict(selectionId=dict(enum=['S' + str(index)]),
                    positiveModuleIds=dict(type='array', items=dict(enum=['work_speed'])),
                    drawbackModuleIds=dict(type='array', items=dict(type='string')),
                    evidenceFactIds=dict(type='array', items=dict(enum=['E0'])),
                    displayName=dict(enum=['이름' + str(index)]),
                    narrativeFlavor=dict(type='string')))

        schemas = [candidate_schema(index) for index in range(3)]
        output_schema = dict(type='object', additionalProperties=False,
            required=['requestRef', 'candidates'], properties=dict(
                requestRef=dict(enum=['request:exact']),
                candidates=dict(type='array', minItems=3, maxItems=3,
                                items=deepcopy(schemas), additionalItems=False)))
        wire = dict(messages=[dict(role='system', content='caller system'),
                              dict(role='user', content='character=유나 2\ncaller original public facts')],
                    response_format=dict(json_schema=dict(schema=output_schema)))
        original_wire = deepcopy(wire)
        table = dict(C=[['S' + str(index), index, None, [0], []] for index in range(3)],
                     M=[['work_speed', 'work speed']],
                     X_columns=['mechanics', 'context', 'primary_P'], P=['original primary rule'],
                     X=[['target=Self', 'exact original context', 0] for unused in range(3)])

        class SpyEngine:
            def __init__(self): self.calls = []
            def call(self, messages, schema, transform, folder, stage, limit, excluded_names=()):
                index = int(folder.name.rsplit('-', 1)[1])
                self.calls.append((deepcopy(messages), deepcopy(schema), stage, limit, list(excluded_names)))
                if stage == 'authored-selection':
                    return dict(selectionId='S' + str(index), positiveModuleIds=['work_speed'],
                                drawbackModuleIds=[], evidenceFactIds=['E0'])
                return dict(displayName='이름' + str(index), narrativeFlavor='설명')

        engine = SpyEngine()
        with TemporaryDirectory() as folder, patch.object(bridge, 'authored_skill_schema',
                side_effect=lambda unused_wire, index, previous: deepcopy(schemas[index])):
            result = bridge.generate_authored_skills(engine, wire, Path(folder), table)
        self.assertEqual(wire, original_wire)
        self.assertEqual([call[2] for call in engine.calls],
                         ['authored-selection'] * 3 + ['authored-prose'] * 3)
        self.assertTrue(all(call[3] == 512 for call in engine.calls))
        self.assertEqual(result['requestRef'], 'request:exact')
        self.assertEqual(len(result['candidates']), 3)
        for index in range(3):
            call = engine.calls[index]
            self.assertEqual(call[0][:2], original_wire['messages'])
            candidate_input = json.loads(call[0][-1]['content'])
            previous = result['candidates'][:index]
            self.assertEqual(candidate_input['previousCandidateChoices'], [
                {key: value for key, value in candidate.items() if key not in ('displayName', 'narrativeFlavor')}
                for candidate in previous])
            self.assertEqual(candidate_input['previousCandidateNames'], [])
            self.assertEqual(call[4], [])
            self.assertEqual(candidate_input['currentOffer'][0], 'S' + str(index))
            self.assertEqual(candidate_input['offeredEffects'], [dict(id='work_speed', meaning='work speed')])
            self.assertEqual(set(call[1]['properties']),
                             set(schemas[index]['properties']) - {'displayName', 'narrativeFlavor'})
            prose = engine.calls[index + 3]
            prose_input = json.loads(prose[0][1]['content'])
            self.assertEqual(prose_input['character'], '유나 2')
            self.assertEqual(prose_input['currentMechanics'], dict(mechanics=table['X'][index][0],
                context=table['X'][index][1], primaryContract=table['P'][table['X'][index][2]]))
            self.assertEqual(prose_input['selection'], {key: value for key, value in
                             result['candidates'][index].items() if key not in ('displayName', 'narrativeFlavor')})
            self.assertEqual(prose_input['selectedEffects'], [dict(id='work_speed', meaning='work speed')])
            self.assertEqual(prose[4], [candidate['displayName'] for candidate in previous])
            self.assertEqual(set(prose[1]['properties']), {'displayName', 'narrativeFlavor'})
            self.assertNotIn('caller original public facts', json.dumps(prose[0], ensure_ascii=False))

        class InvalidSelection(SpyEngine):
            def call(self, *args, **kwargs):
                value = super().call(*args, **kwargs)
                if args[4] == 'authored-selection':
                    value['positiveModuleIds'] = ['not-offered']
                return value

        from jsonschema.exceptions import ValidationError
        invalid = InvalidSelection()
        with TemporaryDirectory() as folder, patch.object(bridge, 'authored_skill_schema',
                side_effect=lambda unused_wire, index, previous: deepcopy(schemas[index])):
            with self.assertRaises(ValidationError):
                bridge.generate_authored_skills(invalid, wire, Path(folder), table)
        self.assertEqual([call[2] for call in invalid.calls], ['authored-selection'])
        self.assertEqual(wire, original_wire)

    def test_selected_prose_keeps_exact_mechanics_and_every_selected_effect(self):
        wire = dict(messages=[dict(role='user', content='character=유나 2\noriginal full facts')])
        table = dict(C=[['S0', 0, '1..3', [0, 1, 2], []]],
                     X_columns=['mechanics', 'context', 'primary_P'], P=['원래 주효과 규칙'],
                     X=[['target=Ally', '아군 하나에게 적용한다.', 0]],
                     M=[['heal', '체력을 회복한다.'], ['cleanse', '해로운 상태를 제거한다.'],
                        ['guard', '선택하지 않은 피해 감소 설명']])
        decision = dict(selectionId='S0', positiveModuleIds=['heal', 'cleanse'],
                        drawbackModuleIds=[], evidenceFactIds=['E0', 'E1'])
        previous = [dict(displayName='이전 이름', narrativeFlavor='다른 후보의 설명')]
        original = deepcopy((wire, table, decision, previous))
        result = bridge.authored_selected_prose_messages(wire, table, 0, decision, previous)
        payload = json.loads(result[1]['content'])
        self.assertEqual(payload['currentMechanics'], dict(mechanics=table['X'][0][0],
            context=table['X'][0][1], primaryContract=table['P'][0]))
        self.assertEqual(payload['selection'], decision)
        self.assertEqual(payload['selectedEffects'],
                         [dict(id='heal', meaning='체력을 회복한다.'), dict(id='cleanse', meaning='해로운 상태를 제거한다.')])
        self.assertEqual(payload['previousCandidateNames'], ['이전 이름'])
        self.assertNotIn('선택하지 않은 피해 감소 설명', json.dumps(result, ensure_ascii=False))
        self.assertNotIn('다른 후보의 설명', json.dumps(result, ensure_ascii=False))
        self.assertEqual((wire, table, decision, previous), original)

    def test_selected_prose_rejects_unknown_choice_and_ambiguous_subject(self):
        wire = dict(messages=[dict(role='user', content='character=유나')])
        table = dict(C=[['S0', 0, '1..1', [0], []]], X=[['target=Self']], M=[['heal', '체력 회복']])
        decision = dict(selectionId='S0', positiveModuleIds=['guard'], drawbackModuleIds=[], evidenceFactIds=['E0'])
        with self.assertRaisesRegex(ValueError, 'outside'):
            bridge.authored_selected_prose_messages(wire, table, 0, decision, [])
        decision['positiveModuleIds'] = ['heal']
        wire['messages'][0]['content'] += '\ncharacter=세나'
        with self.assertRaisesRegex(ValueError, 'One caller'):
            bridge.authored_selected_prose_messages(wire, table, 0, decision, [])

    def test_authored_prose_context_is_lossless_and_rejects_broken_references(self):
        table = dict(X_columns=['mechanics', 'context', 'primary_P'],
                     X=[['target=Ally; trigger=WorkCompleted', '아군에게 적용한다.', 1]],
                     P=['unselected primary rule', '원래 주효과 원문'])
        before = deepcopy(table)
        result = bridge.authored_prose_context(table, 0)
        self.assertEqual(result, dict(mechanics=table['X'][0][0], context=table['X'][0][1],
                                      primaryContract=table['P'][1]))
        self.assertEqual(table, before)
        for index in [-1, True, 1]:
            with self.subTest(index=index), self.assertRaises(ValueError):
                bridge.authored_prose_context(table, index)
        for invalid in [-1, True, 2, '1']:
            changed = deepcopy(table)
            changed['X'][0][2] = invalid
            with self.subTest(reference=invalid), self.assertRaises(ValueError):
                bridge.authored_prose_context(changed, 0)
        for columns in [[], ['context', 'mechanics', 'primary_P'], ['mechanics', 'context', 'primaryContract']]:
            changed = deepcopy(table)
            changed['X_columns'] = columns
            with self.subTest(columns=columns), self.assertRaises(ValueError):
                bridge.authored_prose_context(changed, 0)

    def test_compact_skill_prose_retains_every_selected_property_and_decision(self):
        import build_aligned_language_training as aligned
        import practical_prototype_prose_view as practical
        upstream = ' '.join((aligned.SCOPE, practical.STYLE, practical.SUMMARY, practical.PRIMARY))
        modules = dict(
            heal=dict(polarity='positive', meaning='대상의 체력을 회복한다.', requiredEvidence=['E0']),
            aura=dict(polarity='positive', meaning='완료 후 아군의 힘을 높이고 자신의 속도를 낮춘다.',
                      receiver='Ally', condition=dict(event='completion', scope='nearby')),
            fatigue=dict(polarity='negative', meaning='자신의 피로가 늘어난다.', requiredEvidence=['E1']))
        decision = dict(selectionId='S1', positiveModuleIds=['heal', 'aura'],
                        drawbackModuleIds=['fatigue'], evidenceFactIds=['E0', 'E1'])
        document = dict(profile='CharacterSkillModuleSelection', selection=deepcopy(decision),
                        characterName='유나 2', futureTarget='Ally', futureTargetLabel='아군',
                        mechanics='target=Ally; trigger=Completion; area=Square',
                        explicitPublicSubject=dict(character='유나 2'), unrelated=dict(retain=True))
        schema = dict(type='object', description='original schema unchanged')
        source = [dict(role='system', content=upstream),
                  dict(role='user', content=json.dumps(document, ensure_ascii=False))]
        row = dict(profile='CharacterSkillModuleSelection', _gamePreviousCandidateNames=['기존 기술'])
        before = deepcopy((row, source, decision, modules, schema))
        with patch.object(bridge.scoped_auxiliary_view, 'prose_messages', return_value=(source, schema)), \
                patch.object(bridge.candidate_task_view, 'contract', return_value=dict(modules=modules)):
            messages, actual_schema = bridge.game_prose_messages(row, 1, decision)
        payload = json.loads(messages[1]['content'])
        self.assertEqual(payload['selectedModules'], modules)
        self.assertEqual(payload['selection'], decision)
        self.assertEqual(payload['previousCandidateNames'], ['기존 기술'])
        for field, value in document.items():
            self.assertEqual(payload[field], value)
        self.assertEqual(payload['descriptionScope'], dict(
            allSelectedModuleIds=['heal', 'aura', 'fatigue'], omitSecondaryEffects=False))
        self.assertNotIn('selectedPositiveEffects', payload)
        self.assertNotIn('selectedSeparateDrawbacks', payload)
        self.assertEqual(messages[0]['content'], bridge.SKILL_PROSE_GUIDANCE)
        self.assertEqual(actual_schema, schema)
        self.assertEqual((row, source, decision, modules, schema), before)

    def test_compact_skill_prose_still_rejects_decision_drift(self):
        source = [dict(role='system', content='unused'),
                  dict(role='user', content=json.dumps(dict(selection=dict(selectionId='other'))))]
        with patch.object(bridge.scoped_auxiliary_view, 'prose_messages', return_value=(source, {})):
            with self.assertRaisesRegex(ValueError, 'decision changed'):
                bridge.game_prose_messages(dict(profile='CharacterSkillModuleSelection'), 0,
                                           dict(selectionId='S0'))

    def test_trait_evidence_sorting_is_enforced_before_generation(self):
        import sys, itertools
        sys.path.insert(0, str(Path(__file__).parent))
        from test_r80_name_grammar import regex_for
        values = ['E0', 'E1', 'E2']
        gameplay, other = ['E1'], ['E0', 'E2']
        branches = []
        for length in range(1, 4):
            for first in range(length):
                slots = [other] * first + [gameplay] + [values] * (length-first-1)
                branches.append(dict(type='array', minItems=length, maxItems=length,
                    items=[dict(type='string', enum=slot) for slot in slots], additionalItems=False))
        field = dict(type='array', minItems=1, maxItems=3, items=dict(type='string', enum=values), anyOf=branches)
        native = dict(anyOf=[dict(type='object', properties=dict(decision=dict(type='object',
            properties=dict(evidenceFactIds=field), required=['evidenceFactIds'], additionalProperties=False)),
            required=['decision'], additionalProperties=False)])
        rules = dict(line.split(' ::= ', 1) for line in bridge.sorted_trait_grammar(native).splitlines())
        import re
        pattern = re.compile(regex_for(rules, 'root'))
        for length in range(4):
            for candidate in itertools.product(values, repeat=length):
                text = json.dumps(dict(decision=dict(evidenceFactIds=list(candidate))), separators=(',', ':'))
                expected = 'E1' in candidate and list(candidate) == sorted(set(candidate))
                self.assertEqual(bool(pattern.fullmatch(text)), expected, candidate)

    def test_auxiliary_packet_keeps_model_facing_reference_suffix(self):
        event = dict(message='소량의 피해: 출혈', amount=0.00012, outcome='damaged')
        packet = dict(profile='CharacterRecord', publicFacts=dict(sourceEvent=event),
                      originalCallerPrompt='원 사건과 작성 지시')
        suffix = '\nAvailable character facts (request-local references):\nF01 = 실제 사실\n'
        text = bridge.json.dumps(packet, ensure_ascii=False) + suffix
        messages = [dict(role='system', content='기존 지시'), dict(role='user', content=text)]
        result = bridge.game_auxiliary_messages('CharacterRecord', messages)
        actual = bridge.json.loads(result[1]['content'])
        self.assertEqual(actual['publicFacts']['sourceEvent'], event)
        self.assertEqual(actual['callerBackground'], suffix)
        self.assertEqual(actual['originalCallerPrompt'], packet['originalCallerPrompt'])
        self.assertEqual(messages[1]['content'], text)

    def test_record_direct_event_instruction_preserves_original_contract(self):
        packet = dict(profile='CharacterRecord', publicFacts=dict(sourceEvent=dict(
            message='작업 완료: 제본: 기록실', outcome='completed')),
            publicBindings=dict(requiredSubject='도린이', requiredExactPhrases=['제본', '기록실']),
            originalCallerPrompt='원래 기록 작성 규칙')
        messages = [dict(role='system', content='원래 system'),
                    dict(role='user', content=json.dumps(packet, ensure_ascii=False))]
        before = deepcopy(messages)
        actual = bridge.game_auxiliary_messages('CharacterRecord', messages)
        self.assertEqual(actual[1:], before[1:])
        self.assertEqual(messages, before)
        self.assertIn('requiredExactPhrases', actual[0]['content'])
        self.assertIn('직접 서술', actual[0]['content'])
        self.assertIn('추가 행동이나 결과를 만들지 않는다', actual[0]['content'])

    def test_typed_record_view_preserves_every_public_fact_binding_and_reference(self):
        packet = dict(profile='CharacterRecord', recordPromptVersion=2,
            publicFacts=dict(publicCharacterFacts=dict(name='도린', species='human', role='scribe',
                personaTrait='차분함', personaFlavor='꼼꼼하게 기록한다.', recentVisibleRecords='앞선 기록'),
                sourceEvent=dict(message='작업 완료: 제본: 기록실', tag='work', eventKind='work',
                    actionId='work:binding', target='책', place='기록실', outcome='completed',
                    reasonCode='', amount=2.5, quantity=3)),
            publicBindings=dict(requiredSubject='도린이', requiredExactPhrases=['제본', '기록실']),
            callerBackground='F01=실제 사실\nM02=원래 문화 참조', writingFeedback='keep exact source words',
            originalCallerPrompt='원래 작성 지시와 같은 사실의 중복 표기')
        messages = [dict(role='system', content='original system'), dict(role='user', content=json.dumps(packet, ensure_ascii=False))]
        before = deepcopy(messages)
        actual = bridge.record_writing_messages(messages)
        document = json.loads(actual[1]['content'])
        self.assertEqual(document, {key: value for key, value in packet.items() if key != 'originalCallerPrompt'})
        self.assertEqual(document['publicFacts'], packet['publicFacts'])
        self.assertEqual(document['publicBindings'], packet['publicBindings'])
        self.assertEqual(document['callerBackground'], packet['callerBackground'])
        self.assertEqual(messages, before)
        self.assertIn('영문 이름, 숫자와 띄어쓰기', actual[0]['content'])
        self.assertNotIn(packet['originalCallerPrompt'], actual[1]['content'])

    def test_record_view_keeps_legacy_inputs_and_rejects_incomplete_v2(self):
        messages = [dict(role='system', content='original'), dict(role='user', content='legacy record prompt')]
        self.assertEqual(bridge.record_writing_messages(messages), messages)
        malformed = dict(profile='CharacterRecord', recordPromptVersion=2, publicFacts=dict(sourceEvent={}),
                         publicBindings={}, originalCallerPrompt='legacy')
        with self.assertRaisesRegex(ValueError, 'Complete V2'):
            bridge.record_writing_messages([messages[0], dict(role='user', content=json.dumps(malformed))])

    def test_auxiliary_event_background_boundary_is_lossless(self):
        event = '피해 0: 출혈\r\n\r\nAvailable character facts (request-local references):\r\nF01 = 과거 치료 경험'
        packet = dict(profile='BubbleLine', publicFacts=dict(sourceEvent=event, outcome='outcome:damaged'))
        messages = [dict(role='system', content='original'), dict(role='user', content=json.dumps(packet))]
        actual = json.loads(bridge.game_auxiliary_messages('BubbleLine', messages)[1]['content'])
        self.assertEqual(actual['publicFacts']['sourceEvent'] + actual['callerBackground'], event)
        self.assertEqual(actual['publicFacts']['outcome'], 'outcome:damaged')
        self.assertNotIn('과거 치료', actual['publicFacts']['sourceEvent'])
        raw = 'Character:\nrequiredSubject: Arin이\nSource event:\ntag: health\nmessage: 피해 3.5: 출혈\nrequiredAnchorWords: 피해,3.5,출혈\nRecent visible records to avoid repeating their phrasing:\n과거 치료'
        result = bridge.game_auxiliary_messages('CharacterRecord', [messages[0], dict(role='user', content=raw)])
        actual = json.loads(result[1]['content'])
        self.assertEqual(actual['publicFacts']['sourceEvent'], '피해 3.5: 출혈')
        self.assertEqual(actual['originalCallerPrompt'], raw)

    def test_auxiliary_fact_focus_keeps_original_public_input(self):
        messages = [dict(role='system', content='original contract'), dict(role='user', content='피해 3.5: 출혈')]
        original = deepcopy(messages)
        for profile in ['BubbleLine', 'CharacterRecord', 'Persona', 'SocialRumor']:
            actual = bridge.game_auxiliary_messages(profile, messages)
            self.assertEqual(actual[1:], original[1:])
            self.assertIn(bridge.scoped_auxiliary_view.INSTRUCTIONS[profile], actual[0]['content'])
            self.assertIn('소수점', actual[0]['content'])
        self.assertEqual(messages, original)
        self.assertIs(bridge.game_auxiliary_messages('CharacterSkillModuleSelection', messages), messages)

    def test_closed_tuple_union_keeps_root_and_extra_branch_constraints(self):
        import itertools
        root = dict(type='array', minItems=2, maxItems=2, uniqueItems=True,
            items=dict(type='string', enum=['a', 'b']), anyOf=[dict(type='array', minItems=2, maxItems=2,
                items=[dict(enum=['a']), dict(enum=['a', 'b'])], additionalItems=False)])
        for extra_branch_constraint in [False, True]:
            schema = deepcopy(root)
            if extra_branch_constraint:
                schema['anyOf'][0]['not'] = dict(enum=[['a', 'b']])
            expected = {parts for parts in itertools.product(['a', 'b'], repeat=2)
                        if bridge.Draft7Validator(schema).is_valid(list(parts))}
            if expected:
                self.assertEqual(set(bridge.finite_array_intersection(json.dumps(schema, sort_keys=True))), expected)
            else:
                with self.assertRaises(ValueError): bridge.finite_array_intersection(json.dumps(schema, sort_keys=True))

    def test_unscoped_game_observation_keeps_the_full_reading_domain(self):
        messages = [dict(role='system', content='reading'), dict(role='user', content=json.dumps(
            dict(offer={}, remainingPlainHeaders=[], facts={'e1': {}, 'e2': {}})))]
        schema = dict(properties=dict(latestOwnOutcome=dict(enum=['quote one', 'quote two'])))
        with patch.object(bridge.followup_scope_view, 'original_reading', return_value=(deepcopy(messages), deepcopy(schema))):
            actual_messages, actual_schema = bridge.game_reading_messages({}, 0, [])
        self.assertEqual(actual_schema, schema)
        self.assertNotIn('currentFollowupFactIds', json.loads(actual_messages[1]['content']))
        self.assertTrue(actual_messages[0]['content'].endswith(bridge.single_decision_view.READING_RULE))
        messages[1]['content'] = json.dumps(dict(offer={}, remainingPlainHeaders=['양쪽 근거를 인용'], facts={}))
        with patch.object(bridge.followup_scope_view, 'original_reading', return_value=(messages, schema)):
            with self.assertRaises(ValueError): bridge.game_reading_messages({}, 0, [])

    def test_required_request_reference_is_generated_in_every_closed_decision(self):
        original = dict(anyOf=[dict(properties=dict(decision=dict(enum=[dict(choice='a'), dict(choice='b')])) )])
        schema = dict(properties=dict(decision=dict(type='object', additionalProperties=False,
            required=['choice', 'requestRef'], properties=dict(choice=dict(enum=['a', 'b']),
            requestRef=dict(type='string', enum=['[[request:0]]'])))))
        before = deepcopy(original)
        with patch.object(bridge.single_decision_view, 'selection_native', return_value=deepcopy(original)):
            actual = bridge.game_selection_native({}, 0, [], {}, schema)
        decisions = actual['anyOf'][0]['properties']['decision']['enum']
        self.assertEqual([value['requestRef'] for value in decisions], ['[[request:0]]'] * 2)
        self.assertEqual([{k: v for k, v in value.items() if k != 'requestRef'} for value in decisions],
                         before['anyOf'][0]['properties']['decision']['enum'])
        for value in decisions:
            bridge.Draft7Validator(schema['properties']['decision']).validate(value)
        schema['properties']['decision']['properties']['requestRef']['enum'].append('[[request:1]]')
        with patch.object(bridge.single_decision_view, 'selection_native', return_value=deepcopy(original)):
            with self.assertRaises(ValueError):
                bridge.game_selection_native({}, 0, [], {}, schema)

    def wire(self, profile, prompt):
        schemas = json.loads((Path(bridge.__file__).parent / 'recipe/static-schemas.json').read_bytes())['schemas']
        return dict(messages=[dict(role='user', content=prompt)],
                    response_format=dict(json_schema=dict(schema=deepcopy(schemas[profile]))))

    def test_presentation_uses_only_the_literal_authority(self):
        identity = 'presentation:skill:' + 'a' * 64
        wire = self.wire('CharacterSkill', 'presentationId=' + identity + '\nsourceEvent=식사했다.')
        original = deepcopy(wire)
        bound = bridge.bind_caller_choices('CharacterSkill', wire)
        self.assertEqual(wire, original)
        self.assertEqual(bound['response_format']['json_schema']['schema']['properties']['presentationId']['enum'], [identity])
        self.assertEqual(bound['response_format']['json_schema']['schema']['properties']['narrativeFlavor'],
                         original['response_format']['json_schema']['schema']['properties']['narrativeFlavor'])

    def test_missing_or_invalid_binding_fails_without_an_invented_id(self):
        for prompt in ['sourceEvent=식사했다.', 'presentationId=not-a-real-presentation']:
            with self.assertRaises(Exception):
                bridge.bind_caller_choices('CharacterSkill', self.wire('CharacterSkill', prompt))

    def test_absent_facility_cannot_be_invented_or_used_for_a_goal(self):
        wire = self.wire('MacroGoal', 'hunger: 15.0\nfun: 90.0\nDungeon summary:\ncharacterCount: 1')
        original = deepcopy(wire)
        bound = bridge.bind_caller_choices('MacroGoal', wire)['response_format']['json_schema']['schema']['properties']
        self.assertEqual(wire, original)
        self.assertEqual(bound['targetFacilityId']['enum'], [-1])
        self.assertEqual(bound['targetFacilityTag']['const'], '')
        self.assertNotIn('AvoidFacility', bound['macroGoal']['enum'])
        self.assertNotIn('Vandalize', bound['macroGoal']['enum'])
        self.assertIn('SeekFood', bound['macroGoal']['enum'])
        self.assertIn('SeekFun', bound['macroGoal']['enum'])
        self.assertNotIn('const', bound['macroGoal'])

    def test_module_request_remains_the_original_public_contract(self):
        wire = dict(messages=[dict(role='user', content='original module offer')])
        self.assertIs(bridge.bind_caller_choices('CharacterSkillModuleSelection', wire), wire)

    def test_real_facility_header_keeps_character_rumor_contract(self):
        wire = self.wire('SocialRumor', '- id=9; targetFacilityId=9; name=식당; tag=food')
        original = deepcopy(wire['response_format']['json_schema']['schema']['properties'])
        bound = bridge.bind_caller_choices('SocialRumor', wire)['response_format']['json_schema']['schema']['properties']
        self.assertEqual(bound['targetFacilityId']['enum'], [-1, 9])
        for field in ['targetType', 'targetCharacterId', 'targetCharacterName']:
            self.assertEqual(bound[field], original[field])

    def test_unassigned_context_id_is_not_an_available_target(self):
        wire = self.wire('SocialRumor', '- id=-55; targetFacilityId=-55\n- id=9; targetFacilityId=9')
        node = bridge.bind_caller_choices('SocialRumor', wire)['response_format']['json_schema']['schema']['properties']['targetFacilityId']
        self.assertEqual(node['enum'], [-1, 9])
        with self.assertRaises(Exception):
            bridge.bind_caller_choices('SocialRumor', self.wire('SocialRumor', 'targetFacilityId=-55'))

    def test_finite_pattern_is_proven_redundant_without_changing_enum(self):
        original = dict(type='string', enum=['heal', 'work_speed'], pattern='^[a-z][a-z0-9_]*$', minLength=1)
        native = bridge.finite_schema(original)
        self.assertEqual(native, dict(type='string', enum=['heal', 'work_speed']))
        for candidate in ['heal', 'work_speed', 'invented', '', 0]:
            self.assertEqual(bridge.Draft7Validator(original).is_valid(candidate),
                             bridge.Draft7Validator(native).is_valid(candidate))
        original['enum'].append('INVALID')
        with self.assertRaises(Exception):
            bridge.finite_schema(original)

    def test_trait_name_comes_only_from_the_exact_public_subject(self):
        context = dict(subjectKind='character', subjectId='actor:1', entities=[
            dict(kind='character', entityId='actor:2', displayName='다른 인물'),
            dict(kind='character', entityId='actor:1', displayName='세나 1')])
        text = json.dumps(dict(publicNarrativeContext=context), ensure_ascii=False)
        wire = dict(messages=[dict(role='user', content=text)])
        self.assertTrue(bridge.bind_public_subject('AcquiredTraitModuleSelection', wire)['messages'][0]['content'].endswith('\ncharacter=세나 1\n'))
        self.assertEqual(wire['messages'][0]['content'], text)
        wire['messages'][0]['content'] += '\ncharacter=다른 인물\n'
        with self.assertRaises(Exception):
            bridge.bind_public_subject('AcquiredTraitModuleSelection', wire)

    def test_local_refs_and_gameplay_evidence_intersection_keep_all_legal_arrays(self):
        schema = dict(type='array', minItems=1, maxItems=2,
            items=dict(type='string', enum=['E0', 'E1']),
            anyOf=[dict(type='array', items=[{'$ref':'#/$defs/gameplay'}], minItems=1, maxItems=1, additionalItems=False),
                   dict(type='array', items=[dict(const='E0'), dict(const='E1')], minItems=2, maxItems=2, additionalItems=False)],
            **{'$defs':dict(gameplay=dict(type='string', const='E1'))})
        localized = bridge.inline_local_refs(schema)
        native = bridge.finite_schema(localized)
        self.assertEqual(native['enum'], [['E1'], ['E0', 'E1']])
        for value in [[], ['E0'], ['E1'], ['E0','E1'], ['E1','E0'], ['E1','E1'], ['invented']]:
            self.assertEqual(bridge.Draft7Validator(schema).is_valid(value), bridge.Draft7Validator(native).is_valid(value))

    def test_rumor_target_fields_keep_the_exact_speaker_and_an_available_facility(self):
        wire = self.wire('SocialRumor', 'speakerCharacterId=character:1\nspeakerCharacterName=Yuna\n- targetFacilityId=9')
        schema = bridge.bind_caller_choices('SocialRumor', wire)['response_format']['json_schema']['schema']
        bound = bridge.social_target_schema(schema)
        def accepts(kind, facility, identity, name):
            values = dict(rumorType='Praise', targetType=kind, targetFacilityId=facility, targetFacilityTag='',
                targetCharacterId=identity, targetCharacterName=name, sentiment=0.5, summary='Yuna는 접객 작업을 마쳤다.',
                spreadChance=0.5, trustImpact=0.1, validSeconds=600)
            return bridge.Draft7Validator(bound).is_valid(values)
        self.assertTrue(accepts('Character', -1, 'character:1', 'Yuna'))
        self.assertTrue(accepts('Facility', 9, '', ''))
        self.assertFalse(accepts('Facility', -1, '', ''))
        self.assertFalse(accepts('Character', -1, 'invented', 'Yuna'))
        self.assertFalse(accepts('Character', -1, 'character:1', '다른 인물'))

    def test_rumor_cannot_copy_a_tag_from_the_example(self):
        wire = self.wire('SocialRumor', '- id=0; targetFacilityId=0; name=복도; tag=\nExample: tag=Rest')
        props = bridge.bind_caller_choices('SocialRumor', wire)['response_format']['json_schema']['schema']['properties']
        self.assertEqual(props['targetFacilityTag']['enum'], [''])
        self.assertFalse(bridge.Draft7Validator(props['targetFacilityTag']).is_valid('Rest'))

    def test_subject_agreement_keeps_exact_names_and_numbers(self):
        for name, particle in [('Yuna','는'),('Arin','은'),('미루','는'),('가람','은'),('세나 1','은'),('레오 2','는')]:
            self.assertEqual(bridge.subject_particles(name), (particle,))


if __name__ == '__main__':
    unittest.main()
