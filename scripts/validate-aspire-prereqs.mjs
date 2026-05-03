import { execSync } from 'node:child_process';

function tryCommand(command) {
  try {
    return {
      ok: true,
      output: execSync(command, { encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] })
    };
  } catch (error) {
    return {
      ok: false,
      output: `${error.stdout ?? ''}${error.stderr ?? ''}`.trim()
    };
  }
}

const problems = [];
const runLocalhostAuthSuite = process.argv.includes('--localhost-auth-suite');

const dotnetVersionResult = tryCommand('dotnet --version');
if (!dotnetVersionResult.ok) {
  problems.push('The .NET SDK is not available on PATH. Install the .NET 10 SDK before launching the full stack.');
} else {
  const [major] = dotnetVersionResult.output.trim().split('.');
  if (!Number.isInteger(Number(major)) || Number(major) < 10) {
    problems.push(`Detected .NET SDK ${dotnetVersionResult.output.trim()}. Install .NET 10 SDK before launching the full stack.`);
  }
}

const dockerResult = tryCommand('docker info');
if (!dockerResult.ok) {
  problems.push('Docker is not available. Start Docker Desktop (or another supported OCI runtime exposed via docker CLI) before launching the full stack.');
}

if (runLocalhostAuthSuite) {
  const requiredPorts = [
    ['Aspire dashboard', 17214],
    ['Aspire dashboard HTTP (legacy)', 15135],
    ['Aspire dashboard OTLP', 21233],
    ['Aspire resource service', 22194],
    ['TestSite', 44345],
    ['Keycloak proxy', 8443],
    ['MockBusinessApp', 7245]
  ];

  let occupiedPorts = getOccupiedPorts(requiredPorts);
  const waitStart = Date.now();
  while (occupiedPorts.length > 0 && Date.now() - waitStart < 45_000) {
    await delay(1_000);
    occupiedPorts = getOccupiedPorts(requiredPorts);
  }

  if (occupiedPorts.length > 0) {
    problems.push(
      `The localhost auth Playwright suite owns the full Aspire lifecycle and requires these ports to be free: ${occupiedPorts.join(', ')}.`
    );
  }
}

if (problems.length > 0) {
  console.error('Full-stack Aspire launch prerequisites are missing:');

  for (const problem of problems) {
    console.error(`- ${problem}`);
  }

  console.error('');
  console.error('This repo now uses the Aspire AppHost SDK and NuGet packages, so no separate `dotnet workload install aspire` step is required.');
  process.exit(1);
}

console.log('Aspire prerequisites look good.');

function getOccupiedPorts(requiredPorts) {
  return requiredPorts
    .map(([name, port]) => {
      const pids = listListeningPids(port);
      return pids.length > 0 ? `${name} (${port}) [pid ${pids.join(', ')}]` : null;
    })
    .filter(Boolean);
}

function listListeningPids(port) {
  const result = tryCommand(`lsof -t -iTCP:${port} -sTCP:LISTEN`);
  if (!result.ok || !result.output.trim()) {
    return [];
  }

  return result.output
    .trim()
    .split(/\s+/)
    .filter(value => /^\d+$/.test(value));
}

function delay(ms) {
  return new Promise(resolve => setTimeout(resolve, ms));
}
