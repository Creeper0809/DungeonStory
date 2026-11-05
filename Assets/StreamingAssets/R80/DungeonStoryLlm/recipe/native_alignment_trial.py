# Reachable R80 inference definitions. See recipe-provenance.json.
from copy import deepcopy

def reading_native(native):
    native = deepcopy(native)
    native['properties'] = {k: native['properties'][k] for k in ('latestOwnOutcome', 'drawbackModuleIds')}
    return native
