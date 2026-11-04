# Reachable R80 inference definitions. See recipe-provenance.json.
import json
from copy import deepcopy
import re
import hashlib
VERSION = 'dungeonstory-prose-format-intersection@1'
PARTICLE = '(은|는) '
TAILS = (('[^.!?\\r\\n]*\\.$', '.', frozenset('.!?\r\n')), ('[^\\r\\n]*$', '', frozenset('\r\n')))

def require(condition, message):
    if not condition:
        raise ValueError(message)

def literal(value):
    value.encode('utf8')
    return json.dumps(value, ensure_ascii=False)

def fragment(value):
    value.encode('utf8')
    return json.dumps(value, ensure_ascii=False, separators=(',', ':'))[1:-1]

def bounds(schema, *, patterned):
    allowed = {'type', 'minLength', 'maxLength'} | ({'pattern'} if patterned else set())
    require(isinstance(schema, dict) and (not set(schema) - allowed) and (schema.get('type') == 'string'), 'unsupported original prose string schema; no fallback')
    lower, upper = (schema.get('minLength', 0), schema.get('maxLength'))
    require(type(lower) is int and type(upper) is int and (0 <= lower <= upper), 'original finite character bounds required')
    return (lower, upper)

def fixed_pattern(pattern):
    require(isinstance(pattern, str) and pattern.startswith('^'), 'anchored original prose pattern required')
    for tail, suffix, forbidden in TAILS:
        if pattern.endswith(tail):
            prefix = pattern[1:-len(tail)]
            require(prefix.endswith(PARTICLE), 'unsupported original fixed prefix')
            escaped = prefix[:-len(PARTICLE)]
            decoded, i = ([], 0)
            while i < len(escaped):
                if escaped[i] == '\\':
                    require(i + 1 < len(escaped), 'incomplete original literal escape')
                    i += 1
                decoded.append(escaped[i])
                i += 1
            subject = ''.join(decoded)
            require(re.escape(subject) == escaped, 'original prefix must be an exact escaped literal')
            subject.encode('utf8')
            return (subject, suffix, forbidden)
    raise ValueError('unsupported original prose pattern; no unconstrained fallback')

def atom_rule(forbidden):
    excluded = ''.join((c for c in '.!?' if c in forbidden))
    raw = '[^"\\\\\\x00-\\x1F\\uD800-\\uDFFF' + excluded + ']'
    escaped = [literal(fragment(c)) for c in ('"', '\\')]
    escaped += [literal(fragment(chr(c))) for c in range(32) if chr(c) not in forbidden]
    return ' | '.join([raw] + escaped)

def prose_grammar(original):
    """Return GBNF for patterned prose; unpatterned prose keeps its old decoder."""
    schema = deepcopy(original)
    require(set(schema) == {'type', 'properties', 'required', 'additionalProperties'} and schema['type'] == 'object' and (schema['additionalProperties'] is False) and (set(schema['properties']) == set(schema['required']) == {'displayName', 'narrativeFlavor'}), 'exact original two-field prose object required')
    name_lower, name_upper = bounds(schema['properties']['displayName'], patterned=False)
    narrative = schema['properties']['narrativeFlavor']
    lower, upper = bounds(narrative, patterned='pattern' in narrative)
    if 'pattern' not in narrative:
        return None
    subject, suffix, forbidden = fixed_pattern(narrative['pattern'])
    fixed_length = len(subject) + 2 + len(suffix)
    body_lower, body_upper = (max(0, lower - fixed_length), upper - fixed_length)
    require(body_upper >= body_lower, 'original pattern and length constraints have no common values')
    quote = literal('"')
    prefixes = ' | '.join((literal(fragment(subject + particle + ' ')) for particle in ('은', '는')))
    name = f'{quote} json-char{{{name_lower},{name_upper}}} {quote}'
    narrative_rule = f'{quote} ({prefixes}) prose-char{{{body_lower},{body_upper}}}'
    if suffix:
        narrative_rule += ' ' + literal(fragment(suffix))
    narrative_rule += ' ' + quote
    root = literal('{"displayName":') + ' display-name ' + literal(',"narrativeFlavor":') + ' narrative ' + literal('}')
    return '\n'.join(('root ::= ' + root, 'display-name ::= ' + name, 'narrative ::= ' + narrative_rule, 'json-char ::= ' + atom_rule(frozenset()), 'prose-char ::= ' + atom_rule(forbidden))) + '\n'

def native_constraint(native, *, prose):
    """Transport + replay receipt; all semantic native schemas stay untouched."""
    grammar = prose_grammar(native) if prose else None
    schema_pin = 'sha256:' + hashlib.sha256(json.dumps(native, ensure_ascii=False, sort_keys=True, separators=(',', ':')).encode('utf8')).hexdigest()
    record = dict(schema='dungeonstory-native-format-constraint@1', nativeSchemaSha256=schema_pin, decoder=VERSION if grammar is not None else 'ORIGINAL_JSON_SCHEMA', nativeGrammarSha256=None if grammar is None else 'sha256:' + hashlib.sha256(grammar.encode('utf8')).hexdigest(), outputRepair=False, semanticChoicesComputedByCode=False)
    fields = dict(json_schema=deepcopy(native)) if grammar is None else dict(grammar=grammar)
    return (fields, record)
