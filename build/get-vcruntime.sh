#!/usr/bin/env bash
# Fetches Microsoft's official Visual C++ 2015-2022 Redistributable (x64) and extracts the four DLLs the
# AI engine (llama.cpp) needs on Windows, into build/tools/vcruntime/. They are shipped app-local next to
# Unplugged.exe (Microsoft permits redistributing these files with an application), so the setup needs
# no admin rights and no separate installer. Nothing is executed: the files are only unpacked.
set -euo pipefail
OUT="$(cd "$(dirname "$0")" && pwd)/tools/vcruntime"
WORK="$(mktemp -d)"; trap 'rm -rf "$WORK"' EXIT
mkdir -p "$OUT"
curl -sSL --fail -o "$WORK/vc_redist.x64.exe" https://aka.ms/vs/17/release/vc_redist.x64.exe
python3 - "$WORK" <<'PY'
import struct, sys
w = sys.argv[1]; b = open(f"{w}/vc_redist.x64.exe", "rb").read(); i = n = 0
while (i := b.find(b"MSCF\0\0\0\0", i)) >= 0:
    size = struct.unpack("<I", b[i+8:i+12])[0]
    if 0 < size <= len(b) - i:
        open(f"{w}/outer{n}.cab", "wb").write(b[i:i+size]); n += 1
    i += 4
PY
for c in "$WORK"/outer*.cab; do 7z x -y -o"$WORK/pay" "$c" >/dev/null; done
for f in "$WORK"/pay/*; do
  if 7z l "$f" 2>/dev/null | grep -q "vcruntime140.dll_amd64"; then
    7z e -y -o"$WORK/x64" "$f" msvcp140.dll_amd64 vcruntime140.dll_amd64 vcruntime140_1.dll_amd64 vcomp140.dll_amd64 >/dev/null
  fi
done
for d in msvcp140 vcruntime140 vcruntime140_1 vcomp140; do cp "$WORK/x64/$d.dll_amd64" "$OUT/$d.dll"; done
file "$OUT"/*.dll | sed 's/,.*//'
