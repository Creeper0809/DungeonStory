# Reachable R80 inference definitions. See recipe-provenance.json.
from subject_staged_view import reading_messages, prose_messages, public_subject
from subject_staged_view import prose_native as subject_native
import re

def prose_native(row, native):
    result = subject_native(row, native)
    if row['profile'] == 'AcquiredTraitModuleSelection':
        result['properties']['narrativeFlavor']['pattern'] = '^' + re.escape(public_subject(row)['character']) + '(은|는) [^\\r\\n]*$'
    return result
