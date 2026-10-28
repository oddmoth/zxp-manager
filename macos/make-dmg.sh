#!/bin/zsh
# Packages the built app into a .dmg: app + Applications shortcut + "How to open" guide,
# on a background image. Run build.sh first.
#
# Uses create-dmg (brew install create-dmg) for the window layout; without it,
# falls back to a plain .dmg with the same contents.
#
# Usage: zsh make-dmg.sh [output.dmg]
set -e
cd "$(dirname "$0")"

APP="build/ZXP Manager.app"
OUT="${1:-build/ZXP-Manager.dmg}"
GUIDE="../docs/how-to-open.html"
GUIDE_NAME="How to open ZXP Manager.html"

[[ -d "$APP" ]] || { echo "Build the app first: zsh build.sh"; exit 1; }

STAGE="$(mktemp -d)"
cp -R "$APP" "$STAGE/"
rm -f "$OUT"

# Background: one image holding both resolutions, so it stays sharp on Retina displays
BG_DIR="$(mktemp -d)"
BG="$BG_DIR/background.tiff"
tiffutil -cathidpicheck dmg/background.png dmg/background@2x.png -out "$BG" >/dev/null

if command -v create-dmg >/dev/null; then
  echo "Creating the .dmg with create-dmg..."
  for attempt in 1 2 3; do
    if create-dmg \
        --volname "ZXP Manager" \
        --background "$BG" \
        --window-pos 200 120 \
        --window-size 660 440 \
        --icon-size 100 \
        --text-size 12 \
        --icon "ZXP Manager.app" 165 175 \
        --hide-extension "ZXP Manager.app" \
        --app-drop-link 495 175 \
        --add-file "$GUIDE_NAME" "$GUIDE" 330 330 \
        --no-internet-enable \
        "$OUT" "$STAGE"; then
      break
    fi
    echo "create-dmg failed (attempt $attempt), retrying..."
    rm -f "$OUT"
    sleep 5
  done
fi

if [[ ! -f "$OUT" ]]; then
  echo "Creating a plain .dmg (no custom layout)..."
  ln -s /Applications "$STAGE/Applications"
  cp "$GUIDE" "$STAGE/$GUIDE_NAME"
  hdiutil create -volname "ZXP Manager" -srcfolder "$STAGE" -ov -format UDZO "$OUT" >/dev/null
fi

rm -rf "$STAGE" "$BG_DIR"
echo "Done: $OUT ($(du -h "$OUT" | cut -f1))"
