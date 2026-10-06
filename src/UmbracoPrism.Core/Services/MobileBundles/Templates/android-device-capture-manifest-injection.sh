
echo "Injecting camera and location permissions into AndroidManifest.xml..."
MANIFEST_PATH="android/app/src/main/AndroidManifest.xml"
if [ -f "$MANIFEST_PATH" ]; then
  for PERMISSION in ACCESS_FINE_LOCATION ACCESS_COARSE_LOCATION CAMERA; do
    if ! grep -q "android.permission.$PERMISSION" "$MANIFEST_PATH"; then
      # Insert before the <application> tag (perl for macOS/Linux compat)
      perl -i -pe "s|(<application)|    <uses-permission android:name=\"android.permission.$PERMISSION\" />\n\$1|" "$MANIFEST_PATH"
      echo "✓ $PERMISSION permission added to AndroidManifest.xml"
    else
      echo "✓ $PERMISSION permission already present in AndroidManifest.xml"
    fi
  done
  # A phone without a camera can still install the app: the camera is only used when a page asks for a photo.
  if ! grep -q "android.hardware.camera" "$MANIFEST_PATH"; then
    perl -i -pe 's|(<application)|    <uses-feature android:name="android.hardware.camera" android:required="false" />\n$1|' "$MANIFEST_PATH"
    echo "✓ optional camera feature added to AndroidManifest.xml"
  fi
else
  echo "⚠️ AndroidManifest.xml not found. Run 'npx cap add android' first."
fi
