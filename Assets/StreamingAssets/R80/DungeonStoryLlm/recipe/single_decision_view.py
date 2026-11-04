# Reachable R80 inference definitions. See recipe-provenance.json.
from followup_scope_view import reading_messages as scoped_reading_messages
from copy import deepcopy
from audit_sft_token_lengths import require
from effect_bound_view import BINDING_RULE
import json
from followup_scope_view import selection_messages as double_selection_messages
from outcome_staged_view import selection_native as original_flat_native
import itertools
from jsonschema import Draft7Validator
from followup_scope_view import prose_messages, prose_native
RULE = ' 이번 응답에서는 decision에 공개 선택을 한 번만 확정한다. decision.positiveModuleIds의 첫 효과가 주효과다. effectEvidenceFactIds의 i번째 배열은 decision.positiveModuleIds의 i번째 효과를 직접 뒷받침하는 근거다. 효과 개수와 같은 수의 배열을 같은 순서로 작성하고, 모듈 목록이나 선택을 다시 쓰지 않는다. 각 효과에는 그 효과에 직접 관련된 본인의 행동·실제 결과만 연결한다. 다른 효과의 근거·타인·배경을 대신 연결하지 않는다. decision.evidenceFactIds에는 그 개별 근거와 공개 요청이 명시적으로 요구한 후속 근거만 인용한다. mandatoryFollowupEvidenceAnyOf가 있으면 회복되어 단점이 없어도 그 공개 지정 근거 중 하나를 인용한다. 후속 근거는 긍정 효과의 근거로 억지 연결하지 않아도 된다. decision.drawbackModuleIds는 앞선 실제 모델 판단을 유지한다. 호출자는 decision을 그대로 사용하며 잘못된 선택이나 근거를 고치지 않는다.'
READING_RULE = ' 상태를 정하기 전에 당사자가 같은 활동·대상을 실제로 다시 수행했을 때의 마지막 관측을 찾는다. 옛 실패, 수행하려는 시도, 일정 변경, 준비 조치와 실제 수행 결과를 구분한다. 일반적인 주의나 회복을 단정할 수 없다는 문장은 그 자체로 회복·지속 어느 쪽의 관측도 아니다. 최종 실제 결과가 있다면 그것을 짧게 인용하고 잔여 방해의 유무를 판단한다. 본문에 없는 완치·감염·성과를 덧붙이지 않으며 타인의 관측을 당사자의 결과로 바꾸지 않는다.'

def reading_messages(row, index, previous):
    result, schema = scoped_reading_messages(row, index, previous)
    result[0]['content'] += READING_RULE
    return (result, schema)

def required_followup(doc):
    """Recognize a literal public directive, never infer an outcome or ID."""
    headers = doc.get('remainingPlainHeaders', [])
    require(isinstance(headers, list) and all((isinstance(x, str) for x in headers)), 'original public plain headers required')
    ids = doc['currentOffer'].get('qualifiedNegativeEvidence', [])
    require(isinstance(ids, list) and len(ids) == len(set(ids)), 'public follow-up IDs must be unique')
    require(all((isinstance(k, str) and k in doc['facts'] for k in ids)), 'unknown public follow-up ID')
    if not any(('양쪽 근거를 인용' in h for h in headers)):
        return []
    return deepcopy(ids)

def project_selection(messages, double_schema):
    result = deepcopy(messages)
    require(result[0]['content'].count(BINDING_RULE) == 1, 'one frozen double-choice rule required')
    result[0]['content'] = result[0]['content'].replace(BINDING_RULE, RULE, 1)
    doc = json.loads(result[1]['content'])
    require('mandatoryFollowupEvidenceAnyOf' not in doc, 'already projected single-choice input')
    required = required_followup(doc)
    if required:
        doc['mandatoryFollowupEvidenceAnyOf'] = required
    doc['responseBindingFormat'] = 'ONE_PUBLIC_DECISION_WITH_POSITIONAL_EFFECT_PROOFS_V1'
    result[1]['content'] = json.dumps(doc, ensure_ascii=False, separators=(',', ':'))
    flat = deepcopy(double_schema['properties']['decision'])
    positive = flat['properties']['positiveModuleIds']
    evidence = deepcopy(flat['properties']['evidenceFactIds'])
    evidence['minItems'] = 1
    schema = dict(type='object', properties=dict(decision=flat, effectEvidenceFactIds=dict(type='array', items=evidence, minItems=positive.get('minItems', 1), maxItems=positive['maxItems'])), required=['decision', 'effectEvidenceFactIds'], additionalProperties=False)
    return (result, schema)

def selection_messages(row, index, previous, reading):
    return project_selection(*double_selection_messages(row, index, previous, reading))

def _proof_options(evidence, requirements, followup):
    """All ordered nonempty sublists satisfying declared ID constraints only."""
    options = [list(p) for n in range(1, len(evidence) + 1) for p in itertools.permutations(evidence, n)]
    slots = [[p for p in options if not req or set(p).intersection(req)] for req in requirements]
    require(all(slots), 'no proof assignment for original declared module requirements')
    count = 1
    for slot in slots:
        count *= len(slot)
    require(count <= 65536, 'proof grammar exceeds explicit bound; do not shrink public choices')
    expected = set(evidence)
    values = [list(p) for p in itertools.product(*slots) if set().union(set(followup), *(set(ids) for ids in p)) == expected]
    require(values, 'original flat choice has no representable proof; no fallback')
    return values

def constrain_native(native, flat_choices, doc):
    """Reuse ALL original legal choices except the explicit missing-cite gap.

    Root anyOf ties positional proof count/membership to the model's one flat
    decision. Shared local refs avoid copying the proof Cartesian product for
    every effect combination. The pinned official compiler resolves local
    JSON pointers, anyOf and JSON-valued enum constants.
    """
    require(set(flat_choices) == {'type', 'enum'} and flat_choices['type'] == 'object', 'complete original public flat choice enum required')
    followup = required_followup(doc)
    choices = [deepcopy(c) for c in flat_choices['enum'] if not followup or set(c['evidenceFactIds']).intersection(followup)]
    require(choices and len(choices) <= 32768, 'empty/oversize public legal choice space; no default')
    groups = {}
    for choice in choices:
        key = (choice['selectionId'], tuple(choice['positiveModuleIds']), tuple(sorted(choice['evidenceFactIds'])), tuple(choice['drawbackModuleIds']))
        groups.setdefault(key, []).append(choice)
    defs, templates, branches = ({}, {}, [])
    for (_, positive, evidence, _), group in groups.items():
        required = tuple((tuple(sorted(set(doc['modules'][k]['requiredEvidence']).intersection(evidence))) for k in positive))
        require(all((not doc['modules'][k]['requiredEvidence'] or req for k, req in zip(positive, required))), 'original flat module requirement drift')
        outcome = tuple(sorted(set(doc['currentOffer'].get('qualifiedNegativeEvidence', [])).intersection(evidence)))
        key = (evidence, required, outcome)
        if key not in templates:
            name = 'proof' + str(len(templates))
            templates[key] = name
            defs[name] = dict(type='array', enum=_proof_options(evidence, required, outcome))
        name = templates[key]
        branches.append(dict(type='object', properties=dict(decision=dict(type='object', enum=group), effectEvidenceFactIds={'$ref': '#/$defs/' + name}), required=['decision', 'effectEvidenceFactIds'], additionalProperties=False))
    require(sum((len(b['properties']['decision']['enum']) for b in branches)) == len(choices), 'original choice lost during grammar grouping')
    return {'$defs': defs, 'anyOf': branches}

def selection_native(row, index, previous, reading, native):
    messages, _ = selection_messages(row, index, previous, reading)
    doc = json.loads(messages[1]['content'])
    flat = original_flat_native(row, index, previous, reading, native['properties']['decision'])
    return constrain_native(native, flat, doc)

def binding_errors(value, schema, doc=None):
    """Report invalid responses; never fill, deduplicate, reorder, or repair."""
    errors = ['single-choice schema ' + str(list(e.absolute_path)) + ': ' + e.message for e in Draft7Validator(schema).iter_errors(value)]
    if errors:
        return errors
    decision, proofs = (value['decision'], value['effectEvidenceFactIds'])
    if len(proofs) != len(decision['positiveModuleIds']):
        errors.append('one positional proof array per selected effect required')
    evidence = set(decision['evidenceFactIds'])
    if any((not set(ids) <= evidence for ids in proofs)):
        errors.append('effect proof outside the exact model public decision')
    if doc is not None:
        qualified = set(doc['currentOffer'].get('qualifiedNegativeEvidence', []))
        required = required_followup(doc)
        if required and (not evidence.intersection(required)):
            errors.append('explicit public follow-up citation missing')
        used = set().union(*(set(ids) for ids in proofs))
        if evidence - qualified - used:
            errors.append('selected non-follow-up fact unused by every effect proof')
        for module, ids in zip(decision['positiveModuleIds'], proofs):
            req = doc['modules'][module]['requiredEvidence']
            if req and (not set(ids).intersection(req)):
                errors.append('effect proof lacks declared module evidence: ' + module)
    return errors
