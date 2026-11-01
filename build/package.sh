#!/usr/bin/env bash
# Builds the three Unplugged downloads into dist/:
#   Unplugged-Windows-x64.zip            (folder with Unplugged.exe)
#   Unplugged-macOS-AppleSilicon.zip      (Unplugged.app; ad-hoc signed by the .NET SDK, not notarized)
#   Unplugged-Linux-x64.tar.gz            (folder with the Unplugged program)
#   SHA256SUMS.txt
# Each one bundles the AI model, its Gemma license files, and a "README-FIRST" with opening instructions.
# Builds are NOT code-signed/notarized (by choice): the README and website explain the OS warning.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DIST="$ROOT/dist"; STAGE="$DIST/stage"
VERSION="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$ROOT/src/Unplugged.App/Unplugged.App.csproj")"
MODEL="$ROOT/models/gemma-3-1b-it-qat-Q4_0.gguf"
[ -f "$MODEL" ] || "$ROOT/build/get-model.sh"
rm -rf "$DIST"; mkdir -p "$STAGE"

publish() {   # $1 = runtime id, $2 = output dir
  dotnet publish "$ROOT/src/Unplugged.App" -c Release -r "$1" --self-contained true \
    -p:DebugType=none -p:DebugSymbols=false -o "$2" -v q
  # keep only this platform's AI engine (LLamaSharp ships all 7 platforms in every build)
  find "$2/runtimes" -mindepth 1 -maxdepth 1 -type d ! -name "$1" -exec rm -rf {} +
  # Intel-Mac "rosetta2" variant is not needed on Apple Silicon
  rm -rf "$2/runtimes/osx-arm64/native/rosetta2"
}

add_model() {   # $1 = models dir
  mkdir -p "$1"
  cp "$MODEL" "$ROOT/models/NOTICE.txt" "$ROOT/models/GEMMA_TERMS_OF_USE.txt" "$ROOT/models/GEMMA_PROHIBITED_USE_POLICY.txt" "$1/"
}

add_docs() {    # $1 = dir, $2 = readme source
  cp "$ROOT/LICENSE" "$1/LICENSE.txt"
  cp "$ROOT/THIRD_PARTY_NOTICES.md" "$1/THIRD_PARTY_NOTICES.md"
  cp "$2" "$1/README-FIRST.txt"
}

# ---------- Windows ----------
echo ">> Windows"
W="$STAGE/win/Unplugged"
publish win-x64 "$W"
add_model "$W/models"
add_docs "$W" "$ROOT/build/readme/windows.txt"
(cd "$STAGE/win" && zip -q -r -9 "$DIST/Unplugged-Windows-x64.zip" Unplugged)

# ---------- Linux ----------
echo ">> Linux"
L="$STAGE/linux/Unplugged"
publish linux-x64 "$L"
add_model "$L/models"
add_docs "$L" "$ROOT/build/readme/linux.txt"
cp "$ROOT/src/Unplugged.App/Assets/unplugged.png" "$L/unplugged.png"
chmod +x "$L/Unplugged"
(cd "$STAGE/linux" && tar -czf "$DIST/Unplugged-Linux-x64.tar.gz" Unplugged)

# ---------- macOS (Apple Silicon) ----------
echo ">> macOS"
APP="$STAGE/mac/Unplugged.app"
mkdir -p "$APP/Contents/Resources"
publish osx-arm64 "$APP/Contents/MacOS"
add_model "$APP/Contents/Resources/models"
cp "$ROOT/src/Unplugged.App/Assets/unplugged.icns" "$APP/Contents/Resources/unplugged.icns"
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
cp "$ROOT/build/readme/mac.txt" "$STAGE/mac/README-FIRST.txt"
cp "$ROOT/LICENSE" "$STAGE/mac/LICENSE.txt"
(cd "$STAGE/mac" && zip -q -r -y -9 "$DIST/Unplugged-macOS-AppleSilicon.zip" Unplugged.app README-FIRST.txt LICENSE.txt)

# ---------- checksums ----------
(cd "$DIST" && sha256sum Unplugged-* > SHA256SUMS.txt)
rm -rf "$STAGE"
echo; ls -la "$DIST"; echo; cat "$DIST/SHA256SUMS.txt"
