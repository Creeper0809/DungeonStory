# Reachable R80 inference definitions. See recipe-provenance.json.
from candidate_outcome_view import messages as original_reading
from audit_sft_token_lengths import require
import re, itertools, json
from candidate_task_view import contract, candidate_schema, SKILL
from outcome_native_trial import final_messages
from copy import deepcopy

def excerpts(facts):
    result = []
    for fact in facts.values():
        text = fact['text']
        parts = re.split('(?<=[.!?])\\s+', text)
        starts = [0]
        for match in re.finditer('(?<=[.!?:])\\s+', text):
            starts.append(match.end())
        ends = [m.start() for m in re.finditer('(?<=[.!?])\\s+', text)] + [len(text)]
        for start in starts:
            for end in ends:
                if end <= start:
                    continue
                value = text[start:end]
                if 0 < len(value) <= 600:
                    result.append(value)
    values = list(dict.fromkeys(result))
    require(values and len(values) <= 4096, 'bounded exact public excerpts required')
    require(all((any((value in fact['text'] for fact in facts.values())) for value in values)), 'non-extractive excerpt')
    return values

def reading_messages(row, index, previous):
    m, s = original_reading(row, index, previous)
    s['properties']['latestOwnOutcome']['enum'] = excerpts(contract(row)['facts'])
    m[0]['content'] += ' 인용은 공개 원문에 연속해서 등장하는 짧은 구절 하나여야 한다. 서로 떨어진 구절을 이어 붙이거나 반복하지 않는다.'
    return (m, s)

def selection_messages(row, index, previous, reading):
    m, s = final_messages(row, index, previous, reading)
    m[0]['content'] += ' 이번 호출에서는 근거와 모듈 선택만 확정한다. 이름이나 서사는 다음 호출에서 쓴다. 직접 관련된 최소 근거를 고르고, 상태를 읽었다는 이유만으로 단점 없는 후보에 N0를 자동 인용하지 않는다.'
    for key in ('displayName', 'narrativeFlavor'):
        del s['properties'][key]
        s['required'].remove(key)
    return (m, s)

def selection_native(row, index, previous, reading, native):
    c = contract(row)
    offer = c['offers'][index]
    schema = candidate_schema(row, index)
    ef = schema['properties']['evidenceFactIds']
    pf = schema['properties']['positiveModuleIds']

    def sequences(field):
        options = field['items']['enum']
        minimum = field.get('minItems', 0)
        maximum = field['maxItems']
        return [list(p) for n in range(minimum, maximum + 1) for p in itertools.permutations(options, n)]
    evidence = sequences(ef)
    positive = sequences(pf)
    cost = reading['drawbackModuleIds']
    forbidden = {tuple(sorted(p['positiveModuleIds'])) for p in previous if set(p['drawbackModuleIds']) == set(cost)}
    conflicts = set()
    for identity in cost:
        meaning = c['modules'][identity]['meaning']
        if '함께 선택 금지 이로운 기능=' in meaning:
            conflicts.update(meaning.split('함께 선택 금지 이로운 기능=', 1)[1].split(','))
    choices = []
    for ev in evidence:
        e = set(ev)
        if row['profile'] == 'AcquiredTraitModuleSelection' and ev != sorted(e):
            continue
        if cost and offer.get('qualifiedNegativeEvidence') and (not e.intersection(offer['qualifiedNegativeEvidence'])):
            continue
        if offer.get('gameplayEvidence') and (not e.intersection(offer['gameplayEvidence'])):
            continue
        for pos in positive:
            if row['profile'] == SKILL and ({'guard', 'protect'} <= set(pos) or tuple(sorted(pos)) in forbidden):
                continue
            if set(pos).intersection(conflicts):
                continue
            if any((c['modules'][k]['requiredEvidence'] and (not e.intersection(c['modules'][k]['requiredEvidence'])) for k in pos + cost)):
                continue
            choices.append(dict(selectionId=offer['selectionId'], evidenceFactIds=ev, positiveModuleIds=pos, drawbackModuleIds=cost))
    require(choices and len(choices) <= 32768, 'bounded public legal choice space unavailable; no fallback')
    return dict(type='object', enum=choices)

def prose_messages(row, index, selection):
    c = contract(row)
    offer = c['offers'][index]
    full = candidate_schema(row, index)
    keys = selection['positiveModuleIds'] + selection['drawbackModuleIds']
    target = None
    if row['profile'] == SKILL:
        axes = dict((p.split('=', 1) for p in offer['mechanics'].split('; ')))
        target = axes['target']
    from candidate_task_view import stat_term_gloss
    modules = {key: deepcopy(c['modules'][key]) for key in keys}
    doc = dict(task='NARRATE_ACTUAL_SELECTED_EFFECTS_ONLY', profile=row['profile'], characterName=c['characterName'], selection=deepcopy(selection), futureTarget=target, mechanics=offer.get('mechanics'), selectedModules=modules, statTermGlossary=stat_term_gloss(modules), citedOriginalFacts={k: deepcopy(c['facts'][k]) for k in selection['evidenceFactIds']})
    schema = dict(type='object', properties={k: deepcopy(full['properties'][k]) for k in ('displayName', 'narrativeFlavor')}, required=['displayName', 'narrativeFlavor'], additionalProperties=False)
    system = '이미 네가 고른 선택의 한국어 이름과 앞으로의 효과를 설명한다. 선택은 바꾸지 않는다. selectedModules의 모든 이로운 효과·내재된 부담·별도 단점을 빠뜨리거나 반대로 설명하지 않는다. 선택하지 않은 효과나 조건문(단점이 있다면 등)은 쓰지 않는다. 미래 효과를 이미 발생한 사건으로 만들지 않는다. Self는 자신, Ally는 아군, Enemy는 적이다. 실제 수신 대상과 증가·감소 방향을 정확히 쓴다. 기술은 인물 이름을 주어로 하는 완결된 한 문장·48자 이내다. 그 밖의 서사는180자 이내다. 서론과 과거 요약을 생략하고 효과 자체를 간결하게 적는다. JSON 객체만 출력한다.'
    return ([dict(role='system', content=system), dict(role='user', content=json.dumps(doc, ensure_ascii=False, separators=(',', ':')))], schema)

def prose_native(row, native):
    value = deepcopy(native)
    if row['profile'] == SKILL:
        value['properties']['narrativeFlavor']['pattern'] = '^' + re.escape(contract(row)['characterName']) + '(은|는) [^.!?\\r\\n]*\\.$'
    return value
