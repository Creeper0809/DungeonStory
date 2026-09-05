import { defineMiddleware } from 'astro:middleware';

export const onRequest = defineMiddleware(async (_context, next) => {
  const response = await next();
  const contentType = response.headers.get('content-type') ?? '';
  if (contentType.includes('text/html') || contentType.includes('application/json')) {
    response.headers.set('cache-control', 'no-store, max-age=0');
  }
  return response;
});
