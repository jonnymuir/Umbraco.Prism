
# The native tab shell (native/android/, see the README's "Native shell" section) replaces
# Capacitor's single-webview MainActivity.
echo "Adding the native tab shell..."
JAVA_DIR="android/app/src/main/java/@@PRISM_JAVA_DIR@@"
if [ -d "$JAVA_DIR" ]; then
  cp native/android/java/*.java "$JAVA_DIR/"
  mkdir -p android/app/src/main/res/xml
  cp native/android/res/xml/network_security_config.xml android/app/src/main/res/xml/

  # Debug builds also trust certificates the device user installed, such as a local development
  # certificate; release builds trust only the system's (res/xml/network_security_config.xml).
  ANDROID_MANIFEST="android/app/src/main/AndroidManifest.xml"
  if ! grep -q "networkSecurityConfig" "$ANDROID_MANIFEST"; then
    perl -0pi -e 's/<application\b/<application\n        android:networkSecurityConfig="\@xml\/network_security_config"/' "$ANDROID_MANIFEST"
  fi

  # The sign-in callback, on the site's own domain, opens this app once Android has checked the
  # site's assetlinks.json. Auth Tab receives it directly; a Custom Tab needs this App Link.
  if ! grep -q "/umbraco/prism/mobile/auth/callback" "$ANDROID_MANIFEST"; then
    perl -0pi -e 's#(<category android:name="android.intent.category.LAUNCHER" />\s*</intent-filter>)#$1\n\n            <intent-filter android:autoVerify="true">\n                <action android:name="android.intent.action.VIEW" />\n                <category android:name="android.intent.category.DEFAULT" />\n                <category android:name="android.intent.category.BROWSABLE" />\n                <data android:scheme="https" android:host="@@PRISM_HOST@@" android:path="/umbraco/prism/mobile/auth/callback" />\n            </intent-filter>#' "$ANDROID_MANIFEST"
    echo "✓ Sign-in callback App Link added"
  fi

  # androidx.browser 1.9, the first with Auth Tab, needs Android Gradle plugin 8.9.1 or later.
  sed -i.bak -E 's/(com\.android\.tools\.build:gradle:)8\.[0-8]\.[0-9]+/\18.9.1/' android/build.gradle
  rm -f android/build.gradle.bak

  APP_GRADLE="android/app/build.gradle"
  for DEPENDENCY in "com.google.android.material:material:1.12.0" "androidx.browser:browser:1.9.0"; do
    if ! grep -q "${DEPENDENCY%:*}" "$APP_GRADLE"; then
      perl -pi -e "s/^(\\s*)(implementation project\\(.:capacitor-android.\\))\\s*\$/\$1\$2\n\$1implementation \"$DEPENDENCY\"\n/" "$APP_GRADLE"
      echo "✓ $DEPENDENCY added"
    fi
  done
else
  echo "⚠️ $JAVA_DIR not found. Run 'npx cap add android' first."
fi
