
# Journey statuses (native/ios-journey-status/, see the README's "Journey status" section): the
# app's Live Activity code, and the PrismJourneyStatus widget extension that draws them.
echo "Adding journey statuses..."
if [ -d ios/App/App ]; then
  cp native/ios-journey-status/App/*.swift ios/App/App/
  mkdir -p ios/App/PrismJourneyStatus
  cp native/ios-journey-status/PrismJourneyStatus/* ios/App/PrismJourneyStatus/
  node scripts/ios-add-journey-status.mjs
  plutil -replace NSSupportsLiveActivities -bool YES ios/App/App/Info.plist
  if ! grep -q "PrismJourneyStatusDevice" ios/App/App/AppDelegate.swift; then
    perl -0pi -e 's/(window\?\.rootViewController = PrismTabBarController\(\)\n)/$1        PrismJourneyStatusDevice.shared.start()\n/' ios/App/App/AppDelegate.swift
  fi
  echo "✓ Journey statuses added"
else
  echo "⚠️ ios/App/App not found. Run 'npx cap add ios' first."
fi
