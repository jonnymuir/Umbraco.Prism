import { BiometricAuth, CheckBiometryResult, BiometryType } from '@aparajita/capacitor-biometric-auth';
import { SecureStorage } from '@aparajita/capacitor-secure-storage';
import { Preferences } from '@capacitor/preferences';
import { Capacitor } from '@capacitor/core';

export class BiometricError extends Error {
  constructor(
    message: string,
    public code: 'cancelled' | 'locked_out' | 'not_enrolled' | 'unavailable' | 'unknown'
  ) {
    super(message);
    this.name = 'BiometricError';
  }
}

export interface BiometricBridge {
  isAvailable(): Promise<boolean>;
  getOrCreateDeviceId(): Promise<string>;
  checkEnrollmentChange(tenantHost: string): Promise<boolean>;
  register(tenantHost: string, loginHint?: string): Promise<void>;
  authenticate(tenantHost: string): Promise<void>;
  revokeDevice(tenantHost: string): Promise<void>;
  unenrolBiometric(tenantHostname: string): Promise<void>;
  clearLocalCredentials(tenantHost?: string): Promise<void>;
}

/**
 * Multi-tenant keystore key design:
 *
 * All SecureStorage and Preferences keys are suffixed with the tenant hostname
 * (e.g. "tenant-a.example.com"), NOT an integer tenantId. This guarantees:
 *   1. Keys are naturally unique across tenants (hostnames are globally unique).
 *   2. The client never needs to resolve or cache numeric tenant IDs.
 *   3. clearLocalCredentials() can enumerate and purge all tenant keys by prefix.
 *
 * Key patterns:
 *   - `prism_biometric_token_{hostname}`  → SecureStorage (biometric JWT)
 *   - `prism_biometric_enrollment_state_{hostname}` → Preferences (enrollment fingerprint)
 *   - `prism_device_id` → Preferences (device-scoped, not tenant-scoped)
 */
class BiometricBridgeImpl implements BiometricBridge {
  private readonly DEVICE_ID_KEY = 'prism_device_id';
  private readonly BIOMETRIC_TOKEN_PREFIX = 'prism_biometric_token_';
  private readonly ENROLLMENT_STATE_PREFIX = 'prism_biometric_enrollment_state_';

  async isAvailable(): Promise<boolean> {
    try {
      const result: CheckBiometryResult = await BiometricAuth.checkBiometry();
      return result.isAvailable && result.biometryType !== BiometryType.none;
    } catch (error) {
      console.warn('Biometric availability check failed:', error);
      return false;
    }
  }

  async getOrCreateDeviceId(): Promise<string> {
    try {
      const { value } = await Preferences.get({ key: this.DEVICE_ID_KEY });
      
      if (value) {
        return value;
      }

      const newDeviceId = crypto.randomUUID();
      await Preferences.set({ key: this.DEVICE_ID_KEY, value: newDeviceId });
      return newDeviceId;
    } catch (error) {
      throw new Error(`Failed to get or create device ID: ${error}`);
    }
  }

  async checkEnrollmentChange(tenantHost: string): Promise<boolean> {
    try {
      const result: CheckBiometryResult = await BiometricAuth.checkBiometry();
      const currentFingerprint = this._buildEnrollmentFingerprint(result);
      const { value: storedFingerprint } = await Preferences.get({
        key: `${this.ENROLLMENT_STATE_PREFIX}${tenantHost}`
      });

      if (!storedFingerprint) {
        // No stored state yet — first run, not a change
        return false;
      }

      if (storedFingerprint !== currentFingerprint) {
        console.warn('Biometric enrollment change detected for tenant:', tenantHost);
        return true;
      }

      return false;
    } catch (error) {
      // Fail closed: treat errors as enrollment change to force re-auth
      console.warn('Enrollment change check failed, treating as changed:', error);
      return true;
    }
  }

  private async _saveEnrollmentState(tenantHost: string): Promise<void> {
    try {
      const result: CheckBiometryResult = await BiometricAuth.checkBiometry();
      const fingerprint = this._buildEnrollmentFingerprint(result);
      await Preferences.set({
        key: `${this.ENROLLMENT_STATE_PREFIX}${tenantHost}`,
        value: fingerprint
      });
    } catch (error) {
      console.warn('Failed to save enrollment state:', error);
    }
  }

  private _buildEnrollmentFingerprint(result: CheckBiometryResult): string {
    const types = [...(result.biometryTypes || [])].sort().join(',');
    return `${result.biometryType}|${types}|${result.isAvailable}|${result.strongBiometryIsAvailable}|${result.deviceIsSecure}`;
  }

  async register(tenantHost: string, loginHint?: string): Promise<void> {
    const available = await this.isAvailable();
    if (!available) {
      throw new BiometricError('Biometric authentication is not available on this device', 'unavailable');
    }

    try {
      await BiometricAuth.authenticate({
        reason: 'Register biometric login for Prism',
        allowDeviceCredential: true,
        iosFallbackTitle: 'Use Passcode'
      });
    } catch (error: any) {
      throw this._handleBiometricError(error, 'registration');
    }

    const deviceId = await this.getOrCreateDeviceId();
    const platform = Capacitor.getPlatform();

    const requestBody: any = {
      deviceId,
      platform
    };

    if (loginHint) {
      requestBody.loginHint = loginHint;
    }

    try {
      const response = await fetch(`https://${tenantHost}/umbraco/prism/mobile/biometric/register`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        credentials: 'include',
        body: JSON.stringify(requestBody)
      });

      if (!response.ok) {
        const errorData = await response.json().catch(() => ({ message: 'Registration failed' }));
        throw new Error(errorData.message || `Registration failed with status ${response.status}`);
      }

      const data = await response.json();
      const biometricToken = data.biometricToken;

      if (!biometricToken) {
        throw new Error('Server did not return a biometric token');
      }

      await SecureStorage.set(
        `${this.BIOMETRIC_TOKEN_PREFIX}${tenantHost}`,
        biometricToken
      );

      console.log('Biometric registration successful for tenant:', tenantHost);

      await this._saveEnrollmentState(tenantHost);
    } catch (error: any) {
      if (error instanceof BiometricError) {
        throw error;
      }
      throw new Error(`Registration request failed: ${error.message || error}`);
    }
  }

  async authenticate(tenantHost: string): Promise<void> {
    // Check for biometric enrollment changes before attempting auth
    const enrollmentChanged = await this.checkEnrollmentChange(tenantHost);
    if (enrollmentChanged) {
      await this.clearLocalCredentials(tenantHost);
      throw new BiometricError(
        'Biometric enrollment has changed. Please re-register biometric login.',
        'unavailable'
      );
    }

    let storedToken: string;

    try {
      const result = await SecureStorage.get(`${this.BIOMETRIC_TOKEN_PREFIX}${tenantHost}`);
      
      if (!result || typeof result !== 'string') {
        throw new BiometricError('Biometric authentication not registered for this tenant', 'unavailable');
      }
      
      storedToken = result;
    } catch (error) {
      throw new BiometricError('Biometric authentication not registered for this tenant', 'unavailable');
    }

    try {
      await BiometricAuth.authenticate({
        reason: 'Sign in with biometrics',
        allowDeviceCredential: true,
        iosFallbackTitle: 'Use Passcode'
      });
    } catch (error: any) {
      throw this._handleBiometricError(error, 'authentication');
    }

    const deviceId = await this.getOrCreateDeviceId();

    try {
      const response = await fetch(`https://${tenantHost}/umbraco/prism/mobile/biometric/exchange`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        credentials: 'include',
        body: JSON.stringify({
          biometricToken: storedToken,
          deviceId
        })
      });

      if (!response.ok) {
        if (response.status === 401 || response.status === 403) {
          await this.clearLocalCredentials(tenantHost);
          throw new BiometricError(
            'Biometric credentials are no longer valid. Please register again.',
            'unavailable'
          );
        }

        const errorData = await response.json().catch(() => ({ message: 'Token exchange failed' }));
        throw new Error(errorData.message || `Exchange failed with status ${response.status}`);
      }

      // Server sets PrismMemberCookie via Set-Cookie — no JSON body to parse.
      await this._saveEnrollmentState(tenantHost);
    } catch (error: any) {
      if (error instanceof BiometricError) {
        throw error;
      }
      throw new Error(`Authentication exchange failed: ${error.message || error}`);
    }
  }

  async revokeDevice(tenantHost: string): Promise<void> {
    let storedToken: string | null = null;

    try {
      const result = await SecureStorage.get(`${this.BIOMETRIC_TOKEN_PREFIX}${tenantHost}`);
      if (result && typeof result === 'string') {
        storedToken = result;
      }
    } catch (error) {
      // Token not found, nothing to revoke on server
    }

    if (storedToken) {
      const deviceId = await this.getOrCreateDeviceId();

      try {
        await fetch(`https://${tenantHost}/umbraco/prism/mobile/biometric/revoke`, {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json'
          },
          body: JSON.stringify({
            biometricToken: storedToken,
            deviceId
          })
        });
      } catch (error) {
        console.warn('Failed to revoke device on server:', error);
      }
    }

    await this.clearLocalCredentials(tenantHost);
  }

  async unenrolBiometric(tenantHostname: string): Promise<void> {
    const deviceId = await this.getOrCreateDeviceId();

    const response = await fetch(
      `https://${tenantHostname}/umbraco/prism/mobile/biometric/unenrol/${deviceId}`,
      {
        method: 'DELETE',
        credentials: 'include'
      }
    );

    if (response.status === 204) {
      await SecureStorage.remove(`${this.BIOMETRIC_TOKEN_PREFIX}${tenantHostname}`);
      await Preferences.remove({ key: `${this.ENROLLMENT_STATE_PREFIX}${tenantHostname}` });
      console.log('Biometric unenrolment successful for tenant:', tenantHostname);
      return;
    }

    const errorData = await response.json().catch(() => ({ message: 'Unenrolment failed' }));
    throw new Error(errorData.message || `Unenrolment failed with status ${response.status}`);
  }

  async clearLocalCredentials(tenantHost?: string): Promise<void> {
    try {
      if (tenantHost) {
        await SecureStorage.remove(`${this.BIOMETRIC_TOKEN_PREFIX}${tenantHost}`);
        await Preferences.remove({ key: `${this.ENROLLMENT_STATE_PREFIX}${tenantHost}` });
        console.log('Cleared biometric credentials for tenant:', tenantHost);
      } else {
        const allKeys = await SecureStorage.keys();
        const tokenKeys = allKeys.filter(key => key.startsWith(this.BIOMETRIC_TOKEN_PREFIX));
        
        for (const key of tokenKeys) {
          await SecureStorage.remove(key);
        }

        // Extract tenant hosts from token keys to clean up matching enrollment state
        for (const key of tokenKeys) {
          const tenant = key.slice(this.BIOMETRIC_TOKEN_PREFIX.length);
          await Preferences.remove({ key: `${this.ENROLLMENT_STATE_PREFIX}${tenant}` });
        }

        await Preferences.remove({ key: this.DEVICE_ID_KEY });
        console.log('Cleared all biometric credentials');
      }
    } catch (error) {
      console.warn('Error clearing credentials:', error);
    }
  }

  private _handleBiometricError(error: any, context: string): BiometricError {
    const errorMessage = error?.message || String(error);
    
    if (errorMessage.includes('cancel') || errorMessage.includes('Cancel')) {
      return new BiometricError(`Biometric ${context} was cancelled`, 'cancelled');
    }
    
    if (errorMessage.includes('locked') || errorMessage.includes('too many attempts')) {
      return new BiometricError('Biometric authentication is temporarily locked', 'locked_out');
    }
    
    if (errorMessage.includes('not enrolled') || errorMessage.includes('no biometry')) {
      return new BiometricError('No biometric credentials are enrolled on this device', 'not_enrolled');
    }
    
    if (errorMessage.includes('not available') || errorMessage.includes('unavailable')) {
      return new BiometricError('Biometric authentication is not available', 'unavailable');
    }
    
    return new BiometricError(
      `Biometric ${context} failed: ${errorMessage}`,
      'unknown'
    );
  }
}

export const biometricBridge: BiometricBridge = new BiometricBridgeImpl();

/**
 * Registers a listener for the `prismBiometricLoginComplete` custom event.
 * When fired, navigates to the given startUrl.
 * Called from the generated www/index.html bootstrap when biometric auth is enabled.
 */
export function initBiometricLoginListener(startUrl: string): void {
  document.addEventListener('prismBiometricLoginComplete', () => {
    window.location.href = startUrl;
  });
}
