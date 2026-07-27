import { spawn, spawnSync, type ChildProcessWithoutNullStreams } from 'node:child_process';
import http from 'node:http';
import https from 'node:https';
import path from 'node:path';

const repoRoot = path.resolve(import.meta.dirname, '../../../..');
const appHostProject = path.join(repoRoot, 'src/UmbracoPrism.AppHost');
const isolatedTestSiteRuntimeRoot = path.join(repoRoot, 'artifacts', 'aspire', 'testsite-runtime');
const readinessTimeoutMs = 480_000; // 8 minutes — CI runners vary; 5min was too tight on slow runners
const readinessPollIntervalMs = 10_000;
const readinessCheckpointIntervalMs = 30_000;
const probeTimeoutMs = 5_000;
// A wedged resource (port still listening, process not actually responding) doesn't recover on
// its own — waiting out the full readinessTimeoutMs just burns the CI budget. If the exact same
// set of checks has been pending this long with zero progress, restart the whole stack once
// instead of waiting it out.
const stallRecoveryThresholdMs = 180_000; // 3 minutes
const maxRecoveryAttempts = 1;

// Known patterns in Umbraco's default "unseeded" splash page — when we see these,
// classify as "still seeding" (not a hard failure). CI hardware timing can push
// the seed task past the probe's initial retry window, so the probe must distinguish
// "Umbraco booting" from "Umbraco up but unseeded" from "Umbraco fully ready".
// See: PR #52 (squad/planning-service-blueprint-editor-walkthrough), CI run 25987849590.
const umbracoUnseededPageMarkers = [
  '<title>Umbraco: No Published Content</title>',
  'Welcome to your Umbraco installation',
  'This page is intentionally left ugly',
  'You have <strong>no content'
] as const;
const responsePreviewLength = 2000;
const notableHeaders = ['location', 'content-type', 'server', 'x-powered-by'] as const;
const resourceLogTerms = ['keycloak', 'keycloak-proxy', 'testsite', 'businessapp', 'aspire-dashboard'] as const;

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
      '"challengePath":"/auth/login?ReturnUrl=%2Fmy-service-requests"'
    ]
  },
  {
    // Behavioural confirmation: the protected authored URL now challenges with the expected return target.
    // Doubles as a Razor view-compilation warmup so the first test doesn't pay the cold-render cost.
    name: 'ServiceBlueprint hub seed',
    url: 'https://localhost:44345/my-service-requests',
    allowedStatuses: [302],
    headerIncludes: [{ name: 'location', valueIncludes: '/auth/login?ReturnUrl=%2Fmy-service-requests' }]
  },
  {
    // Dashboard route warmup. MemberDashboardController has [Authorize]; the cookie auth
    // scheme's challenge issues the canonical redirect with ReturnUrl=%2Fdashboard
    // (the controller's literal Redirect() is dead code — Authorize fires first).
    name: 'Dashboard route',
    url: 'https://localhost:44345/dashboard',
    allowedStatuses: [302],
    headerIncludes: [{ name: 'location', valueIncludes: '/auth/login?ReturnUrl=%2Fdashboard' }]
  },
  {
    // Community-enquiry service blueprint page warmup (used by both localhost-auth and service-blueprint-all-demos suites).
    name: 'Community enquiry service blueprint route',
    url: 'https://localhost:44345/get-in-touch',
    allowedStatuses: [302],
    headerIncludes: [{ name: 'location', valueIncludes: '/auth/login?ReturnUrl=%2Fget-in-touch' }]
  },
  {
    // Planning service blueprint page warmup (service-blueprint-gds-journey + service-blueprint-all-demos).
    name: 'Planning service blueprint route',
    url: 'https://localhost:44345/apply-for-planning-permission',
    allowedStatuses: [302],
    headerIncludes: [{ name: 'location', valueIncludes: '/auth/login?ReturnUrl=%2Fapply-for-planning-permission' }]
  },
  {
    // Payment-demo service blueprint page warmup (service-blueprint-all-demos).
    name: 'Payment demo service blueprint route',
    url: 'https://localhost:44345/payment-demo',
    allowedStatuses: [302],
    headerIncludes: [{ name: 'location', valueIncludes: '/auth/login?ReturnUrl=%2Fpayment-demo' }]
  },
  {
    // Information-request service blueprint page warmup (service-blueprint-all-demos).
    name: 'Information request service blueprint route',
    url: 'https://localhost:44345/request-information',
    allowedStatuses: [302],
    headerIncludes: [{ name: 'location', valueIncludes: '/auth/login?ReturnUrl=%2Frequest-information' }]
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
  { name: 'Aspire dashboard HTTP (legacy)', port: 15135 },
  { name: 'Aspire dashboard OTLP', port: 21233 },
  { name: 'Aspire resource service', port: 22194 },
  { name: 'Keycloak upstream', port: 8080 },
  { name: 'TestSite', port: 44345 },
  { name: 'Keycloak proxy', port: 8443 },
  { name: 'MockBusinessApp', port: 7245 }
] as const;

type ProbeResult = {
  status: number | null;
  headers: Record<string, string>;
  body: string;
  error: string | null;
};

type ReadinessStatus = {
  check: (typeof readinessChecks)[number];
  response: ProbeResult;
  ok: boolean;
  failures: string[];
};

class StallDetectedError extends Error {
  constructor(
    readonly reason: string,
    readonly statuses: ReadinessStatus[]
  ) {
    super(`Readiness stalled: ${reason}`);
    this.name = 'StallDetectedError';
  }
}

export class LiveAppHost {
  private child: ChildProcessWithoutNullStreams | undefined;
  private resetTestSiteRuntimeOnNextStart = true;
  private readonly logs: string[] = [];

  async start(): Promise<void> {
    if (this.child) {
      return;
    }

    await this.attemptStart(0);
  }

  private async attemptStart(recoveryAttempt: number): Promise<void> {
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

      if (error instanceof StallDetectedError && recoveryAttempt < maxRecoveryAttempts) {
        console.log(
          `[readiness] stack appears wedged (${error.reason}); restarting once and retrying ` +
            `(recovery attempt ${recoveryAttempt + 1}/${maxRecoveryAttempts}).`
        );
        await this.attemptStart(recoveryAttempt + 1);
        return;
      }

      if (error instanceof StallDetectedError) {
        throw new Error(
          this.buildTimeoutDiagnostics(`Stack stalled: ${error.reason}`, error.statuses)
        );
      }

      throw error;
    }
  }

  isRunning(): boolean {
    return this.child !== undefined && this.child.exitCode === null;
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
    if (this.logs.length > 400) {
      this.logs.splice(0, this.logs.length - 400);
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
    const start = Date.now();
    let latestStatuses: ReadinessStatus[] = [];
    const firstReadyAt = new Map<string, number>();
    const lastObservedFailure = new Map<string, string>();
    let lastCheckpointAt = -readinessCheckpointIntervalMs;
    let stalledPendingNames = '';
    let stalledSinceMs = 0;

    while (Date.now() - start < readinessTimeoutMs) {
      if (this.child && this.child.exitCode !== null) {
        throw new Error(
          `AppHost exited before the Aspire localhost stack became ready.\n\n` +
            `Recent logs:\n${this.formatLogs()}`
        );
      }

      latestStatuses = await this.getReadinessStatuses();
      const elapsedMs = Date.now() - start;
      lastCheckpointAt = this.logReadinessProgress(latestStatuses, elapsedMs, firstReadyAt, lastObservedFailure, lastCheckpointAt);
      if (latestStatuses.every(status => status.ok)) {
        console.log(`[readiness] ${formatDuration(elapsedMs)} all localhost auth dependencies are ready.`);
        return;
      }

      const pendingNames = latestStatuses
        .filter(status => !status.ok)
        .map(status => status.check.name)
        .sort()
        .join(',');

      if (pendingNames !== stalledPendingNames) {
        stalledPendingNames = pendingNames;
        stalledSinceMs = elapsedMs;
      } else if (elapsedMs - stalledSinceMs >= stallRecoveryThresholdMs) {
        throw new StallDetectedError(
          `no progress on [${pendingNames}] for ${formatDuration(elapsedMs - stalledSinceMs)}`,
          latestStatuses
        );
      }

      await delay(readinessPollIntervalMs);
    }

    throw new Error(
      this.buildTimeoutDiagnostics(
        `Timed out waiting ${formatDuration(readinessTimeoutMs)} for the Aspire localhost stack to become ready.`,
        latestStatuses
      )
    );
  }

  private buildTimeoutDiagnostics(headline: string, statuses: ReadinessStatus[]): string {
    return (
      `${headline}\n\n` +
      `Readiness diagnostics:\n${formatReadinessDiagnostics(statuses)}\n\n` +
      `Port diagnostics:\n${formatPortDiagnostics()}\n\n` +
      `Keycloak container logs:\n${captureDockerContainerLogs('keycloak')}\n\n` +
      `Relevant resource logs:\n${this.formatResourceLogs()}\n\n` +
      `Recent logs:\n${this.formatLogs()}`
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
            // Special case: if this is the TestSite home marker check and we got Umbraco's
            // default unseeded splash page, classify this as "still seeding" by checking
            // for known unseeded-page markers. This prevents the probe from giving up when
            // Umbraco's HTTP listener starts responding before content seeding completes.
            const isHomeMarker = check.name === 'TestSite home marker';
            const isUnseededSplash = umbracoUnseededPageMarkers.some(marker => 
              response.body.includes(marker)
            );
            
            if (isHomeMarker && isUnseededSplash) {
              failures.push(`body missing ${JSON.stringify(text)} (Umbraco unseeded splash page detected; still seeding)`);
            } else {
              failures.push(`body missing ${JSON.stringify(text)}`);
            }
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

  private logReadinessProgress(
    statuses: ReadinessStatus[],
    elapsedMs: number,
    firstReadyAt: Map<string, number>,
    lastObservedFailure: Map<string, string>,
    lastCheckpointAt: number
  ): number {
    const lines: string[] = [];
    const checkpointDue = elapsedMs - lastCheckpointAt >= readinessCheckpointIntervalMs;

    for (const status of statuses) {
      if (!status.ok || firstReadyAt.has(status.check.name)) {
        continue;
      }

      firstReadyAt.set(status.check.name, elapsedMs);
      lastObservedFailure.delete(status.check.name);
      lines.push(`[readiness] ${formatDuration(elapsedMs)} ${status.check.name} became ready (${formatObserved(status.response)}).`);
    }

    const pending = statuses.filter(status => !status.ok);
    const pendingLines = pending
      .map(status => {
        const fingerprint = `${status.failures.join('|')}|${formatObserved(status.response)}`;
        const changed = lastObservedFailure.get(status.check.name) !== fingerprint;
        if (!changed && !checkpointDue) {
          return null;
        }

        lastObservedFailure.set(status.check.name, fingerprint);
        return (
          `  - ${status.check.name}: expected ${formatExpectations(status.check)}; observed ${formatObserved(
            status.response
          )}; listener ${describeUrlListener(status.check.url)}; failures: ${status.failures.join('; ')}`
        );
      })
      .filter((line): line is string => line !== null);

    if (pendingLines.length > 0) {
      lines.push(
        `[readiness] ${formatDuration(elapsedMs)} waiting on ${pending.map(status => status.check.name).join(', ')}.`
      );
      lines.push(...pendingLines);
    }

    if (lines.length > 0) {
      console.log(lines.join('\n'));
    }

    return checkpointDue ? elapsedMs : lastCheckpointAt;
  }

  private formatResourceLogs(): string {
    const pattern = new RegExp(resourceLogTerms.join('|'), 'i');
    const matchingLines = this.logs.filter(line => pattern.test(line));
    return matchingLines.length > 0 ? matchingLines.join('\n') : '(no matching AppHost resource log lines captured)';
  }
}

async function probe(urlString: string): Promise<ProbeResult> {
  const url = new URL(urlString);
  const client = url.protocol === 'https:' ? https : http;

  return new Promise(resolve => {
    let settled = false;
    const settle = (result: ProbeResult) => {
      if (settled) {
        return;
      }

      settled = true;
      resolve(result);
    };

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
          settle({
            status: response.statusCode ?? null,
            headers: Object.fromEntries(
              Object.entries(response.headers).map(([name, value]) => [name, Array.isArray(value) ? value.join(', ') : value ?? ''])
            ),
            body,
            error: null
          })
        );
      }
    );

    request.setTimeout(probeTimeoutMs, () => {
      request.destroy();
      settle({ status: null, headers: {}, body: '', error: `request timed out after ${probeTimeoutMs}ms` });
    });

    request.on('error', error =>
      settle({ status: null, headers: {}, body: '', error: error instanceof Error ? error.message : String(error) })
    );
    request.end();
  });
}

function formatReadinessDiagnostics(statuses: ReadinessStatus[]): string {
  if (statuses.length === 0) {
    return '(no readiness probes captured)';
  }

  return statuses
    .map(({ check, response, ok, failures }) => {
      const result = ok ? 'ready' : failures.join('; ');
      return [
        `- ${check.name}`,
        `  url: ${check.url}`,
        `  expected: ${formatExpectations(check)}`,
        `  observed: ${formatObserved(response)}`,
        `  listener: ${describeUrlListener(check.url)}`,
        `  result: ${result}`
      ].join('\n');
    })
    .join('\n');
}

function formatExpectations(check: (typeof readinessChecks)[number]): string {
  const parts: string[] = [];
  const allowedStatuses = check.allowedStatuses ?? [200];
  parts.push(
    allowedStatuses.length === 1
      ? `HTTP ${allowedStatuses[0]}`
      : `HTTP one of [${allowedStatuses.join(', ')}]`
  );

  for (const header of check.headerIncludes ?? []) {
    parts.push(`header ${header.name} includes ${JSON.stringify(header.valueIncludes)}`);
  }

  for (const bodyText of check.bodyIncludes ?? []) {
    parts.push(`body includes ${JSON.stringify(bodyText)}`);
  }

  return parts.join(', ');
}

function formatObserved(response: ProbeResult): string {
  const statusLabel = response.status === null ? 'no HTTP response' : `HTTP ${response.status}`;
  const details = [statusLabel];
  if (response.error) {
    details.push(`error=${JSON.stringify(response.error)}`);
  }

  const headerSummary = summarizeHeaders(response.headers);
  if (headerSummary) {
    details.push(`headers=${headerSummary}`);
  }

  const bodySummary = summarizeBody(response.body);
  if (bodySummary) {
    details.push(`body=${JSON.stringify(bodySummary)}`);
  }

  return details.join('; ');
}

function summarizeHeaders(headers: Record<string, string>): string {
  const pairs = notableHeaders
    .map(name => [name, headers[name]])
    .filter((entry): entry is [string, string] => Boolean(entry[1] && entry[1].trim()));

  if (pairs.length === 0) {
    return '';
  }

  return pairs.map(([name, value]) => `${name}=${JSON.stringify(value)}`).join(', ');
}

function summarizeBody(body: string): string {
  const normalized = body.replace(/\s+/g, ' ').trim();
  if (!normalized) {
    return '';
  }

  return normalized.length > responsePreviewLength
    ? `${normalized.slice(0, responsePreviewLength)}…`
    : normalized;
}

function formatDuration(ms: number): string {
  const totalSeconds = Math.round(ms / 1_000);
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  return `${minutes}m ${seconds.toString().padStart(2, '0')}s`;
}

function formatPortDiagnostics(): string {
  return requiredPorts
    .map(({ name, port }) => `- ${name} (${port}): ${describePortListener(port)}`)
    .join('\n');
}

function describeUrlListener(urlString: string): string {
  const url = new URL(urlString);
  const port =
    url.port.length > 0
      ? Number(url.port)
      : url.protocol === 'https:'
        ? 443
        : 80;

  return describePortListener(port);
}

function describePortListener(port: number): string {
  const pids = findListeningPids(port);
  return pids.length > 0 ? `listening [pid ${pids.join(', ')}]` : 'not listening';
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

function captureDockerContainerLogs(namePattern: string): string {
  // Try docker first (includes stopped containers with -a flag)
  let psResult = spawnSync('docker', ['ps', '-a', '--format', '{{.Names}}'], { encoding: 'utf8' });
  let runtime = 'docker';
  
  // Fall back to podman if docker returns no results
  if (psResult.status !== 0 || !psResult.stdout.trim()) {
    psResult = spawnSync('podman', ['ps', '-a', '--format', '{{.Names}}'], { encoding: 'utf8' });
    runtime = 'podman';
  }
  
  if (psResult.status !== 0 || psResult.error) {
    return `(${runtime} ps failed: ${psResult.error?.message ?? psResult.stderr?.trim() ?? 'unknown error'})`;
  }

  const allNames = psResult.stdout.trim().split(/\r?\n/).filter(Boolean);
  const matching = allNames.filter(name => name.toLowerCase().includes(namePattern.toLowerCase()));

  if (matching.length === 0) {
    return `(no matching containers found for pattern: ${JSON.stringify(namePattern)} using ${runtime})`;
  }

  return matching
    .map(containerName => {
      const logsResult = spawnSync(runtime, ['logs', '--tail', '100', containerName], {
        encoding: 'utf8'
      });
      const output = [logsResult.stdout, logsResult.stderr].filter(Boolean).join('');
      return `--- ${containerName} (${runtime}) ---\n${output.trim() || '(no output)'}`;
    })
    .join('\n\n');
}
