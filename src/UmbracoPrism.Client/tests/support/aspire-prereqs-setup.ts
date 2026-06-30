import { execSync } from 'node:child_process';

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
  ['Aspire dashboard', 17214],
  ['Aspire dashboard HTTP (legacy)', 15135],
  ['Aspire dashboard OTLP', 21233],
  ['Aspire resource service', 22194],
  ['TestSite', 44345],
  ['Keycloak proxy', 8443],
  ['MockBusinessApp', 7245]
];

function delay(ms: number): Promise<void> {
  return new Promise(resolve => setTimeout(resolve, ms));
}

export default async function globalSetup() {
  const problems: string[] = [];

  const dotnetResult = tryCommand('dotnet --version');
  if (!dotnetResult.ok) {
    problems.push(
      'The .NET SDK is not available on PATH. Install the .NET 10 SDK before running live-stack tests.'
    );
  } else {
    const major = Number(dotnetResult.output.trim().split('.')[0]);
    if (!Number.isInteger(major) || major < 10) {
      problems.push(
        `Detected .NET SDK ${dotnetResult.output.trim()}. Install the .NET 10 SDK before running live-stack tests.`
      );
    }
  }

  const dockerResult = tryCommand('docker info');
  if (!dockerResult.ok) {
    problems.push(
      'Docker is not available. Start Docker Desktop (or another OCI runtime on the docker CLI) before running live-stack tests.'
    );
  }

  if (problems.length > 0) {
    throw new Error(
      `Live-stack test prerequisites are missing:\n` +
        problems.map(p => `  - ${p}`).join('\n') +
        '\n\nRun via the npm script to get a clearer diagnostic: npm run test:playwright:localhost-auth'
    );
  }

  // Wait up to 45 s for any previously-occupied ports to clear.
  const getOccupied = () =>
    requiredPorts
      .filter(([, port]) => listListeningPids(port).length > 0)
      .map(([name, port]) => `${name} (${port})`);

  let occupied = getOccupied();
  const waitStart = Date.now();
  while (occupied.length > 0 && Date.now() - waitStart < 45_000) {
    await delay(1_000);
    occupied = getOccupied();
  }

  if (occupied.length > 0) {
    throw new Error(
      `Live-stack tests need exclusive control of the Aspire-hosted stack. ` +
        `These ports are still in use: ${occupied.join(', ')}.\n` +
        `Stop any running Aspire instance before running the suite.`
    );
  }
}
