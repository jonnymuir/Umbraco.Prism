import { PushNotifications, PushNotificationSchema, ActionPerformed, Token, RegistrationError, PermissionStatus } from '@capacitor/push-notifications';
import { Capacitor, type PluginListenerHandle } from '@capacitor/core';
import { Preferences } from '@capacitor/preferences';

export type PushPermissionState = 'granted' | 'denied' | 'prompt';

/**
 * Prism Push Notifications Module
 * 
 * Wraps @capacitor/push-notifications for mobile push notification support.
 * Degrades gracefully on web/simulator via Capacitor.isNativePlatform() checks.
 * 
 * iOS Setup Required:
 * - Enable "Push Notifications" capability in Xcode (Signing & Capabilities tab)
 * - Add `aps-environment` key to App.entitlements:
 *   <key>aps-environment</key>
 *   <string>development</string>  <!-- or "production" for release builds -->
 * - APNs authentication via p8 key or p12 certificate (configured server-side)
 * 
 * Android Setup Required:
 * - Place google-services.json in android/app/ directory
 * - FCM project configured in Firebase Console
 * - google-services plugin applied in android/app/build.gradle
 * 
 * See PUSH_SETUP.md for detailed native configuration instructions.
 */

const PERMISSION_STATE_KEY = 'prism_push_permission_state';
const TOKEN_REGISTERED_KEY = 'prism_push_token_registered';

export class PrismPushNotifications {
  private static _initialized = false;

  /**
   * Request push notification permissions from the user.
   * On iOS, triggers system permission prompt if not already determined.
   * On Android, permissions are granted by default (no prompt).
   * 
   * @returns Permission state: 'granted', 'denied', or 'prompt'
   */
  static async requestPermission(): Promise<PushPermissionState> {
    if (!Capacitor.isNativePlatform()) {
      console.log('[PrismPush] Web platform detected, skipping permission request');
      return 'denied';
    }

    try {
      const result: PermissionStatus = await PushNotifications.requestPermissions();
      const state = result.receive as PushPermissionState;
      
      await Preferences.set({
        key: PERMISSION_STATE_KEY,
        value: state
      });

      console.log(`[PrismPush] Permission state: ${state}`);
      return state;
    } catch (error) {
      console.warn('[PrismPush] Permission request failed:', error);
      return 'denied';
    }
  }

  /**
   * Check current push notification permission state without requesting.
   * Reads from cached Preferences state.
   * 
   * @returns Current permission state or 'prompt' if never requested
   */
  static async checkPermission(): Promise<PushPermissionState> {
    if (!Capacitor.isNativePlatform()) {
      return 'denied';
    }

    try {
      const { value } = await Preferences.get({ key: PERMISSION_STATE_KEY });
      return (value as PushPermissionState) || 'prompt';
    } catch {
      return 'prompt';
    }
  }

  /**
   * Register device for push notifications and send token to backend.
   * 
   * Flow:
   * 1. Request permissions (if not already granted)
   * 2. Register with APNs/FCM via PushNotifications.register()
   * 3. Listen for 'registration' event to capture device token
   * 4. POST token to /umbraco/prism/push/register with Bearer auth
   * 
   * Requires user to be authenticated (valid authToken).
   * 
   * @param apiBaseUrl - Base URL of the Prism backend (e.g., 'https://portal.example.com')
   * @param authToken - Bearer token for authentication (from PrismMemberCookie or Entra)
   */
  static async registerDevice(apiBaseUrl: string, authToken: string): Promise<void> {
    if (!Capacitor.isNativePlatform()) {
      console.log('[PrismPush] Web platform, skipping device registration');
      return;
    }

    const permissionState = await this.requestPermission();
    if (permissionState !== 'granted') {
      console.warn('[PrismPush] Permission not granted, cannot register device');
      return;
    }

    if (!this._initialized) {
      this._initializeListeners();
      this._initialized = true;
    }

    return new Promise((resolve, reject) => {
      const timeout = setTimeout(() => {
        reject(new Error('[PrismPush] Device registration timeout'));
      }, 10000);

      const tokenHandler = async (token: Token) => {
        clearTimeout(timeout);
        
        try {
          const platform = Capacitor.getPlatform();
          const response = await fetch(`${apiBaseUrl}/umbraco/prism/push/register`, {
            method: 'POST',
            headers: {
              'Content-Type': 'application/json',
              'Authorization': `Bearer ${authToken}`
            },
            body: JSON.stringify({ token: token.value })
          });

          if (!response.ok) {
            const errorData = await response.json().catch(() => ({ message: 'Registration failed' }));
            throw new Error(errorData.message || `Registration failed with status ${response.status}`);
          }

          await Preferences.set({
            key: TOKEN_REGISTERED_KEY,
            value: 'true'
          });

          console.log(`[PrismPush] Device registered successfully (${platform})`);
          resolve();
        } catch (error: any) {
          console.error('[PrismPush] Failed to register token with backend:', error);
          reject(error);
        }
      };

      PushNotifications.addListener('registration', tokenHandler).then(() => {
        PushNotifications.register().catch(error => {
          clearTimeout(timeout);
          reject(new Error(`[PrismPush] Registration call failed: ${error.message || error}`));
        });
      });
    });
  }

  /**
   * Unregister device from push notifications and remove token from backend.
   * 
   * Calls DELETE /umbraco/prism/push/register to remove device token from server.
   * Note: Does not call PushNotifications.unregister() as this would disable
   * notifications for all apps on the device — server-side removal is sufficient.
   * 
   * @param apiBaseUrl - Base URL of the Prism backend
   * @param authToken - Bearer token for authentication
   */
  static async unregisterDevice(apiBaseUrl: string, authToken: string): Promise<void> {
    if (!Capacitor.isNativePlatform()) {
      console.log('[PrismPush] Web platform, skipping device unregistration');
      return;
    }

    try {
      const response = await fetch(`${apiBaseUrl}/umbraco/prism/push/register`, {
        method: 'DELETE',
        headers: {
          'Authorization': `Bearer ${authToken}`
        }
      });

      if (!response.ok) {
        const errorData = await response.json().catch(() => ({ message: 'Unregistration failed' }));
        throw new Error(errorData.message || `Unregistration failed with status ${response.status}`);
      }

      await Preferences.remove({ key: TOKEN_REGISTERED_KEY });
      console.log('[PrismPush] Device unregistered successfully');
    } catch (error) {
      console.error('[PrismPush] Failed to unregister device:', error);
      throw error;
    }
  }

  /**
   * Subscribe to notifications for a specific content genre/category.
   * 
   * Genre examples: "news", "events", "alerts", "promotions"
   * Genres are defined by the Prism backend implementation.
   * 
   * @param apiBaseUrl - Base URL of the Prism backend
   * @param authToken - Bearer token for authentication
   * @param genre - Genre/category identifier
   */
  static async subscribeToGenre(apiBaseUrl: string, authToken: string, genre: string): Promise<void> {
    if (!Capacitor.isNativePlatform()) {
      console.log('[PrismPush] Web platform, skipping genre subscription');
      return;
    }

    try {
      const response = await fetch(`${apiBaseUrl}/umbraco/prism/push/subscribe`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${authToken}`
        },
        body: JSON.stringify({ genre })
      });

      if (!response.ok) {
        const errorData = await response.json().catch(() => ({ message: 'Subscription failed' }));
        throw new Error(errorData.message || `Subscription failed with status ${response.status}`);
      }

      console.log(`[PrismPush] Subscribed to genre: ${genre}`);
    } catch (error) {
      console.error(`[PrismPush] Failed to subscribe to genre '${genre}':`, error);
      throw error;
    }
  }

  /**
   * Unsubscribe from notifications for a specific content genre/category.
   * 
   * @param apiBaseUrl - Base URL of the Prism backend
   * @param authToken - Bearer token for authentication
   * @param genre - Genre/category identifier
   */
  static async unsubscribeFromGenre(apiBaseUrl: string, authToken: string, genre: string): Promise<void> {
    if (!Capacitor.isNativePlatform()) {
      console.log('[PrismPush] Web platform, skipping genre unsubscription');
      return;
    }

    try {
      const response = await fetch(`${apiBaseUrl}/umbraco/prism/push/unsubscribe`, {
        method: 'DELETE',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${authToken}`
        },
        body: JSON.stringify({ genre })
      });

      if (!response.ok) {
        const errorData = await response.json().catch(() => ({ message: 'Unsubscription failed' }));
        throw new Error(errorData.message || `Unsubscription failed with status ${response.status}`);
      }

      console.log(`[PrismPush] Unsubscribed from genre: ${genre}`);
    } catch (error) {
      console.error(`[PrismPush] Failed to unsubscribe from genre '${genre}':`, error);
      throw error;
    }
  }

  /**
   * Add listener for notifications received while app is in foreground.
   * 
   * When a notification arrives while the app is open, this callback is invoked.
   * The app must handle displaying the notification to the user (e.g., via a toast/banner).
   * 
   * @param callback - Handler invoked with notification payload
   * @returns Listener handle (call .remove() to unregister)
   */
  static async addForegroundListener(
    callback: (notification: PushNotificationSchema) => void
  ): Promise<PluginListenerHandle> {
    if (!Capacitor.isNativePlatform()) {
      return { remove: async () => {} };
    }

    return await PushNotifications.addListener('pushNotificationReceived', callback);
  }

  /**
   * Add listener for notification tap actions (user taps notification).
   * 
   * Invoked when the user taps a notification in the system tray.
   * Use to navigate to relevant content or trigger app-specific actions.
   * 
   * @param callback - Handler invoked with action details
   * @returns Listener handle (call .remove() to unregister)
   */
  static async addNotificationActionListener(
    callback: (action: ActionPerformed) => void
  ): Promise<PluginListenerHandle> {
    if (!Capacitor.isNativePlatform()) {
      return { remove: async () => {} };
    }

    return await PushNotifications.addListener('pushNotificationActionPerformed', callback);
  }

  /**
   * Initialize internal event listeners for registration and errors.
   * Called automatically by registerDevice().
   * 
   * @private
   */
  private static _initializeListeners(): void {
    if (!Capacitor.isNativePlatform()) {
      return;
    }

    PushNotifications.addListener('registrationError', (error: RegistrationError) => {
      console.error('[PrismPush] Registration error:', error);
    });
  }

  /**
   * Clean up all listeners. Call before app shutdown if needed.
   */
  static async removeAllListeners(): Promise<void> {
    if (!Capacitor.isNativePlatform()) {
      return;
    }

    await PushNotifications.removeAllListeners();
    this._initialized = false;
  }
}
