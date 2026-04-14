import { spawn, spawnSync, type ChildProcessWithoutNullStreams } from 'node:child_process';
import http from 'node:http';
import https from 'node:https';
import path from 'node:path';

const repoRoot = path.resolve(import.meta.dirname, '../../../..');
const appHostProject = path.join(repoRoot, 'src/UmbracoPrism.AppHost');
const isolatedTestSiteRuntimeRoot = path.join(repoRoot, 'artifacts', 'aspire', 'testsite-runtime');

const readinessChecks = [
  { name: 'Aspire dashboard', url: 'https://localhost:17214/', allowedStatuses: [200, 302] },
  {
    // Smoke check: prove the real Razor home page is serving, without coupling readiness to page copy.
    name: 'TestSite home marker',
    url: 'https://localhost:44345/',
    bodyIncludes: ['data-prism-home-ready="true"']
  },
  {
    // Authoritative readiness gate: the seeded Umbraco route/auth contract has converged.
    name: 'TestSite seed contract',
    url: 'https://localhost:44345/api/prism/downstream-demo/seed-contract-ready',
    bodyIncludes: [
      '"ready":true',
      '"routeContractReady":true',
      '"challengePath":"/auth/login?ReturnUrl=%2Fmy-workflows"'
    ]
  },
  {
    // Behavioural confirmation: the protected authored URL now challenges with the expected return target.
    name: 'Workflow hub seed',
    url: 'https://localhost:44345/my-workflows',
    allowedStatuses: [302],
    headerIncludes: [{ name: 'location', valueIncludes: '/auth/login?ReturnUrl=%2Fmy-workflows' }]
  },
  {
    name: 'Keycloak',
    url: 'https://localhost:8443/realms/prism-dev/.well-known/openid-configuration',
    bodyIncludes: ['"issuer":"https://localhost:8443/realms/prism-dev"']
  },
  { name: 'MockBusinessApp', url: 'https://localhost:7245/api/backoffice/me', allowedStatuses: [401] }
] as const;

const requiredPorts = [
  { name: 'Aspire dashboard', port: 17214 },
  { name: 'Aspire dashboard HTTP', port: 15135 },
  { name: 'Aspire dashboard OTLP', port: 21233 },
  { name: 'Aspire resource service', port: 22194 },
  { name: 'TestSite', port: 44345 },
  { name: 'Keycloak proxy', port: 8443 },
  { name: 'MockBusinessApp', port: 7245 }
] as const;

type ProbeResult = {
  status: number | null;
  headers: Record<string, string>;
  body: string;
};

type ReadinessStatus = {
  check: (typeof readinessChecks)[number];
  response: ProbeResult;
  ok: boolean;
  failures: string[];
};

export class LiveAppHost {
  private child: ChildProcessWithoutNullStreams | undefined;
  private resetTestSiteRuntimeOnNextStart = true;
  private readonly logs: string[] = [];

  async start(): Promise<void> {
    if (this.child) {
      return;
    }

    await this.ensurePortsAreAvailable();

    this.child = spawn('dotnet', ['run', '--project', appHostProject], {
      cwd: repoRoot,
      detached: true,
      env: {
        ...process.env,
        DOTNET_CLI_TELEMETRY_OPTOUT: process.env.DOTNET_CLI_TELEMETRY_OPTOUT ?? '1',
        PRISM_TESTSITE_RUNTIME_ROOT: isolatedTestSiteRuntimeRoot,
        PRISM_TESTSITE_RESET_RUNTIME: this.resetTestSiteRuntimeOnNextStart ? 'true' : 'false'
      },
      stdio: ['ignore', 'pipe', 'pipe']
    });

    this.child.stdout.on('data', chunk => this.captureLog('stdout', chunk));
    this.child.stderr.on('data', chunk => this.captureLog('stderr', chunk));

    try {
      await this.waitForReadiness();
      this.resetTestSiteRuntimeOnNextStart = false;
    } catch (error) {
      await this.stop().catch(() => undefined);
      throw error;
    }
  }

  async restart(): Promise<void> {
    await this.stop();
    await this.start();
  }

  async stop(): Promise<void> {
    const child = this.child;
    this.child = undefined;

    if (child) {
      const exited = waitForExit(child);

      sendSignal(child.pid, 'SIGINT');
      const graceful = await Promise.race([exited.then(() => true), delay(30_000).then(() => false)]);

      if (!graceful) {
        sendSignal(child.pid, 'SIGTERM');
      }

      const terminated = await Promise.race([exited.then(() => true), delay(30_000).then(() => false)]);
      if (!terminated) {
        sendSignal(child.pid, 'SIGKILL');
        await exited;
      }
    }

    if (await waitForPortsToStop(30_000)) {
      return;
    }

    await terminatePortListeners('SIGTERM');
    if (await waitForPortsToStop(30_000)) {
      return;
    }

    await terminatePortListeners('SIGKILL');
    if (await waitForPortsToStop(30_000)) {
      return;
    }

    throw new Error(`Timed out waiting for Aspire localhost ports to stop.\n\nRecent logs:\n${this.formatLogs()}`);
  }

  private captureLog(stream: 'stdout' | 'stderr', chunk: Buffer): void {
    const lines = chunk
      .toString()
      .split(/\r?\n/)
      .map(line => line.trim())
      .filter(Boolean)
      .map(line => `[${stream}] ${line}`);

    this.logs.push(...lines);
    if (this.logs.length > 200) {
      this.logs.splice(0, this.logs.length - 200);
    }
  }

  private async ensurePortsAreAvailable(): Promise<void> {
    let occupied = getOccupiedPorts();
    if (occupied.length === 0) {
      return;
    }

    const waitStart = Date.now();
    while (occupied.length > 0 && Date.now() - waitStart < 45_000) {
      await delay(1_000);
      occupied = getOccupiedPorts();
    }

    if (occupied.length === 0) {
      return;
    }

    throw new Error(
      `Live localhost auth tests need exclusive control of the Aspire-hosted stack so the suite can reset ` +
      `the isolated TestSite runtime root at ${isolatedTestSiteRuntimeRoot}. ` +
      `Port(s) already in use: ${occupied
        .map(port => `${port.name} (${port.port}) [pid ${port.pids.join(', ')}]`)
        .join(', ')}.`
    );
  }

  private async waitFor(
    condition: () => Promise<boolean>,
    timeoutMs: number,
    label: string
  ): Promise<void> {
    const start = Date.now();

    while (Date.now() - start < timeoutMs) {
      if (await condition()) {
        return;
      }

      await delay(1_000);
    }

    throw new Error(`Timed out waiting for ${label}.\n\nRecent logs:\n${this.formatLogs()}`);
  }

  private formatLogs(): string {
    return this.logs.length > 0 ? this.logs.join('\n') : '(no AppHost logs captured)';
  }

  private async waitForReadiness(): Promise<void> {
    const timeoutMs = 240_000;
    const start = Date.now();
    let latestStatuses: ReadinessStatus[] = [];

    while (Date.now() - start < timeoutMs) {
      latestStatuses = await this.getReadinessStatuses();
      if (latestStatuses.every(status => status.ok)) {
        return;
      }

      await delay(1_000);
    }

    throw new Error(
      `Timed out waiting for Aspire localhost stack to become ready.\n\n` +
        `Readiness status:\n${formatReadinessStatuses(latestStatuses)}\n\nRecent logs:\n${this.formatLogs()}`
    );
  }

  private async getReadinessStatuses(): Promise<ReadinessStatus[]> {
    return Promise.all(
      readinessChecks.map(async check => {
        const response = await probe(check.url);
        const failures: string[] = [];
        const allowedStatuses = check.allowedStatuses ?? [200];

        if (response.status === null) {
          failures.push('no HTTP response');
        } else if (!allowedStatuses.includes(response.status as never)) {
          failures.push(`status ${response.status} not in [${allowedStatuses.join(', ')}]`);
        }

        for (const expected of check.headerIncludes ?? []) {
          const actual = response.headers[expected.name] ?? '';
          if (!actual.includes(expected.valueIncludes)) {
            failures.push(`header ${expected.name} missing ${JSON.stringify(expected.valueIncludes)}`);
          }
        }

        for (const text of check.bodyIncludes ?? []) {
          if (!response.body.includes(text)) {
            failures.push(`body missing ${JSON.stringify(text)}`);
          }
        }

        return {
          check,
          response,
          ok: failures.length === 0,
          failures
        };
      })
    );
  }
}

async function probe(urlString: string): Promise<ProbeResult> {
  const url = new URL(urlString);
  const client = url.protocol === 'https:' ? https : http;

  return new Promise(resolve => {
    const request = client.request(
      url,
      {
        method: 'GET',
        rejectUnauthorized: false
      },
      response => {
        let body = '';

        response.setEncoding('utf8');
        response.on('data', chunk => {
          if (body.length < 8_192) {
            body += chunk;
          }
        });
        response.on('end', () =>
          resolve({
            status: response.statusCode ?? null,
            headers: Object.fromEntries(
              Object.entries(response.headers).map(([name, value]) => [name, Array.isArray(value) ? value.join(', ') : value ?? ''])
            ),
            body
          })
        );
      }
    );

    request.setTimeout(5_000, () => {
      request.destroy();
      resolve({ status: null, headers: {}, body: '' });
    });

    request.on('error', () => resolve({ status: null, headers: {}, body: '' }));
    request.end();
  });
}

function formatReadinessStatuses(statuses: ReadinessStatus[]): string {
  if (statuses.length === 0) {
    return '(no readiness probes captured)';
  }

  return statuses
    .map(({ check, response, ok, failures }) => {
      const statusLabel = response.status === null ? 'no response' : `HTTP ${response.status}`;
      const details = ok ? 'ready' : failures.join('; ');
      return `- ${check.name}: ${statusLabel} — ${details}`;
    })
    .join('\n');
}

function waitForExit(child: ChildProcessWithoutNullStreams): Promise<void> {
  return new Promise(resolve => {
    if (child.exitCode !== null) {
      resolve();
      return;
    }

    child.once('exit', () => resolve());
  });
}

async function waitForPortsToStop(timeoutMs: number): Promise<boolean> {
  const start = Date.now();

  while (Date.now() - start < timeoutMs) {
    if (getOccupiedPorts().length === 0) {
      return true;
    }

    await delay(1_000);
  }

  return false;
}

async function terminatePortListeners(signal: 'SIGTERM' | 'SIGKILL'): Promise<void> {
  const pids = new Set<number>();

  for (const { port } of requiredPorts) {
    for (const pid of findListeningPids(port)) {
      pids.add(pid);
    }
  }

  for (const pid of pids) {
    sendSignal(pid, signal);
  }
}

function findListeningPids(port: number): number[] {
  const result = spawnSync('lsof', ['-t', `-iTCP:${port}`, '-sTCP:LISTEN'], {
    encoding: 'utf8'
  });

  if (result.status !== 0 || !result.stdout.trim()) {
    return [];
  }

  return result.stdout
    .trim()
    .split(/\s+/)
    .map(value => Number(value))
    .filter(pid => Number.isInteger(pid));
}

function getOccupiedPorts(): Array<{ name: string; port: number; pids: number[] }> {
  return requiredPorts
    .map(({ name, port }) => ({
      name,
      port,
      pids: findListeningPids(port)
    }))
    .filter(port => port.pids.length > 0);
}

function sendSignal(pid: number | undefined, signal: 'SIGINT' | 'SIGTERM' | 'SIGKILL'): void {
  if (!pid) {
    return;
  }

  try {
    process.kill(pid, signal);
  } catch (error) {
    if (!isMissingProcess(error)) {
      throw error;
    }
  }
}

function delay(ms: number): Promise<void> {
  return new Promise(resolve => setTimeout(resolve, ms));
}

function isMissingProcess(error: unknown): boolean {
  return error instanceof Error && 'code' in error && error.code === 'ESRCH';
}
