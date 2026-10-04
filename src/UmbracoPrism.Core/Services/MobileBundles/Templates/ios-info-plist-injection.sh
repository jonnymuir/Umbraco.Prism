
echo "Injecting NSFaceIDUsageDescription into Info.plist..."
if [ -f ios/App/App/Info.plist ]; then
  if ! grep -q "NSFaceIDUsageDescription" ios/App/App/Info.plist; then
    plutil -insert NSFaceIDUsageDescription -string "We use Face ID to securely log you in without requiring your password each time." ios/App/App/Info.plist
    echo "✓ NSFaceIDUsageDescription added to Info.plist"
  else
    echo "✓ NSFaceIDUsageDescription already present in Info.plist"
  fi
else
  echo "⚠️ Info.plist not found. Run 'npx cap add ios' first."
fi
