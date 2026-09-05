import type { APIRoute } from 'astro';
import {
  anatomyHref,
  categoryHref,
  entityHref,
  getAnatomyReferences,
  getCategories,
  getCurrentVersion,
  getEntities,
  getGuides,
  getNeedReferences,
  getSearchAliases,
  getWorkReferences,
  guideHref,
  needHref,
  workHref,
  workTaskHref,
} from '../../lib/wiki-data';

export const prerender = false;

interface SearchResult {
  title: string;
  excerpt: string;
  url: string;
  score: number;
}

const normalize = (value: string): string => value.normalize('NFC').toLocaleLowerCase('ko-KR').replace(/\s+/g, ' ').trim();

const scoreText = (title: string, text: string, terms: string[]): number => terms.reduce((score, term) => {
  const titleIndex = title.indexOf(term);
  const textIndex = text.indexOf(term);
  if (titleIndex === 0) return score + 12;
  if (titleIndex >= 0) return score + 8;
  if (textIndex >= 0) return score + 2;
  return score;
}, 0);

const toResult = (title: string, excerpt: string, url: string, terms: string[]): SearchResult | undefined => {
  const normalizedTitle = normalize(title);
  const normalizedExcerpt = normalize(excerpt);
  if (!terms.every((term) => normalizedTitle.includes(term) || normalizedExcerpt.includes(term))) return undefined;
  return { title, excerpt, url, score: scoreText(normalizedTitle, normalizedExcerpt, terms) };
};

export const GET: APIRoute = ({ url }) => {
  const rawQuery = url.searchParams.get('q') ?? '';
  const terms = normalize(rawQuery).split(' ').filter(Boolean).slice(0, 8);
  const headers = { 'content-type': 'application/json; charset=utf-8', 'cache-control': 'no-store' };
  if (rawQuery.length > 120) {
    return new Response(JSON.stringify({ error: '검색어는 120자 이하로 입력한다.' }), { status: 400, headers });
  }
  if (terms.length === 0) {
    return new Response(JSON.stringify({ total: 0, results: [] }), { headers });
  }

  const version = getCurrentVersion();
  const aliases = new Map(getSearchAliases(version).map((record) => [`${record.kind}/${record.slug}`, record.aliases.join(' ')]));
  const results: SearchResult[] = [
    ...getEntities(version)
      .filter((entity) => entity.spoiler_tier === 'none')
      .map((entity) => toResult(entity.title, `${entity.summary} ${aliases.get(`${entity.kind}/${entity.slug}`) ?? ''}`, entityHref(entity), terms)),
    ...getGuides(version)
      .filter((guide) => guide.spoiler_tier === 'none')
      .map((guide) => toResult(guide.title, `${guide.summary} ${guide.body.replace(/[#*`_[\]]/g, ' ')}`, guideHref(guide.id), terms)),
    ...getCategories(version)
      .map((category) => toResult(category.label, `${category.label} 분류에는 ${category.entry_count.toLocaleString('ko-KR')}개 문서가 있다.`, categoryHref(category.id), terms)),
    ...getWorkReferences(version).flatMap((reference) => [
      toResult(reference.title, reference.summary, workHref(reference.id), terms),
      ...reference.tasks.map((task) => toResult(task.title, `${task.summary} ${task.prepare} ${task.check}`, workTaskHref(reference.id, task.id), terms)),
    ]),
    ...getNeedReferences(version).map((reference) => toResult(reference.title, `${reference.summary} ${reference.read} ${reference.crisis}`, needHref(reference.id), terms)),
    ...getAnatomyReferences(version).map((reference) => toResult(reference.title, `${reference.summary} ${reference.injury}`, anatomyHref(reference.id), terms)),
  ].filter((result): result is SearchResult => Boolean(result));

  results.sort((left, right) => right.score - left.score || left.title.localeCompare(right.title, 'ko'));
  return new Response(JSON.stringify({ total: results.length, results: results.slice(0, 30).map(({ score: _score, ...result }) => result) }), { headers });
};
