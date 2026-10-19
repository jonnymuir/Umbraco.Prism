
echo "Adding the privacy manifest (PrivacyInfo.xcprivacy)..."
cp resources/PrivacyInfo.xcprivacy ios/App/App/PrivacyInfo.xcprivacy
cat > .prism-add-privacy-manifest.mjs << 'PRISM_PRIVACY_NODE_EOF'
import xcode from 'xcode';
import fs from 'node:fs';

const pbxprojPath = 'ios/App/App.xcodeproj/project.pbxproj';
const project = xcode.project(pbxprojPath);
project.parseSync();

const refs = project.hash.project.objects.PBXFileReference || {};
const alreadyPresent = Object.values(refs).some(
  ref => ref && typeof ref === 'object' && typeof ref.path === 'string' && ref.path.includes('PrivacyInfo.xcprivacy')
);
if (!alreadyPresent) {
  // addResourceFile needs a group named "Resources", which a fresh Capacitor project lacks.
  if (!project.pbxGroupByName('Resources')) {
    project.addPbxGroup([], 'Resources');
  }
  project.addResourceFile('App/PrivacyInfo.xcprivacy', { target: project.getFirstTarget().uuid });
  fs.writeFileSync(pbxprojPath, project.writeSync());
  console.log('✓ PrivacyInfo.xcprivacy registered in project.pbxproj (Copy Bundle Resources)');
} else {
  console.log('✓ PrivacyInfo.xcprivacy already registered in project.pbxproj');
}
PRISM_PRIVACY_NODE_EOF
node .prism-add-privacy-manifest.mjs
rm -f .prism-add-privacy-manifest.mjs
