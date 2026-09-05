import type { APIRoute } from 'astro';
import { getEntity, hasGameVersion } from '../../../../../lib/wiki-data';

export const prerender = false;

export const GET: APIRoute = ({ params }) => {
  const version = params.version ?? '';
  if (!hasGameVersion(version)) return new Response(null, { status: 404 });
  const entity = getEntity(version, params.kind ?? '', params.slug ?? '');
  if (!entity || entity.spoiler_tier !== 'warning') return new Response(null, { status: 404 });
  return new Response(JSON.stringify({ title: entity.title, summary: entity.summary, facts: entity.facts }), {
    headers: { 'content-type': 'application/json; charset=utf-8', 'cache-control': 'no-store' },
  });
};
