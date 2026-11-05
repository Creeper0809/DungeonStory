"""Offline authenticated game transport for the already evaluated R80 recipe.

The existing C# host owns this process and its llama.cpp child in one Windows
kill-on-close job. No model, module choice, evidence or prose is selected or
repaired here. Errors return to the normal game queue explicitly.
"""
import argparse
from contextlib import contextmanager
import queue as slot_queue
from copy import deepcopy
from functools import lru_cache
import hashlib
import itertools
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from http.client import HTTPConnection
import json
from pathlib import Path
import re
import secrets
import select
import socket
import subprocess
import threading
import time
from types import SimpleNamespace
from urllib.request import build_opener, ProxyHandler, Request
from urllib.error import HTTPError
from urllib.parse import urlsplit

from jsonschema import Draft7Validator
from audit_sft_token_lengths import canonical
import candidate_task_view
import single_decision_view
import followup_scope_view
import scoped_auxiliary_view
from student_llama_inference import llama_schema, validated_response
import r80_request


def write(path, value):
    with path.open('x', encoding='utf8') as stream:
        json.dump(value, stream, ensure_ascii=False, indent=2)


@lru_cache(maxsize=32)
def finite_array_intersection(serialized):
    schema = json.loads(serialized)
    item, lower, upper = schema.get('items', {}), schema.get('minItems', 0), schema.get('maxItems')
    values = item.get('enum')
    if item.get('type') != 'string' or not values or type(upper) is not int or not 0 <= lower <= upper <= 6:
        raise ValueError('Unsupported finite public array intersection')
    validator = Draft7Validator(schema)
    # Closed tuple branches already specify every position. Enumerate their
    # complete languages rather than test unrelated arrays against every branch.
    branches = schema['anyOf']
    if all(isinstance(b.get('items'), list) and b.get('additionalItems') is False
           and b.get('minItems') == b.get('maxItems') == len(b['items']) for b in branches):
        pools = []
        for branch in branches:
            slots = [[v for v in values if Draft7Validator(slot).is_valid(v)] for slot in branch['items']]
            pools.append(slots)
        count = sum(__import__('math').prod(map(len, slots)) for slots in pools)
        if count > 100000:
            raise ValueError('Public tuple intersection exceeds explicit bound; no shrinking')
        candidates = dict.fromkeys(parts for slots in pools for parts in itertools.product(*slots))
        if all(set(branch) <= {'type', 'items', 'minItems', 'maxItems', 'additionalItems'}
               and branch.get('type', 'array') == 'array' for branch in branches):
            # Each candidate was constructed from a complete valid tuple branch.
            # Rechecking that same union for every tuple is redundant. Retain
            # all root constraints, including uniqueItems, in the intersection.
            validator = Draft7Validator({key: value for key, value in schema.items() if key != 'anyOf'})
    else:
        if sum(len(values) ** size for size in range(lower, upper + 1)) > 100000:
            raise ValueError('Public array intersection exceeds explicit bound; no shrinking')
        candidates = (parts for size in range(lower, upper + 1) for parts in itertools.product(values, repeat=size))
    arrays = tuple(parts for parts in candidates if validator.is_valid(list(parts)))
    if not arrays:
        raise ValueError('Public array intersection has no legal values')
    return arrays


def finite_schema(schema):
    """Drop only constraints already proven redundant over an exact finite enum."""
    if not isinstance(schema, dict):
        return deepcopy(schema)
    if schema.get('type') == 'array' and 'anyOf' in schema:
        arrays = finite_array_intersection(json.dumps(schema, ensure_ascii=False, sort_keys=True))
        return dict(type='array', enum=[list(parts) for parts in arrays])
    if 'enum' in schema or 'const' in schema:
        values = schema.get('enum', [schema.get('const')])
        if not values:
            raise ValueError('Empty finite schema has no legal output')
        for value in values:
            Draft7Validator(schema).validate(value)
        return {key: deepcopy(schema[key]) for key in ('type', 'enum', 'const') if key in schema}
    result = deepcopy(schema)
    if 'properties' in result:
        result['properties'] = {key: finite_schema(value) for key, value in result['properties'].items()}
    if isinstance(result.get('items'), list):
        result['items'] = [finite_schema(value) for value in result['items']]
    elif isinstance(result.get('items'), dict):
        result['items'] = finite_schema(result['items'])
    return result


def inline_local_refs(schema):
    """Expand closed local JSON pointers before a stage nests its schema."""
    root = schema
    def visit(value, ancestors=()):
        if isinstance(value, list):
            return [visit(child, ancestors) for child in value]
        if not isinstance(value, dict):
            return deepcopy(value)
        if '$ref' in value:
            ref = value['$ref']
            if set(value) != {'$ref'} or not ref.startswith('#/') or ref in ancestors:
                raise ValueError('Only acyclic standalone local schema references are supported')
            target = root
            for part in ref[2:].split('/'):
                target = target[part.replace('~1', '/').replace('~0', '~')]
            return visit(target, ancestors + (ref,))
        return {key: visit(child, ancestors) for key, child in value.items() if key not in ('$defs', 'definitions')}
    return visit(schema)


def bind_public_subject(profile, wire):
    """Copy the exact subject entity's public name, never infer one from prose."""
    if profile != 'AcquiredTraitModuleSelection':
        return wire
    result = deepcopy(wire)
    users = [m for m in result['messages'] if m.get('role') == 'user']
    if len(users) != 1:
        raise ValueError('One original trait user message required')
    text = users[0]['content']
    contexts = []
    for line in text.splitlines():
        try:
            value = json.loads(line)
        except ValueError:
            continue
        if isinstance(value, dict) and 'publicNarrativeContext' in value:
            contexts.append(value['publicNarrativeContext'])
    if len(contexts) != 1:
        raise ValueError('One authoritative public trait subject context required')
    context = contexts[0]
    entities = [e for e in context['entities'] if e['entityId'] == context['subjectId']
                and e['kind'] == context['subjectKind'] == 'character']
    if len(entities) != 1 or not entities[0].get('displayName') or any(c in entities[0]['displayName'] for c in '\r\n'):
        raise ValueError('Exact public trait subject identity is missing or ambiguous')
    name = entities[0]['displayName']
    existing = re.findall(r'(?m)^character=([^\r\n]+)', text)
    if existing and existing != [name]:
        raise ValueError('Trait header differs from its authoritative public subject')
    if not existing:
        users[0]['content'] += '\ncharacter=' + name + '\n'
    return result


def bind_caller_choices(profile, wire):
    """Intersect old C# static schemas with literal caller-supplied identifiers.

    This transports existing legal candidates. It never ranks them or reads an
    event's meaning. An absent concrete facility leaves the documented -1 target.
    """
    if profile in r80_request.MODULES:
        if 'response_format' not in wire:
            return wire
        result = deepcopy(wire)
        document = result['response_format']['json_schema']
        if profile in ('EquipmentEvolutionModuleSelection', 'FacilityEvolutionModuleSelection'):
            document['schema'] = inline_local_refs(document['schema'])
            contract = candidate_task_view.contract(dict(profile=profile, wire=result,
                sourceWireSha256='sha256:' + hashlib.sha256(canonical(result)).hexdigest()))
            if len(contract['offers']) != 1:
                raise ValueError('One public equipment/facility offer required')
            offer = contract['offers'][0]
            props = document['schema']['properties']
            Draft7Validator(props['selectionId']).validate(offer['selectionId'])
            props['selectionId']['enum'] = [offer['selectionId']]
            for field, key, maximum in [('positiveModuleIds', 'positive', 'maximumPositive'),
                                        ('drawbackModuleIds', 'drawback', 'maximumDrawback'),
                                        ('evidenceFactIds', 'evidence', 'maximumEvidence')]:
                node = props[field]
                values = offer[key]
                bound = min(node.get('maxItems', offer[maximum]), offer[maximum])
                if bound < node.get('minItems', 0):
                    raise ValueError('Public offer/cardinality conflict')
                node['maxItems'] = bound
                if values:
                    for value in values:
                        Draft7Validator(node['items']).validate(value)
                    node['items']['enum'] = deepcopy(values)
                else:
                    if node.get('minItems', 0) != 0 or bound != 0:
                        raise ValueError('Empty public domain cannot satisfy caller')
                    node['enum'] = [[]]
        return result
    result = deepcopy(wire)
    schema = result['response_format']['json_schema']['schema']
    text = '\n'.join(m.get('content', '') for m in result['messages'] if m.get('role') == 'user')
    for path in r80_request.FINITE_CHOICES.get(profile, []):
        node = schema
        for key in path:
            if node.get('type') == 'array':
                node = node['items']
            node = node['properties'][key]
        if 'const' in node or node.get('enum'):
            continue
        if node.get('type') == 'array':
            node = node['items']
        key = path[-1]
        # Exact named public bindings, including semicolon-separated offer rows.
        values = re.findall(r'(?m)(?:^|[;\s])' + re.escape(key) + r'=([^\s;\r\n]+)', text)
        if key == 'targetFacilityId':
            values = [-1] + [int(v) for v in values if re.fullmatch(r'-?\d+', v)]
            # Context rows can contain unassigned runtime IDs. They are not legal
            # targets under the caller schema or its consumer. Intersect only
            # that identifier domain; do not pick a facility or change an event.
            explicit = re.findall(r'(?m)^targetFacilityId=(-?\d+)\r?$', text)
            for value in explicit:
                Draft7Validator(node).validate(int(value))
            values = [value for value in values if Draft7Validator(node).is_valid(value)]
        elif key == 'selectedIndex':
            values = [int(v) for v in re.findall(r'(?m)^(\d+) = effectId=', text)]
        if not values:
            raise ValueError('No finite caller binding for ' + profile + '.' + '.'.join(path))
        values = list(dict.fromkeys(values))
        for value in values:
            Draft7Validator(node).validate(value)
        node['enum'] = values
        if key == 'targetFacilityId' and values == [-1]:
            schema['properties']['targetFacilityTag']['const'] = ''
            field = 'macroGoal' if profile == 'MacroGoal' else 'moodImpulse' if profile == 'MoodImpulse' else None
            if field:
                schema['properties'][field]['enum'] = [v for v in schema['properties'][field]['enum']
                    if v not in ('AvoidFacility', 'Vandalize')]
    if profile == 'SocialRumor':
        # The English example is not an offered facility tag. Bind only the
        # actual roster's tags, including its explicitly empty tags.
        tags = re.findall(r'(?m)^- id=-?\d+; targetFacilityId=-?\d+; name=[^\r\n]*; tag=([^\r\n]*)', text)
        if tags:
            values = list(dict.fromkeys([''] + [tag.strip() for tag in tags]))
            for value in values:
                Draft7Validator(schema['properties']['targetFacilityTag']).validate(value)
            schema['properties']['targetFacilityTag']['enum'] = values
        ids = re.findall(r'(?m)^speakerCharacterId=([^\r\n]+)', text)
        names = re.findall(r'(?m)^speakerCharacterName=([^\r\n]+)', text)
        if ids or names:
            if len(ids) != 1 or len(names) != 1:
                raise ValueError('Exact public rumor speaker identity required')
            for key, value in [('targetCharacterId', ids[0]), ('targetCharacterName', names[0])]:
                Draft7Validator(schema['properties'][key]).validate(value)
                schema['properties'][key]['enum'] = ['', value]
    return result


def social_target_schema(schema):
    """Enforce target fields together using only the caller's literal domains."""
    props = schema['properties']
    if not props.get('targetCharacterId', {}).get('enum'):
        return schema
    branches = []
    for kind in props['targetType']['enum']:
        branch = deepcopy(schema)
        p = branch['properties']
        p['targetType'] = dict(type='string', const=kind)
        if kind == 'None':
            p['rumorType'] = dict(type='string', const='None')
        else:
            p['rumorType']['enum'] = [v for v in p['rumorType']['enum'] if v != 'None']
        if kind != 'Facility':
            p['targetFacilityId'] = dict(type='integer', const=-1)
            p['targetFacilityTag'] = dict(type='string', const='')
        else:
            values = [v for v in p['targetFacilityId']['enum'] if v >= 0]
            if not values:
                continue
            p['targetFacilityId']['enum'] = values
        for key in ('targetCharacterId', 'targetCharacterName'):
            if kind == 'Character':
                p[key]['enum'] = [v for v in p[key]['enum'] if v]
            else:
                p[key] = dict(type='string', const='')
        branches.append(branch)
    return dict(anyOf=branches)


def subject_particles(name):
    """Mechanical Korean subject agreement before generation, not prose repair."""
    last = name.rstrip()[-1:]
    if last and '\uac00' <= last <= '\ud7a3':
        return ('은',) if (ord(last) - 0xac00) % 28 else ('는',)
    if last in '0123456789' and last:
        return ('은',) if last in '013678' else ('는',)
    if last.lower() in 'aeiou' and last:
        return ('는',)
    if last.lower() in 'nlm' and last:
        return ('은',)
    return ('은', '는')


def authored_skill_table(profile, wire):
    if profile != 'CharacterSkillModuleSelection':
        return None
    tables = []
    for message in wire['messages']:
        if message.get('role') != 'user':
            continue
        for line in message.get('content', '').splitlines():
            try:
                value = json.loads(line)
            except ValueError:
                continue
            if isinstance(value, dict) and value.get('transport') in ('skill-request-refs-v1', 'skill-request-refs-v2'):
                tables.append(value)
    if len(tables) != 1:
        return None
    return tables[0] if tables[0].get('noOutcomes') is True else None


def authored_skill_schema(wire, index, previous):
    """All supplied choices, with only declared uniqueness and prose bounds."""
    original = wire['response_format']['json_schema']['schema']
    schema = deepcopy(original['properties']['candidates']['items'][index])
    props = schema['properties']
    if props['drawbackModuleIds'].get('maxItems') != 0:
        raise ValueError('Authored initial skills require the caller no-drawback contract')
    used = {frozenset(value['positiveModuleIds']) for value in previous}
    used_primary = {value['positiveModuleIds'][0] for value in previous}
    for field in ('positiveModuleIds', 'evidenceFactIds'):
        node = props[field]
        options = node['items']['enum']
        arrays = [list(p) for n in range(node.get('minItems', 0), node['maxItems'] + 1)
                  for p in itertools.permutations(options, n)
                  if field != 'positiveModuleIds' or (frozenset(p) not in used and p[0] not in used_primary)]
        if not arrays or len(arrays) > 32768:
            raise ValueError('No bounded public initial skill choices; no fallback')
        node['enum'] = arrays
    # C# supplies a JSON-safe character pattern with its original 48-char cap.
    # Intersect that pattern with the already required exact subject, before
    # generation. Nothing in an emitted answer is trimmed or rewritten.
    text = '\n'.join(m.get('content', '') for m in wire['messages'] if m.get('role') == 'user')
    names = re.findall(r'(?m)^character=([^\r\n]+)$', text)
    if len(names) != 1:
        raise ValueError('One caller-supplied character subject is required')
    narrative = props['narrativeFlavor']
    old_bound = '{0,' + str(narrative['maxLength'] - 1) + '}'
    body_limit = narrative['maxLength'] - len(names[0]) - 3
    if body_limit < 1 or narrative['pattern'].count(old_bound) != 1:
        raise ValueError('Unsupported bounded initial skill prose pattern')
    # Regex literals need escapes for metacharacters, not ordinary spaces.
    # Python re.escape's backslash-space is not accepted by the official
    # regex-to-GBNF compiler, although it denotes the same literal space.
    subject = ''.join('\\' + char if char in r'\.^$|?*+()[]{}' else char for char in names[0])
    narrative['pattern'] = '^' + subject + '(' + '|'.join(subject_particles(names[0])) + ') ' + narrative['pattern'][1:].replace(
        old_bound, '{0,' + str(body_limit) + '}', 1)
    return schema


def generate_history_narrative(engine, row, folder):
    if row['profile'] != 'EvolutionHistoryLegacyV2':
        raise ValueError('Only locked legacy history uses the owner envelope')
    r80_request.validate_request(SimpleNamespace(canonical=canonical), row)
    wire = row['wire']
    schema = wire['response_format']['json_schema']['schema']
    Draft7Validator.check_schema(schema)
    fixed_fields = ('requestKey', 'targetPersistentId', 'nodeId', 'parentNodeId',
                    'effectId', 'effectBudget', 'evidenceIds')
    prose_fields = ('displayName', 'description', 'historyReason')
    if set(schema['properties']) != set(fixed_fields + prose_fields):
        raise ValueError('Exact original history fields required')
    fixed = {}
    for key in fixed_fields:
        field = schema['properties'][key]
        if 'const' not in field:
            raise ValueError('History owner must lock ' + key + ' before inference')
        Draft7Validator(field).validate(field['const'])
        fixed[key] = deepcopy(field['const'])
    prose_schema = dict(type='object', additionalProperties=False,
        required=list(prose_fields), properties={key: deepcopy(schema['properties'][key]) for key in prose_fields})
    limit = wire.get('max_tokens')
    if type(limit) is not int or not 64 <= limit <= 1024:
        raise ValueError('Explicit bounded history output budget required')
    messages = deepcopy(wire['messages'])
    messages.append(dict(role='system', content=(
        '게임이 이미 확정한 식별자, 효과, 근거 목록은 원값 그대로 별도 전달된다. '
        '이번 응답은 displayName, description, historyReason 세 필드만 작성한다. '
        '식별정보를 복사하거나 효과를 다시 선택하지 않는다. 원래 공개 사실과 효과를 지킨다. '
        '플레이어에게 보여 줄 짧은 이름, 한 문장 설명, 변화의 이유를 자연스러운 한국어로 쓴다. '
        '분류됩니다 같은 행정적인 판정 문구나 내부 시스템 용어는 쓰지 않는다.')))
    messages.append(dict(role='user', content=json.dumps(dict(mode='LOCKED_HISTORY_PROSE',
        responseFields=list(prose_fields)), ensure_ascii=False, separators=(',', ':'))))
    write(folder / 'history-envelope-input.json', dict(ownerMetadata=fixed,
        sourceWireSha256=row['sourceWireSha256'], modelFields=list(prose_fields),
        modelSelectsMetadata=False, outputRepair=False))
    prose = engine.call(messages, prose_schema, lambda native: native, folder, 'auxiliary', limit)
    Draft7Validator(prose_schema).validate(prose)
    # Protocol assembly uses the owner's already locked fields. Generated prose
    # values are transferred unchanged, never shortened, repaired or replaced.
    value = dict(fixed, **prose)
    Draft7Validator(schema).validate(value)
    write(folder / 'history-envelope-assembly.json', dict(ownerMetadataAssembled=True,
        modelFields=list(prose_fields), modelProseUnchanged=True,
        modelSelectsMetadata=False, outputRepair=False))
    return value


def authored_stage_messages(wire, instruction, stage_input):
    # Keep the unchanged caller messages as the shared KV prefix. Stage rules
    # retain the system role; every actual choice and earlier name stays in the
    # following request-local user message. No output or evidence is selected here.
    messages = deepcopy(wire['messages'])
    messages.append(dict(role='system', content=instruction))
    messages.append(dict(role='user', content=json.dumps(stage_input, ensure_ascii=False, separators=(',', ':'))))
    return messages


def generate_authored_skills(engine, wire, folder, table):
    """Starting-party authoring has no observed outcome to classify.

    Route by the caller's explicit noOutcomes flag before inference, using the
    same R80 checkpoint. This path does not claim the event-evaluation score.
    """
    schema = wire['response_format']['json_schema']['schema']
    items = schema['properties']['candidates']['items']
    if not isinstance(items, list) or len(items) != len(table['C']):
        raise ValueError('Initial authored skill candidate tuple mismatch')
    decisions, schemas = [], []
    for index in range(len(items)):
        candidate_schema = authored_skill_schema(wire, index, decisions)
        instruction = (' 이번 호출은 사건 결과 판정이 아닌 처음 합류한 인물의 기술 창작이다. '
            '원래 요청의 현재 후보 객체 하나만 응답한다. 후보 목록이나 requestRef를 응답하지 않는다. '
            '원래 공개 경험과 허용 효과만 사용하고 실제로 하지 않은 활동을 과거 사건으로 만들지 않는다. '
            '현재 후보의 대상과 조건을 지키며 허용된 효과와 공개 근거를 선택한다. '
            '이번 단계는 selectionId, positiveModuleIds, drawbackModuleIds, evidenceFactIds만 확정한다. '
            '이름과 설명은 이 결정을 바꾸지 않는 다음 단계에서 쓴다.')
        module_map = {table['M'][slot][0]: table['M'][slot][1]
                      for slot in table['C'][index][3] + table['C'][index][4]}
        messages = authored_stage_messages(wire, instruction, dict(
            mode='AUTHORED_START_PARTY_WITHOUT_RUNTIME_OUTCOMES', currentCandidateIndex=index,
            currentOffer=table['C'][index], previousCandidateChoices=decisions,
            currentMechanics=table['X'][table['C'][index][1]],
            offeredEffects=[dict(id=key, meaning=value) for key, value in module_map.items()],
            previousCandidateNames=[],
            responseFields=list(candidate_schema['properties'])))
        phase = folder / ('authored-candidate-' + str(index))
        phase.mkdir()
        selection_schema = deepcopy(candidate_schema)
        for field in ('displayName', 'narrativeFlavor'):
            del selection_schema['properties'][field]
            selection_schema['required'].remove(field)
        decision = engine.call(messages, selection_schema, lambda native: native, phase, 'authored-selection', 512)
        Draft7Validator(selection_schema).validate(decision)
        decisions.append(decision)
        schemas.append(candidate_schema)
    # 같은 단계의 원문 접두를 연속 사용해 KV 캐시를 재사용. 효과는 여전히 모델이 후보별 한 번 결정.
    previous = []
    for index, (decision, candidate_schema) in enumerate(zip(decisions, schemas)):
        phase = folder / ('authored-candidate-' + str(index))
        prose_schema = dict(type='object', additionalProperties=False,
            required=['displayName', 'narrativeFlavor'], properties={
                field: deepcopy(candidate_schema['properties'][field])
                for field in ('displayName', 'narrativeFlavor')})
        prose_messages = authored_selected_prose_messages(wire, table, index, decision, previous)
        prose = engine.call(prose_messages, prose_schema, lambda native: native, phase, 'authored-prose', 512,
                            excluded_names=[value['displayName'] for value in previous])
        Draft7Validator(prose_schema).validate(prose)
        value = dict(decision, **prose)
        Draft7Validator(candidate_schema).validate(value)
        previous.append(value)
    refs = schema['properties']['requestRef']['enum']
    if len(refs) != 1:
        raise ValueError('Exact initial skill request reference required')
    result = dict(requestRef=refs[0], candidates=previous)
    Draft7Validator(schema).validate(result)
    write(folder / 'initial-authoring-route.json', dict(
        typedNoOutcomes=True, eventJudgmentPerformed=False, oneCheckpoint=True,
        inheritsDevelopment288Score=False, outputRepair=False,
        twoNativePhasesPerCandidate=True, modelDecisionBeforeProse=True,
        phaseGrouping='all-selections-before-all-prose',
        generatedProseUsedToSelectLaterEffects=False,
        allOriginalFactsInSelectionPrompt=True, allOriginalFactsInProsePrompt=False,
        selectedEffectProjectionOnly=True))
    return result


PROSE_NAME_GUIDANCE = (
    ' 이름은 뜻이 완결된 자연스러운 한국어 명사구로 짓는다.'
    ' previousCandidateNames에 있는 이름과 다르게 지을 때는 처음부터 다른 단어를 고른다.'
    ' 이름에 문장 부호, 필드명, 임시 표기나 여러 이름을 연결한 조각을 붙이지 않는다.'
    ' 설명은 효과를 받는 대상과 바뀌는 상태를 구분하고, 주어와 목적어의 조사를 맞춘다.')


SKILL_PROSE_GUIDANCE = (
    '이미 확정된 selection의 기술 이름과 미래 효과만 쉬운 한국어로 설명한다. 선택은 바꾸지 않는다.'
    ' displayName과 narrativeFlavor의 JSON 객체만 출력한다.'
    ' selectedModules는 실제 선택된 모든 효과의 원문이다. selection.positiveModuleIds는 이점,'
    ' selection.drawbackModuleIds는 별도 부담이다. 이점 자체에 포함된 부담과 부효과도 빠뜨리지 않는다.'
    ' mechanics의 수신 대상, 방향, 발동 조건과 범위를 지킨다. Self는 자신, Ally는 아군, Enemy는 적이다.'
    ' 인물이 주어여도 효과 수신자는 mechanics의 대상이다. 능력치 용어는 statTermGlossary를 따른다.'
    ' narrativeFlavor는 정확한 인물 이름을 주어로 하는 48자 이내의 완결된 한 문장으로 쓴다.'
    ' 과거 사건 요약, 서론, 단점이 없다는 안내는 생략한다. 허용하지 않은 효과나 조건은 추가하지 않는다.'
    ' 회복, 해로운 상태 제거, 방어, 보호, 회피는 서로 다른 효과이므로 바꿔 쓰지 않는다.'
    ' 조건부 이점을 반대 조건의 불이익으로 바꾸지 않는다. 한자나 외국어 표현은 섞지 않는다.'
    + PROSE_NAME_GUIDANCE)


def authored_prose_context(table, context_index):
    """원래 C# 압축 표의 열 이름과 P 참조만 해제. 효과를 추론하거나 고르지 않음."""
    if table.get('X_columns') != ['mechanics', 'context', 'primary_P']:
        raise ValueError('Exact authored mechanics columns required')
    if (not isinstance(table.get('X'), list) or not isinstance(table.get('P'), list)
            or type(context_index) is not int or not 0 <= context_index < len(table['X'])):
        raise ValueError('Invalid authored context reference')
    row = table['X'][context_index]
    if (not isinstance(row, list) or len(row) != 3
            or not isinstance(row[0], str) or not isinstance(row[1], str)
            or type(row[2]) is not int or not 0 <= row[2] < len(table.get('P', []))
            or not isinstance(table['P'][row[2]], str)):
        raise ValueError('Invalid authored mechanics or primary-rule reference')
    return dict(mechanics=row[0], context=row[1], primaryContract=table['P'][row[2]])


def authored_selected_prose_messages(wire, table, index, decision, previous):
    # 모델이 고른 효과의 원 설명과 현재 대상만 전달. 선택과 원 응답은 수정하지 않음.
    headers = '\n'.join(m['content'] for m in wire['messages'] if m.get('role') == 'user')
    names = re.findall(r'(?m)^character=([^\r\n]+)$', headers)
    if len(names) != 1:
        raise ValueError('One caller-supplied character subject is required')
    offer = table['C'][index]
    modules = {table['M'][slot][0]: table['M'][slot][1] for slot in offer[3] + offer[4]}
    selected = decision['positiveModuleIds'] + decision['drawbackModuleIds']
    if not set(selected) <= set(modules):
        raise ValueError('Model selected an effect outside this caller offer')
    document = dict(character=names[0], currentMechanics=authored_prose_context(table, offer[1]),
        selectedEffects=[dict(id=key, meaning=modules[key]) for key in selected],
        selection=deepcopy(decision), previousCandidateNames=[value['displayName'] for value in previous])
    return [dict(role='system', content=(
        '선택된 기술의 이름과 설명을 쉬운 한국어로 쓴다. displayName과 narrativeFlavor만 응답한다. '
        'currentMechanics의 공식 대상, 발동 조건, 범위를 지키고 selectedEffects의 효과만 빠짐없이 설명한다. '
        '다른 효과나 조건을 추가하지 않는다. 본명을 주어로 써도 효과 수신자는 현재 기술의 대상이다. '
        '회복, 해로운 상태 제거, 피해 감소는 서로 다른 효과다. 한 문장으로 완결한다. '
        'currentMechanics.context의 실제 수신자와 작동 방식을 구분하고 primaryContract를 따른다. '
        '주효과 중심이라는 규칙은 선택된 부효과를 생략해도 된다는 뜻이 아니다. '
        '설명은 선택된 효과가 일으키는 변화를 직접 서술하고 다른 행동이나 전달 상황으로 바꾸지 않는다. '
        '이름은 행동을 말하는 문장이 아니라 첫 이로운 효과를 중심으로 한 짧은 명사구로 쓴다. '
        'previousCandidateNames와 정확히 같은 기술 이름을 반복하지 않는다.' + PROSE_NAME_GUIDANCE)),
        dict(role='user', content=json.dumps(document, ensure_ascii=False, separators=(',', ':')))]


def game_selection_messages(row, index, previous, reading):
    # Preserve the original one-decision prompt. Retain only the actual preceding
    # model names for the next prose call's existing game uniqueness contract.
    row['_gamePreviousCandidateNames'] = [value['displayName'] for value in previous]
    return single_decision_view.selection_messages(row, index, previous, reading)


def game_assemble(row, candidates):
    if row['profile'] != 'AcquiredTraitModuleSelection':
        return candidate_task_view.assemble(row, candidates)
    # The reference contract's default numeric guard remains strict. The game
    # additionally supplies the same exact subject authority used by C# and
    # the native prose pattern. Generated fields remain byte-for-byte values.
    bound = bind_public_subject(row['profile'], row['wire'])
    names = re.findall(r'(?m)^character=([^\r\n]+)', '\n'.join(
        message['content'] for message in bound['messages'] if message.get('role') == 'user'))
    if len(names) != 1 or not isinstance(candidates, list) or len(candidates) != 1:
        raise ValueError('One exact trait candidate and subject required')
    Draft7Validator(game_candidate_schema(row, 0)).validate(candidates[0])
    from live_module_selection_contract import validate_response
    result = deepcopy(candidates[0])
    validate_response(canonical(row['wire']), row['sourceWireSha256'], row['profile'], result,
                      authoritative_name=names[0])
    return result


def game_reading_messages(row, index, previous):
    messages, schema = followup_scope_view.original_reading(row, index, previous)
    doc = json.loads(messages[1]['content'])
    ids = doc['offer'].get('qualifiedNegativeEvidence')
    if ids:
        messages, schema = followup_scope_view.scope_reading(messages, schema)
    elif ids not in (None, []):
        raise ValueError('Invalid explicit public follow-up scope')
    elif any('양쪽 근거를 인용' in header for header in doc.get('remainingPlainHeaders', [])):
        raise ValueError('Explicit follow-up directive has no public fact scope')
    # New game observations need not be follow-up observations. Keep their full
    # original extractive domain; never manufacture a scoped/latest fact ID.
    messages[0]['content'] += single_decision_view.READING_RULE
    return messages, schema


def game_candidate_schema(row, index):
    """Resolve a nested stage against its own root, leaving wire custody compact."""
    contract = candidate_task_view.contract(row)
    schema = inline_local_refs(row['wire']['response_format']['json_schema']['schema'])
    if row['profile'] == candidate_task_view.SKILL:
        items = schema['properties']['candidates']['items']
        if not isinstance(items, list) or len(items) != len(contract['offers']):
            raise ValueError('Original positional candidate schema required')
        return deepcopy(items[index])
    if index != 0:
        raise ValueError('Single-output candidate index must be zero')
    return schema


def install_game_candidate_schema():
    import candidate_cost_view, outcome_native_trial, outcome_staged_view
    for module in (candidate_task_view, candidate_cost_view, outcome_native_trial, outcome_staged_view):
        module.candidate_schema = game_candidate_schema


def game_selection_native(row, index, previous, reading, native):
    offer = (candidate_task_view.contract(row)['offers'][index]
             if row.get('profile') in r80_request.MODULES else None)
    if offer is not None and offer['maximumEvidence'] > 6:
        # Real game ledgers can contain dozens of public facts. A permutation
        # table is factorial in their count. Compile the complete finite item
        # domains and cardinalities symbolically instead. The original schema
        # and positional binding_errors still validate the actual one decision;
        # no proof, choice or prose is repaired after generation.
        branches = []
        original_schema = row['wire']['response_format']['json_schema']['schema']
        exact_evidence = original_schema.get('properties', {}).get('evidenceFactIds')
        lower = native['properties']['decision']['properties']['positiveModuleIds'].get('minItems', 1)
        for count in range(lower, offer['maximumPositive'] + 1):
            branch = deepcopy(native)
            props = branch['properties']['decision']['properties']
            props['positiveModuleIds']['minItems'] = props['positiveModuleIds']['maxItems'] = count
            props['drawbackModuleIds'] = dict(type='array', enum=[deepcopy(reading['drawbackModuleIds'])])
            if exact_evidence and 'anyOf' in exact_evidence:
                props['evidenceFactIds'] = deepcopy(exact_evidence)
            proofs = branch['properties']['effectEvidenceFactIds']
            proofs['minItems'] = proofs['maxItems'] = count
            if exact_evidence and 'anyOf' in exact_evidence:
                proofs['items'] = deepcopy(exact_evidence)
            branches.append(branch)
        if not branches:
            raise ValueError('No public symbolic decision branch')
        result = dict(anyOf=branches)
        if original_schema.get('$defs'):
            result['$defs'] = deepcopy(original_schema['$defs'])
        if row['profile'] == 'AcquiredTraitModuleSelection':
            result['x-r80-canonical-evidence'] = True
            if lower == offer['maximumPositive'] == 1 and reading['drawbackModuleIds'] == []:
                messages, _ = single_decision_view.selection_messages(row, index, previous, reading)
                doc = json.loads(messages[1]['content'])
                if (not doc['currentOffer'].get('qualifiedNegativeEvidence')
                        and not doc.get('mandatoryFollowupEvidenceAnyOf')):
                    values, gameplay = trait_evidence_domain(result)
                    modules = branches[0]['properties']['decision']['properties']['positiveModuleIds']['items']['enum']
                    result['x-r80-single-effect-proof'] = dict(values=values, gameplay=sorted(gameplay),
                        requiredByModule={key: doc['modules'][key]['requiredEvidence'] for key in modules})
        return result
    result = single_decision_view.selection_native(row, index, previous, reading, native)
    decision_schema = native['properties']['decision']
    # The game's lossless reference transport may require a correlation literal
    # on a flat decision. Include it in the grammar before generation. It is not
    # a module/evidence choice, and no generated response is filled or repaired.
    reference = decision_schema.get('properties', {}).get('requestRef')
    if reference is None:
        return result
    values = reference.get('enum', [])
    if 'requestRef' not in decision_schema.get('required', []) or len(values) != 1:
        raise ValueError('Required requestRef must have one exact caller literal')
    Draft7Validator(reference).validate(values[0])
    for branch in result['anyOf']:
        for decision in branch['properties']['decision']['enum']:
            if 'requestRef' in decision and decision['requestRef'] != values[0]:
                raise ValueError('Conflicting native request reference')
            decision['requestRef'] = values[0]
            # The original generator has already intersected every semantic
            # choice. Re-validating its large anyOf once for every choice is
            # quadratic and can block the single model's next request. Check
            # this literal's exact shape here; validate the actual output once.
            if (not set(decision_schema.get('required', [])) <= set(decision)
                    or not set(decision) <= set(decision_schema['properties'])):
                raise ValueError('Native decision has unexpected required fields')
    return result


def game_prose_messages(row, index, decision):
    messages, schema = scoped_auxiliary_view.prose_messages(row, index, decision)
    messages = deepcopy(messages)
    document = json.loads(messages[1]['content'])
    if row['profile'] == candidate_task_view.SKILL:
        import build_aligned_language_training as aligned
        import practical_prototype_prose_view as practical
        import readable_prose_view as readable
        if document.get('selection') != decision:
            raise ValueError('Actual model decision changed before prose')
        contract = candidate_task_view.contract(row)
        keys = decision['positiveModuleIds'] + decision['drawbackModuleIds']
        document['selectedModules'] = {key: deepcopy(contract['modules'][key]) for key in keys}
        document['selectedPositiveEffects'] = {key: deepcopy(contract['modules'][key])
                                             for key in decision['positiveModuleIds']}
        document['selectedSeparateDrawbacks'] = {key: deepcopy(contract['modules'][key])
                                                for key in decision['drawbackModuleIds']}
        document['statTermGlossary'] = candidate_task_view.stat_term_gloss(document['selectedModules'])
        document['descriptionScope'] = dict(allSelectedModuleIds=deepcopy(keys),
                                           omitSecondaryEffects=False)
        for old, new in ((aligned.SCOPE, ''), (practical.STYLE, readable.OLD),
                         (practical.SUMMARY, practical.FULL), (practical.PRIMARY, practical.POLARITY)):
            if messages[0]['content'].count(old) != 1:
                raise ValueError('Expected prototype prose projection clause changed')
            messages[0]['content'] = messages[0]['content'].replace(old, new, 1)
        # 동일 효과 원문을 세 번 보내지 않음. 선택 결과와 모든 모듈 속성은 그대로 유지.
        for field, ids in (('selectedPositiveEffects', decision['positiveModuleIds']),
                           ('selectedSeparateDrawbacks', decision['drawbackModuleIds'])):
            expected = {key: document['selectedModules'][key] for key in ids}
            if document[field] != expected:
                raise ValueError('Selected prose effect partition changed')
            del document[field]
        messages[0]['content'] = SKILL_PROSE_GUIDANCE
    document['previousCandidateNames'] = list(row.get('_gamePreviousCandidateNames', []))
    messages[1]['content'] = json.dumps(document, ensure_ascii=False, separators=(',', ':'))
    if row['profile'] == candidate_task_view.SKILL:
        return messages, schema
    messages[0]['content'] += ' previousCandidateNames와 정확히 같은 기술 이름을 반복하지 않는다.'
    messages[0]['content'] += PROSE_NAME_GUIDANCE + (
        ' 이름과 설명은 쉬운 한국어로 쓴다. 한자나 외국어 표현을 한국어 문장 중간에 섞지 않는다.'
        ' 선택된 모든 효과의 대상, 동작과 발동 조건을 빠짐없이 짧게 설명한다.'
        ' 회복, 방어, 보호, 회피는 서로 다른 효과이므로 바꿔 쓰지 않는다.'
        ' 특정 조건에서 이점이 있다는 사실은 그 밖의 조건에서 불리하다는 뜻이 아니다.'
        ' 반대 조건의 불이익이나 대가를 대비 문장으로 새로 만들지 않는다.'
        ' 선택하지 않은 효과와 부담을 추가하지 않고, 이미 선택한 부효과를 생략하지 않는다.')
    return messages, schema


def fixed_facility_fields(schema):
    """독립 선택이 전혀 없는 caller 계약만 인식. 복수 후보에서 값을 고르지 않음."""
    fields = ('selectionId', 'positiveModuleIds', 'drawbackModuleIds', 'evidenceFactIds')
    all_fields = set(fields) | {'displayName', 'narrativeFlavor'}
    if (schema.get('type') != 'object' or schema.get('additionalProperties') is not False
            or set(schema.get('properties', {})) != all_fields
            or set(schema.get('required', [])) != all_fields):
        return None
    fixed = {}
    for field in fields:
        node = schema['properties'][field]
        if 'const' in node:
            value = deepcopy(node['const'])
        elif len(node.get('enum', [])) == 1:
            value = deepcopy(node['enum'][0])
        elif (node.get('type') == 'array' and node.get('minItems') == node.get('maxItems') == 1
              and len(node.get('items', {}).get('enum', [])) == 1):
            value = [deepcopy(node['items']['enum'][0])]
        else:
            return None
        Draft7Validator(node).validate(value)
        fixed[field] = value
    if (not isinstance(fixed['selectionId'], str) or not fixed['selectionId']
            or not isinstance(fixed['positiveModuleIds'], list) or len(fixed['positiveModuleIds']) != 1
            or fixed['drawbackModuleIds'] != []
            or not isinstance(fixed['evidenceFactIds'], list) or len(fixed['evidenceFactIds']) != 1):
        return None
    return fixed


def generate_fixed_facility(module, engine, row, folder):
    # 원래 contract 검사부터 수행. 모든 ID도 모델이 원 응답 안에 직접 출력.
    r80_request.validate_request(module, row)
    schema = row['wire']['response_format']['json_schema']['schema']
    if fixed_facility_fields(schema) is None:
        raise ValueError('Fixed facility route requires exact caller singleton fields')
    messages = deepcopy(row['wire']['messages'])
    messages.append(dict(role='system', content=(
        '이 응답 스키마는 효과와 근거가 각각 하나로 고정되어 있다. 고정된 모든 ID와 빈 부담 목록을'
        ' 원래 여섯 필드의 JSON 객체 안에 직접 출력한다. 허용된 효과와 실제 공개 사실만 설명한다.'
        ' 효과의 방향, 대상, 조건과 내재된 부담을 바꾸거나 생략하지 않는다.'
        ' displayName은 32자 이내의 자연스러운 한국어 이름, narrativeFlavor는 180자 이내의 짧은 설명이다.'
        ' 새 효과, 숫자, 비용, 사건이나 감정을 만들지 않는다.' + PROSE_NAME_GUIDANCE)))
    result = engine.call(messages, schema, lambda native: native, folder, 'fixed-facility', 768)
    Draft7Validator(schema).validate(result)
    write(folder / 'fixed-facility-route.json', dict(
        callerSingletonsOnly=True, allOriginalMessagesPreserved=True,
        fullResponseGeneratedByModel=True, modelCalls=1, outputRepair=False,
        semanticChoicesComputedByCode=False))
    return result


def _readable_public_tables(value):
    if isinstance(value, list):
        return [_readable_public_tables(item) for item in value]
    if not isinstance(value, dict):
        return value
    if set(value) == {'columns', 'rows'}:
        from live_module_selection_contract import _table
        return dict(columns=deepcopy(value['columns']), records=[
            {key: _readable_public_tables(item) for key, item in row.items()} for row in _table(value)])
    return {key: _readable_public_tables(item) for key, item in value.items()}


def persona_readable_messages(messages):
    """Decode the caller's full lossless transport, without selecting facts."""
    from live_module_selection_contract import _expand
    result = deepcopy(messages)
    for message in result:
        if message.get('role') != 'user':
            continue
        headers, documents = [], []
        for line in message['content'].splitlines(keepends=True):
            try:
                value = json.loads(line)
            except ValueError:
                value = None
            if isinstance(value, dict) and value.get('encoding') == 'repeated-public-text-v1':
                documents.append(_readable_public_tables(_expand(value)))
            else:
                headers.append(line)
        if documents:
            message['content'] = json.dumps(dict(profile='Persona',
                callerInstructions=''.join(headers), publicInputDocuments=documents),
                ensure_ascii=False, separators=(',', ':'))
    return result


def game_auxiliary_messages(profile, messages):
    if profile not in ('BubbleLine', 'CharacterRecord', 'Persona', 'SocialRumor', 'MacroGoal', 'MoodImpulse'):
        return messages
    result = persona_readable_messages(messages) if profile == 'Persona' else deepcopy(messages)
    for message in result:
        if message.get('role') != 'user':
            continue
        original_text = message['content']
        try:
            packet = json.loads(original_text)
        except (ValueError, TypeError):
            packet = None
            # The game queue keeps its exact request-local context outside the
            # event packet so reference validation can still read it. Bind that
            # already validated model-facing suffix as background, losslessly.
            if isinstance(original_text, str) and original_text.startswith('{'):
                try:
                    candidate, end = json.JSONDecoder().raw_decode(original_text)
                    suffix = original_text[end:]
                    if isinstance(candidate, dict) and candidate.get('profile') == profile and suffix.strip():
                        packet = candidate
                        packet['callerBackground'] = suffix
                        message['content'] = json.dumps(packet, ensure_ascii=False, separators=(',', ':'))
                except (ValueError, TypeError):
                    pass
        if profile == 'BubbleLine' and isinstance(packet, dict):
            facts = packet.get('publicFacts', {})
            event = facts.get('sourceEvent')
            if isinstance(event, str):
                boundary = re.search(r'\r?\nAvailable character facts \(request-local references\):', event)
                if boundary is not None:
                    facts['sourceEvent'] = event[:boundary.start()]
                    packet['callerBackground'] = event[boundary.start():]
                    message['content'] = json.dumps(packet, ensure_ascii=False, separators=(',', ':'))
        elif packet is None and profile in ('CharacterRecord', 'SocialRumor'):
            if profile == 'CharacterRecord':
                patterns = [r'(?ms)^Source event:\r?\n.*?^message: (.*?)\r?\nrequiredAnchorWords:',
                            r'(?ms)^Source event: (.*?)\r?\nRequired exact phrases:']
            else:
                patterns = [r'(?ms)^Event:\r?\n(.*?)\r?\ncandidateFacilities:',
                            r'(?ms)^Experience:\r?\n(.*?)\r?\n(?:Available |JSON Example)']
            events = [match.group(1) for pattern in patterns for match in re.finditer(pattern, original_text)]
            if len(events) == 1:
                message['content'] = json.dumps(dict(profile=profile, publicFacts=dict(sourceEvent=events[0]),
                    originalCallerPrompt=original_text), ensure_ascii=False, separators=(',', ':'))
    rule = scoped_auxiliary_view.INSTRUCTIONS[profile] + (
        '\n대사와 설명 문자열은 쉬운 한국어로 쓴다. ID와 정해진 분류 값은 원래 값을 지킨다. '
        'Source event, sourceEvent 또는 Event는 이번 사건이며 배경 성격과 과거 기록은 이번 결과가 아니다. '
        '발생, 지속, 종료를 구별하고 종료나 회복이 명시되지 않았으면 해소나 치료를 덧붙이지 않는다. '
        '원문 수치는 소수점까지 그대로 보존하고 새로운 피해, 보상, 원인이나 대상을 만들지 않는다. '
        'publicFacts.sourceEvent는 이번 사건이다. callerBackground와 originalCallerPrompt의 과거 기록을 이번 사건으로 바꾸지 않는다.')
    if profile == 'CharacterRecord':
        rule += (' publicBindings.requiredSubject로 시작하고 requiredExactPhrases를 각각 한 번씩 그대로 쓴다. '
                 '주어진 행동과 결과를 직접 서술하는 짧은 기록으로 쓴다. '
                 '수치나 원인을 정의하고 설명하는 해설문으로 바꾸거나 같은 사실을 중복해서 말하지 않는다. '
                 '이름의 원래 표기를 보존하고 인물의 추가 행동이나 결과를 만들지 않는다.')
    if profile == 'SocialRumor':
        rule += (' summary는 이번 실제 행동과 결과만 한 문장으로 쓴다. '
                 '시설의 관측 경험이 아니면 임의 시설에 추천이나 불평을 붙이지 않는다. '
                 '작업의 중단 이유와 대상 시설의 용도는 별개다. 휴식 필요나 피로는 행위자의 상태이며 '
                 '그 시설이 휴식 공간이라는 뜻이 아니다. 작업 중단을 시설 이용 성공으로 바꾸지 않는다. '
                 'count와 이용 횟수는 이미 겪은 빈도이며 시설의 수용량이나 새 기능이 아니다. '
                 '시설을 소개하거나 앞으로 효과가 좋아진다고 약속하지 말고 이번 경험만 전한다.')
    if profile == 'Persona':
        rule += (' 서로 다른 특성이나 출신을 억지로 원인과 결과로 연결하지 않는다. '
                 '문장마다 주어와 서술어가 자연스럽게 호응하는 간단한 완결문을 쓴다. '
                 '근접 소질 등 기술 성향에서 근거 없는 사회성이나 마음의 능력을 추론하지 않는다. '
                 'personaName은 짧은 한국어 별칭이다. flavorText는 공개된 성향이나 배경을 바탕으로 '
                 '그 인물의 평소 모습을 한두 문장으로 소개한다. 이름을 다시 쓰지 않아도 된다. '
                 '배경 사실을 나열하거나 추상적인 변화를 배운다고 쓰지 않는다. '
                 '제공되지 않은 성별, 직업, 새로운 능력이나 미래의 이득을 만들지 않는다. '
                 '별칭은 행동 성향이나 생활 습관을 드러내고 성별이나 직업을 추측한 칭호로 꾸미지 않는다. '
                 'publicInputDocuments의 records는 압축을 이미 풀어 전달한 원래 공개 사실이다. '
                 '각 레코드의 text와 값을 직접 읽고 행 번호나 문자열 참조를 다시 계산하지 않는다. '
                 '한자를 섞지 말고 쉬운 한국어를 쓴다. 테스트, 시범, 모델이나 호출 과정의 설명을 본문에 넣지 않는다.')
    if profile == 'BubbleLine':
        rule += (' 이번 사건을 겪은 사람이 입으로 할 짧은 반응 한 문장만 쓴다. '
                 '원인이 분명하지 않으면 원인 대신 직접 느낀 상태에 반응한다. '
                 '꾸민 비유나 낯선 외래어, 명사 나열을 피하고 평소 쓰는 말로 끝맺는다. '
                 '관형형 뒤에서 끊거나 대상 없는 동사를 이어 쓰지 않는다. '
                 '길이가 모자라면 효과나 사실을 바꾸지 않고 처음부터 더 짧은 반응을 쓴다.')
    result[0]['content'] += '\n' + rule
    return result


def bubble_writing_messages(messages, schema):
    """원래 호출 규격의 길이에 작성 지시만 맞춘다. 공개 입력과 출력은 수정하지 않음."""
    properties = schema.get('properties', {})
    line = properties.get('line', {})
    maximum = line.get('maxLength')
    if (set(properties) != {'line'} or line.get('type') != 'string'
            or type(maximum) is not int or maximum <= 0):
        raise ValueError('Exact caller-bounded BubbleLine schema required')
    result = deepcopy(messages)
    if not result or result[0].get('role') != 'system':
        raise ValueError('BubbleLine writing requires its original system instruction')
    legacy = '80자 이내의 4~10어절로 쓰며 원문을 그대로 복사하지 않는다.'
    bounded = ('공백과 문장부호를 포함해 ' + str(maximum)
               + '자 이내의 완결된 짧은 반응 한 문장으로 쓰며 원문을 그대로 복사하지 않는다.')
    replaced = False
    for message in result:
        if message.get('role') == 'system' and legacy in message['content']:
            message['content'] = message['content'].replace(legacy, bounded)
            replaced = True
    if not replaced and not any(bounded in m.get('content', '')
                                for m in result if m.get('role') == 'system'):
        result[0]['content'] += '\n' + bounded
    return result


def record_writing_messages(messages):
    """V2의 모든 공개 사실과 참조는 유지. 중복된 옛 작성 지시만 모델 입력에서 분리."""
    result, projected = deepcopy(messages), 0
    for message in result:
        if message.get('role') != 'user':
            continue
        try:
            packet = json.loads(message['content'])
        except (ValueError, TypeError):
            continue
        if not isinstance(packet, dict) or packet.get('profile') != 'CharacterRecord' or packet.get('recordPromptVersion') != 2:
            continue
        facts = packet.get('publicFacts')
        if (not isinstance(facts, dict) or not isinstance(facts.get('publicCharacterFacts'), dict)
                or not isinstance(facts.get('sourceEvent'), dict) or not isinstance(packet.get('publicBindings'), dict)
                or not isinstance(packet.get('originalCallerPrompt'), str)):
            raise ValueError('Complete V2 public character and event facts required')
        # 원래 wire와 originalCallerPrompt는 request.json에 그대로 보존한다.
        # 인물/과거 기록/이벤트 속성은 모두 구조화된 publicFacts에 그대로 있다.
        document = {key: value for key, value in packet.items()
                    if key not in ('originalCallerPrompt', 'publicFacts', 'publicBindings')}
        document['publicFacts'] = facts
        document['publicBindings'] = packet['publicBindings']
        message['content'] = json.dumps(document, ensure_ascii=False, separators=(',', ':'))
        projected += 1
    if projected > 1:
        raise ValueError('One V2 character record event required')
    if projected:
        result[0]['content'] += ('\n주어진 sourceEvent의 행동과 결과만 짧은 평서문으로 쓴다. '
            'requiredSubject와 requiredExactPhrases의 영문 이름, 숫자와 띄어쓰기까지 원문 그대로 보존한다. '
            'publicCharacterFacts와 과거 기록은 말투의 배경일 뿐 이번 사건의 추가 사실이 아니다. '
            '하나의 사건을 두 번 설명하거나 제공되지 않은 세부 상황이나 결과를 붙이지 않는다.')
    return result


def persona_generation_schema(schema):
    """A bounded Korean writing request, retaining the original consumer schema."""
    if (set(schema.get('properties', {})) != {'personaName', 'flavorText'}
            or set(schema.get('required', [])) != {'personaName', 'flavorText'}):
        raise ValueError('Exact public Persona fields required')
    result = deepcopy(schema)
    alphabet = r'[^"\\\x00-\x1F\u3400-\u4DBF\u4E00-\u9FFF\uD800-\uDFFF\uF900-\uFAFF]'
    for field, lower, upper in [('personaName', 2, 14), ('flavorText', 8, 96)]:
        node = result['properties'][field]
        if node.get('type') != 'string' or set(node) - {'type', 'minLength', 'maxLength', 'description'}:
            raise ValueError('Unsupported public Persona text constraint')
        minimum, maximum = max(lower, node.get('minLength', 0)), min(upper, node['maxLength'])
        if minimum > maximum:
            raise ValueError('Empty Persona writing bounds')
        node['minLength'], node['maxLength'] = minimum, maximum
        node['pattern'] = ('^' + alphabet + '{' + str(minimum) + ',' + str(maximum) + '}$'
            if field == 'personaName' else '^' + alphabet + '{' + str(minimum - 1) + ',' + str(maximum - 1) + '}[.!?]$')
    return result


@lru_cache(maxsize=16)
def canonical_name_alphabet(pattern):
    check = re.compile(pattern)
    # Canonical player-facing BMP names: no JSON escapes, controls or surrogate
    # spellings. Every character still satisfies the caller's original pattern.
    return tuple(code for code in range(32, 65536) if code not in (34, 92)
                 and not 0xD800 <= code <= 0xDFFF and check.fullmatch(chr(code)))


def class_for(codes):
    codes = sorted(codes)
    if not codes:
        return None
    ranges = []
    start = end = codes[0]
    for code in codes[1:]:
        if code == end + 1:
            end = code
        else:
            ranges.append((start, end))
            start = end = code
    ranges.append((start, end))
    return '[' + ''.join('\\u%04X' % start + (('-\\u%04X' % end) if end != start else '')
                         for start, end in ranges) + ']'


def unique_name_rules(node, excluded):
    """A finite-prefix DFA excludes earlier names before generation.

    It never supplies a name. All canonical nonblank names in the caller's
    alphabet and length range, except the actual earlier names, remain open.
    """
    upper = node['maxLength']
    if node.get('minLength') != 1 or not 1 <= upper <= 64:
        raise ValueError('Unsupported caller name bounds')
    alphabet = set(canonical_name_alphabet(node['pattern']))
    nonspace = {code for code in alphabet if not chr(code).isspace()}
    whitespace = alphabet - nonspace
    forbidden = {name.strip() for name in excluded if name.strip()}
    rules = {'r80-name-char': class_for(alphabet), 'r80-name-final': class_for(nonspace)}
    labels = {}
    def state(prefix):
        if prefix in labels:
            return labels[prefix]
        if len(prefix) == upper:
            return None if prefix in forbidden else '""'
        label = 'r80-name-state-' + str(len(labels))
        labels[prefix] = label
        branches = []
        if prefix and not prefix[-1].isspace() and prefix not in forbidden:
            branches.append('""')
        children = {name[len(prefix)] for name in forbidden if name.startswith(prefix) and len(name) > len(prefix)}
        for char in sorted(children):
            if ord(char) not in alphabet:
                continue
            target = state(prefix + char)
            if target is not None:
                branches.append(json.dumps(char, ensure_ascii=False) + ' ' + target)
        remaining = upper - len(prefix) - 1
        tail = ('r80-name-char{0,' + str(remaining - 1) + '} r80-name-final') if remaining > 0 else None
        ordinary = class_for(nonspace - {ord(char) for char in children})
        if ordinary:
            branches.append(ordinary + (' ("" | ' + tail + ')' if tail else ''))
        spaced = class_for(whitespace - {ord(char) for char in children})
        if prefix and spaced and tail:
            branches.append(spaced + ' ' + tail)
        rules[label] = ' | '.join(branches)
        return label
    root = state('')
    rules['displayName'] = json.dumps('"') + ' ' + root + ' ' + json.dumps('"')
    return rules


@lru_cache(maxsize=32)
def canonical_prose_alphabet(original_pattern, native_pattern, subject, suffix):
    original, native = re.compile(original_pattern), re.compile(native_pattern)
    def permitted(char):
        return all(original.fullmatch(subject + particle + ' ' + char + suffix)
                   and native.fullmatch(subject + particle + ' ' + char + suffix) for particle in ('은', '는'))
    return tuple(code for code in range(32, 65536) if code not in (34, 92)
                 and not 0xD800 <= code <= 0xDFFF and permitted(chr(code)))


def game_prose_grammar(original, native, excluded_names):
    """Intersect the real caller's character rules with R80's exact subject.

    Canonical BMP text needs no JSON escape spellings. The fixed subject is a
    JSON/GBNF literal, so a real name containing spaces or digits remains exact.
    The body satisfies both patterns; no completed answer is altered.
    """
    from prose_bounds_grammar import fixed_pattern, literal, fragment
    props = native['properties']
    if set(props) != {'displayName', 'narrativeFlavor'} or set(native.get('required', [])) != set(props) or native.get('additionalProperties') is not False:
        raise ValueError('Exact two-field public game prose contract required')
    narrative = props['narrativeFlavor']
    if narrative['pattern'].startswith('^([^"'):
        # Current C# equipment/facility schemas encode JSON-safe numeric-free
        # characters as a bounded repeated group, rather than a fixed person.
        # Compile that actual alphabet and bounds; do not drop its pattern.
        for node in (narrative, original['properties']['narrativeFlavor']):
            if (not node['pattern'].startswith('^([^"')
                    or not node['pattern'].endswith('){1,' + str(node['maxLength']) + '}$')
                    or r'\\u(' not in node['pattern']):
                raise ValueError('Unsupported non-person repeated prose contract')
        alphabet = set(canonical_name_alphabet(narrative['pattern'])) & set(canonical_name_alphabet(
            original['properties']['narrativeFlavor']['pattern']))
        final = {code for code in alphabet if not chr(code).isspace()}
        lower = max(1, narrative.get('minLength', 0), original['properties']['narrativeFlavor'].get('minLength', 0))
        upper = min(narrative['maxLength'], original['properties']['narrativeFlavor']['maxLength'])
        if not final or lower > upper:
            raise ValueError('Empty bounded non-person prose intersection')
        rules = unique_name_rules(props['displayName'], excluded_names)
        rules['r80-prose-char'] = class_for(alphabet)
        rules['r80-prose-final'] = class_for(final)
        quote = literal('"')
        rules['r80-narrative'] = quote + ' r80-prose-char{' + str(lower - 1) + ',' + str(upper - 1) + '} r80-prose-final ' + quote
        rules['root'] = literal('{"displayName":') + ' displayName ' + literal(',"narrativeFlavor":') + ' r80-narrative ' + literal('}')
        return '\n'.join(name + ' ::= ' + body for name, body in rules.items()) + '\n'
    subject, suffix, _ = fixed_pattern(narrative['pattern'])
    supplied = original['properties']['narrativeFlavor']
    alphabet = canonical_prose_alphabet(supplied['pattern'], narrative['pattern'], subject, suffix)
    fixed = len(subject) + 2 + len(suffix)
    lower = max(0, supplied.get('minLength', 0) - fixed, narrative.get('minLength', 0) - fixed)
    upper = min(supplied['maxLength'], narrative['maxLength']) - fixed
    if upper < lower or not alphabet:
        raise ValueError('Caller prose and exact subject have no bounded canonical intersection')
    names = unique_name_rules(props['displayName'], excluded_names)
    prefix = ' | '.join(literal(fragment(subject + particle + ' ')) for particle in subject_particles(subject))
    quote = literal('"')
    rules = dict(names)
    rules['r80-prose-char'] = class_for(alphabet)
    rules['r80-narrative'] = quote + ' (' + prefix + ') r80-prose-char{' + str(lower) + ',' + str(upper) + '} ' + literal(fragment(suffix)) + ' ' + quote
    rules['root'] = literal('{"displayName":') + ' displayName ' + literal(',"narrativeFlavor":') + ' r80-narrative ' + literal('}')
    return '\n'.join(name + ' ::= ' + body for name, body in rules.items()) + '\n'


def trait_evidence_domain(native):
    """Read and verify the original caller's full at-least-one-gameplay union."""
    expanded = inline_local_refs(native)
    fields = [branch['properties']['decision']['properties']['evidenceFactIds']
              for branch in expanded['anyOf']]
    if not fields or any(field != fields[0] for field in fields):
        raise ValueError('One exact trait evidence domain required')
    field = fields[0]
    values = sorted(field['items']['enum'])
    lower, upper = field.get('minItems', 0), field['maxItems']
    branches = field['anyOf']
    gameplay = set(branches[0]['items'][0]['enum'])
    nongameplay = set(values) - gameplay
    expected = []
    for length in range(lower, upper + 1):
        for first in range(length):
            expected.append([nongameplay] * first + [gameplay] + [set(values)] * (length-first-1))
    actual = [[set(slot['enum']) for slot in branch['items']] for branch in branches]
    if (actual != expected or not gameplay or len(values) != len(set(values))
            or any(branch.get('additionalItems') is not False
                   or branch.get('minItems') != len(branch['items'])
                   or branch.get('maxItems') != len(branch['items']) for branch in branches)):
        raise ValueError('Unsupported trait evidence union; no sorting repair')
    return values, gameplay


def single_effect_trait_grammar(native, info):
    """Mirror the model's one evidence set in its one positional proof.

    A recursive rule emits a sorted ID before recursion and the same ID after
    it. No response is filled, reordered or repaired by the transport. Every
    offered module and every declared supporting fact remains a model choice.
    """
    from prose_bounds_grammar import literal
    values, gameplay = trait_evidence_domain(native)
    expanded = inline_local_refs(native)
    if (len(expanded['anyOf']) != 1 or values != sorted(info['values'])
            or gameplay != set(info['gameplay'])):
        raise ValueError('Single-effect proof domain mismatch')
    branch = expanded['anyOf'][0]
    decision = branch['properties']['decision']
    props = decision['properties']
    positive, evidence = props['positiveModuleIds'], props['evidenceFactIds']
    proof = branch['properties']['effectEvidenceFactIds']
    fields = {'selectionId', 'positiveModuleIds', 'drawbackModuleIds', 'evidenceFactIds'}
    if 'requestRef' in props:
        fields.add('requestRef')
    modules = positive['items']['enum']
    if (set(props) != fields or set(decision['required']) != fields
            or decision.get('additionalProperties') is not False
            or positive.get('minItems') != 1 or positive.get('maxItems') != 1
            or props['drawbackModuleIds'].get('enum') != [[]]
            or evidence.get('minItems') != 1 or evidence.get('maxItems') != len(values)
            or proof.get('minItems') != 1 or proof.get('maxItems') != 1
            or proof.get('items') != evidence
            or len(modules) != len(set(modules)) or set(modules) != set(info['requiredByModule'])):
        raise ValueError('Unsupported single-effect proof shape')

    fixed = {}
    for key in ('requestRef', 'selectionId'):
        if key not in props:
            continue
        options = props[key].get('enum', [props[key].get('const')])
        if len(options) != 1 or not isinstance(options[0], str):
            raise ValueError('One exact public correlation literal required')
        fixed[key] = options[0]
    rules, roots = {}, []
    for module_index, module in enumerate(modules):
        required = set(info['requiredByModule'][module])
        if required and not required.intersection(values):
            raise ValueError('Declared module has no legal proof: ' + module)
        states = {}
        def state(last, seen_gameplay, seen_required):
            key = (last, seen_gameplay, seen_required)
            if key in states:
                return states[key]
            name = 'r80-proof-' + str(module_index) + '-' + str(len(states))
            states[key] = name
            alternatives = []
            for index in range(last + 1, len(values)):
                game = seen_gameplay or values[index] in gameplay
                req = seen_required or values[index] in required
                token = literal(json.dumps(values[index], ensure_ascii=False))
                if game and req:
                    alternatives.append(token + ' ' + literal(']},"effectEvidenceFactIds":[[') + ' ' + token)
                remaining = values[index + 1:]
                if (remaining and (game or gameplay.intersection(remaining))
                        and (req or required.intersection(remaining))):
                    child = state(index, game, req)
                    alternatives.append(token + ' ' + literal(',') + ' ' + child + ' ' + literal(',') + ' ' + token)
            if not alternatives:
                raise ValueError('Declared module has no legal proof: ' + module)
            rules[name] = ' | '.join(alternatives)
            return name
        prefix = dict(fixed, positiveModuleIds=[module], drawbackModuleIds=[])
        start = '{"decision":' + json.dumps(prefix, ensure_ascii=False, separators=(',', ':'))[:-1] + ',"evidenceFactIds":['
        roots.append(literal(start) + ' ' + state(-1, False, not required) + ' ' + literal(']]}'))
    if not roots:
        raise ValueError('No declared trait module')
    rules['root'] = ' | '.join(roots)
    return '\n'.join(name + ' ::= ' + body for name, body in rules.items()) + '\n'


def sorted_trait_grammar(native):
    """Intersect the caller's evidence union with its existing sorted-ID rule."""
    from general_prototype_auxiliary_grammar import converter
    from prose_bounds_grammar import literal
    official = converter()
    values, gameplay = trait_evidence_domain(native)
    field = inline_local_refs(native)['anyOf'][0]['properties']['decision']['properties']['evidenceFactIds']
    lower, upper = field.get('minItems', 0), field['maxItems']

    class SortedEvidence(official.SchemaConverter):
        def visit(self, node, name):
            ordered = name.endswith('decision-evidenceFactIds')
            if ordered or name.endswith('effectEvidenceFactIds-item'):
                states = {}
                def state(last, count, seen):
                    key = (last, count, seen)
                    if key in states: return states[key]
                    rule = 'r80-sorted-evidence-' + name + '-' + str(len(states))
                    states[key] = rule
                    alternatives = []
                    if count >= lower and seen:
                        alternatives.append(literal(']') + ' space')
                    if count < upper:
                        for index in range(last + 1 if ordered else 0, len(values)):
                            next_seen = seen or values[index] in gameplay
                            if not next_seen and (count + 1 >= upper or
                                (ordered and not any(v in gameplay for v in values[index+1:]))):
                                continue
                            child = state(index if ordered else -1, count + 1, next_seen)
                            comma = literal(',') + ' space ' if count else ''
                            alternatives.append(comma + literal(json.dumps(values[index], ensure_ascii=False))
                                                + ' space ' + child)
                    # Unreachable branches are omitted by their parent.
                    body = ' | '.join(alternatives)
                    if not body: raise ValueError('Empty sorted evidence state')
                    self._add_rule(rule, body)
                    return rule
                return self._add_rule(name, literal('[') + ' space ' + state(-1, 0, False))
            return super().visit(node, name)
    builder = SortedEvidence(prop_order={}, allow_fetch=False, dotall=False, raw_pattern=False)
    resolved = builder.resolve_refs(deepcopy(native), 'https://r80.invalid/trait-schema')
    builder.visit(resolved, '')
    text = builder.format_grammar() + '\n'
    if not any('decision-evidenceFactIds ::=' in line and 'r80-sorted-evidence-' in line
               for line in text.splitlines()):
        raise ValueError('Sorted evidence rule was not bound')
    return text


def factored_finite_decision_grammar(native):
    """Factor only the existing finite decision/proof union. No new choice or proof."""
    if set(native) - {'anyOf', '$defs'} or not isinstance(native.get('anyOf'), list) or not native['anyOf']:
        return None
    definitions = native.get('$defs', {})
    pairs = {}
    for branch in native['anyOf']:
        if (set(branch) != {'type', 'properties', 'required', 'additionalProperties'}
                or branch['type'] != 'object' or branch['additionalProperties'] is not False
                or branch['required'] != ['decision', 'effectEvidenceFactIds']
                or list(branch['properties']) != ['decision', 'effectEvidenceFactIds']):
            return None
        decision, proof = (branch['properties'][key] for key in ('decision', 'effectEvidenceFactIds'))
        if set(proof) == {'$ref'} and proof['$ref'].startswith('#/$defs/'):
            proof = definitions.get(proof['$ref'][8:])
        if (not isinstance(proof, dict) or set(proof) != {'type', 'enum'} or proof['type'] != 'array'
                or set(decision) != {'type', 'enum'} or decision['type'] != 'object'
                or not isinstance(decision['enum'], list) or not decision['enum']
                or not isinstance(proof['enum'], list) or not proof['enum']):
            return None
        if any(not isinstance(value, dict) for value in decision['enum']):
            return None
        if any(not isinstance(value, list) or any(not isinstance(ids, list) or
               any(not isinstance(identity, str) for identity in ids) for ids in value) for value in proof['enum']):
            return None
        proof_texts = {json.dumps(value, ensure_ascii=False, separators=(',', ':')) for value in proof['enum']}
        for value in decision['enum']:
            text = json.dumps(value, ensure_ascii=False, separators=(',', ':'))
            if len(text) > 512 or any(len(proof_text) > 512 for proof_text in proof_texts):
                return None
            pairs.setdefault(text, set()).update(proof_texts)
    from prose_bounds_grammar import literal
    rules, interned = {}, {}

    def trie(values, prefix):
        root = {}
        for text, terminal in values.items():
            node = root
            for character in text:
                node = node.setdefault(character, {})
            node[None] = terminal

        def emit(node):
            alternatives = [node[None]] if None in node else []
            for character in sorted(key for key in node if key is not None):
                word, child = character, node[character]
                while None not in child and len(child) == 1:
                    character = next(iter(child)); word += character; child = child[character]
                alternatives.append(literal(word) + ' ' + emit(child))
            body = ' | '.join(alternatives)
            if body not in interned:
                name = prefix + '-' + str(len(rules))
                interned[body] = name; rules[name] = body
            return interned[body]
        return emit(root)

    proof_roots = {}
    decisions = {}
    for text, proofs in pairs.items():
        key = tuple(sorted(proofs))
        if key not in proof_roots:
            proof_roots[key] = trie({value: '""' for value in key}, 'proof')
        decisions[text] = ('space "," space ' + literal('"effectEvidenceFactIds"')
                           + ' space ":" space ' + proof_roots[key] + ' space "}"')
    decision_root = trie(decisions, 'decision')
    # The original native object order and bounded whitespace remain unchanged.
    root = '"{" space ' + literal('"decision"') + ' space ":" space ' + decision_root
    text = 'root ::= ' + root + '\nspace ::= | " " | "\\n"{1,2} [ \\t]{0,20}\n'
    text += '\n'.join(key + ' ::= ' + value for key, value in rules.items()) + '\n'
    return text


def decoder_for(schema, transform, stage, excluded_names=()):
    # R80's evaluated auxiliary path preserves numeric limits and patterns
    # directly. The event-stage tuple converter intentionally has a narrower
    # vocabulary and must not be applied to game authoring/auxiliary schemas.
    base = deepcopy(schema)
    if stage == 'selection' and set(base.get('properties', {})) == {'decision', 'effectEvidenceFactIds'}:
        evidence = base['properties']['decision']['properties']['evidenceFactIds']
        if len(evidence.get('items', {}).get('enum', [])) > 6 and 'anyOf' in evidence:
            # The game transform below rebinds the exact caller tuple union and
            # local definitions into the final native root. Do not first expand
            # that union into a factorial table or a multi-megabyte wire copy.
            evidence.pop('anyOf')
            base['properties']['effectEvidenceFactIds']['items'].pop('anyOf', None)
    native = transform(base if stage in ('auxiliary', 'prose', 'fixed-facility') or stage.startswith('authored-') else llama_schema(finite_schema(base)))
    single_proof = native.pop('x-r80-single-effect-proof', None)
    if single_proof is not None:
        native.pop('x-r80-canonical-evidence', None)
        text = single_effect_trait_grammar(native, single_proof)
        return native, dict(grammar=text), dict(decoder='original-single-effect-mirrored-evidence@1',
            nativeGrammarSha256=hashlib.sha256(text.encode()).hexdigest(),
            outputRepair=False, semanticChoicesComputedByCode=False)
    if native.pop('x-r80-canonical-evidence', False):
        text = sorted_trait_grammar(native)
        return native, dict(grammar=text), dict(decoder='original-trait-tuple-and-sorted-evidence@1',
            nativeGrammarSha256=hashlib.sha256(text.encode()).hexdigest(),
            outputRepair=False, semanticChoicesComputedByCode=False)
    if stage == 'selection':
        text = factored_finite_decision_grammar(native)
        if text is not None:
            return native, dict(grammar=text), dict(decoder='same-finite-decision-proof-prefix-trie@1',
                nativeGrammarSha256=hashlib.sha256(text.encode('utf8')).hexdigest(),
                originalFiniteChoiceAndProofLanguagePreserved=True,
                outputRepair=False, semanticChoicesComputedByCode=False)
    if stage == 'fixed-facility':
        fixed = fixed_facility_fields(native)
        if fixed is None:
            raise ValueError('Fixed facility native contract contains a free choice')
        prose = dict(type='object', additionalProperties=False,
                     required=['displayName', 'narrativeFlavor'], properties={
                         key: deepcopy(native['properties'][key])
                         for key in ('displayName', 'narrativeFlavor')})
        text = game_prose_grammar(prose, prose, ())
        from prose_bounds_grammar import literal
        original_prefix = literal('{"displayName":')
        prefix = json.dumps(fixed, ensure_ascii=False, separators=(',', ':'))[:-1] + ',"displayName":'
        root = next(line for line in text.splitlines() if line.startswith('root ::= '))
        if root.count(original_prefix) != 1:
            raise ValueError('Exact fixed facility prose root required')
        text = text.replace(root, root.replace(original_prefix, literal(prefix), 1), 1)
        return native, dict(grammar=text), dict(
            decoder='caller-singleton-facility-full-response@1',
            nativeGrammarSha256=hashlib.sha256(text.encode('utf8')).hexdigest(),
            fullResponseGeneratedByModel=True, outputRepair=False, semanticChoicesComputedByCode=False)
    if stage == 'prose':
        text = game_prose_grammar(schema, native, excluded_names)
        return native, dict(grammar=text), dict(decoder='caller-pattern-and-exact-subject-intersection@1',
            nativeGrammarSha256=hashlib.sha256(text.encode('utf8')).hexdigest(),
            canonicalBmpText=True, fixedPublicSubjectExemptFromBodyNumberRule=True,
            excludedEarlierCandidateNames=list(excluded_names),
            outputRepair=False, semanticChoicesComputedByCode=False)
    if stage.startswith('authored-'):
        from general_prototype_auxiliary_grammar import grammar
        ordered = deepcopy(native)
        if stage == 'authored-candidate':
            order = ('selectionId', 'positiveModuleIds', 'drawbackModuleIds', 'evidenceFactIds',
                     'displayName', 'narrativeFlavor')
            if set(ordered['properties']) != set(order):
                raise ValueError('Exact authored candidate fields required')
            ordered['properties'] = {key: ordered['properties'][key] for key in order}
        text = grammar(ordered)
        if 'displayName' in native.get('properties', {}):
            rules = unique_name_rules(native['properties']['displayName'], excluded_names)
            if len(re.findall(r'(?m)^displayName ::= .*$', text)) != 1:
                raise ValueError('One original displayName grammar rule required')
            text = '\n'.join(line for line in text.splitlines() if not line.startswith('displayName ::=')) + '\n'
            text += '\n'.join(name + ' ::= ' + body for name, body in rules.items()) + '\n'
        return native, dict(grammar=text), dict(decoder='caller-bounded-initial-skill-grammar',
            nativeGrammarSha256=hashlib.sha256(text.encode('utf8')).hexdigest(), outputRepair=False,
            canonicalUniqueNameConstraint='displayName' in native.get('properties', {}),
            excludedEarlierCandidateNames=list(excluded_names))
    if stage == 'auxiliary':
        if 'rumorType' in native.get('properties', {}):
            from general_prototype_auxiliary_grammar import native_constraint
            native = social_target_schema(native)
            decoder, constraint = native_constraint(native)
            constraint['literalSocialTargetFieldsEnforcedTogether'] = True
            return native, decoder, constraint
        from behavior_auxiliary_grammar import native_constraint
        decoder, constraint = native_constraint(native)
    else:
        from numeric_free_prose_grammar import native_constraint
        decoder, constraint = native_constraint(native, prose=stage == 'prose')
    return native, decoder, constraint


INTERACTIVE_PROFILES = frozenset(('Persona', 'MacroGoal', 'MoodImpulse', 'SocialRumor', 'CharacterRecord', 'BubbleLine', 'EvolutionHistoryLegacyV2'))


class InferenceTransportError(RuntimeError):
    pass


def retryable_failure(error):
    return isinstance(error, (OSError, InferenceTransportError))


class Engine:
    def __init__(self, base, key, context_size=8192, profile=None, slot=0, caller_connected=None):
        self.base, self.key = base, key
        self.profile = profile
        self.slot = slot
        self.caller_connected = caller_connected
        if type(context_size) is not int or not 2048 <= context_size <= 32768:
            raise ValueError('Explicit configured model context size required')
        self.context_size = context_size
        self.opener = build_opener(ProxyHandler({}))

    def for_request(self, profile, slots, caller_connected, slot=None):
        return Engine(self.base, self.key, self.context_size, profile=profile,
            slot=(0 if slots == 1 or profile == 'BubbleLine' else
                  1 if profile in INTERACTIVE_PROFILES and slots >= 3 else
                  0 if profile in INTERACTIVE_PROFILES else slots - 1) if slot is None else slot,
            caller_connected=caller_connected)

    def require_connected(self):
        if self.caller_connected is not None and not self.caller_connected():
            raise ConnectionAbortedError('Caller disconnected; staged request cancelled.')

    def http(self, path, body=None, timeout=45):
        self.require_connected()
        data = None if body is None else json.dumps(body, ensure_ascii=False, separators=(',', ':')).encode('utf8')
        if path == '/completion' and self.caller_connected is not None:
            return self.completion_http(path, data, timeout)
        request = Request(self.base + path, data=data,
                          headers={'Content-Type': 'application/json', 'Authorization': 'Bearer ' + self.key})
        try:
            with self.opener.open(request, timeout=timeout) as response:
                raw = response.read()
        except HTTPError as error:
            detail = error.read(4096).decode('utf8', errors='replace')
            failure = InferenceTransportError if error.code >= 500 else ValueError
            raise failure('Inference engine HTTP ' + str(error.code) + ': ' + detail) from error
        return json.loads(raw), raw

    def completion_http(self, path, data, timeout):
        # A queue Abort must also close the upstream inference socket. Merely
        # ignoring the final body leaves a timed-out generation occupying CUDA.
        # Keep the exact non-streaming native response bytes on healthy calls.
        address = urlsplit(self.base)
        if address.scheme != 'http' or address.hostname != '127.0.0.1':
            raise ValueError('Native cancellation requires owned loopback HTTP')
        connection = HTTPConnection(address.hostname, address.port, timeout=timeout)
        done, cancelled = threading.Event(), threading.Event()

        def watch_caller():
            while not done.wait(.05):
                if not self.caller_connected():
                    cancelled.set()
                    upstream = connection.sock
                    if upstream is not None:
                        try:
                            upstream.shutdown(socket.SHUT_RDWR)
                        except OSError:
                            pass
                    return

        watcher = None
        try:
            connection.request('POST', path, body=data,
                headers={'Content-Type': 'application/json', 'Authorization': 'Bearer ' + self.key})
            watcher = threading.Thread(target=watch_caller, daemon=True)
            watcher.start()
            with connection.getresponse() as response:
                raw = response.read()
                if cancelled.is_set():
                    raise ConnectionAbortedError('Caller disconnected; native request cancelled.')
                if response.status >= 400:
                    failure = InferenceTransportError if response.status >= 500 else ValueError
                    raise failure('Inference engine HTTP ' + str(response.status) + ': '
                        + raw[:4096].decode('utf8', errors='replace'))
            return json.loads(raw), raw
        except Exception as failure:
            if cancelled.is_set():
                raise ConnectionAbortedError('Caller disconnected; native request cancelled.') from failure
            raise
        finally:
            done.set()
            connection.close()
            if watcher is not None:
                watcher.join(timeout=1)

    def call(self, messages, schema, transform, folder, stage, limit, excluded_names=()):
        Draft7Validator.check_schema(schema)
        typed_record_view = False
        bounded_bubble_view = False
        if stage == 'auxiliary':
            messages = game_auxiliary_messages(self.profile, messages)
            if self.profile == 'BubbleLine':
                projected = bubble_writing_messages(messages, schema)
                bounded_bubble_view = projected != messages
                messages = projected
            if self.profile == 'CharacterRecord':
                projected = record_writing_messages(messages)
                typed_record_view = projected != messages
                messages = projected
            if self.profile == 'Persona':
                original_transform = transform
                transform = lambda native: persona_generation_schema(original_transform(native))
        if stage == 'prose':
            excluded_names = json.loads(messages[1]['content']).get('previousCandidateNames', [])
        applied, _ = self.http('/apply-template', dict(messages=messages, add_generation_prompt=True,
                              chat_template_kwargs=dict(enable_thinking=False)))
        prompt = applied['prompt']
        checked, raw_tokens = self.http('/tokenize', dict(content=prompt, add_special=False, parse_special=True))
        # The game starts this engine with an explicit context capacity. Keep
        # every caller fact, reserve the entire output and a special-token margin,
        # and reject an overflow rather than inheriting the smaller eval budget.
        budget = self.context_size - limit - 32
        write(folder / (stage + '-token-budget.json'), dict(
            promptTokens=len(checked['tokens']), contextTokens=self.context_size,
            outputReservedTokens=limit, specialTokenMargin=32,
            maximumPromptTokens=budget, truncatedInput=False))
        (folder / (stage + '-tokenize-http.json')).write_bytes(raw_tokens)
        if budget <= 0 or len(checked['tokens']) > budget:
            raise ValueError('R80 input has ' + str(len(checked['tokens'])) + ' tokens; configured safe budget is '
                             + str(budget) + '; no automatic truncation.')
        native, decoder, constraint = decoder_for(schema, transform, stage, excluded_names)
        if stage == 'auxiliary' and self.profile == 'CharacterRecord':
            constraint['typedPublicRecordWritingView'] = typed_record_view
        if stage == 'auxiliary' and self.profile == 'BubbleLine':
            constraint['callerBoundedBubbleWritingView'] = bounded_bubble_view
        write(folder / (stage + '-input.json'), dict(messages=messages, renderedPrompt=prompt,
              schema=schema, nativeSchema=native, constraint=constraint, outputRepair=False))
        body, raw = self.http('/completion', dict(prompt=prompt, **decoder, n_predict=limit, temperature=0.,
            samplers=['temperature'], seed=0, stream=False, cache_prompt=True, id_slot=self.slot, return_tokens=True,
            repeat_penalty=1., presence_penalty=0., frequency_penalty=0., ignore_eos=False,
            response_fields=['content', 'tokens', 'stop', 'stop_type', 'truncated', 'timings']), timeout=300)
        (folder / (stage + '-http.json')).write_bytes(raw)
        self.require_connected()
        value = json.loads(validated_response(body))
        Draft7Validator(schema).validate(value)
        return value


class RequestLanes:
    def __init__(self, engine, slots):
        self.engine, self.slots = engine, slots
        self.pools = [slot_queue.Queue() for _ in range(3)]
        # This pinned native build fills prompt batches in slot order. Put
        # speech first so a large background prefill cannot occupy every batch.
        # llama.cpp 7ba604f1c tools/server/server-context.cpp:2847-2852,3199.
        if slots == 4:
            self.pools[0].put(2)
        self.pools[0].put(slots - 1)
        if slots == 1:
            self.pools[1] = self.pools[2] = self.pools[0]
        else:
            self.pools[1].put(1 if slots >= 3 else 0)
            if slots >= 3:
                self.pools[2].put(0)
            else:
                self.pools[2] = self.pools[1]

    @contextmanager
    def acquire(self, profile, connected):
        pool = self.pools[2 if profile == 'BubbleLine' else 1 if profile in INTERACTIVE_PROFILES else 0]
        while True:
            if not connected():
                raise ConnectionAbortedError('Caller disconnected while awaiting its lane.')
            try:
                slot = pool.get(timeout=.1)
                break
            except slot_queue.Empty:
                pass
        try:
            yield self.engine.for_request(profile, self.slots, connected, slot=slot)
        finally:
            pool.put(slot)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--engine', type=Path, required=True)
    parser.add_argument('--trace-dir', type=Path, required=True)
    parser.add_argument('--request-context-size', type=int)
    options, arguments = parser.parse_known_args()
    def argument(name):
        return arguments[arguments.index(name) + 1]
    if argument('--host') != '127.0.0.1':
        raise ValueError('Only loopback is allowed')
    port, token = int(argument('--port')), argument('--api-key')
    # Keep the queue-facing token separate from the private inference endpoint.
    with socket.socket() as reservation:
        reservation.bind(('127.0.0.1', 0))
        inner_port = reservation.getsockname()[1]
    inner_token = secrets.token_hex(32)
    inner = list(arguments)
    inner[inner.index('--port') + 1] = str(inner_port)
    inner[inner.index('--api-key') + 1] = inner_token
    # All model layers live on this job's GPU. Read/upload weights without
    # retaining a second memory-mapped host copy during the game simulation.
    inner += ['--load-mode', 'none', '--jinja', '--chat-template-kwargs', '{"enable_thinking":false}']
    options.trace_dir.mkdir(parents=True, exist_ok=False)
    slots = int(argument('--parallel'))
    total_context = int(argument('--ctx-size'))
    if slots not in (1, 2, 3, 4) or (options.request_context_size is None and total_context % slots):
        raise ValueError('Exact per-slot context capacity required')
    if slots == 4 and ('--kv-unified' not in arguments or options.request_context_size is None):
        raise ValueError('Four slots require explicit shared KV and request context budget')
    context_size = options.request_context_size or total_context // slots
    if context_size > total_context:
        raise ValueError('Request context exceeds native shared capacity')
    engine = Engine('http://127.0.0.1:' + str(inner_port), inner_token, context_size)
    lanes = RequestLanes(engine, slots)
    runtime_module = SimpleNamespace(canonical=canonical, contract=candidate_task_view.contract,
        selection_messages=game_selection_messages,
        selection_native=game_selection_native,
        binding_errors=single_decision_view.binding_errors,
        prose_messages=game_prose_messages,
        prose_native=scoped_auxiliary_view.prose_native, assemble=game_assemble)
    # The frozen dispatcher imports this reading hook at call time. Its original
    # source remains intact; game-only unscoped inputs use the original broad
    # reading contract rather than pretending to contain a follow-up directive.
    single_decision_view.reading_messages = game_reading_messages
    install_game_candidate_schema()

    class Handler(BaseHTTPRequestHandler):
        def reply(self, status, value):
            body = json.dumps(value, ensure_ascii=False, separators=(',', ':')).encode('utf8')
            self.send_response(status)
            self.send_header('Content-Type', 'application/json; charset=utf-8')
            self.send_header('Content-Length', str(len(body)))
            self.end_headers()
            self.wfile.write(body)

        def authorized(self):
            return (self.headers.get('Host') in ('127.0.0.1', f'127.0.0.1:{port}')
                    and secrets.compare_digest(self.headers.get('Authorization', ''), 'Bearer ' + token)
                    and self.headers.get('Origin') is None)

        def do_GET(self):
            if not self.authorized():
                self.reply(403, dict(error=dict(message='Authenticated loopback only.')))
                return
            if self.path != '/health':
                self.reply(404, dict(error=dict(message='Unknown endpoint.')))
                return
            try:
                engine.http('/health', timeout=1)
                self.reply(200, dict(status='ok'))
            except Exception:
                self.reply(503, dict(error=dict(message='R80 model is loading.')))

        def caller_connected(self):
            try:
                readable, _, _ = select.select([self.connection], [], [], 0)
                return not readable or bool(self.connection.recv(1, socket.MSG_PEEK))
            except (OSError, ValueError):
                return False

        def do_POST(self):
            if not self.authorized():
                self.reply(403, dict(error=dict(message='Authenticated loopback only.')))
                return
            length = int(self.headers.get('Content-Length', '0'))
            if not 0 < length <= 8 * 1024 * 1024 or self.path not in ('/v1/chat/completions', '/completion'):
                self.reply(400, dict(error=dict(message='Unsupported request.')))
                return
            folder = options.trace_dir / ('request-' + str(time.time_ns()))
            folder.mkdir()
            try:
                body = json.loads(self.rfile.read(length))
                profile = body.get('dungeonstory_profile')
                with lanes.acquire(profile, self.caller_connected) as request_engine:
                    request_engine.require_connected()
                    if self.path == '/completion':
                        # Existing finite-index equipment choice uses the same checkpoint.
                        write(folder / 'choice-request.json', body)
                        value, raw = request_engine.http('/completion', dict(body, id_slot=request_engine.slot), timeout=300)
                        (folder / 'choice-http.json').write_bytes(raw)
                        self.reply(200, value)
                        return
                    profile = body.pop('dungeonstory_profile')
                    write(folder / 'original-game-wire.json', body)
                    body = bind_public_subject(profile, bind_caller_choices(profile, body))
                    row = dict(profile=profile, wire=body, requestKey=folder.name,
                               sourceWireSha256='sha256:' + hashlib.sha256(canonical(body)).hexdigest())
                    write(folder / 'request.json', row)
                    table = authored_skill_table(profile, body)
                    fixed_facility = (profile == 'FacilityEvolutionModuleSelection'
                        and fixed_facility_fields(body['response_format']['json_schema']['schema']) is not None)
                    value = (generate_fixed_facility(runtime_module, request_engine, row, folder)
                             if fixed_facility
                             else generate_history_narrative(request_engine, row, folder)
                             if profile == 'EvolutionHistoryLegacyV2'
                             else generate_authored_skills(request_engine, body, folder, table) if table is not None
                             else r80_request.generate(runtime_module, request_engine, row, folder))
                    write(folder / 'result.json', value)
                    self.reply(200, dict(choices=[dict(message=dict(content=json.dumps(value, ensure_ascii=False,
                                                                                                  separators=(',', ':'))))]))
            except (ConnectionAbortedError, ConnectionResetError, BrokenPipeError) as error:
                write(folder / 'failure.json', dict(error=str(error), cancelled=True, outputRepair=False))
            except Exception as error:
                retryable = retryable_failure(error)
                write(folder / 'failure.json', dict(error=str(error), retryable=retryable, outputRepair=False))
                try:
                    self.reply(503 if retryable else 422,
                        dict(error=dict(message=str(error)), requiresInputChange=not retryable))
                except (ConnectionResetError, BrokenPipeError):
                    pass

        def log_message(self, *_):
            pass

    server = ThreadingHTTPServer(('127.0.0.1', port), Handler)
    # This child inherits the parent's existing noninteractive window station
    # and kill-on-close Job Object. CREATE_NO_WINDOW alone is not our isolation.
    engine_log = (options.trace_dir / 'engine.log').open('xb')
    child = subprocess.Popen([str(options.engine), *inner], cwd=options.engine.parent,
                             stdout=engine_log, stderr=subprocess.STDOUT,
                             creationflags=subprocess.CREATE_NO_WINDOW)
    def monitor():
        child.wait()
        server.shutdown()
    threading.Thread(target=monitor, daemon=True).start()
    try:
        server.serve_forever(poll_interval=.2)
    finally:
        server.server_close()
        if child.poll() is None:
            child.terminate()
            child.wait(timeout=15)
        engine_log.close()


if __name__ == '__main__':
    main()
