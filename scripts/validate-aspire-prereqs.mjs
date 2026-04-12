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
