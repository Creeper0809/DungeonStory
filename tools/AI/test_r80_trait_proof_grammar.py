"""CPU-only checks for the exact single-effect trait evidence grammar.

The fixture enumerates supplied IDs and never invokes a model or repairs output.
"""
from copy import deepcopy
import itertools
import json
import unittest

import r80_game_bridge as bridge
from test_r80_name_grammar import regex_for


def make_native(values, gameplay, modules):
    values = sorted(values)
    gameplay = set(gameplay)
    others = [value for value in values if value not in gameplay]
    branches = []
    for length in range(1, len(values) + 1):
        for first in range(length):
            slots = [others] * first + [sorted(gameplay)] + [values] * (length - first - 1)
            branches.append(dict(type='array', minItems=length, maxItems=length,
                items=[dict(type='string', enum=slot) for slot in slots], additionalItems=False))
    evidence = dict(type='array', minItems=1, maxItems=len(values),
        uniqueItems=True, items=dict(type='string', enum=values), anyOf=branches)
    decision_properties = dict(
        requestRef=dict(type='string', enum=['request:exact']),
        selectionId=dict(type='string', enum=['selection:exact']),
        positiveModuleIds=dict(type='array', minItems=1, maxItems=1,
            items=dict(type='string', enum=list(modules))),
        drawbackModuleIds=dict(type='array', enum=[[]]),
        evidenceFactIds=deepcopy(evidence))
    decision = dict(type='object', additionalProperties=False,
        required=list(decision_properties), properties=decision_properties)
    proof = dict(type='array', minItems=1, maxItems=1, items=deepcopy(evidence))
    return dict(anyOf=[dict(type='object', additionalProperties=False,
        required=['decision', 'effectEvidenceFactIds'],
        properties=dict(decision=decision, effectEvidenceFactIds=proof))])


def encode(module, evidence, proof, request_ref='request:exact'):
    return json.dumps(dict(decision=dict(requestRef=request_ref,
        selectionId='selection:exact', positiveModuleIds=[module], drawbackModuleIds=[],
        evidenceFactIds=list(evidence)), effectEvidenceFactIds=[list(proof)]),
        ensure_ascii=False, separators=(',', ':'))


def compile_small(native, info):
    text = bridge.single_effect_trait_grammar(native, info)
    rules = dict(line.split(' ::= ', 1) for line in text.splitlines() if ' ::= ' in line)
    return regex_for(rules, 'root')


class TraitProofGrammarTests(unittest.TestCase):
    def test_all_small_subsets_module_requirements_and_mirrored_proofs(self):
        values = ['E0', 'E1', 'E2', 'E3']
        gameplay = ['E0', 'E1']
        required = {'module:a': ['E2'], 'module:b': ['E1', 'E3']}
        info = dict(values=values, gameplay=gameplay, requiredByModule=required)
        native = make_native(values, gameplay, required)
        original_native, original_info = deepcopy(native), deepcopy(info)
        pattern = compile_small(native, info)
        self.assertEqual(native, original_native)
        self.assertEqual(info, original_info)
        all_proofs = [p for length in range(len(values) + 1)
                      for p in itertools.permutations(values, length)]
        all_proofs += [('E0', 'E0'), ('E1', 'E1')]
        for module, module_required in required.items():
            for length in range(len(values) + 1):
                for subset in itertools.combinations(values, length):
                    expected_valid = bool(set(subset) & set(gameplay)) and bool(set(subset) & set(module_required))
                    reverse = tuple(reversed(subset))
                    self.assertEqual(bool(pattern.fullmatch(encode(module, subset, reverse))), expected_valid,
                                     (module, subset, reverse))
                    if expected_valid:
                        for proof in all_proofs:
                            self.assertEqual(bool(pattern.fullmatch(encode(module, subset, proof))),
                                             proof == reverse, (module, subset, proof))
                        self.assertFalse(pattern.fullmatch(encode(module, subset, reverse, 'request:wrong')))
                    if length > 1:
                        for disordered in itertools.permutations(subset):
                            if disordered != subset:
                                self.assertFalse(pattern.fullmatch(encode(module, disordered,
                                                                          tuple(reversed(disordered)))))

    def test_quote_and_backslash_ids_are_escaped_exactly(self):
        values = ['E0', 'E1', 'E"2', 'E\\3']
        gameplay = ['E0', 'E1']
        module = 'module:"a\\b'
        required = {module: ['E"2']}
        info = dict(values=values, gameplay=gameplay, requiredByModule=required)
        native = make_native(values, gameplay, required)
        original_native, original_info = deepcopy(native), deepcopy(info)
        pattern = compile_small(native, info)
        evidence = sorted(['E0', 'E"2', 'E\\3'])
        self.assertTrue(pattern.fullmatch(encode(module, evidence, reversed(evidence))))
        self.assertFalse(pattern.fullmatch(encode(module, evidence, evidence)))
        self.assertFalse(pattern.fullmatch(encode('module:ab', evidence, reversed(evidence))))
        self.assertEqual(native, original_native)
        self.assertEqual(info, original_info)

    def test_twenty_four_facts_keep_a_bounded_declared_grammar(self):
        values = ['E%02d' % index for index in range(24)]
        gameplay = ['E00', 'E01']
        required = {'module:a': ['E02'], 'module:b': ['E01', 'E03']}
        info = dict(values=values, gameplay=gameplay, requiredByModule=required)
        native = make_native(values, gameplay, required)
        original_native, original_info = deepcopy(native), deepcopy(info)
        text = bridge.single_effect_trait_grammar(native, info)
        rules = [line for line in text.splitlines() if ' ::= ' in line]
        self.assertTrue(any(line.startswith('root ::= ') for line in rules))
        self.assertLess(len(rules), 250)
        self.assertLess(len(text.encode('utf8')), 200_000)
        self.assertEqual(native, original_native)
        self.assertEqual(info, original_info)

    def test_impossible_required_evidence_is_explicitly_rejected(self):
        values = ['E0']
        gameplay = ['E0']
        required = {'module:a': ['E1']}
        native = make_native(values, gameplay, required)
        info = dict(values=values, gameplay=gameplay, requiredByModule=required)
        original_native, original_info = deepcopy(native), deepcopy(info)
        with self.assertRaises(ValueError):
            bridge.single_effect_trait_grammar(native, info)
        self.assertEqual(native, original_native)
        self.assertEqual(info, original_info)


if __name__ == '__main__':
    unittest.main()
