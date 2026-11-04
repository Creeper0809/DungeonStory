# Reachable R80 inference definitions. See recipe-provenance.json.
from subject_anchored_view import reading_messages, prose_messages, prose_native
from subject_staged_view import selection_messages as original_selection

def selection_messages(row, index, previous, reading):
    messages, schema = original_selection(row, index, previous, reading)
    ambiguous = '직접 관련된 최소 근거를 고르고, 상태를 읽었다는 이유만으로 단점 없는 후보에 N0를 자동 인용하지 않는다.'
    explicit = '직접 관련된 근거를 고른다. 공개 요청이 부정 경험과 후속 결과 양쪽의 인용을 요구하면 단점을 선택하지 않은 회복 판단에도 해당 후속 근거가 필요하다. 그 요구가 없는 후보에 무관한 후속 기록을 덧붙이지 않는다. 같은 인물이라도 다른 대상의 결과와, 결과가 아닌 준비·예약·해설 문장을 혼동하지 않는다.'
    assert messages[0]['content'].count(ambiguous) == 1
    messages[0]['content'] = messages[0]['content'].replace(ambiguous, explicit)
    return (messages, schema)
