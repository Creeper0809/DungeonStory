const initCatalogues = () => {
  document.querySelectorAll('[data-catalogue]').forEach((catalogue) => {
    const search = catalogue.querySelector('[data-catalogue-search]');
    const kind = catalogue.querySelector('[data-catalogue-kind]');
    const count = catalogue.querySelector('[data-catalogue-count]');
    const rows = Array.from(catalogue.querySelectorAll('[data-catalogue-row]'));
    const update = () => {
      const query = search?.value.trim().toLocaleLowerCase() ?? '';
      const selectedKind = kind?.value ?? 'all';
      let shown = 0;
      rows.forEach((row) => {
        const matches = (!query || row.dataset.title?.toLocaleLowerCase().includes(query))
          && (selectedKind === 'all' || row.dataset.kind === selectedKind);
        row.hidden = !matches;
        if (matches) shown += 1;
      });
      if (count) count.value = `${shown}개 표시`;
    };
    search?.addEventListener('input', update);
    kind?.addEventListener('change', update);
  });
};

const initRelationGraphs = () => {
  document.querySelectorAll('[data-relation-graph]').forEach((graph) => {
    const filters = Array.from(graph.querySelectorAll('[data-graph-filter]'));
    const edges = Array.from(graph.querySelectorAll('[data-graph-edge]'));
    const update = () => {
      const selected = new Set(filters
        .filter((input) => input.checked && input.dataset.graphFilter !== 'all')
        .map((input) => input.dataset.graphFilter));
      const all = filters.find((input) => input.dataset.graphFilter === 'all')?.checked ?? true;
      edges.forEach((edge) => {
        edge.hidden = !all && selected.size > 0 && !selected.has(edge.dataset.graphEdge);
      });
    };
    filters.forEach((input) => input.addEventListener('change', update));
  });
};

const initSpoilers = () => {
  document.querySelectorAll('[data-spoiler-block]').forEach((block) => {
    const button = block.querySelector('[data-spoiler-reveal]');
    const content = block.querySelector('[data-spoiler-content]');
    if (!button || !content) return;
    button.addEventListener('click', async () => {
      button.disabled = true;
      button.textContent = '상세 정보를 불러오는 중…';
      try {
        const response = await fetch(block.dataset.source ?? '', { credentials: 'same-origin' });
        if (!response.ok) throw new Error('spoiler payload request failed');
        const payload = await response.json();
        const title = document.createElement('h2');
        title.textContent = payload.title;
        const summary = document.createElement('p');
        summary.textContent = payload.summary;
        content.replaceChildren(title, summary);
        if (Array.isArray(payload.facts) && payload.facts.length) {
          const list = document.createElement('dl');
          list.className = 'spoiler-facts';
          payload.facts.forEach((fact) => {
            const wrapper = document.createElement('div');
            const term = document.createElement('dt');
            const definition = document.createElement('dd');
            term.textContent = fact.label;
            definition.textContent = fact.value;
            wrapper.append(term, definition);
            list.append(wrapper);
          });
          content.append(list);
        }
        button.remove();
      } catch {
        button.disabled = false;
        button.textContent = '상세 정보를 열지 못했다. 다시 시도';
      }
    });
  });
};

initCatalogues();
initRelationGraphs();
initSpoilers();
