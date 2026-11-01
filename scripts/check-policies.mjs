// Enforces the size budgets (see STANDARDS.md in jonnymuir/engineering-standards, "Simplicity").
//
//   node scripts/check-policies.mjs            fail if a file or C# method grew past its budget or baseline, or a new
//                                              `as unknown as` cast appeared (a cast that hides a model mismatch)
//   node scripts/check-policies.mjs --update   lower the baseline to today's sizes (never raises it or adds paths)
//
// Budgets: C# file 400 lines, C# method 60 lines; TypeScript/JavaScript file 500 lines. Anything already over is
// recorded in .policy/size-baseline.json at its current size and may only shrink.
import { execFileSync } from 'node:child_process';
import { readFileSync, writeFileSync } from 'node:fs';
import { join, relative } from 'node:path';

const root = new URL('..', import.meta.url).pathname.replace(/\/$/, '');
const baselinePath = join(root, '.policy', 'size-baseline.json');
const FILE_BUDGET = { '.cs': 400, '.ts': 500, '.tsx': 500, '.js': 500, '.mjs': 500 };
const METHOD_BUDGET = 60;
const SKIP_DIRS = new Set(['node_modules', 'bin', 'obj', 'generated', 'storybook-static', 'test-results', '.git', 'dist', 'fixtures', 'tests', 'lib', 'wwwroot']);
const SKIP_FILE = /\.(test|stories|spec)\.(ts|tsx)$|\.d\.ts$/;

// Tracked files only, so build output and runtime state (generated models, caches) are never measured.
function* walk(dir) {
  const files = execFileSync('git', ['ls-files', '-z'], { cwd: dir, encoding: 'utf8', maxBuffer: 1 << 28 }).split('\0').filter(Boolean);
  for (const file of files) {
    const parts = file.split('/');
    const name = parts.at(-1);
    if (parts.some((part) => SKIP_DIRS.has(part) || part.endsWith('Tests')) || SKIP_FILE.test(name) || !FILE_BUDGET[name.slice(name.lastIndexOf('.'))]) continue;
    yield join(dir, file);
  }
}

/** Longest C# member in lines, judged by formatted-code structure: a member starts at an indented modifier line and ends at the matching closing brace. */
function longestCSharpMethod(lines) {
  let longest = 0;
  const start = /^(\s{4,12})(public|private|protected|internal)\b[^;=]*\([^;]*$/;
  for (let i = 0; i < lines.length; i++) {
    const match = start.exec(lines[i]);
    if (!match || /\b(class|record|struct|interface|enum)\b/.test(lines[i])) continue;
    const indent = match[1];
    let end = -1;
    for (let j = i; j < lines.length && j < i + 2000; j++) {
      if (j > i && lines[j] === `${indent}}`) { end = j; break; }
      if (j > i && lines[j].startsWith(indent) && /;\s*$/.test(lines[j]) && !lines[j].startsWith(`${indent} `) && j > i) { end = j; break; }
    }
    if (end > 0) longest = Math.max(longest, end - i + 1);
  }
  return longest;
}

function measure() {
  const files = {};
  const methods = {};
  const casts = {};
  for (const path of walk(root)) {
    const rel = relative(root, path);
    const lines = readFileSync(path, 'utf8').split('\n');
    const count = lines.at(-1) === '' ? lines.length - 1 : lines.length;
    const budget = FILE_BUDGET[path.slice(path.lastIndexOf('.'))];
    if (count > budget) files[rel] = count;
    if (/\.tsx?$/.test(path)) {
      const found = lines.filter((line) => line.includes('as unknown as')).length;
      if (found > 0) casts[rel] = found;
    }
    if (path.endsWith('.cs')) {
      const longest = longestCSharpMethod(lines);
      if (longest > METHOD_BUDGET) methods[rel] = longest;
    }
  }
  return { files, methods, casts };
}

function compare(kind, actual, baseline, budget, errors, tighten) {
  for (const [path, size] of Object.entries(actual)) {
    const allowed = baseline[path];
    if (allowed === undefined) errors.push(`${path}: ${size} ${kind} is over the budget of ${budget}. Split it (new code must stay within budget).`);
    else if (size > allowed) errors.push(`${path}: grew to ${size} ${kind} (baseline ${allowed}). It may only shrink.`);
    else if (size < allowed) tighten.push(`${path}: ${allowed} -> ${size}`);
  }
  for (const path of Object.keys(baseline)) {
    if (!(path in actual)) tighten.push(`${path}: now within budget, remove from baseline`);
  }
}

function checkSizes(update) {
  const actual = measure();
  let baseline = { files: {}, methods: {}, casts: {} };
  try {
    baseline = JSON.parse(readFileSync(baselinePath, 'utf8'));
  } catch (error) {
    if (error.code !== 'ENOENT') throw error;
  }
  baseline.casts ??= {};
  if (update) {
    const next = { files: {}, methods: {}, casts: {} };
    for (const kind of ['files', 'methods', 'casts']) {
      // Only existing entries are lowered or dropped. Adding a path (e.g. after splitting a file) is a deliberate,
      // reviewed edit of the JSON, never something --update does on its own.
      for (const [path, size] of Object.entries(actual[kind])) if (path in baseline[kind]) next[kind][path] = Math.min(size, baseline[kind][path]);
    }
    writeFileSync(baselinePath, `${JSON.stringify(next, null, 2)}\n`);
    console.log(`Baseline written: ${Object.keys(next.files).length} large files, ${Object.keys(next.methods).length} files with a long method.`);
    return 0;
  }
  const errors = [];
  const tighten = [];
  compare('lines', actual.files, baseline.files, 'the file budget', errors, tighten);
  compare('lines (longest method)', actual.methods, baseline.methods, `${METHOD_BUDGET}`, errors, tighten);
  compare('`as unknown as` casts', actual.casts, baseline.casts, '0', errors, tighten);
  for (const message of errors) console.error(`FAIL ${message}`);
  if (errors.length === 0 && tighten.length > 0) {
    console.error('FAIL baseline is stale; run `node scripts/check-policies.mjs --update` and commit it:');
    for (const message of tighten) console.error(`  ${message}`);
    return 1;
  }
  if (errors.length === 0) console.log('Size policies hold.');
  return errors.length === 0 ? 0 : 1;
}

process.exitCode = checkSizes(process.argv[2] === '--update');
