# Reachable R80 inference definitions. See recipe-provenance.json.
from __future__ import annotations
import hashlib

def require(condition, message):
    if not condition:
        raise ValueError(message)

def byte_hash(payload: bytes) -> str:
    return 'sha256:' + hashlib.sha256(payload).hexdigest()
