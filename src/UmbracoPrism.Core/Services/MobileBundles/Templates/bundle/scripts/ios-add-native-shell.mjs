// Registers the native tab shell's Swift files with Xcode, which compiles only the files a project
// lists (and builds cleanly without the rest), and sets the build settings the shell needs.
// Run by bootstrap-ios.sh after it copies native/ios/ into ios/App/App/.
import xcode from 'xcode';
import fs from 'node:fs';

const pbxprojPath = 'ios/App/App.xcodeproj/project.pbxproj';
const project = xcode.project(pbxprojPath);
project.parseSync();

const target = project.getFirstTarget().uuid;
const references = Object.values(project.hash.project.objects.PBXFileReference || {});
const isRegistered = name => references.some(ref => typeof ref?.path === 'string' && ref.path.endsWith(name));

for (const file of fs.readdirSync('native/ios').filter(name => name.endsWith('.swift'))) {
  if (!isRegistered(file)) {
    project.addSourceFile(`App/${file}`, { target }, 'App');
    console.log(`✓ ${file} registered in project.pbxproj`);
  }
}

// iOS 17.4 is the first with https callbacks for the system sign-in sheet.
project.updateBuildProperty('IPHONEOS_DEPLOYMENT_TARGET', '17.4');
project.updateBuildProperty('CODE_SIGN_ENTITLEMENTS', 'App/App.entitlements', null, 'App');
fs.writeFileSync(pbxprojPath, project.writeSync());
