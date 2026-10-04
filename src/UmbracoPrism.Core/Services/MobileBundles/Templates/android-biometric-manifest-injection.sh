
echo "Injecting USE_BIOMETRIC permission into AndroidManifest.xml..."
MANIFEST_PATH="android/app/src/main/AndroidManifest.xml"
if [ -f "$MANIFEST_PATH" ]; then
  if ! grep -q "android.permission.USE_BIOMETRIC" "$MANIFEST_PATH"; then
    # Insert USE_BIOMETRIC permission before the <application> tag (perl for macOS/Linux compat)
    perl -i -pe 's|(<application)|    <uses-permission android:name="android.permission.USE_BIOMETRIC" />\n$1|' "$MANIFEST_PATH"
    echo "✓ USE_BIOMETRIC permission added to AndroidManifest.xml"
  else
    echo "✓ USE_BIOMETRIC permission already present in AndroidManifest.xml"
  fi
else
  echo "⚠️ AndroidManifest.xml not found. Run 'npx cap add android' first."
fi
