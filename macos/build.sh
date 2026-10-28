#!/bin/zsh
# Builds ZXP Manager as a native Apple Silicon app.
# Requirements: Xcode Command Line Tools (xcode-select --install)
set -e
cd "$(dirname "$0")"

OUT="build"
APP="$OUT/ZXP Manager.app"
VERSION="${ZXP_VERSION:-1.0.0}"   # set by the release workflow from the git tag
BUILD="${ZXP_BUILD:-1}"
I18N="../i18n"

# Output goes to macos/build/ (created if missing; any previous build is replaced)
mkdir -p "$OUT"
rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"

echo "Compiling..."
swiftc -O -parse-as-library -target arm64-apple-macos13.0 \
  ZXPManager.swift -o "$APP/Contents/MacOS/ZXPManager"

echo "Adding translations..."
count=0
for json in "$I18N"/*.json; do
  lang="$(basename "$json" .json)"
  mkdir -p "$APP/Contents/Resources/$lang.lproj"
  plutil -convert binary1 -o "$APP/Contents/Resources/$lang.lproj/Localizable.strings" "$json"
  count=$((count + 1))
done
echo "  $count languages"

# Icon: the Icon Composer file (AppIcon.icon) needs full Xcode, which compiles it into
# Assets.car (adaptive Liquid Glass icon on macOS 26+) plus AppIcon.icns for older macOS.
# Without Xcode, AppIcon.png is converted to a classic .icns instead.
XCODE_DEV="${XCODE_DEVELOPER_DIR:-/Applications/Xcode.app/Contents/Developer}"
if [[ -d AppIcon.icon && -d "$XCODE_DEV" ]]; then
  echo "Compiling icon with Xcode (Icon Composer)..."
  DEVELOPER_DIR="$XCODE_DEV" xcrun actool AppIcon.icon \
    --compile "$APP/Contents/Resources" \
    --app-icon AppIcon \
    --platform macosx \
    --minimum-deployment-target 13.0 \
    --output-partial-info-plist "$OUT/icon-partial.plist" > "$OUT/actool.log" 2>&1 || true
  if [[ -f "$APP/Contents/Resources/Assets.car" ]]; then
    echo "  $(ls "$APP/Contents/Resources" | grep -E 'Assets.car|AppIcon.icns' | tr '\n' ' ')"
  else
    echo "  WARNING: Xcode could not compile AppIcon.icon. Details:"
    sed 's/^/    /' "$OUT/actool.log"
  fi
elif [[ -f AppIcon.png ]]; then
  echo "Adding icon (classic, Xcode not found)..."
  ICONSET="$(mktemp -d)/AppIcon.iconset"
  mkdir -p "$ICONSET"
  for size in 16 32 128 256 512; do
    sips -z $size $size AppIcon.png --out "$ICONSET/icon_${size}x${size}.png" >/dev/null
    sips -z $((size * 2)) $((size * 2)) AppIcon.png --out "$ICONSET/icon_${size}x${size}@2x.png" >/dev/null
  done
  iconutil -c icns "$ICONSET" -o "$APP/Contents/Resources/AppIcon.icns"
else
  echo "WARNING: no icon found (expected AppIcon.icon or AppIcon.png next to build.sh)."
  [[ -d AppIcon.icon ]] && echo "  AppIcon.icon is present, but Xcode was not found at: $XCODE_DEV"
fi

cat > "$APP/Contents/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleExecutable</key><string>ZXPManager</string>
  <key>CFBundleIdentifier</key><string>com.oddmoth.zxpmanager</string>
  <key>CFBundleName</key><string>ZXP Manager</string>
  <key>CFBundleDisplayName</key><string>ZXP Manager</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleShortVersionString</key><string>$VERSION</string>
  <key>CFBundleVersion</key><string>$BUILD</string>
  <key>CFBundleDevelopmentRegion</key><string>en</string>
  <key>NSHumanReadableCopyright</key><string>© 2026 Odd Moth · MIT License</string>
  <key>CFBundleIconFile</key><string>AppIcon</string>
  <key>CFBundleIconName</key><string>AppIcon</string>
  <key>LSMinimumSystemVersion</key><string>13.0</string>
  <key>NSHighResolutionCapable</key><true/>
  <key>NSPrincipalClass</key><string>NSApplication</string>
  <key>UTImportedTypeDeclarations</key>
  <array>
    <dict>
      <key>UTTypeIdentifier</key><string>com.adobe.zxp</string>
      <key>UTTypeDescription</key><string>Adobe ZXP extension</string>
      <key>UTTypeConformsTo</key><array><string>public.data</string></array>
      <key>UTTypeTagSpecification</key>
      <dict><key>public.filename-extension</key><array><string>zxp</string></array></dict>
    </dict>
    <dict>
      <key>UTTypeIdentifier</key><string>com.adobe.ccx</string>
      <key>UTTypeDescription</key><string>Adobe CCX plugin</string>
      <key>UTTypeConformsTo</key><array><string>public.data</string></array>
      <key>UTTypeTagSpecification</key>
      <dict><key>public.filename-extension</key><array><string>ccx</string></array></dict>
    </dict>
  </array>
  <key>CFBundleDocumentTypes</key>
  <array>
    <dict>
      <key>CFBundleTypeName</key><string>Adobe extension</string>
      <key>CFBundleTypeRole</key><string>Viewer</string>
      <key>LSHandlerRank</key><string>Alternate</string>
      <key>LSItemContentTypes</key>
      <array><string>com.adobe.zxp</string><string>com.adobe.ccx</string></array>
    </dict>
  </array>
</dict>
</plist>
EOF

codesign --force --sign - "$APP"
echo "Done: $APP $VERSION ($(lipo -archs "$APP/Contents/MacOS/ZXPManager"))"
