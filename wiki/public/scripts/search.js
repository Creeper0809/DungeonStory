const form = document.querySelector('[data-search-form]');
const input = document.querySelector('#wiki-search');
const statusNode = document.querySelector('[data-search-status]');
const target = document.querySelector('[data-search-results]');

const render = (items) => {
  if (!target) return;
  target.replaceChildren();
  items.forEach((item) => {
    const article = document.createElement('article');
    article.className = 'search-result';
    const heading = document.createElement('h2');
    const link = document.createElement('a');
    link.href = item.url;
    link.textContent = item.title ?? '제목 없는 문서';
    heading.append(link);
    const excerpt = document.createElement('p');
    excerpt.textContent = item.excerpt ?? '';
    article.append(heading, excerpt);
    target.append(article);
  });
};

form?.addEventListener('submit', async (event) => {
  event.preventDefault();
  const query = input?.value.trim() ?? '';
  if (!query || !statusNode) return;
  statusNode.textContent = '검색 중…';
  try {
    const response = await fetch(`/api/search.json?q=${encodeURIComponent(query)}`, { cache: 'no-store' });
    if (!response.ok) throw new Error(`Search request failed: ${response.status}`);
    const result = await response.json();
    render(result.results);
    statusNode.textContent = `${result.total}개 결과 중 최대 30개를 표시한다.`;
  } catch {
    statusNode.textContent = '검색 색인을 불러오지 못했다. 잠시 뒤 다시 시도한다.';
  }
});
