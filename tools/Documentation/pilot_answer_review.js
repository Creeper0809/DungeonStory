function renderAuthoredAnswer(row) {
  const authored = row.authoredAnswer;
  if (!authored) return;
  if (row.identityReview?.characters?.length) {
    $('inputPeople').textContent = row.identityReview.characters.map(person =>
      `이름: ${person.name} · 직급: ${person.roleLabel} · 구분: ${person.typeLabel}`).join(' / ');
  }
  const box = document.querySelector('.input-answer');
  const guide = $('inputOutputGuide');
  box.replaceChildren(inputElement('span', '이 입력으로 작성한 답변', 'eyebrow'),
    inputElement('h3', '답변 초안 · 사람 검토 전'), guide);
  const candidates = authored.response.candidates || [authored.response];
  const selected = new Set();
  for (const [index, candidate] of candidates.entries()) {
    const detail = authored.displayCandidates[index];
    const card = inputElement('article', undefined, 'input-candidate');
    card.append(inputElement('span', candidates.length > 1 ? `후보 ${index + 1}` : '제안 답변', 'eyebrow'),
      inputElement('h3', candidate.displayName), inputElement('p', candidate.narrativeFlavor));
    if (detail.context) card.append(inputElement('p', detail.context, 'muted'));
    card.append(inputElement('h4', '선택한 효과'));
    for (const module of detail.modules) {
      selected.add(module.id);
      card.append(inputElement('strong', module.name), inputElement('p', module.description));
    }
    card.append(inputElement('h4', '이렇게 쓴 근거'));
    for (const fact of detail.evidence) {
      const evidence = inputElement('div', undefined, 'input-fact');
      const context = fact.presentationKind === 'character-context';
      evidence.append(inputElement('strong', context || fact.background ? '인물 기본정보·배경 (사건 아님)' : (fact.eventLabel || '공개 기록')),
        inputElement('p', answerEvidenceText(fact.text)));
      appendNameRole(evidence, fact);
      if (fact.importance && !context) evidence.append(inputElement('p', fact.importance.salience === null ?
        '중요도 미평가' : `중요도 ${fact.importance.salience.toFixed(2)} · ${fact.importance.tierLabel}`, 'muted'));
      card.append(evidence);
    }
    const reasoning = inputElement('details');
    reasoning.append(inputElement('summary', '작성 판단 보기'), inputElement('p', detail.explanation));
    card.append(reasoning);
    box.append(card);
  }
  box.append(inputElement('p', '답변 형식·허용 효과·근거 연결 확인 완료. 실제 게임 효과와 최종 수치는 실행 검증 전입니다.', 'muted'));
  const link = inputElement('a', row.identityReview ? '이름·직급을 구분한 100개 초안 내려받기' : '100개 입력–답변 초안 내려받기');
  link.href = row.identityReview ? '/api/dataset-names' : '/api/dataset';
  link.download = 'dungeonstory-dataset-drafts-100.jsonl';
  box.append(link);
  document.querySelectorAll('#inputOffers .offer').forEach((element, index) => {
    element.querySelector('strong').textContent = selected.has(row.presentation.modules[index].id) ? '선택한 효과' : '미선택 제안';
  });
}

function appendNameRole(element, fact) {
  if (fact.identityParticipants?.length) element.append(inputElement('p',
    fact.identityParticipants.map(person => `이름: ${person.name} · 직급: ${person.roleLabel} · 구분: ${person.typeLabel}`).join(' / '), 'muted'));
  const original = fact.recordedDisplayText ?? fact.recordedText;
  const current = fact.displayText ?? fact.text;
  if (original && original !== current) {
    const details = inputElement('details');
    details.append(inputElement('summary', '수집 당시 표기'), inputElement('p', original),
      inputElement('p', '같은 인물 ID의 저장된 이름과 직급을 구분해 표시했습니다.', 'muted'));
    element.append(details);
  }
}

function answerEvidenceText(text) {
  if (!text.includes('캠페인 사건') || !text.includes('resolution=started')) return text;
  const names = {'seasonal:spring-spore-rain': '봄 포자비', 'seasonal:spring-thaw-flood': '봄 해빙 홍수',
    'seasonal:spring-migrant-herd': '봄철 이동 무리'};
  const match = text.match(/definition=([^;\s]+)/);
  return match && names[match[1]] ? `${names[match[1]]} 사건이 시작됐다. 개인의 구체적인 행동은 이 기록에 나와 있지 않다.` : text;
}
