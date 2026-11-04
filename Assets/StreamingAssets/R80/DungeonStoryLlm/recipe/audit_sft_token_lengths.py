# Reachable R80 inference definitions. See recipe-provenance.json.
from __future__ import annotations
import json

def canonical(value):
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(',', ':')).encode('utf-8')

def require(ok, message):
    if not ok:
        raise ValueError(message)
