
# Saved pages run in a service worker, which WKWebView allows only for the app's own domains
# (WKAppBoundDomains, at most 10). capacitor.config.ts keeps the app's webviews to them.
echo "Declaring the app's own domains for saved pages..."
if [ -f ios/App/App/Info.plist ]; then
  plutil -remove WKAppBoundDomains ios/App/App/Info.plist 2>/dev/null || true
  plutil -insert WKAppBoundDomains -array ios/App/App/Info.plist
  index=0
  for domain in @@PRISM_APP_BOUND_DOMAINS@@; do
    plutil -insert "WKAppBoundDomains.$index" -string "$domain" ios/App/App/Info.plist
    index=$((index + 1))
  done
  echo "✓ WKAppBoundDomains: @@PRISM_APP_BOUND_DOMAINS@@"
else
  echo "⚠️ Info.plist not found. Run 'npx cap add ios' first."
fi
