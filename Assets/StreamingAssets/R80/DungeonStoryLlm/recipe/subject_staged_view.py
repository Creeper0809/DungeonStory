# Reachable R80 inference definitions. See recipe-provenance.json.
from audit_sft_token_lengths import require
import re, json
from outcome_staged_view import reading_messages as original_reading, selection_messages as original_selection
from copy import deepcopy
from candidate_task_view import contract, SKILL
from balanced_staged_view import prose_messages as original_prose
from balanced_staged_view import prose_native as original_prose_native

def public_subject(row):
    fields = {}
    for key in ('character', 'subjectKind', 'subjectName'):
        values = [line.split('=', 1)[1] for line in row['wire']['messages'][1]['content'].splitlines() if line.startswith(key + '=')]
        require(len(values) <= 1, 'conflicting explicit public identity header: ' + key)
        if values:
            fields[key] = values[0]
    c = contract(row)
    if row['profile'] in (SKILL, 'AcquiredTraitModuleSelection'):
        require(fields.get('character'), 'explicit character identity is required, never guess a name')
        if c['characterName'] is not None:
            require(c['characterName'] == fields['character'], 'character header/contract mismatch')
        if fields.get('subjectKind') == 'character':
            require(fields['subjectName'] == fields['character'], 'subject/name mismatch')
    return fields

def with_identity(messages, row):
    messages = deepcopy(messages)
    doc = json.loads(messages[1]['content'])
    identity = public_subject(row)
    doc['explicitPublicSubject'] = identity
    if row['profile'] in (SKILL, 'AcquiredTraitModuleSelection'):
        doc['characterName'] = identity['character']
    messages[1]['content'] = json.dumps(doc, ensure_ascii=False, separators=(',', ':'))
    return messages

def atomic_quotes(facts):
    values = []
    for f in facts.values():
        for sentence in re.split('(?<=[.!?])\\s+', f['text']):
            for start in [0] + [m.end() for m in re.finditer(':\\s+', sentence)]:
                value = sentence[start:]
                if 0 < len(value) <= 600:
                    values.append(value)
    result = list(dict.fromkeys(values))
    require(result, 'original sentence quotes required')
    require(all((any((v in f['text'] for f in facts.values())) for v in result)), 'non-extractive quote')
    return result

def reading_messages(row, index, previous):
    m, s = original_reading(row, index, previous)
    m = with_identity(m, row)
    s['properties']['latestOwnOutcome']['enum'] = atomic_quotes(contract(row)['facts'])
    m[0]['content'] += ' 이번 인용은 하나의 원문 문장이다. 앞선 실패와 나중 결과를 긴 문단으로 합쳐 답하지 않는다. 모든 facts는 그대로 읽고 해당 당사자의 마지막 실제 결과를 고른다.'
    return (m, s)

def selection_messages(row, index, previous, reading):
    m, s = original_selection(row, index, previous, reading)
    return (with_identity(m, row), s)

def prose_messages(row, index, selection):
    m, s = original_prose(row, index, selection)
    return (with_identity(m, row), s)

def prose_native(row, native):
    result = original_prose_native(row, native)
    if row['profile'] == 'AcquiredTraitModuleSelection':
        result['properties']['narrativeFlavor']['pattern'] = '^' + re.escape(public_subject(row)['character']) + '(은|는) '
    return result
