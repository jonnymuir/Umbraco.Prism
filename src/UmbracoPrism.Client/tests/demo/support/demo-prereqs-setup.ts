import { execSync } from 'node:child_process';

// Inverted-polarity sibling of tests/support/aspire-prereqs-setup.ts: that file waits for ports
// to be FREE (its tests spawn their own AppHost and need exclusive control). This script assumes
// the opposite — you've already warmed the stack yourself, off-camera, and it just checks that's
// actually true before burning a recording take on a stack that isn't ready.

function tryCommand(command: string): { ok: boolean; output: string } {
  try {
    return {
      ok: true,
      output: execSync(command, { encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] })
    };
  } catch (error: unknown) {
    const e = error as { stdout?: string; stderr?: string };
    return { ok: false, output: `${e.stdout ?? ''}${e.stderr ?? ''}`.trim() };
  }
}

function listListeningPids(port: number): string[] {
  const result = tryCommand(`lsof -t -iTCP:${port} -sTCP:LISTEN`);
  if (!result.ok || !result.output.trim()) return [];
  return result.output.trim().split(/\s+/).filter(v => /^\d+$/.test(v));
}

const requiredPorts: [string, number][] = [
  ['TestSite', 44345],
  ['Keycloak proxy', 8443],
  ['MockBusinessApp', 7245]
];

export default async function globalSetup() {
  const notListening = requiredPorts
    .filter(([, port]) => listListeningPids(port).length === 0)
    .map(([name, port]) => `${name} (${port})`);

  if (notListening.length > 0) {
    throw new Error(
      `Demo recording requires the Aspire stack to already be warmed and running. ` +
        `These ports are not listening: ${notListening.join(', ')}.\n` +
        `Start the stack first (e.g. \`dotnet run --project src/UmbracoPrism.AppHost\`), wait ` +
        `for the dashboard to go green, then run this script — see tests/demo/README.md.`
    );
  }
}
