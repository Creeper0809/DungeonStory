# Reachable R80 inference definitions. See recipe-provenance.json.
from live_module_selection_contract import inspect_wire, validate_response
from audit_sft_token_lengths import canonical, require
from copy import deepcopy
from jsonschema import Draft7Validator
from public_task_projection import project_contract
import re, json
from formula_reviewer.mechanical_summary import EQUIPMENT_STATS
SKILL = 'CharacterSkillModuleSelection'
RECIPIENTS = {'Self': '자신', 'Ally': '아군', 'Enemy': '적'}
OWNERS = {SKILL: '해당 기술', 'AcquiredTraitModuleSelection': '해당 캐릭터의 특성', 'EquipmentEvolutionModuleSelection': '해당 장비', 'FacilityEvolutionModuleSelection': '해당 시설'}
VIEW = 'ONE_PUBLIC_OFFER_DEDUPLICATED_TRANSPORT_V2'

def contract(row):
    return inspect_wire(canonical(row['wire']), row['sourceWireSha256'], row['profile'])

def candidate_schema(row, index):
    c = contract(row)
    require(type(index) is int and 0 <= index < len(c['offers']), 'candidate index outside original request')
    schema = row['wire']['response_format']['json_schema']['schema']
    if row['profile'] == SKILL:
        items = schema['properties']['candidates']['items']
        require(isinstance(items, list) and len(items) == len(c['offers']), 'original positional schema required')
        return deepcopy(items[index])
    require(index == 0, 'single-output profile index must be zero')
    return deepcopy(schema)

def stat_term_gloss(modules):
    """Reuse display labels ONLY, never the older allocation-burden formulas."""
    terms = {term for m in modules.values() for term in re.findall('combat\\.[a-z_]+', m['meaning'])}
    result = {}
    for term in sorted(terms):
        require(term in EQUIPMENT_STATS, 'unmapped public stat term: ' + term)
        label = EQUIPMENT_STATS[term]
        require(label[-1:] in ('이', '가') and len(label) > 1, 'unexpected source display-label form')
        result[term] = label[:-1]
    return result

def nonduplicated_headers(headers, c):
    """Remove transport copies only after exact equality with parsed authority."""
    result = []
    all_evidence = {k for offer in c['offers'] for k in offer['evidence']}
    for line in headers:
        if line.startswith(('module=', '- moduleId=')):
            match = re.fullmatch('module=([^ ]+) \\| polarity=(Positive|Drawback) \\| (.+)', line)
            if match is None:
                match = re.fullmatch('- moduleId=([^;]+); polarity=(Positive|Drawback); meaning=(.+)', line)
            require(match is not None, 'unknown module transport header')
            key, polarity, meaning = match.groups()
            meaning = meaning.rsplit('; domainEvidenceGroup=', 1)[0]
            require(key in c['modules'] and c['modules'][key]['polarity'] == polarity.lower() and (c['modules'][key]['meaning'] == meaning), 'module header is not an exact parsed duplicate')
            continue
        if line.startswith('evidence='):
            match = re.fullmatch('evidence=([^\\s|]+)(?: \\| authoritativeFact=(.+))?', line)
            require(match is not None, 'unknown evidence transport header')
            key, text = match.groups()
            require(key in all_evidence and key in c['facts'], 'evidence transport identity is not preserved')
            require(text is None or c['facts'][key]['text'] == text, 'evidence header is not an exact parsed duplicate')
            continue
        result.append(line)
    return result

def candidate_messages(row, index, previous):
    c = contract(row)
    schema = candidate_schema(row, index)
    require(isinstance(previous, list) and len(previous) == index, 'exact preceding candidate count required')
    history = []
    for i, value in enumerate(previous):
        Draft7Validator(candidate_schema(row, i)).validate(value)
        require(value['selectionId'] == c['offers'][i]['selectionId'], 'prior candidate identity mismatch')
        history.append({k: deepcopy(value[k]) for k in ('selectionId', 'positiveModuleIds', 'drawbackModuleIds', 'displayName')})
    headers = [x for x in row['wire']['messages'][1]['content'].splitlines() if x.strip() and (not x.lstrip().startswith('{'))]
    original = project_contract(c, headers)
    offer = deepcopy(original['offers'][index])
    allowed = list(dict.fromkeys(offer['positive'] + offer['drawback']))
    modules = {key: deepcopy(original['modules'][key]) for key in allowed}
    recipient = None
    if row['profile'] == SKILL:
        axes = dict((part.split('=', 1) for part in offer['mechanics'].split('; ')))
        require(axes['target'] in RECIPIENTS, 'unknown public future target')
        recipient = dict(target=axes['target'], display=RECIPIENTS[axes['target']], targeting=axes['targeting'], area=axes['area'])
    doc = dict(view=VIEW, profile=row['profile'], originalRequestRef=c['requestRef'], originalCandidateCount=len(c['offers']), currentCandidateIndex=index, definitionOwner=OWNERS[row['profile']], characterName=c['characterName'], remainingPlainHeaders=nonduplicated_headers(original['originalPlainHeaders'], c), background=deepcopy(original['background']), previousCandidateChoices=history, currentOffer=offer, futureRecipient=recipient, modules=modules, statTermGlossary=stat_term_gloss(modules), facts=deepcopy(original['facts']))
    require(doc['facts'] == c['facts'] and all((doc['modules'][k] == c['modules'][k] for k in allowed)), 'public fact or module meaning changed')
    require(all((offer[k] == v for k, v in c['offers'][index].items())), 'original offered choices or mechanics changed')
    instruction = '\n이번 호출은 원래 요청을 후보별로 나눈 한 단계다. 현재 currentOffer의 후보 객체 하나만 작성한다. 원래 요청 전체의 의미·인용·중복 금지 규칙은 유지된다. 이전 선택과 같은 이로운/해로운 모듈 조합을 반복하지 않는다. 다른 후보의 대상이 아니라 현재 futureRecipient를 따른다. 과거 사실의 당사자를 임의로 바꾸지 않는다. 현재 후보에 허용된 효과만 선택하고, statTermGlossary의 표시명으로 설명하되 수치나 효과를 만들지 않는다. 이로운 모듈 설명의 추가 부담 없음은 그 모듈 자체의 효과에 딸린 부담을 말한다. 별도로 선택한 해로운 모듈이 없다는 뜻으로 옮기지 않는다. 확인된 후속 회복과 남은 부담은 원문 facts로 판단한다. 출력 형식은 현재 후보 하나의 response schema이며 완성된 원래 요청은 호출자가 원순서로 조립·검증한다.\n'
    return ([dict(role='system', content=row['wire']['messages'][0]['content'] + instruction), dict(role='user', content=json.dumps(doc, ensure_ascii=False, separators=(',', ':')))], schema)

def assemble(row, candidates):
    c = contract(row)
    require(isinstance(candidates, list) and len(candidates) == len(c['offers']), 'complete original candidate set required')
    for i, value in enumerate(candidates):
        Draft7Validator(candidate_schema(row, i)).validate(value)
    result = dict(requestRef=c['requestRef'], candidates=deepcopy(candidates)) if row['profile'] == SKILL else deepcopy(candidates[0])
    validate_response(canonical(row['wire']), row['sourceWireSha256'], row['profile'], result)
    return result
