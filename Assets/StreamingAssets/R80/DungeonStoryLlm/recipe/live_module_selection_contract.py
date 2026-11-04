# Reachable R80 inference definitions. See recipe-provenance.json.
from __future__ import annotations
from gameplay_module_selection_package import require, byte_hash
from strict_json import loads
from jsonschema import Draft7Validator
from decimal import InvalidOperation, Decimal
import re
import unicodedata
PROFILES = ('CharacterSkillModuleSelection', 'AcquiredTraitModuleSelection', 'EquipmentEvolutionModuleSelection', 'FacilityEvolutionModuleSelection')
FIELDS = {'selectionId', 'positiveModuleIds', 'drawbackModuleIds', 'evidenceFactIds', 'displayName', 'narrativeFlavor'}
SKILL_TRANSPORT_H_COLUMNS = {'skill-request-refs-v1': ['sourceKind', 'selectable', 'outcomeType', 'subject_S', 'days', 'roles_R', 'cause', 'fact_T'], 'skill-request-refs-v2': ['sourceKind', 'selectable', 'outcomeType', 'subject_S', 'days', 'roles_R', 'cause', 'fact_T', 'experience']}
FACILITY_FACT_ID = re.compile('facility-fact:[0-9a-f]{64}\\Z')
FACILITY_USAGE_FACT_FIELDS = {'factId', 'sourceKind', 'text', 'event'}
FACILITY_USAGE_EVENT_FIELDS = {'eventId', 'actorId', 'targetId', 'outcomeId', 'generation', 'repeatCount', 'narrativeContext'}
FACILITY_USAGE_CONTEXT_FIELDS = {'eventInstanceId', 'chainId', 'locationId', 'locationDisplayName', 'actorId', 'actorDisplayName', 'counterpartyId', 'counterpartyDisplayName', 'usedObjectId', 'usedObjectDisplayName', 'resultDetail', 'occurredDay', 'sequence'}

def _strings(value, label, *, empty=False):
    require(isinstance(value, list) and (empty or bool(value)), label + ': list required')
    require(all((isinstance(x, str) and x and (x.strip() == x) for x in value)), label + ': text required')
    require(len(set(value)) == len(value), label + ': duplicate')
    return value

def _at(values, index):
    require(isinstance(values, list) and type(index) is int and (0 <= index < len(values)), 'invalid request-local table reference')
    return values[index]

def _table(value):
    if isinstance(value, list):
        require(all((isinstance(row, dict) for row in value)), 'public table needs object rows')
        return value
    require(isinstance(value, dict) and set(value) == {'columns', 'rows'}, 'unsupported public table')
    columns = _strings(value['columns'], 'table columns')
    require(isinstance(value['rows'], list) and all((isinstance(row, list) and len(row) == len(columns) for row in value['rows'])), 'public table row width mismatch')
    return [dict(zip(columns, row)) for row in value['rows']]

def _expand(document):
    if document.get('encoding') != 'repeated-public-text-v1':
        return document
    strings = document['strings']
    require(isinstance(strings, list) and all((isinstance(s, str) for s in strings)), 'invalid string table')

    def expand(value):
        if isinstance(value, list):
            return [expand(item) for item in value]
        if not isinstance(value, dict):
            return value
        if set(value) == {'$s'}:
            return _at(strings, value['$s'])
        if set(value) == {'$text'}:
            parts = expand(value['$text'])
            require(isinstance(parts, list) and all((isinstance(p, str) for p in parts)), 'invalid text fragments')
            return ''.join(parts)
        if set(value) == {'columns', 'columnValues', 'rowCount'}:
            count = value['rowCount']
            require(type(count) is int and 0 <= count <= 10000, 'invalid repeated row count')
            _strings(value['columns'], 'repeated columns')
            require(len(value['columns']) == len(value['columnValues']), 'repeated column mismatch')
            columns = [[expand(c['$repeat'])] * count if isinstance(c, dict) and set(c) == {'$repeat'} else expand(c) for c in value['columnValues']]
            require(all((isinstance(c, list) and len(c) == count for c in columns)), 'repeated row mismatch')
            return {'columns': value['columns'], 'rows': [[c[i] for c in columns] for i in range(count)]}
        return {key: expand(item) for key, item in value.items()}
    return expand(document['data'])

def _header(lines, name, *, optional=False):
    values = [line[len(name) + 1:] for line in lines if line.startswith(name + '=')]
    require(len(values) == 1 or (optional and (not values)), 'missing/duplicate header: ' + name)
    return values[0] if values else None

def _compact(document):
    transport = document.get('transport')
    require(transport in SKILL_TRANSPORT_H_COLUMNS, 'unsupported skill transport')
    require(document.get('noOutcomes') is False, 'skill request has no outcomes')
    expected = {'C': ['selectionId', 'X', 'positiveModuleLimit', 'positive_M', 'drawback_M', 'allowed_A', 'emptyDrawbackMarker'], 'X': ['mechanics', 'context', 'primary_P'], 'O': ['E', 'H', 'G'], 'H': SKILL_TRANSPORT_H_COLUMNS[transport]}
    tables = {}
    for name, columns in expected.items():
        require(document.get(name + '_columns') == columns, 'unknown compact columns: ' + name)
        tables[name] = _table({'columns': columns, 'rows': document[name]})
    _strings(document['E'], 'evidence identities')
    facts, modules, offers = ({}, {}, [])
    for item in tables['O']:
        identity = _at(document['E'], item['E'])
        require(identity not in facts, 'duplicate compact evidence')
        detail = dict(_at(tables['H'], item['H']))
        require(detail['selectable'] in ('true', 'false'), 'invalid evidence selectable value')
        detail.update(text=_at(document['T'], detail.pop('fact_T')), subjectId=_at(document['S'], detail.pop('subject_S')), roles=_at(document['R'], detail.pop('roles_R')))
        detail['selectable'] = detail['selectable'] == 'true'
        if transport == 'skill-request-refs-v2':
            experience = detail['experience']
            require(experience is None or (isinstance(experience, str) and bool(experience.strip())), 'invalid evidence experience annotation')
        metrics = []
        for pair in _at(document['G'], item['G']):
            require(isinstance(pair, list) and len(pair) == 2, 'invalid metric pair')
            metric = _at(document['D_metricId_unit'], pair[0])
            require(isinstance(metric, list) and len(metric) == 2 and all((isinstance(x, str) and x for x in metric)), 'invalid metric definition')
            value = _at(document['V'], pair[1])
            require(isinstance(value, str) and 0 < len(value) <= 100, 'invalid metric value')
            try:
                finite = Decimal(value).is_finite()
            except InvalidOperation:
                finite = False
            require(finite, 'nonfinite/invalid metric value')
            metrics.append(dict(metricId=metric[0], unit=metric[1], value=value))
        require(len({m['metricId'] for m in metrics}) == len(metrics), 'duplicate metric identity')
        facts[identity] = dict(detail, metrics=metrics)
    for candidate in tables['C']:
        require(re.fullmatch(r'1\.\.[1-3]', candidate['positiveModuleLimit']) is not None, 'unsupported skill cardinality')
        context = _at(tables['X'], candidate['X'])
        offer = dict(selectionId=candidate['selectionId'], positive=[], drawback=[], evidence=[_at(document['E'], i) for i in _at(document['A'], candidate['allowed_A'])], context=context['context'], mechanics=context['mechanics'], primaryRule=_at(document['P'], context['primary_P']), maximumPositive=int(candidate['positiveModuleLimit'][-1]), maximumDrawback=1, maximumEvidence=2)
        for column, polarity in (('positive_M', 'positive'), ('drawback_M', 'drawback')):
            require(isinstance(candidate[column], list), 'module references must be a list')
            for index in candidate[column]:
                module = _at(document['M'], index)
                require(isinstance(module, list) and len(module) == 2 and all((isinstance(x, str) and x for x in module)), 'invalid compact module')
                identity, meaning = module
                definition = dict(polarity=polarity, meaning=meaning, requiredEvidence=[])
                require(identity not in modules or modules[identity] == definition, 'conflicting module definition')
                modules[identity] = definition
                offer[polarity].append(identity)
        require(type(candidate['emptyDrawbackMarker']) is bool and candidate['emptyDrawbackMarker'] == (not offer['drawback']), 'drawback marker mismatch')
        offers.append(offer)
    return (offers, modules, facts)

def _facility_usage_facts(documents, offered, facts):
    usage_documents = [document for document in documents if 'facilityUsageFactsVersion' in document or 'facilityUsageFacts' in document]
    require(len(usage_documents) <= 1, 'duplicate facility usage facts document')
    if not usage_documents:
        return
    document = usage_documents[0]
    require(set(document) == {'facilityUsageFactsVersion', 'facilityUsageFacts'} and type(document['facilityUsageFactsVersion']) is int and (document['facilityUsageFactsVersion'] == 1), 'invalid facility usage facts document')
    rows = document['facilityUsageFacts']
    require(isinstance(rows, list) and bool(rows), 'facility usage facts must be a nonempty list')
    identities = set()
    for row in rows:
        require(isinstance(row, dict) and set(row) == FACILITY_USAGE_FACT_FIELDS, 'invalid facility usage fact fields')
        identity = row['factId']
        require(isinstance(identity, str) and FACILITY_FACT_ID.fullmatch(identity), 'invalid facility usage fact identity')
        require(identity in offered, 'facility usage fact was not offered')
        require(identity not in identities and identity not in facts, 'duplicate facility usage fact')
        identities.add(identity)
        require(row['sourceKind'] == 'facility-usage', 'invalid facility usage fact source kind')
        require(isinstance(row['text'], str) and bool(row['text'].strip()), 'facility usage fact text is empty')
        event = row['event']
        require(isinstance(event, dict) and set(event) == FACILITY_USAGE_EVENT_FIELDS, 'invalid facility usage event fields')
        require(all((isinstance(event[key], str) for key in ('eventId', 'actorId', 'targetId', 'outcomeId'))) and bool(event['eventId'].strip()) and bool(event['targetId'].strip()), 'invalid facility usage event identities')
        require(type(event['generation']) is int and event['generation'] >= 0 and (type(event['repeatCount']) is int) and (event['repeatCount'] >= 1), 'invalid facility usage event generation/repeat count')
        context = event['narrativeContext']
        require(isinstance(context, dict) and set(context) == FACILITY_USAGE_CONTEXT_FIELDS, 'invalid facility usage narrative context fields')
        text_fields = FACILITY_USAGE_CONTEXT_FIELDS - {'occurredDay', 'sequence'}
        require(all((isinstance(context[key], str) for key in text_fields)), 'invalid facility usage narrative context text')
        require(all((bool(context[key].strip()) for key in ('eventInstanceId', 'locationDisplayName', 'actorDisplayName', 'resultDetail'))), 'facility usage narrative context required text is empty')
        require(type(context['occurredDay']) is int and context['occurredDay'] >= 0 and (type(context['sequence']) is int) and (context['sequence'] >= 0), 'invalid facility usage narrative context time/sequence')
        target = event['targetId']
        require(context['chainId'] == target and context['locationId'] == target and (context['usedObjectId'] == target), 'facility usage narrative context target mismatch')
        require(context['actorId'] == event['actorId'] if event['actorId'] else bool(context['actorId'].strip()), 'facility usage narrative context actor mismatch')
        facts[identity] = dict(row, selectable=True)
    offered_usage = {identity for identity in offered if FACILITY_FACT_ID.fullmatch(identity)}
    require(identities == offered_usage, 'facility usage facts do not cover offered facility evidence')

def _single(lines, documents, profile):
    require(_header(lines, 'profile') == profile, 'public profile mismatch')
    selection = _header(lines, 'selectionId')
    groups = {}
    for document in documents:
        for key, value in document.items():
            if re.fullmatch('DG\\d+', key):
                require(key not in groups and isinstance(value, str), 'duplicate/invalid domain group')
                groups[key] = value.split(',')
    modules, facts, facility_evidence = ({}, {}, [])
    for line in lines:
        match = re.fullmatch('- moduleId=([^;]+); polarity=(Positive|Drawback); meaning=(.+)', line)
        if line.startswith('module='):
            match = re.fullmatch('module=([^ ]+) \\| polarity=(Positive|Drawback) \\| (.+)', line)
        if line.startswith(('- moduleId=', 'module=')):
            require(match is not None, 'module polarity/meaning not explicitly offered')
            identity, polarity, meaning = match.groups()
            required = []
            if '; domainEvidenceGroup=' in meaning:
                meaning, group = meaning.rsplit('; domainEvidenceGroup=', 1)
                require(group in groups, 'module domain evidence group missing')
                required = _strings(groups[group], 'module domain evidence')
            require(identity not in modules, 'duplicate module offer')
            modules[identity] = dict(polarity=polarity.lower(), meaning=meaning, requiredEvidence=required)
        if line.startswith('evidence='):
            if profile == PROFILES[3]:
                match = re.fullmatch('evidence=([^\\s|]+)(?: \\| authoritativeFact=(.+))?', line)
                require(match is not None, 'malformed facility evidence offer')
                identity, text = match.groups()
                require(identity not in facility_evidence, 'duplicate facility evidence offer')
                facility_evidence.append(identity)
                if text is not None:
                    require(bool(text.strip()), 'empty facility authoritative fact')
                    require(identity not in facts, 'duplicate facility evidence')
                    facts[identity] = dict(text=text, selectable=True, sourceKind='exact')
            else:
                match = re.fullmatch('evidence=([^ ]+) \\| authoritativeFact=(.+)', line)
                if match:
                    require(match[1] not in facts, 'duplicate facility evidence')
                    facts[match[1]] = dict(text=match[2], selectable=True, sourceKind='exact')
    public = [doc for doc in documents if 'publicFacts' in doc]
    require(len(public) <= 1, 'ambiguous public fact document')
    if public:
        for fact in _table(public[0]['publicFacts']):
            identity = fact.get('factId')
            require(isinstance(identity, str) and identity and isinstance(fact.get('text'), str), 'invalid public fact')
            if profile == PROFILES[2]:
                refs = re.findall('\\[evidence=([^\\]\\s]+)\\]', fact['text'])
                require(len(refs) == 1, 'equipment fact lacks an unambiguous evidence identity')
                identity = refs[0]
            require(identity not in facts, 'duplicate public evidence')
            facts[identity] = dict(fact, selectable=True)
    offered = _header(lines, 'evidenceFactIds', optional=True)
    evidence = facility_evidence if profile == PROFILES[3] else offered.split(',') if offered else list(facts)
    require(offered is not None or profile == PROFILES[3], 'missing explicit evidence offer')
    if profile == PROFILES[3]:
        _facility_usage_facts(documents, evidence, facts)
    gameplay = _header(lines, 'gameplayEvidenceFactIds', optional=True)
    gameplay_evidence = [] if gameplay is None else _strings(gameplay.split(','), 'gameplay evidence')
    maximum_drawback = 0
    if profile == PROFILES[3] or _header(lines, 'maximumDrawbackModules', optional=True) is not None:
        drawback_limit = _header(lines, 'maximumDrawbackModules')
        require(_header(lines, 'maximumPositiveModules') == '1' and drawback_limit in ('0', '1'), 'unsupported single-profile limits')
        maximum_drawback = int(drawback_limit)
        has_drawback = any((value['polarity'] == 'drawback' for value in modules.values()))
        require(maximum_drawback == int(has_drawback), 'facility drawback limit/offer mismatch')
        require(any((value['polarity'] == 'positive' for value in modules.values())), 'facility positive module offer missing')
    offer = dict(selectionId=selection, positive=[key for key, value in modules.items() if value['polarity'] == 'positive'], drawback=[key for key, value in modules.items() if value['polarity'] == 'drawback'], evidence=evidence, gameplayEvidence=gameplay_evidence, maximumPositive=1, maximumDrawback=maximum_drawback, maximumEvidence=len(evidence))
    return ([offer], modules, facts)

def _skill_narrative_evidence(lines, document, facts):
    """Consume explicit public joins only; never infer old missing E-to-F joins."""
    raw = _header(lines, 'narrativeEvidence', optional=True)
    if raw is None:
        return
    mapping = loads(raw)
    missing = set(document['E']) - set(facts)
    require(isinstance(mapping, dict) and 0 < len(mapping) <= 24 and (set(mapping) == missing), 'narrative evidence coverage differs from offered E identities')
    identities = set()
    for reference, row in mapping.items():
        require(isinstance(row, dict) and set(row) == {'sourceKind', 'fact', 'event', 'participants'} and (row['sourceKind'] == 'progression-aggregate'), 'invalid narrative evidence source kind/fields')
        fact, event, participants = (row['fact'], row['event'], row['participants'])
        required = {'factId', 'domain', 'text'}
        optional = {'subjectId', 'outcome', 'lastDay', 'count', 'milestoneCount', 'totalValueDecimal'}
        require(isinstance(fact, dict) and required <= set(fact) <= required | optional, 'invalid narrative public fact fields')
        require(all((isinstance(fact[key], str) and fact[key] for key in required)) and re.fullmatch('public-fact:sha256:[0-9a-f]{64}', fact['factId']) and (fact['factId'] not in identities), 'invalid/duplicate narrative public fact identity')
        identities.add(fact['factId'])
        require(not fact['domain'].startswith('GameplayOutcome:'), 'exact outcome cannot masquerade as an aggregate')
        for key in ('subjectId', 'outcome', 'totalValueDecimal'):
            require(key not in fact or isinstance(fact[key], str), 'invalid narrative text: ' + key)
        for key in ('lastDay', 'count', 'milestoneCount'):
            require(key not in fact or (type(fact[key]) is int and fact[key] >= 0), 'invalid narrative count/day')
        if 'totalValueDecimal' in fact:
            try:
                finite = Decimal(fact['totalValueDecimal']).is_finite()
            except InvalidOperation:
                finite = False
            require(finite, 'invalid narrative total value')
        require(isinstance(event, dict) and set(event) == {'actorId', 'targetId', 'count', 'day', 'eventType', 'evidenceId', 'factId', 'generation', 'outcome'}, 'invalid narrative public event fields')
        require(all((isinstance(event[key], str) for key in ('actorId', 'targetId', 'eventType', 'evidenceId', 'factId', 'outcome'))) and event['eventType'] and (event['factId'] == fact['factId']), 'narrative fact/event identity mismatch')
        require(type(event['count']) is int and event['count'] > 0 and all((event[key] is None or (type(event[key]) is int and event[key] >= 0) for key in ('day', 'generation'))), 'invalid narrative event time/count')
        require(event['count'] == fact.get('count') and event['outcome'] == fact.get('outcome', '') and (event['day'] == fact.get('lastDay')), 'narrative fact/event values disagree')
        require(isinstance(participants, list) and all((isinstance(p, dict) and set(p) == {'entityId', 'displayName', 'kind'} and all((isinstance(v, str) and v for v in p.values())) for p in participants)), 'invalid narrative participants')
        actor_ids = {event[key] for key in ('actorId', 'targetId') if event[key]}
        require(len(participants) == len(actor_ids) and {p['entityId'] for p in participants} == actor_ids, 'narrative participants do not match event')
        facts[reference] = dict(sourceKind='progression-aggregate', selectable=True, text=fact['text'], outcomeType=event['eventType'], actorId=event['actorId'], subjectId=fact.get('subjectId', ''), targetId=event['targetId'], metrics=[], publicFact=fact, publicEvent=event, participants=participants)

def inspect_wire(raw: bytes, expected_sha256: str, profile: str) -> dict:
    """Require an externally pinned wire. Return no rewritten training messages."""
    require(isinstance(raw, bytes) and 0 < len(raw) <= 1000000, 'wire size/type invalid')
    require(byte_hash(raw) == expected_sha256, 'wire hash mismatch')
    require(profile in PROFILES, 'unsupported selection profile')
    wire = loads(raw)
    messages = wire.get('messages')
    require(isinstance(messages, list) and len(messages) == 2 and ([m.get('role') for m in messages] == ['system', 'user']) and all((isinstance(m.get('content'), str) for m in messages)), 'invalid public messages')
    schema = wire.get('response_format', {}).get('json_schema', {}).get('schema')
    require(isinstance(schema, dict), 'missing response schema')
    checked = set()

    def no_refs(value, ancestors=()):
        if isinstance(value, dict):
            require('$id' not in value, 'response schema base URI is unsupported')
            if '$ref' in value:
                ref = value['$ref']
                require(isinstance(ref, str) and ref.startswith('#/') and (ref not in ancestors), 'external/cyclic response schema reference')
                if ref not in checked:
                    target = schema
                    for key in ref[2:].split('/'):
                        key = key.replace('~1', '/').replace('~0', '~')
                        require(isinstance(target, dict) and key in target, 'missing local schema reference')
                        target = target[key]
                    no_refs(target, ancestors + (ref,))
                    checked.add(ref)
            for child in value.values():
                no_refs(child, ancestors)
        elif isinstance(value, list):
            for child in value:
                no_refs(child, ancestors)
    no_refs(schema)
    Draft7Validator.check_schema(schema)
    lines = [line.strip() for line in messages[1]['content'].splitlines() if line.strip()]
    documents = [_expand(loads(line)) for line in lines if line.startswith('{')]
    require(all((isinstance(doc, dict) for doc in documents)), 'public JSON must be an object')
    usage_documents = [document for document in documents if 'facilityUsageFactsVersion' in document or 'facilityUsageFacts' in document]
    require(profile == PROFILES[3] or not usage_documents, 'facility usage facts require the facility profile')
    compact = [doc for doc in documents if 'C_columns' in doc]
    if profile == PROFILES[0]:
        require(len(compact) == 1, 'current skill compact document required')
        offers, modules, facts = _compact(compact[0])
        _skill_narrative_evidence(lines, compact[0], facts)
        require(1 <= len(offers) <= 3, 'current game skill requires one to three candidates')
        name = _header(lines, 'character')
    else:
        require(not compact, 'skill transport on a different profile')
        offers, modules, facts = _single(lines, documents, profile)
        name = None
    _strings([o['selectionId'] for o in offers], 'selection identities')
    require(modules, 'missing public modules')
    for offer in offers:
        negative_header = _header(lines, 'qualifiedNegativeEvidenceFactIds', optional=True)
        if negative_header is not None:
            negative_ids = _strings(negative_header.split(','), 'qualified negative evidence')
            offer['qualifiedNegativeEvidence'] = [key for key in negative_ids if key in offer['evidence']]
            require(all((key in facts and facts[key].get('selectable') is True for key in negative_ids)), 'qualified negative evidence must have selectable public facts')
            require(not offer['drawback'] or bool(offer['qualifiedNegativeEvidence']), 'drawback offer lacks qualified negative evidence')
        for field in ('positive', 'drawback', 'evidence'):
            _strings(offer[field], field, empty=field == 'drawback')
        require(not set(offer['positive']) & set(offer['drawback']), 'mixed module polarities')
        offer['unresolvedEvidence'] = sorted(set(offer['evidence']) - set(facts))
        offer['groundableEvidence'] = [key for key in offer['evidence'] if key in facts and facts[key].get('selectable') is True]
        require(offer['groundableEvidence'], 'no publicly groundable evidence')
        require(set(offer.get('gameplayEvidence', [])) <= set(offer['evidence']), 'gameplay evidence not offered')
        for identity in offer['positive'] + offer['drawback']:
            require(set(modules[identity]['requiredEvidence']) <= set(offer['evidence']), 'domain evidence not offered')
    reference_documents = [doc for doc in documents if 'requestRef' in doc]
    request_refs = {doc['requestRef'] for doc in reference_documents}
    require(len(request_refs) <= 1, 'conflicting public request references')
    request_ref = next(iter(request_refs), None)
    if profile == PROFILES[0]:
        require(isinstance(request_ref, str) and re.fullmatch('[0-9a-f]{64}', request_ref), 'missing request reference')
    if profile == PROFILES[1]:
        offer = offers[0]
        properties = schema.get('properties', {})
        required = schema.get('required', [])
        evidence_enum = properties.get('evidenceFactIds', {}).get('items', {}).get('enum')
        require(evidence_enum == offer['evidence'], 'trait schema evidence mismatch')
        reference_mode = bool(offer['gameplayEvidence'])
        if reference_mode:
            require(len(reference_documents) == 1 and isinstance(request_ref, str) and re.fullmatch('[0-9a-f]{64}', request_ref), 'missing request reference')
            reference_document = reference_documents[0]
            references = reference_document.get('evidence')
            expected_references = [f'[[trait-e:{index}]]' for index in range(len(offer['evidence']))]
            require(set(reference_document) == {'requestRef', 'evidence'} and isinstance(references, dict) and (list(references) == expected_references) and (offer['evidence'] == expected_references), 'trait evidence reference table mismatch')
            _strings(list(references.values()), 'trait evidence reference values')
            require('requestRef' in required and properties.get('requestRef', {}).get('enum') == [request_ref], 'trait reference schema mismatch')
        else:
            require(not reference_documents and request_ref is None, 'trait full-ID transport has request reference')
            require('requestRef' not in required and 'requestRef' not in properties, 'trait full-ID schema has request reference')
            require(not any((identity.startswith('[[trait-e:') for identity in offer['evidence'])), 'trait full-ID transport contains evidence reference')
            require(all((modules[identity]['requiredEvidence'] for identity in offer['positive'] + offer['drawback'])), 'trait full-ID module domain evidence missing')
    elif request_ref:
        require(schema.get('properties', {}).get('requestRef', {}).get('enum') == [request_ref], 'schema request reference mismatch')
    return dict(profile=profile, wireSha256=expected_sha256, responseSchema=schema, offers=offers, modules=modules, facts=facts, publicDocuments=documents, characterName=name, requestRef=request_ref, status='PUBLIC_CONTRACT_ONLY', humanApprovalClaimed=False, trainingEligible=False)

def has_unbound_authored_number(value, authoritative_name):
    """Allow the exact caller identity, leaving every other digit/percent guard intact."""
    if '%' in value or '％' in value:
        return True
    allow = (isinstance(authoritative_name, str) and authoritative_name.strip()
             and any(char.isalpha() for char in authoritative_name)
             and '%' not in authoritative_name and '％' not in authoritative_name)
    index = 0
    while index < len(value):
        end = index + len(authoritative_name) if allow else index
        before = unicodedata.category(value[index - 1]) if index else ''
        after = unicodedata.category(value[end]) if end < len(value) else ''
        if (allow and value.startswith(authoritative_name, index)
                and not (before.startswith('L') or before == 'Nd')
                and not after.startswith('N')):
            index = end
            continue
        if '0' <= value[index] <= '9' or '０' <= value[index] <= '９':
            return True
        index += 1
    return False


def validate_response(raw: bytes, expected_sha256: str, profile: str, response: dict, *, authoritative_name=None) -> dict:
    contract = inspect_wire(raw, expected_sha256, profile)
    errors = list(Draft7Validator(contract['responseSchema']).iter_errors(response))
    require(not errors, 'response schema mismatch: ' + '; '.join((e.message[:160] for e in errors[:3])))
    skill = profile == PROFILES[0]
    if skill:
        require(set(response) == {'requestRef', 'candidates'}, 'unexpected skill response fields')
    candidates = response['candidates'] if skill else [response]
    require(len(candidates) == len(contract['offers']), 'candidate count mismatch')
    combinations = set()
    for candidate, offer in zip(candidates, contract['offers']):
        require(set(candidate) == FIELDS | ({'requestRef'} if not skill and contract['requestRef'] else set()), 'unexpected candidate fields')
        require(candidate['selectionId'] == offer['selectionId'], 'candidate selection/order mismatch')
        for field, key, minimum, maximum in (('positiveModuleIds', 'positive', 1, offer['maximumPositive']), ('drawbackModuleIds', 'drawback', 0, offer['maximumDrawback']), ('evidenceFactIds', 'evidence', 1, offer['maximumEvidence'])):
            ids = _strings(candidate[field], field, empty=minimum == 0)
            require(minimum <= len(ids) <= maximum and set(ids) <= set(offer[key]), 'response outside public offer: ' + field)
        chosen, evidence = (candidate['positiveModuleIds'], set(candidate['evidenceFactIds']))
        if candidate['drawbackModuleIds'] and 'qualifiedNegativeEvidence' in offer:
            require(bool(evidence & set(offer['qualifiedNegativeEvidence'])), 'selected drawback lacks qualified negative evidence')
        require(evidence <= set(contract['facts']), 'evidence has no public fact mapping')
        require(all((contract['facts'][key].get('selectable') is True for key in evidence)), 'unselectable evidence')
        for identity in chosen + candidate['drawbackModuleIds']:
            required = contract['modules'][identity]['requiredEvidence']
            require(not required or bool(evidence & set(required)), 'missing module domain evidence')
        require(not offer.get('gameplayEvidence') or bool(evidence & set(offer['gameplayEvidence'])), 'missing gameplay evidence')
        if profile == PROFILES[1]:
            require(candidate['evidenceFactIds'] == sorted(evidence), 'trait evidence must be sorted')
        if skill:
            require(not {'guard', 'protect'} <= set(chosen), 'forbidden guard/protect combination')
            combination = (tuple(sorted(chosen)), tuple(sorted(candidate['drawbackModuleIds'])))
            require(combination not in combinations, 'duplicate candidate combination')
            combinations.add(combination)
        else:
            for identity in candidate['drawbackModuleIds']:
                meaning = contract['modules'][identity]['meaning']
                if '함께 선택 금지 이로운 기능=' in meaning:
                    conflicts = meaning.split('함께 선택 금지 이로운 기능=', 1)[1].split(',')
                    require(not set(chosen) & set(conflicts), 'forbidden facility module combination')
        name, flavor = (candidate['displayName'], candidate['narrativeFlavor'])
        for value, limit in ((name, 14 if skill else 32), (flavor, 48 if skill else 180)):
            require(isinstance(value, str) and value.strip() == value and value and (len(value.encode('utf-16-le')) // 2 <= limit), 'authored text length invalid')
        prose = name + flavor
        if skill:
            character = contract['characterName']
            require(flavor.count(character) == 1 and re.match(re.escape(character) + '(?:은|는)\\s', flavor), 'exact character must be the subject once')
            require(flavor.endswith('.') and flavor.count('.') == 1 and (not re.search('[\\r\\n!?]', flavor)), 'skill needs one completed sentence')
            prose = name + flavor[len(character):]
        if authoritative_name is None:
            require(not re.search('[0-9０-９%％]', prose), 'numeric authored effect')
        else:
            require(profile == PROFILES[1] and isinstance(authoritative_name, str)
                    and authoritative_name.strip() == authoritative_name and authoritative_name,
                    'exact trait subject authority required')
            require(not has_unbound_authored_number(name, authoritative_name)
                    and not has_unbound_authored_number(flavor, authoritative_name), 'numeric authored effect')
    return dict(status='PUBLIC_CONTRACT_PASS', candidates=len(candidates), semanticQuality='NOT_EVALUATED', gameplayReplay='NOT_RUN', humanApprovalClaimed=False, trainingEligible=False)
