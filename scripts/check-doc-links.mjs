#!/usr/bin/env node
// Fails when a Markdown file links to a repo file that doesn't exist, or to a heading anchor that
// doesn't exist in the target. External URLs are not fetched. Archived docs and agent tooling
// folders are skipped: they are kept for history, not as current documentation.
import { execFileSync } from 'node:child_process';
import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs';
import { dirname, join, normalize, relative, resolve } from 'node:path';

const root = resolve(process.argv[2] ?? '.');

// A link must point at a file git tracks: a gitignored file exists on the author's machine but
// not on GitHub, in CI, or in anyone else's checkout.
const tracked = trackedPaths();
function trackedPaths() {
  try {
    const files = execFileSync('git', ['ls-files', '-z'], { cwd: root, encoding: 'utf8', maxBuffer: 1 << 28 })
      .split('\0').filter(Boolean).map((file) => resolve(root, file));
    const paths = new Set(files);
    for (const file of files) {
      for (let dir = dirname(file); dir.startsWith(root) && !paths.has(dir); dir = dirname(dir)) paths.add(dir);
    }
    paths.add(root);
    return paths;
  } catch {
    return null; // not a git checkout: fall back to the filesystem
  }
}
const isPresent = (path) => (tracked ? tracked.has(path.replace(/[\\/]+$/, '')) : existsSync(path));
const skipDirs = new Set(['node_modules', 'bin', 'obj', '.git', 'artifacts', 'test-results', 'archive', '.claude', '.copilot', '.squad', '.agents', 'agents']);

function* markdownFiles(dir) {
  for (const entry of readdirSync(dir)) {
    if (skipDirs.has(entry)) continue;
    const full = join(dir, entry);
    if (statSync(full).isDirectory()) yield* markdownFiles(full);
    else if (entry.endsWith('.md')) yield full;
  }
}

// GitHub's heading slugs: lowercase, drop punctuation other than hyphens, spaces become hyphens,
// and a repeated heading gets -1, -2, ... appended.
function anchorsOf(file) {
  const anchors = new Set();
  const seen = new Map();
  let inFence = false;
  for (const line of readFileSync(file, 'utf8').split('\n')) {
    if (line.trimStart().startsWith('```')) inFence = !inFence;
    if (inFence) continue;
    for (const m of line.matchAll(/<a\s+(?:id|name)="([^"]+)"/g)) anchors.add(m[1]);
    const heading = /^#{1,6}\s+(.*?)\s*#*\s*$/.exec(line);
    if (!heading) continue;
    const base = heading[1]
      .replace(/\[([^\]]*)\]\([^)]*\)/g, '$1')
      .toLowerCase()
      .replace(/[^\p{L}\p{N}\- _]/gu, '')
      .replace(/ /g, '-');
    const count = seen.get(base) ?? 0;
    seen.set(base, count + 1);
    anchors.add(count === 0 ? base : `${base}-${count}`);
  }
  return anchors;
}

const problems = [];
const linkPattern = /!?\[[^\]]*\]\(\s*<?([^)\s>]+)>?(?:\s+"[^"]*")?\s*\)/g;
for (const file of markdownFiles(root)) {
  let inFence = false;
  readFileSync(file, 'utf8').split('\n').forEach((line, index) => {
    if (line.trimStart().startsWith('```')) inFence = !inFence;
    if (inFence) return;
    for (const [, target] of line.matchAll(linkPattern)) {
      if (/^[a-z][a-z0-9+.-]*:/i.test(target)) continue;
      const [path, fragment] = target.split('#');
      const resolved = path ? normalize(join(dirname(file), decodeURIComponent(path.split('?')[0]))) : file;
      const where = `${relative(root, file)}:${index + 1}`;
      if (!isPresent(resolved)) {
        problems.push(`${where}: missing file ${target}`);
      } else if (fragment && resolved.endsWith('.md') && !anchorsOf(resolved).has(decodeURIComponent(fragment).toLowerCase())) {
        problems.push(`${where}: missing anchor ${target}`);
      }
    }
  });
}

if (problems.length > 0) {
  console.error(`Broken documentation links (${problems.length}):\n  ${problems.join('\n  ')}`);
  process.exit(1);
}
console.log('Documentation links OK');
