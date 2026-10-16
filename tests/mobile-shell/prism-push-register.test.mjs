// Security regression for the mobile shell: tapping a push notification must only ever open a page
// on the tenant's own site. Runs the shipped script against a fake Capacitor bridge.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { runInNewContext } from 'node:vm';

const script = readFileSync(
  new URL('../../src/UmbracoPrism.Core/wwwroot/mobile-shell/prism-push-register.js', import.meta.url),
  'utf8');

const origin = 'https://tenant.example';

// Loads the script as the app would, taps a notification carrying `url`, and returns where the
// app navigated to (undefined if it stayed put).
function tapNotification(url) {
  let onTap;
  let navigatedTo;
  const location = {
    origin,
    host: 'tenant.example',
    set href(value) { navigatedTo = value; },
  };
  const window = {
    location,
    Capacitor: {
      isNativePlatform: () => true,
      nativePromise: () => Promise.reject(new Error('no native bridge in tests')),
      Plugins: {
        FirebaseMessaging: { addListener: (event, handler) => { if (event === 'notificationActionPerformed') onTap = handler; } },
      },
    },
  };
  runInNewContext(script, { window, URL, console: { log() {} }, localStorage: {}, fetch() {} });
  onTap({ notification: { data: { url } } });
  // Compare where the WebView ends up, not the exact string handed to location.href.
  return navigatedTo === undefined ? undefined : new URL(navigatedTo, origin).href;
}

for (const path of ['/orders', '/apply-for-a-juggling-licence', '/orders?id=7#latest']) {
  test(`a tapped notification opens ${path} on this site`, () => {
    assert.equal(tapNotification(path), `${origin}${path}`);
  });
}

for (const url of [
  'https://evil.example/login',
  '//evil.example/login',
  '/\\evil.example/login',
  '/\t/evil.example',
  'javascript:alert(document.cookie)',
  'data:text/html,<script>alert(1)</script>',
  `${origin}.evil.example/`,
]) {
  test(`a tapped notification does not follow ${JSON.stringify(url)}`, () => {
    assert.equal(tapNotification(url), undefined);
  });
}

test('a notification without a link leaves the app where it is', () => {
  assert.equal(tapNotification(undefined), undefined);
});
