// Adds journey statuses to the Xcode project (see the README's "Journey status" section): the
// app's own Swift files, and the PrismJourneyStatus widget extension target, which draws a
// journey's Live Activity on the lock screen and in the Dynamic Island. Run by bootstrap-ios.sh
// after it copies native/ios-journey-status/ into ios/App/.
import xcode from 'xcode';
import fs from 'node:fs';

const extensionName = 'PrismJourneyStatus';
const pbxprojPath = 'ios/App/App.xcodeproj/project.pbxproj';
const project = xcode.project(pbxprojPath);
project.parseSync();

const objects = project.hash.project.objects;
const appTarget = project.getFirstTarget();
const appFiles = fs.readdirSync('native/ios-journey-status/App');
const hasFileReference = name => Object.values(objects.PBXFileReference)
  .some(ref => typeof ref?.path === 'string' && ref.path.replaceAll('"', '').endsWith(name));

for (const file of appFiles.filter(name => !hasFileReference(name))) {
  project.addSourceFile(`App/${file}`, { target: appTarget.uuid }, 'App');
  console.log(`✓ ${file} registered in project.pbxproj`);
}

if (Object.values(objects.PBXNativeTarget).some(target => String(target?.name).replaceAll('"', '') === extensionName)) {
  console.log(`✓ ${extensionName} target already present`);
} else {
  addExtension();
}

fs.writeFileSync(pbxprojPath, project.writeSync());

function addExtension() {
  const appSettings = configurationsOf(appTarget.firstTarget).find(configuration => configuration.name === 'Release').buildSettings;
  const bundleId = `${String(appSettings.PRODUCT_BUNDLE_IDENTIFIER).replaceAll('"', '')}.JourneyStatus`;
  const target = project.addTarget(extensionName, 'app_extension', extensionName, bundleId);

  // The group owns the extension's file references. Build phases created with file paths would
  // make second references outside any group, which Xcode then looks for in the wrong folder.
  const sources = fs.readdirSync(`ios/App/${extensionName}`).filter(name => name.endsWith('.swift'));
  const group = project.addPbxGroup(sources, extensionName, extensionName);
  project.addToPbxGroup(group.uuid, project.getFirstProject().firstProject.mainGroup);
  project.addBuildPhase([], 'PBXSourcesBuildPhase', 'Sources', target.uuid);
  project.addBuildPhase([], 'PBXFrameworksBuildPhase', 'Frameworks', target.uuid);
  for (const file of [...sources, 'PrismJourneyStatusAttributes.swift']) {
    compileIn(target.uuid, file);
  }

  for (const configuration of configurationsOf(target.pbxNativeTarget)) {
    Object.assign(configuration.buildSettings, {
      INFOPLIST_FILE: `${extensionName}/Info.plist`,
      IPHONEOS_DEPLOYMENT_TARGET: '17.4',
      SWIFT_VERSION: '5.0',
      TARGETED_DEVICE_FAMILY: '"1,2"',
      SDKROOT: 'iphoneos',
      CODE_SIGN_STYLE: 'Automatic',
      // None of the app's entitlements, even where a project from an earlier bootstrap set them
      // project-wide: an entitlement the extension carries must be in its provisioning profile too.
      CODE_SIGN_ENTITLEMENTS: '""',
      // An extension's versions must match its app's, or App Store Connect refuses the upload.
      // A build number given to xcodebuild applies to both.
      MARKETING_VERSION: appSettings.MARKETING_VERSION,
      CURRENT_PROJECT_VERSION: appSettings.CURRENT_PROJECT_VERSION
    });
  }

  console.log(`✓ ${extensionName} widget extension target added (${bundleId})`);
}

function configurationsOf(nativeTarget) {
  const list = objects.XCConfigurationList[nativeTarget.buildConfigurationList];
  return list.buildConfigurations.map(entry => objects.XCBuildConfiguration[entry.value]);
}

// A build file of the target's own for an existing file reference: the shared attributes file is
// compiled into both the app and the extension.
function compileIn(targetUuid, fileName) {
  const fileRef = Object.entries(objects.PBXFileReference)
    .find(([, ref]) => typeof ref?.path === 'string' && ref.path.replaceAll('"', '').endsWith(fileName))?.[0];
  if (!fileRef) throw new Error(`${fileName} has no file reference in the project`);

  const buildFile = project.generateUuid();
  objects.PBXBuildFile[buildFile] = { isa: 'PBXBuildFile', fileRef, fileRef_comment: fileName };
  objects.PBXBuildFile[`${buildFile}_comment`] = `${fileName} in Sources`;

  const sources = objects.PBXNativeTarget[targetUuid].buildPhases.find(phase => objects.PBXSourcesBuildPhase[phase.value]);
  objects.PBXSourcesBuildPhase[sources.value].files.push({ value: buildFile, comment: `${fileName} in Sources` });
}
