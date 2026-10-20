
# The native tab shell (native/ios/, see the README's "Native shell" section) replaces
# Capacitor's single-webview AppDelegate.
echo "Adding the native tab shell..."
if [ -d ios/App/App ]; then
  cp native/ios/*.swift native/ios/App.entitlements ios/App/App/
  node scripts/ios-add-native-shell.mjs
  echo "✓ Native tab shell added"
else
  echo "⚠️ ios/App/App not found. Run 'npx cap add ios' first."
fi
