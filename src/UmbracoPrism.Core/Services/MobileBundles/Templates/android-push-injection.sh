
echo "Placing google-services.json for Firebase Cloud Messaging..."
if [ -f resources/google-services.json ]; then
  cp resources/google-services.json android/app/google-services.json
  echo "✓ google-services.json copied into android/app/"
else
  echo "⚠️ resources/google-services.json not found — download it from Firebase Console"
  echo "   (Project settings → your Android app) and place it at resources/google-services.json"
  echo "   before running this script, or push notifications will not work on a device."
fi
