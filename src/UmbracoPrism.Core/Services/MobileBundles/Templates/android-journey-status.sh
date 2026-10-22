
# Journey statuses (native/android-journey-status/, see the README's "Journey status" section):
# the app's own Firebase messaging service, in place of the push plugin's, shows them as Live
# Updates, and MainActivity tells the site the app shows them.
echo "Adding journey statuses..."
JAVA_DIR="android/app/src/main/java/@@PRISM_JAVA_DIR@@"
ANDROID_MANIFEST="android/app/src/main/AndroidManifest.xml"
if [ -d "$JAVA_DIR" ]; then
  cp native/android-journey-status/java/*.java "$JAVA_DIR/"

  if ! grep -q "xmlns:tools" "$ANDROID_MANIFEST"; then
    perl -0pi -e 's#<manifest\b#<manifest xmlns:tools="http://schemas.android.com/tools"#' "$ANDROID_MANIFEST"
  fi
  if ! grep -q "PrismJourneyStatusService" "$ANDROID_MANIFEST"; then
    perl -0pi -e 's#(\s*</application>)#\n\n        <service android:name=".PrismJourneyStatusService" android:exported="false">\n            <intent-filter>\n                <action android:name="com.google.firebase.MESSAGING_EVENT" />\n            </intent-filter>\n        </service>\n        <service android:name="io.capawesome.capacitorjs.plugins.firebase.messaging.MessagingService" tools:node="remove" />$1#' "$ANDROID_MANIFEST"
    perl -0pi -e 's#(\n\s*<application\b)#\n    <uses-permission android:name="android.permission.POST_PROMOTED_NOTIFICATIONS" />$1#' "$ANDROID_MANIFEST"
  fi

  # The push plugin ships Firebase Messaging; the app only compiles against it, for its service.
  APP_GRADLE="android/app/build.gradle"
  if ! grep -q "firebase-messaging" "$APP_GRADLE"; then
    perl -pi -e 's/^(\s*)(implementation project\(.:capacitor-android.\))\s*$/$1$2\n$1compileOnly "com.google.firebase:firebase-messaging:24.1.0"\n/' "$APP_GRADLE"
  fi

  if ! grep -q "PrismJourneyStatusDevice" "$JAVA_DIR/MainActivity.java"; then
    perl -0pi -e 's/(icons = new PrismTabIcons\(this\);\n)/$1        PrismJourneyStatusDevice.start(this);\n/' "$JAVA_DIR/MainActivity.java"
  fi
  echo "✓ Journey statuses added"
else
  echo "⚠️ $JAVA_DIR not found. Run 'npx cap add android' first."
fi
