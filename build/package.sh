#!/usr/bin/env bash
# Builds the three ONE-CLICK Unplugged downloads into dist/:
#   Unplugged-Setup-Windows-x64.exe       setup program: installs per-user (no admin prompt), adds shortcuts, opens the app
#   Unplugged-macOS-AppleSilicon.dmg      disk image: drag Unplugged into Applications (ad-hoc signed, not notarized)
#   Unplugged-Linux-x64.AppImage          single file: make it executable once, then double-click
#   SHA256SUMS.txt
# Each bundles the AI model and its Gemma license files. Builds are NOT code-signed/notarized (by choice):
# the website explains the one-time OS warning.
#
# Needs: dotnet, makensis (apt: nsis), mksquashfs (squashfs-tools), mkfs.hfsplus (hfsprogs), and
# build/tools/runtime-x86_64 (AppImage type2 runtime from github.com/AppImage/type2-runtime), and
# build/tools/vcruntime/*.dll (Microsoft VC++ runtime, fetched by build/get-vcruntime.sh; needs 7z).
# The Mac disk image is assembled on a loop mount, so the build asks for your password once (sudo -A).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DIST="$ROOT/dist"; STAGE="$DIST/stage"
VERSION="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$ROOT/src/Unplugged.App/Unplugged.App.csproj")"
MODEL="$ROOT/models/gemma-3-1b-it-qat-Q4_0.gguf"
ICONS="$ROOT/src/Unplugged.App/Assets"
APPIMAGE_RUNTIME="$ROOT/build/tools/runtime-x86_64"
[ -f "$MODEL" ] || "$ROOT/build/get-model.sh"
[ -f "$APPIMAGE_RUNTIME" ] || { echo "missing $APPIMAGE_RUNTIME (see header)"; exit 1; }
rm -rf "$DIST"; mkdir -p "$STAGE"

publish() {   # $1 = runtime id, $2 = output dir
  dotnet publish "$ROOT/src/Unplugged.App" -c Release -r "$1" --self-contained true \
    -p:DebugType=none -p:DebugSymbols=false -o "$2" -v q 2>&1 | grep -v "warning CS0618" || true
  # keep only this platform's AI engine (LLamaSharp ships all 7 platforms in every build)
  find "$2/runtimes" -mindepth 1 -maxdepth 1 -type d ! -name "$1" -exec rm -rf {} +
  rm -rf "$2/runtimes/osx-arm64/native/rosetta2"
}

add_model() {   # $1 = models dir
  mkdir -p "$1"
  cp "$MODEL" "$ROOT/models/NOTICE.txt" "$ROOT/models/GEMMA_TERMS_OF_USE.txt" "$ROOT/models/GEMMA_PROHIBITED_USE_POLICY.txt" "$1/"
}

add_licenses() {   # $1 = dir
  cp "$ROOT/LICENSE" "$1/LICENSE.txt"
  cp "$ROOT/THIRD_PARTY_NOTICES.md" "$1/THIRD_PARTY_NOTICES.md"
}

# ---------- Windows: setup program ----------
echo ">> Windows setup"
W="$STAGE/win/Unplugged"
publish win-x64 "$W"
add_model "$W/models"
add_licenses "$W"
# The AI engine's Windows files need Microsoft's Visual C++ runtime; ship it app-local so fresh
# Windows installs work without a separate (admin-only) redistributable install.
[ -f "$ROOT/build/tools/vcruntime/vcruntime140.dll" ] || "$ROOT/build/get-vcruntime.sh"
cp "$ROOT"/build/tools/vcruntime/*.dll "$W/"
makensis -V2 -DVERSION="$VERSION" -DSRC="$W" -DOUT="$DIST/Unplugged-Setup-Windows-x64.exe" \
  -DICON="$ICONS/unplugged.ico" "$ROOT/build/installer/unplugged.nsi"

# ---------- Linux: AppImage ----------
echo ">> Linux AppImage"
A="$STAGE/linux/Unplugged.AppDir"
publish linux-x64 "$A/usr/lib/unplugged"
add_model "$A/usr/lib/unplugged/models"
add_licenses "$A/usr/lib/unplugged"
chmod +x "$A/usr/lib/unplugged/Unplugged"
cat > "$A/AppRun" <<'RUN'
#!/bin/sh
HERE="$(dirname "$(readlink -f "$0")")"
exec "$HERE/usr/lib/unplugged/Unplugged" "$@"
RUN
chmod +x "$A/AppRun"
cat > "$A/unplugged.desktop" <<DESK
[Desktop Entry]
Type=Application
Name=Unplugged
GenericName=Offline AI demo
Comment=AI that lives entirely on your computer
Exec=Unplugged
Icon=unplugged
Categories=Education;Utility;
Terminal=false
X-AppImage-Version=$VERSION
DESK
cp "$ICONS/unplugged-256.png" "$A/unplugged.png"
ln -s unplugged.png "$A/.DirIcon"
mksquashfs "$A" "$STAGE/linux/app.squashfs" -root-owned -noappend -comp zstd -quiet -no-progress
cat "$APPIMAGE_RUNTIME" "$STAGE/linux/app.squashfs" > "$DIST/Unplugged-Linux-x64.AppImage"
chmod +x "$DIST/Unplugged-Linux-x64.AppImage"

# ---------- macOS: .dmg (drag to Applications) ----------
echo ">> macOS disk image"
M="$STAGE/mac"
APP="$M/Unplugged.app"
mkdir -p "$APP/Contents/Resources"
publish osx-arm64 "$APP/Contents/MacOS"
add_model "$APP/Contents/Resources/models"
add_licenses "$APP/Contents/Resources"
cp "$ICONS/unplugged.icns" "$APP/Contents/Resources/unplugged.icns"
cat > "$APP/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
  <key>CFBundleName</key><string>Unplugged</string>
  <key>CFBundleDisplayName</key><string>Unplugged</string>
  <key>CFBundleIdentifier</key><string>org.keepailocal.unplugged</string>
  <key>CFBundleVersion</key><string>$VERSION</string>
  <key>CFBundleShortVersionString</key><string>$VERSION</string>
  <key>CFBundleExecutable</key><string>Unplugged</string>
  <key>CFBundleIconFile</key><string>unplugged.icns</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>LSMinimumSystemVersion</key><string>13.0</string>
  <key>NSHighResolutionCapable</key><true/>
  <key>LSApplicationCategoryType</key><string>public.app-category.education</string>
</dict></plist>
PLIST
chmod +x "$APP/Contents/MacOS/Unplugged"
cp "$ROOT/build/readme/mac-dmg.txt" "$M/If your Mac warns you.txt"
SIZE_MB=$(( $(du -sm "$M" | cut -f1) + 60 ))
IMG="$DIST/Unplugged-macOS-AppleSilicon.dmg"
truncate -s "${SIZE_MB}M" "$IMG"
mkfs.hfsplus -v "Unplugged" "$IMG" >/dev/null
MNT="$(mktemp -d)"
sudo -A sh -c "mount -t hfsplus -o loop,rw '$IMG' '$MNT' && cp -a '$APP' '$MNT/' && cp '$M/If your Mac warns you.txt' '$MNT/' \
  && ln -s /Applications '$MNT/Applications' && chown -R 501:20 '$MNT/Unplugged.app' '$MNT/If your Mac warns you.txt' \
  && sync && umount '$MNT'"
rmdir "$MNT"

# ---------- checksums ----------
(cd "$DIST" && sha256sum Unplugged-* > SHA256SUMS.txt)
rm -rf "$STAGE"
echo; ls -la "$DIST"; echo; cat "$DIST/SHA256SUMS.txt"
