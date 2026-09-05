import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join, resolve } from 'node:path';

export type SpoilerTier = 'none' | 'warning';

export interface Fact {
  label: string;
  value: string;
}

export interface Relation {
  type: string;
  label: string;
  amount: string | null;
  target_kind: string;
  target_slug: string;
  target_title: string;
  target_spoiler_tier: SpoilerTier;
}

export interface Backlink {
  type: string;
  label: string;
  amount: string | null;
  from_kind: string;
  from_slug: string;
  from_title: string;
  from_spoiler_tier: SpoilerTier;
}

export interface WikiEntity {
  schema_version: number;
  game_version: string;
  kind: string;
  group: string;
  slug: string;
  title: string;
  summary: string;
  in_game_description?: string;
  facts: Fact[];
  spoiler_tier: SpoilerTier;
  relations: Relation[];
}

export interface CategoryEntry {
  kind: string;
  slug: string;
  title: string;
  spoiler_tier: SpoilerTier;
}

export interface Category {
  id: string;
  label: string;
  entry_count: number;
  entries: CategoryEntry[];
}

export interface Guide {
  id: string;
  title: string;
  summary: string;
  system: string;
  spoiler_tier: SpoilerTier;
  body: string;
}

export interface SearchAliasRecord {
  aliases: string[];
  kind: string;
  slug: string;
  title: string;
}

export interface WorkTask {
  id: string;
  title: string;
  summary: string;
  prepare: string;
  check: string;
}

export interface WorkReference {
  id: string;
  title: string;
  summary: string;
  proficiency: { title: string; kind: string; slug: string } | null;
  related_guides: string[];
  tasks: WorkTask[];
}

interface WorkReferenceDocument {
  schema_version: number;
  game_version: string;
  references: WorkReference[];
}

export interface NeedMetric {
  label: string;
  value: string;
  effect: string;
}

export interface NeedThreshold {
  range: string;
  effect: string;
}

export interface NeedReference {
  id: string;
  title: string;
  summary: string;
  measurement: string;
  read: string;
  metrics: NeedMetric[];
  thresholds: NeedThreshold[];
  crisis: string;
  operations: string[];
  related_guides: string[];
}

interface NeedReferenceDocument {
  schema_version: number;
  game_version: string;
  references: NeedReference[];
}

export interface AnatomyMetric {
  label: string;
  value: string;
  detail: string;
}

export interface AnatomyReference {
  id: string;
  title: string;
  group: string;
  summary: string;
  metrics: AnatomyMetric[];
  functions: string[];
  injury: string;
  care: string[];
  related_ids: string[];
}

interface AnatomyReferenceDocument {
  schema_version: number;
  game_version: string;
  references: AnatomyReference[];
}

export interface AnatomyProfileGroup {
  id: string;
  label: string;
  description: string;
  reference_ids: string[];
}

export const anatomyProfileCardTitle = (title: string, profileLabel: string): string => {
  const prefix = `${profileLabel}의 `;
  if (!title.startsWith(prefix)) {
    throw new Error(`Anatomy profile card title must start with "${prefix}": ${title}`);
  }
  return title.slice(prefix.length);
};

const playerFacingAnatomyText = (text: string): string => text
  .replaceAll('해부 구조', '신체 구조')
  .replaceAll('노드', '부위');

const playerFacingAnatomyReference = (reference: AnatomyReference): AnatomyReference => ({
  ...reference,
  summary: playerFacingAnatomyText(reference.summary),
  metrics: reference.metrics.map((metric) => ({ ...metric, detail: playerFacingAnatomyText(metric.detail) })),
  functions: reference.functions.map(playerFacingAnatomyText),
  injury: playerFacingAnatomyText(reference.injury),
  care: reference.care.map(playerFacingAnatomyText),
});

interface AnatomyProfileGroupDocument {
  schema_version: number;
  game_version: string;
  groups: AnatomyProfileGroup[];
}

export interface GuideNavigationPage {
  id: string;
  group: string;
  kind: 'topic' | 'situation';
  directory_visibility?: 'contextual';
  redirect_to?: string;
  related_guide_ids: string[];
  situation_guide_ids: string[];
  category_ids: string[];
}

export interface GuideNavigationSection {
  id: string;
  label: string;
  description: string;
  guide_ids: string[];
}

export interface GuideNavigation {
  schema_version: number;
  game_version: string;
  sections: GuideNavigationSection[];
  pages: GuideNavigationPage[];
}

interface RegistryEntry {
  game_version: string;
  parent_game_version: string | null;
  status: string;
  content_digest: string | null;
}

interface Registry {
  schema_version: number;
  current_game_version: string;
  versions: RegistryEntry[];
}

export interface GameVersionMetadata {
  approved_at: string | null;
  content_digest: string | null;
  game_version: string;
  parent_game_version: string | null;
  published_at: string | null;
  schema_version: number;
  source_digests: Record<string, string>;
  status: string;
}

export interface ModelManifest {
  content_digest: string;
  counts: { categories: number; entities: number; relations: number };
  game_version: string;
  schema_version: number;
  source_digests: Record<string, string>;
}

interface DirectoryEntry {
  entry_id: string;
  label: string;
  safe_label: string;
  target_kind: string;
  target_id: string;
  icon_ref: string;
  visibility: string;
  spoiler_tier: SpoilerTier;
}

interface DirectoryGroup {
  group_id: string;
  label: string;
  sort_order: number;
  entries: DirectoryEntry[];
}

interface DirectoryManifest {
  schema_version: number;
  game_version: string;
  groups: DirectoryGroup[];
}

const wikiRoot = resolve(process.cwd());
interface RevisionedCache<T> {
  revision: string;
  value: T;
}

const dataCache = new Map<string, RevisionedCache<WikiEntity[]>>();
const backlinkCache = new Map<string, RevisionedCache<Record<string, Backlink[]>>>();
const categoryCache = new Map<string, RevisionedCache<Category[]>>();

function readJson<T>(file: string): T {
  return JSON.parse(readFileSync(file, 'utf8')) as T;
}

function versionRoot(version: string): string {
  return join(wikiRoot, 'game-versions', version);
}

function fileRevision(file: string): string {
  const stats = statSync(file);
  return `${stats.size}:${stats.mtimeMs}`;
}

function readEntityFiles(root: string): string[] {
  return readdirSync(root, { withFileTypes: true }).flatMap((entry) => {
    const path = join(root, entry.name);
    if (entry.isDirectory()) return readEntityFiles(path);
    return entry.isFile() && entry.name.endsWith('.json') ? [path] : [];
  });
}

function isSafeEntitySegment(value: string): boolean {
  return /^[a-z0-9][a-z0-9-]*$/.test(value);
}

function parseGuide(source: string): Guide {
  const match = source.match(/^---\r?\n([\s\S]*?)\r?\n---\r?\n([\s\S]*)$/);
  if (!match) throw new Error('Guide requires YAML-style frontmatter.');
  const fields = Object.fromEntries(
    match[1]
      .split(/\r?\n/)
      .filter(Boolean)
      .map((line) => {
        const [key, ...value] = line.split(':');
        return [key.trim(), value.join(':').trim()];
      }),
  );
  if (!fields.id || !fields.title || !fields.summary || !fields.system) {
    throw new Error('Guide frontmatter is incomplete.');
  }
  return {
    id: fields.id,
    title: fields.title,
    summary: fields.summary,
    system: fields.system,
    spoiler_tier: fields.spoiler_tier === 'warning' ? 'warning' : 'none',
    body: match[2].trim(),
  };
}

export function getRegistry(): Registry {
  return readJson<Registry>(join(wikiRoot, 'game-versions', 'registry.json'));
}

export function getCurrentVersion(): string {
  return getRegistry().current_game_version;
}

export function getVersions(): RegistryEntry[] {
  return getRegistry().versions;
}

export function hasGameVersion(version: string): boolean {
  return getVersions().some((entry) => entry.game_version === version);
}

export function getGameVersionMetadata(version: string): GameVersionMetadata {
  return readJson<GameVersionMetadata>(join(versionRoot(version), 'game-version.json'));
}

export function getModelManifest(version: string): ModelManifest {
  return readJson<ModelManifest>(join(versionRoot(version), 'data', 'manifest.json'));
}

export function getEntities(version: string): WikiEntity[] {
  const files = readEntityFiles(join(versionRoot(version), 'data', 'entities')).sort();
  const revision = files.map(fileRevision).join('|');
  const cached = dataCache.get(version);
  if (cached?.revision === revision) return cached.value;
  const entities = files
    .map((file) => readJson<WikiEntity>(file))
    .sort((a, b) => a.title.localeCompare(b.title, 'ko'));
  dataCache.set(version, { revision, value: entities });
  return entities;
}

export function getEntity(version: string, kind: string, slug: string): WikiEntity | undefined {
  if (!isSafeEntitySegment(kind) || !isSafeEntitySegment(slug)) return undefined;
  try {
    return readJson<WikiEntity>(join(versionRoot(version), 'data', 'entities', kind, `${slug}.json`));
  } catch (error) {
    if ((error as NodeJS.ErrnoException).code === 'ENOENT') return undefined;
    throw error;
  }
}

export function getBacklinks(version: string, entity: Pick<WikiEntity, 'kind' | 'slug'>): Backlink[] {
  const file = join(versionRoot(version), 'data', 'relations', 'backlinks.json');
  const revision = fileRevision(file);
  let mapping = backlinkCache.get(version);
  if (mapping?.revision !== revision) {
    mapping = { revision, value: readJson<Record<string, Backlink[]>>(file) };
    backlinkCache.set(version, mapping);
  }
  return mapping.value[`${entity.kind}/${entity.slug}`] ?? [];
}

export function getCategories(version: string): Category[] {
  const file = join(versionRoot(version), 'data', 'navigation', 'categories.json');
  const revision = fileRevision(file);
  const cached = categoryCache.get(version);
  if (cached?.revision === revision) return cached.value;
  const model = readJson<{ categories: Category[] }>(file);
  categoryCache.set(version, { revision, value: model.categories });
  return model.categories;
}

export function getCategory(version: string, id: string): Category | undefined {
  return getCategories(version).find((category) => category.id === id);
}

const publicCategoryByKind: Record<string, string> = {
  character: 'characters', combat: 'equipment', event: 'events', facility: 'facilities', item: 'items',
  medical: 'health', nature: 'nature', recipe: 'recipes', research: 'research', world: 'world',
};

const legacyCategoryRedirects: Record<string, string> = {
  'production-facilities': 'facilities', 'research-effects': 'research', 'characters-traits': 'characters',
  'combat-health-world': 'equipment', 'events-campaign': 'events',
};

export function categoryIdForKind(kind: string): string {
  const categoryId = publicCategoryByKind[kind];
  if (!categoryId) throw new Error(`Missing public category mapping for entity kind: ${kind}`);
  return categoryId;
}

export function canonicalCategoryId(id: string): string {
  return legacyCategoryRedirects[id] ?? id;
}

export function categoryLabel(id: string, version = getCurrentVersion()): string {
  return getCategory(version, id)?.label ?? id;
}

export function getGuides(version: string): Guide[] {
  const guideRoot = join(versionRoot(version), 'content', 'guides');
  const guides = readdirSync(guideRoot)
    .filter((name) => name.endsWith('.md'))
    .map((name) => parseGuide(readFileSync(join(guideRoot, name), 'utf8')))
    .sort((a, b) => a.title.localeCompare(b.title, 'ko'));
  return guides;
}

export function getGuide(version: string, id: string): Guide | undefined {
  return getGuides(version).find((guide) => guide.id === id);
}

export function getGuideRedirect(version: string, id: string): string | undefined {
  return getGuideNavigation(version).pages.find((page) => page.id === id)?.redirect_to;
}

export function getCategoryGuides(version: string, categoryId: string): Guide[] {
  const guideIds = getGuideNavigation(version).pages
    .filter((page) => page.category_ids.includes(categoryId) && page.directory_visibility !== 'contextual')
    .map((page) => page.id);
  return guideIds
    .map((guideId) => getGuide(version, guideId))
    .filter((guide): guide is Guide => Boolean(guide));
}

export function getWorkReferences(version: string): WorkReference[] {
  const document = readJson<WorkReferenceDocument>(join(versionRoot(version), 'content', 'work-references.json'));
  if (document.schema_version !== 1 || document.game_version !== version) {
    throw new Error('Work-reference document version is invalid.');
  }
  return document.references;
}

export function getWorkReference(version: string, id: string): WorkReference | undefined {
  return getWorkReferences(version).find((reference) => reference.id === id);
}

export function getWorkTask(version: string, referenceId: string, taskId: string): { reference: WorkReference; task: WorkTask } | undefined {
  const reference = getWorkReference(version, referenceId);
  const task = reference?.tasks.find((item) => item.id === taskId);
  return reference && task ? { reference, task } : undefined;
}

export function getNeedReferences(version: string): NeedReference[] {
  const document = readJson<NeedReferenceDocument>(join(versionRoot(version), 'content', 'need-references.json'));
  if (document.schema_version !== 1 || document.game_version !== version) {
    throw new Error('Need-reference document version is invalid.');
  }
  return document.references;
}

export function getNeedReference(version: string, id: string): NeedReference | undefined {
  return getNeedReferences(version).find((reference) => reference.id === id);
}

export function getAnatomyReferences(version: string): AnatomyReference[] {
  const documents = ['anatomy-references.json', 'special-anatomy-references.json'].map((file) =>
    readJson<AnatomyReferenceDocument>(join(versionRoot(version), 'content', file))
  );
  if (documents.some((document) => document.schema_version !== 1 || document.game_version !== version)) {
    throw new Error('Anatomy-reference document version is invalid.');
  }
  const references = documents.flatMap((document) => document.references).map(playerFacingAnatomyReference);
  const duplicateIds = references.filter((reference, index) =>
    references.findIndex((candidate) => candidate.id === reference.id) !== index
  );
  if (duplicateIds.length > 0) {
    throw new Error(`Anatomy-reference IDs must be unique: ${duplicateIds.map((reference) => reference.id).join(', ')}`);
  }
  return references;
}

export function getAnatomyReference(version: string, id: string): AnatomyReference | undefined {
  return getAnatomyReferences(version).find((reference) => reference.id === id);
}

export function getAnatomyProfileGroups(version: string): AnatomyProfileGroup[] {
  const document = readJson<AnatomyProfileGroupDocument>(join(versionRoot(version), 'content', 'anatomy-profile-groups.json'));
  if (document.schema_version !== 1 || document.game_version !== version) {
    throw new Error('Anatomy-profile-group document version is invalid.');
  }
  const groupReferenceIds = document.groups.flatMap((group) => group.reference_ids);
  const duplicateIds = groupReferenceIds.filter((id, index) => groupReferenceIds.indexOf(id) !== index);
  const specialReferenceIds = getAnatomyReferences(version)
    .filter((reference) => reference.group === '특수 부위')
    .map((reference) => reference.id);
  const unknownIds = groupReferenceIds.filter((id) => !specialReferenceIds.includes(id));
  const missingIds = specialReferenceIds.filter((id) => !groupReferenceIds.includes(id));
  if (duplicateIds.length > 0 || unknownIds.length > 0 || missingIds.length > 0) {
    throw new Error(`Anatomy-profile groups must cover each special reference exactly once. Duplicates: ${duplicateIds.join(', ')}; unknown: ${unknownIds.join(', ')}; missing: ${missingIds.join(', ')}`);
  }
  return document.groups;
}

export function getGuideNavigation(version: string): GuideNavigation {
  const navigation = readJson<GuideNavigation>(join(versionRoot(version), 'content', 'guides', 'guide-navigation.json'));
  return navigation;
}

export function getDirectory(version: string): DirectoryManifest {
  return readJson<DirectoryManifest>(join(versionRoot(version), 'content', 'directory.yml'));
}

export function getSearchAliases(version: string): SearchAliasRecord[] {
  return readJson<{ records: SearchAliasRecord[] }>(join(versionRoot(version), 'data', 'search', 'aliases.json')).records;
}

export function entityHref(entity: Pick<WikiEntity, 'kind' | 'slug'>, version?: string): string {
  return version ? `/game-versions/${version}/entry/${entity.kind}/${entity.slug}/` : `/entry/${entity.kind}/${entity.slug}/`;
}

export function categoryHref(id: string, version?: string): string {
  return version ? `/game-versions/${version}/category/${id}/` : `/category/${id}/`;
}

export function guideHref(id: string, version?: string): string {
  return version ? `/game-versions/${version}/guide/${id}/` : `/guide/${id}/`;
}

export function workHref(id: string, version?: string): string {
  return version ? `/game-versions/${version}/work/${id}/` : `/work/${id}/`;
}

export function workTaskHref(referenceId: string, taskId: string, version?: string): string {
  return version ? `/game-versions/${version}/work/${referenceId}/${taskId}/` : `/work/${referenceId}/${taskId}/`;
}

export function needHref(id: string, version?: string): string {
  return version ? `/game-versions/${version}/needs/${id}/` : `/needs/${id}/`;
}

export function anatomyHref(id?: string, version?: string): string {
  const root = version ? `/game-versions/${version}/health` : '/health';
  return id ? `${root}/${id}/` : `${root}/`;
}

export function healthCommunityHref(version?: string): string {
  return version ? `/game-versions/${version}/health-community/` : '/health-community/';
}

export function targetHref(entry: DirectoryEntry, version?: string): string {
  if (entry.target_kind === 'guide') return guideHref(entry.target_id, version);
  if (entry.target_kind === 'category') return categoryHref(entry.target_id, version);
  if (entry.target_kind === 'health-community') return healthCommunityHref(version);
  if (entry.target_kind === 'updates') return '/updates/';
  if (entry.target_kind === 'search') return '/search/';
  if (entry.target_kind === 'directory') return '/directory/';
  if (entry.target_kind === 'versions') return '/game-versions/' + (version ?? getCurrentVersion()) + '/';
  if (entry.target_kind === 'recipes') return version ? `/game-versions/${version}/recipes/` : '/recipes/';
  throw new Error(`Directory target is unsupported: ${entry.target_kind}`);
}
