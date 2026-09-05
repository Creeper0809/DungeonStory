FROM node:22-bookworm-slim AS build

WORKDIR /app
ARG DUNGEONSTORY_WIKI_SITE_URL
ENV DUNGEONSTORY_WIKI_SITE_URL=${DUNGEONSTORY_WIKI_SITE_URL}
COPY wiki/package.json wiki/package-lock.json ./
RUN npm ci
COPY wiki/astro.config.mjs wiki/tsconfig.json ./
COPY wiki/src ./src
COPY wiki/public ./public
COPY wiki/game-versions ./game-versions
RUN test -n "$DUNGEONSTORY_WIKI_SITE_URL" && npm run check && npx astro build
RUN npm prune --omit=dev

FROM node:22-bookworm-slim AS runtime

ENV NODE_ENV=production
ENV HOST=0.0.0.0
ENV PORT=4321
WORKDIR /app
COPY --from=build /app/dist ./dist
COPY --from=build /app/game-versions ./game-versions
COPY --from=build /app/node_modules ./node_modules
EXPOSE 4321
CMD ["node", "./dist/server/entry.mjs"]
