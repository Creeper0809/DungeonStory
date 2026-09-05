const FENCED_CODE_TOKEN = /^@@GUIDE_CODE_(\d+)@@$/;

/**
 * @typedef {{ language: string, body: string }} FencedCodeBlock
 */

/**
 * Escapes text before it is inserted through Astro's `set:html` directive.
 *
 * @param {string} value
 * @returns {string}
 */
export function escapeHtml(value) {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}

/**
 * Pulls fenced blocks out before the guide renderer splits prose on blank lines.
 * The returned tokens always occupy their own block, even when the source omits
 * blank lines around a fence.
 *
 * @param {string} value
 * @returns {{ source: string, blocks: FencedCodeBlock[] }}
 */
export function extractFencedCode(value) {
  if (/^@@GUIDE_CODE_\d+@@$/m.test(value)) {
    throw new Error('Guide source contains a reserved fenced-code token.');
  }

  const lines = value.replace(/\r\n?/g, '\n').split('\n');
  /** @type {string[]} */
  const output = [];
  /** @type {FencedCodeBlock[]} */
  const blocks = [];
  /** @type {{ language: string, lines: string[] } | null} */
  let active = null;

  for (const line of lines) {
    if (active) {
      if (/^[ \t]{0,3}```[ \t]*$/.test(line)) {
        const index = blocks.push({ language: active.language, body: active.lines.join('\n') }) - 1;
        output.push('', `@@GUIDE_CODE_${index}@@`, '');
        active = null;
      } else {
        active.lines.push(line);
      }
      continue;
    }

    const opening = line.match(/^[ \t]{0,3}```([A-Za-z0-9_-]*)[ \t]*$/);
    if (opening) {
      active = { language: opening[1], lines: [] };
      continue;
    }
    if (/^[ \t]{0,3}```/.test(line)) {
      throw new Error(`Unsupported fenced-code marker: ${line.trim()}`);
    }
    output.push(line);
  }

  if (active) {
    throw new Error('Unclosed fenced-code block in guide source.');
  }

  return {
    source: output.join('\n').replace(/\n{3,}/g, '\n\n'),
    blocks,
  };
}

/**
 * @param {string} block
 * @param {FencedCodeBlock[]} blocks
 * @returns {FencedCodeBlock | null}
 */
export function fencedCodeForBlock(block, blocks) {
  const match = block.trim().match(FENCED_CODE_TOKEN);
  if (!match) return null;

  const codeBlock = blocks[Number(match[1])];
  if (!codeBlock) {
    throw new Error(`Missing fenced-code block for token ${block.trim()}.`);
  }
  return codeBlock;
}

/**
 * @param {FencedCodeBlock} block
 * @returns {string}
 */
export function renderFencedCode(block) {
  const languageClass = block.language ? ` class="language-${block.language}"` : '';
  return `<pre class="guide-code"><code${languageClass}>${escapeHtml(block.body)}</code></pre>`;
}

/**
 * Renders the small, intentional inline Markdown subset used by guide sources.
 * Code spans are protected first so Markdown inside them remains literal.
 *
 * @param {string} value
 * @param {(href: string) => string} [resolveHref]
 * @returns {string}
 */
export function renderInlineMarkdown(value, resolveHref = (href) => href) {
  /** @type {string[]} */
  const codeSpans = [];
  const protectedValue = value.replace(/`([^`\r\n]+)`/g, (_, code) => {
    const index = codeSpans.push(code) - 1;
    return `\uE000GUIDE_INLINE_CODE_${index}\uE001`;
  });

  return escapeHtml(protectedValue)
    .replace(/\*\*([^*\r\n]+)\*\*/g, '<strong>$1</strong>')
    .replace(
      /\[([^\]]+)\]\((\/[A-Za-z0-9._~/-]+\/(?:#[A-Za-z0-9-]+)?)\)/g,
      (_, label, path) => `<a href="${escapeHtml(resolveHref(path))}">${label}</a>`,
    )
    .replace(/\uE000GUIDE_INLINE_CODE_(\d+)\uE001/g, (_, index) => `<code>${escapeHtml(codeSpans[Number(index)])}</code>`);
}
