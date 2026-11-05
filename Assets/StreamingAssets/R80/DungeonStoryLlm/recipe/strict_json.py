# Reachable R80 inference definitions. See recipe-provenance.json.
from __future__ import annotations
import json
import math

def _object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f'duplicate JSON key: {key}')
        result[key] = value
    return result

def _constant(value):
    raise ValueError(f'non-finite JSON number: {value}')

def _float(value):
    number = float(value)
    if not math.isfinite(number):
        return _constant(value)
    return number

def loads(value, *, parse_float=None):
    return json.loads(value, object_pairs_hook=_object, parse_constant=_constant, parse_float=parse_float or _float)
