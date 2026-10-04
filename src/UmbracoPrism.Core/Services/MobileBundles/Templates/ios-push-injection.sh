
echo "Injecting UIBackgroundModes (remote-notification) into Info.plist..."
if [ -f ios/App/App/Info.plist ]; then
  if ! grep -q "UIBackgroundModes" ios/App/App/Info.plist; then
    plutil -insert UIBackgroundModes -json '["remote-notification"]' ios/App/App/Info.plist
    echo "✓ UIBackgroundModes added to Info.plist"
  else
    echo "✓ UIBackgroundModes already present in Info.plist"
  fi
else
  echo "⚠️ Info.plist not found. Run 'npx cap add ios' first."
fi

echo "Writing ios/App/App/App.entitlements..."
cat > ios/App/App/App.entitlements << 'PRISM_ENTITLEMENTS_EOF'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
	<key>aps-environment</key>
	<string>production</string>
</dict>
</plist>
PRISM_ENTITLEMENTS_EOF
echo "✓ App.entitlements written"

if [ -f resources/GoogleService-Info.plist ]; then
  cp resources/GoogleService-Info.plist ios/App/App/GoogleService-Info.plist
  echo "✓ GoogleService-Info.plist copied into ios/App/App/"
else
  echo "⚠️ resources/GoogleService-Info.plist not found — download it from Firebase Console"
  echo "   (Project settings → your iOS app) and place it at resources/GoogleService-Info.plist"
  echo "   before running this script, or push notifications will not work on a device."
fi

echo "Wiring App.entitlements and GoogleService-Info.plist into project.pbxproj..."
cat > .prism-add-push-config.mjs << 'PRISM_PUSH_NODE_EOF'
import xcode from 'xcode';
import fs from 'node:fs';

const pbxprojPath = 'ios/App/App.xcodeproj/project.pbxproj';
const project = xcode.project(pbxprojPath);
project.parseSync();

project.updateBuildProperty('CODE_SIGN_ENTITLEMENTS', 'App/App.entitlements');
console.log('✓ CODE_SIGN_ENTITLEMENTS set to App/App.entitlements in project.pbxproj');

if (fs.existsSync('ios/App/App/GoogleService-Info.plist')) {
  const refs = project.hash.project.objects.PBXFileReference || {};
  const alreadyPresent = Object.values(refs).some(
    ref => ref && typeof ref === 'object' && typeof ref.path === 'string' && ref.path.includes('GoogleService-Info.plist')
  );
  if (!alreadyPresent) {
    const target = project.getFirstTarget().uuid;
    // addResourceFile() internally calls correctForResourcesPath(), which does
    // `project.pbxGroupByName('Resources').path` with NO null-check — throws
    // "Cannot read properties of null (reading 'path')" on a project with no group literally
    // named "Resources" yet, which a fresh Capacitor-generated App.xcodeproj is (confirmed
    // against xcode@3.0.1's actual source, live in CI — not a guess). Pre-creating an empty
    // one sidesteps the crash; addResourceFile's own fallback (addToResourcesPbxGroup) then
    // finds it and nests the file there, same as it would for any other plain resource.
    // No explicit group argument (was 'App', a group NAME) — addResourceFile's third
    // parameter is actually a PBXGroup KEY (uuid), not a name, so that never resolved to
    // anything anyway; omitting it takes the correct default path instead.
    if (!project.pbxGroupByName('Resources')) {
      project.addPbxGroup([], 'Resources');
    }
    project.addResourceFile('App/GoogleService-Info.plist', { target });
    console.log('✓ GoogleService-Info.plist registered in project.pbxproj (Copy Bundle Resources)');
  } else {
    console.log('✓ GoogleService-Info.plist already registered in project.pbxproj');
  }
}

fs.writeFileSync(pbxprojPath, project.writeSync());
PRISM_PUSH_NODE_EOF
node .prism-add-push-config.mjs
rm -f .prism-add-push-config.mjs

echo "Patching AppDelegate.swift for APNs→FCM token bridging..."
if [ -f ios/App/App/AppDelegate.swift ]; then
  if ! grep -q "didRegisterForRemoteNotificationsWithDeviceToken" ios/App/App/AppDelegate.swift; then
    perl -i -pe 's/(class AppDelegate: UIResponder, UIApplicationDelegate \{)/$1\n    func application(_ application: UIApplication, didRegisterForRemoteNotificationsWithDeviceToken deviceToken: Data) {\n        NotificationCenter.default.post(name: .capacitorDidRegisterForRemoteNotifications, object: deviceToken)\n    }\n\n    func application(_ application: UIApplication, didFailToRegisterForRemoteNotificationsWithError error: Error) {\n        NotificationCenter.default.post(name: .capacitorDidFailToRegisterForRemoteNotifications, object: error)\n    }\n/' ios/App/App/AppDelegate.swift
    echo "✓ AppDelegate.swift patched with remote-notification delegate methods"
  else
    echo "✓ AppDelegate.swift already patched with remote-notification delegate methods"
  fi
else
  echo "⚠️ AppDelegate.swift not found. Run 'npx cap add ios' first."
fi
