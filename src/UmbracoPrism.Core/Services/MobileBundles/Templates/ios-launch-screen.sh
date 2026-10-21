
# capacitor-assets writes every scale of the launch image at 2732px. Drawn at 3x, that is more than
# iOS will draw for a launch screen, which then shows black. Each scale is sized to 683pt instead.
echo "Sizing the launch screen images..."
SPLASH_DIR=ios/App/App/Assets.xcassets/Splash.imageset
if [ -d "$SPLASH_DIR" ]; then
  for scale in 1 2 3; do
    for image in "$SPLASH_DIR"/Default@${scale}x~*.png; do
      [ -f "$image" ] && sips -z $((683 * scale)) $((683 * scale)) "$image" >/dev/null
    done
  done
  echo "✓ Launch screen images sized"
else
  echo "⚠️ $SPLASH_DIR not found. Run 'npx capacitor-assets generate --ios' first."
fi
