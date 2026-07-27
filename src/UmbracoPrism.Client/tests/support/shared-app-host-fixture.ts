/**
 * Shared LiveAppHost fixture for localhost-auth tests.
 * 
 * Strategy: Playwright worker fixture starts the Aspire stack once per worker and tears it down after all tests.
 * Specs get fresh browser contexts (Playwright's default) + explicit server-side reset (resetServiceBlueprints).
 * 
 * Isolation discipline:
 * - Fresh browser context per test (Playwright's default)
 * - Explicit server-side reset via resetServiceBlueprints() in beforeEach
 * - TestSite runtime reset happens only once at worker startup, not per-test
 * 
 * Contract:
 * - Use the `appHost` fixture in your test (destructure from test params)
 * - Call resetServiceBlueprints(request) in beforeEach for test isolation
 */

import { test as base } from '@playwright/test';
import { LiveAppHost } from './live-app-host';

type AppHostWorkerFixtures = {
  appHost: LiveAppHost;
};

const liveAppHostStartupTimeoutMs = 10 * 60_000;

export const test = base.extend<{}, AppHostWorkerFixtures>({
  appHost: [async ({}, use, workerInfo) => {
    console.log(`[worker-fixture] Worker ${workerInfo.workerIndex} starting LiveAppHost...`);
    const liveAppHost = new LiveAppHost();
    await liveAppHost.start();
    console.log(`[worker-fixture] Worker ${workerInfo.workerIndex} LiveAppHost ready.`);
    await use(liveAppHost);
    console.log(`[worker-fixture] Worker ${workerInfo.workerIndex} stopping LiveAppHost...`);
    await liveAppHost.stop();
    console.log(`[worker-fixture] Worker ${workerInfo.workerIndex} LiveAppHost stopped.`);
  }, { scope: 'worker', auto: true, timeout: liveAppHostStartupTimeoutMs }],
});

export { expect } from '@playwright/test';
