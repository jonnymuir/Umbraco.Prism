
    var __prismDebug = (function() {
      var KEY = 'prism_debug_log';
      function store(msg) {
        try {
          var log = JSON.parse(localStorage.getItem(KEY) || '[]');
          log.push({ t: new Date().toISOString(), m: msg });
          if (log.length > 50) log = log.slice(-50);
          localStorage.setItem(KEY, JSON.stringify(log));
        } catch(e) {}
      }
      function replay() {
        try {
          var log = JSON.parse(localStorage.getItem(KEY) || '[]');
          if (log.length > 0) {
            console.log('[Prism Debug Replay] ' + log.length + ' stored log(s) from previous page:');
            log.forEach(function(e) { console.log('  [' + e.t + '] ' + e.m); });
            localStorage.removeItem(KEY);
          }
        } catch(e) {}
      }
      function log(msg) {
        console.log(msg);
        store(msg);
      }
      return { log: log, replay: replay };
    })();

    async function tryBiometricSignIn() {
      __prismDebug.replay();
      try {
        __prismDebug.log('[Prism Bio] tryBiometricSignIn: starting');
        var Cap = window.Capacitor;
        if (!Cap || !Cap.isNativePlatform || !Cap.isNativePlatform()) {
          __prismDebug.log('[Prism Bio] Not a native platform — skipping biometric');
          return false;
        }

        var tenantHost = new URL(prismBootstrap.startUrl).host;
        var SS_PREFIX = 'capacitor-storage_';
        var tokenKey = SS_PREFIX + 'prism_biometric_token_' + tenantHost;
        var enrollKey = 'prism_biometric_enrollment_state_' + tenantHost;
        var deviceIdKey = 'prism_device_id';

        // 1. Check stored biometric token (SecureStorage uses internalGetItem)
        __prismDebug.log('[Prism Bio] Step 1: checking SecureStorage for token, key: ' + tokenKey);
        var storedResult = await Cap.nativePromise('SecureStorage', 'internalGetItem', {
          prefixedKey: tokenKey,
          sync: false
        });
        var storedToken = storedResult && storedResult.data ? JSON.parse(storedResult.data) : null;
        if (!storedToken) {
          __prismDebug.log('[Prism Bio] Step 1: no stored token — not yet enrolled');
          return false;
        }
        __prismDebug.log('[Prism Bio] Step 1: stored token found');

        // 2. Check biometry availability
        __prismDebug.log('[Prism Bio] Step 2: checking biometry availability');
        var biometryInfo = await Cap.nativePromise('BiometricAuthNative', 'checkBiometry', {});
        __prismDebug.log('[Prism Bio] Step 2: biometryInfo = ' + JSON.stringify(biometryInfo));
        if (!biometryInfo || !biometryInfo.isAvailable) {
          __prismDebug.log('[Prism Bio] Step 2: biometry not available');
          return false;
        }

        // 3. Check enrollment change
        var fingerprint = [
          biometryInfo.biometryType,
          (biometryInfo.biometryTypes || []).slice().sort().join(','),
          biometryInfo.isAvailable,
          biometryInfo.strongBiometryIsAvailable,
          biometryInfo.deviceIsSecure
        ].join('|');
        __prismDebug.log('[Prism Bio] Step 3: enrollment fingerprint = ' + fingerprint);
        var storedFingerprint = localStorage.getItem(enrollKey);
        if (storedFingerprint && storedFingerprint !== fingerprint) {
          __prismDebug.log('[Prism Bio] Step 3: enrollment changed — clearing token');
          await Cap.nativePromise('SecureStorage', 'internalRemoveItem', { prefixedKey: tokenKey, sync: false });
          localStorage.removeItem(enrollKey);
          return false;
        }

        // 4. Prompt biometric authentication
        __prismDebug.log('[Prism Bio] Step 4: prompting biometric authentication');
        await Cap.nativePromise('BiometricAuthNative', 'internalAuthenticate', {
          reason: 'Sign in with biometrics',
          allowDeviceCredential: true,
          iosFallbackTitle: 'Use Passcode'
        });
        __prismDebug.log('[Prism Bio] Step 4: biometric authentication passed');

        // 5. Get device ID
        var deviceId = localStorage.getItem(deviceIdKey) || '';
        __prismDebug.log('[Prism Bio] Step 5: deviceId = ' + (deviceId || '(empty)'));

        // 6. Exchange biometric token for PrismMemberCookie (Set-Cookie on response)
        __prismDebug.log('[Prism Bio] Step 6: exchanging token with server');
        var resp = await fetch('https://' + tenantHost + '/umbraco/prism/mobile/biometric/exchange', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          credentials: 'include',
          body: JSON.stringify({ biometricToken: storedToken, deviceId: deviceId })
        });
        __prismDebug.log('[Prism Bio] Step 6: exchange response status = ' + resp.status);

        if (!resp.ok) {
          if (resp.status === 401 || resp.status === 403) {
            __prismDebug.log('[Prism Bio] Step 6: server rejected token — clearing stored credentials');
            await Cap.nativePromise('SecureStorage', 'internalRemoveItem', { prefixedKey: tokenKey, sync: false });
            localStorage.removeItem(enrollKey);
          }
          return false;
        }

        // Save updated enrollment fingerprint
        localStorage.setItem(enrollKey, fingerprint);
        __prismDebug.log('[Prism Bio] Step 6: exchange successful — proceeding to app');
        return true;
      } catch (e) {
        console.warn('[Prism Bio] tryBiometricSignIn threw:', e && (e.message || e));
        return false;
      }
    }