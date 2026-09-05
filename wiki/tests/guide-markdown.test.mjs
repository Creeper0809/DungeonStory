import assert from 'node:assert/strict';
import { readdir, readFile } from 'node:fs/promises';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

import {
  extractFencedCode,
  fencedCodeForBlock,
  renderFencedCode,
  renderInlineMarkdown,
} from '../src/lib/guide-markdown.mjs';

test('multiline formula fences keep blank lines and become independent blocks', () => {
  const source = [
    '앞 문장',
    '```text',
    '무기 준비 점수 = max(0,',
    '  초당 피해 × 관통력)',
    '',
    '방패 준비 점수 = 방어력 × 막기 확률',
    '```',
    '뒤 문장',
  ].join('\n');

  const extracted = extractFencedCode(source);

  assert.equal(extracted.source, '앞 문장\n\n@@GUIDE_CODE_0@@\n\n뒤 문장');
  assert.deepEqual(extracted.blocks, [{
    language: 'text',
    body: '무기 준비 점수 = max(0,\n  초당 피해 × 관통력)\n\n방패 준비 점수 = 방어력 × 막기 확률',
  }]);
  assert.equal(fencedCodeForBlock('@@GUIDE_CODE_0@@', extracted.blocks), extracted.blocks[0]);
});

test('fenced code escapes markup and retains language metadata', () => {
  assert.equal(
    renderFencedCode({ language: 'text', body: '피해 < 방어력 && 값 > 0' }),
    '<pre class="guide-code"><code class="language-text">피해 &lt; 방어력 &amp;&amp; 값 &gt; 0</code></pre>',
  );
});

test('inline code and strong emphasis render without interpreting code contents', () => {
  const rendered = renderInlineMarkdown(
    '**전투력**은 `피해 < 방어력 && **고정**`이며 [신체 구조](/health/)를 따른다.',
    (href) => `/game-versions/0.0.1v${href}`,
  );

  assert.equal(
    rendered,
    '<strong>전투력</strong>은 <code>피해 &lt; 방어력 &amp;&amp; **고정**</code>이며 <a href="/game-versions/0.0.1v/health/">신체 구조</a>를 따른다.',
  );
});

test('malformed and unclosed fences fail clearly', () => {
  assert.throws(() => extractFencedCode('```text extra\n값\n```'), /Unsupported fenced-code marker/);
  assert.throws(() => extractFencedCode('```text\n값'), /Unclosed fenced-code block/);
});

test('every versioned guide has well-formed fenced code', async () => {
  const wikiRoot = fileURLToPath(new URL('..', import.meta.url));
  const versionsRoot = path.join(wikiRoot, 'game-versions');
  const versions = (await readdir(versionsRoot, { withFileTypes: true })).filter((entry) => entry.isDirectory());
  const guideFiles = [];

  for (const version of versions) {
    const guidesRoot = path.join(versionsRoot, version.name, 'content', 'guides');
    const entries = await readdir(guidesRoot, { withFileTypes: true });
    guideFiles.push(...entries.filter((entry) => entry.isFile() && entry.name.endsWith('.md')).map((entry) => path.join(guidesRoot, entry.name)));
  }

  assert.ok(guideFiles.length > 0, 'Expected at least one versioned guide.');
  for (const guideFile of guideFiles) {
    const source = await readFile(guideFile, 'utf8');
    assert.doesNotThrow(() => extractFencedCode(source), guideFile);
  }
});
