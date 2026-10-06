
echo "Injecting camera and location usage descriptions into Info.plist..."
if [ -f ios/App/App/Info.plist ]; then
  # iOS shows no permission prompt at all, and the webview's navigator.geolocation never gets a fix, unless
  # the app declares why it wants the location. Taking a photo from a file input needs the camera string.
  if ! grep -q "NSLocationWhenInUseUsageDescription" ios/App/App/Info.plist; then
    plutil -insert NSLocationWhenInUseUsageDescription -string "Used to fill in where you are when you record something in the field." ios/App/App/Info.plist
    echo "✓ NSLocationWhenInUseUsageDescription added to Info.plist"
  else
    echo "✓ NSLocationWhenInUseUsageDescription already present in Info.plist"
  fi
  if ! grep -q "NSCameraUsageDescription" ios/App/App/Info.plist; then
    plutil -insert NSCameraUsageDescription -string "Used to take a photo when you record something in the field." ios/App/App/Info.plist
    echo "✓ NSCameraUsageDescription added to Info.plist"
  else
    echo "✓ NSCameraUsageDescription already present in Info.plist"
  fi
else
  echo "⚠️ Info.plist not found. Run 'npx cap add ios' first."
fi
