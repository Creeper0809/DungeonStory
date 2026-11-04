# Reachable R80 inference definitions. See recipe-provenance.json.
from copy import deepcopy
import math
import itertools
MAX_ARRAY_ALTERNATIVES = 4096
KEYS = {'$schema', 'type', 'enum', 'const', 'properties', 'required', 'additionalProperties', 'items', 'additionalItems', 'minItems', 'maxItems', 'uniqueItems', 'minLength', 'maxLength'}
FIELD_ORDER = ('requestRef', 'candidates', 'selectionId', 'evidenceFactIds', 'positiveModuleIds', 'drawbackModuleIds', 'displayName', 'narrativeFlavor', 'moduleId', 'latestObservation', 'citedFactIds', 'currentState')

def decoder_schema(schema):
    """Equivalent bounded schema; choices precede prose so prose can condition on them."""
    if type(schema) is bool:
        return schema
    if not isinstance(schema, dict) or set(schema) - KEYS:
        raise ValueError('unsupported public schema keyword; refusing unconstrained fallback')
    value = deepcopy(schema)
    value.pop('$schema', None)
    if 'properties' in value:
        order = lambda name: (FIELD_ORDER.index(name) if name in FIELD_ORDER else len(FIELD_ORDER), name)
        value['properties'] = {k: decoder_schema(value['properties'][k]) for k in sorted(value['properties'], key=order)}
    if isinstance(value.get('items'), list):
        if value.get('uniqueItems'):
            raise ValueError('tuple uniqueness needs an explicit implementation')
        value['prefixItems'] = [decoder_schema(v) for v in value.pop('items')]
        value['items'] = decoder_schema(value.pop('additionalItems', True))
    elif value.get('type') == 'array' and value.get('uniqueItems'):
        item = value.get('items', {})
        options = item.get('enum')
        minimum, maximum = (value.get('minItems', 0), value.get('maxItems'))
        if set(item) - {'type', 'enum'} or item.get('type') != 'string' or (not isinstance(options, list)) or (not all((isinstance(x, str) for x in options))) or (len(set(options)) != len(options)) or (type(minimum) is not int) or (type(maximum) is not int) or (not 0 <= minimum <= maximum):
            raise ValueError('unique public arrays must be bounded string enums')
        count = sum((math.perm(len(options), n) for n in range(minimum, min(maximum, len(options)) + 1)))
        if not 0 < count <= MAX_ARRAY_ALTERNATIVES:
            raise ValueError('public unique-array grammar exceeds bounded alternatives')
        value = {'type': 'array', 'enum': [list(p) for n in range(minimum, min(maximum, len(options)) + 1) for p in itertools.permutations(options, n)]}
    elif isinstance(value.get('items'), dict):
        value['items'] = decoder_schema(value['items'])
    return value
