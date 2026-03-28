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
  register(tenantHost: string, loginHint?: string): Promise<void>;
  authenticate(tenantHost: string): Promise<string>;
  revokeDevice(tenantHost: string): Promise<void>;
  clearLocalCredentials(tenantHost?: string): Promise<void>;
}

class BiometricBridgeImpl implements BiometricBridge {
  private readonly DEVICE_ID_KEY = 'prism_device_id';
  private readonly BIOMETRIC_TOKEN_PREFIX = 'prism_biometric_token_';

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
      const response = await fetch(`https://${tenantHost}/umbraco/prism/biometric/register`, {
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
    } catch (error: any) {
      if (error instanceof BiometricError) {
        throw error;
      }
      throw new Error(`Registration request failed: ${error.message || error}`);
    }
  }

  async authenticate(tenantHost: string): Promise<string> {
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
      const response = await fetch(`https://${tenantHost}/umbraco/prism/biometric/exchange`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({
          biometricToken: storedToken,
          deviceId
        })
      });

      if (!response.ok) {
        const errorData = await response.json().catch(() => ({ message: 'Token exchange failed' }));
        
        if (response.status === 401 || response.status === 403) {
          await this.clearLocalCredentials(tenantHost);
          throw new BiometricError(
            'Biometric credentials are no longer valid. Please register again.',
            'unavailable'
          );
        }
        
        throw new Error(errorData.message || `Exchange failed with status ${response.status}`);
      }

      const data = await response.json();
      const newBiometricToken = data.biometricToken;
      const sessionToken = data.sessionToken;

      if (!sessionToken) {
        throw new Error('Server did not return a session token');
      }

      if (newBiometricToken) {
        await SecureStorage.set(
          `${this.BIOMETRIC_TOKEN_PREFIX}${tenantHost}`,
          newBiometricToken
        );
      }

      return sessionToken;
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
        await fetch(`https://${tenantHost}/umbraco/prism/biometric/revoke`, {
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

  async clearLocalCredentials(tenantHost?: string): Promise<void> {
    try {
      if (tenantHost) {
        await SecureStorage.remove(`${this.BIOMETRIC_TOKEN_PREFIX}${tenantHost}`);
        console.log('Cleared biometric credentials for tenant:', tenantHost);
      } else {
        const allKeys = await SecureStorage.keys();
        const tokenKeys = allKeys.filter(key => key.startsWith(this.BIOMETRIC_TOKEN_PREFIX));
        
        for (const key of tokenKeys) {
          await SecureStorage.remove(key);
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
