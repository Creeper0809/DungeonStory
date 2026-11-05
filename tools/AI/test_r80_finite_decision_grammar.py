"""Independent schema membership vs the factored native grammar. No model quality claim."""
from copy import deepcopy
import itertools
import json
import unittest

from jsonschema import Draft7Validator
import r80_game_bridge as bridge
from test_r80_name_grammar import regex_for


def branch(decision, proof):
    return dict(type='object', additionalProperties=False,
        required=['decision', 'effectEvidenceFactIds'], properties=dict(
            decision=dict(type='object', enum=[decision]), effectEvidenceFactIds=proof))


class FiniteDecisionTests(unittest.TestCase):
    def test_exact_decision_proof_language_and_escaped_ids(self):
        choices = [dict(selectionId='request:"quoted\\id' + str(i), evidenceFactIds=[key],
            positiveModuleIds=['효과:' + str(i)], drawbackModuleIds=[])
            for i, key in enumerate(['E0', 'E1', 'E2'])]
        proofs = [[[key]] for key in ['E0', 'E1', 'E2', 'not-in-any-choice']]
        native = dict(anyOf=[branch(value, dict(type='array', enum=[proofs[i]]))
                             for i, value in enumerate(choices)])
        # The same decision can legally have another proof in another original branch.
        native['anyOf'].append(branch(choices[0], dict(type='array', enum=[proofs[1]])))
        before = deepcopy(native)
        text = bridge.factored_finite_decision_grammar(native)
        rules = dict(line.split(' ::= ', 1) for line in text.splitlines())
        pattern = regex_for(rules, 'root')
        validator = Draft7Validator(native)
        for decision, proof in itertools.product(choices + [dict(selectionId='unknown')], proofs + [[], [['E0', 'E1']]]):
            value = dict(decision=decision, effectEvidenceFactIds=proof)
            self.assertEqual(bool(pattern.fullmatch(json.dumps(value, ensure_ascii=False, separators=(',', ':')))),
                             validator.is_valid(value), value)
        self.assertEqual(native, before)
        value = dict(decision=choices[0], effectEvidenceFactIds=proofs[0])
        self.assertFalse(pattern.fullmatch(json.dumps(dict(value, extra=True), ensure_ascii=False)))

    def test_shared_local_proof_references_and_unknown_shapes(self):
        native = dict(anyOf=[branch(dict(selectionId='S0'), {'$ref': '#/$defs/shared'})],
            **{'$defs': dict(shared=dict(type='array', enum=[[['E0']], [['E1']]]))})
        text = bridge.factored_finite_decision_grammar(native)
        rules = dict(line.split(' ::= ', 1) for line in text.splitlines())
        pattern = regex_for(rules, 'root')
        for proof in [[['E0']], [['E1']]]:
            self.assertTrue(pattern.fullmatch(json.dumps(dict(decision=dict(selectionId='S0'),
                effectEvidenceFactIds=proof), separators=(',', ':'))))
        for mutation in ['root constraint', 'missing proof', 'symbolic choice', 'extra property', 'oversize literal']:
            value = deepcopy(native)
            if mutation == 'root constraint': value['maxProperties'] = 1
            elif mutation == 'missing proof': value['$defs'] = {}
            elif mutation == 'symbolic choice': value['anyOf'][0]['properties']['decision'] = dict(type='object', properties={})
            elif mutation == 'extra property': value['anyOf'][0]['properties']['other'] = dict(type='string')
            else: value['anyOf'][0]['properties']['decision']['enum'][0]['selectionId'] = 'x' * 513
            with self.subTest(mutation=mutation):
                self.assertIsNone(bridge.factored_finite_decision_grammar(value))

    def test_large_repeated_union_is_factored_without_dropping_values(self):
        choices = [dict(selectionId='public-request:shared-selection', positiveModuleIds=['effect-' + str(i)],
                        drawbackModuleIds=[], evidenceFactIds=['E0']) for i in range(500)]
        native = dict(anyOf=[branch(value, {'$ref': '#/$defs/shared'}) for value in choices],
            **{'$defs': dict(shared=dict(type='array', enum=[[['E0']]]))})
        text = bridge.factored_finite_decision_grammar(native)
        self.assertLess(len(text.encode()), len(json.dumps(native).encode()) // 3)
        rules = dict(line.split(' ::= ', 1) for line in text.splitlines())
        pattern = regex_for(rules, 'root')
        for choice in choices:
            self.assertTrue(pattern.fullmatch(json.dumps(dict(decision=choice, effectEvidenceFactIds=[['E0']]),
                                                        separators=(',', ':'))))
        unknown = deepcopy(choices[0]); unknown['positiveModuleIds'] = ['effect-not-offered']
        self.assertFalse(pattern.fullmatch(json.dumps(dict(decision=unknown, effectEvidenceFactIds=[['E0']]),
                                                     separators=(',', ':'))))

    def test_decoder_changes_only_the_finite_selection_grammar(self):
        native = dict(anyOf=[branch(dict(selectionId='S0'), dict(type='array', enum=[[['E0']]]))])
        original = deepcopy(native)
        public = dict(type='object', additionalProperties=False, properties=dict(
            selectionId=dict(type='string', enum=['S0'])), required=['selectionId'])
        actual, decoder, evidence = bridge.decoder_for(public, lambda value: deepcopy(native), 'selection')
        self.assertEqual(actual, original)
        self.assertEqual(native, original)
        self.assertIn('grammar', decoder)
        self.assertFalse(evidence['outputRepair'])
        self.assertFalse(evidence['semanticChoicesComputedByCode'])
        self.assertTrue(evidence['originalFiniteChoiceAndProofLanguagePreserved'])


if __name__ == '__main__':
    unittest.main()
