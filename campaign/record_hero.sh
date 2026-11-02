#!/usr/bin/env bash
# Records the raw footage for the hero video, for real:
#   1. starts Unplugged in hands-free demo mode (CPU only, records its own window)
#   2. after a few seconds online, disconnects this computer's internet connection (nmcli)
#   3. tries to load a website in Chrome while offline and screenshots the failure
#   4. the app notices it's offline, types a prompt, and the AI answers
#   5. networking is turned back ON, always, even if anything fails
#
# Usage: campaign/record_hero.sh [output-dir]
set -u
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="${1:-$ROOT/campaign/raw/hero-$(date +%Y%m%d-%H%M%S)}"
APPDIR="$ROOT/src/Unplugged.App/bin/Debug/net10.0"
mkdir -p "$OUT"
log(){ echo "[$(date +%T)] $*" | tee -a "$OUT/record.log"; }

# Disconnect ONLY the device that carries the internet (e.g. the wired port or Wi-Fi), not all of
# NetworkManager: "nmcli networking off" also tears down virtual bridges such as libvirt's virbr0,
# which knocks virtual machines offline until the bridge is repaired by hand.
DEV="$(ip route show default | awk '{print $5; exit}')"
[ -n "$DEV" ] || { echo "no default route: already offline?"; exit 1; }

restore() {
  nmcli device connect "$DEV" >/dev/null 2>&1
  log "reconnect of $DEV requested"
}
trap restore EXIT INT TERM

# Independent safety net: even if this script is killed, the connection comes back within 3 minutes.
setsid nohup sh -c "sleep 180; nmcli device connect '$DEV'" >/dev/null 2>&1 &

log "starting app (demo mode, CPU only)"
( cd "$APPDIR" && UNPLUGGED_DEMO=1 UNPLUGGED_FORCE_CPU=1 UNPLUGGED_RECORD_DIR="$OUT" ./Unplugged >"$OUT/app.log" 2>&1 ) &
APP=$!

for _ in $(seq 1 120); do grep -q " ready" "$OUT/events.txt" 2>/dev/null && break; sleep 0.5; done
grep -q " ready" "$OUT/events.txt" 2>/dev/null || { log "app never became ready"; kill $APP 2>/dev/null; exit 1; }
log "app ready; filming 5 s online"
sleep 5

log "disconnecting $DEV (internet OFF)"
nmcli device disconnect "$DEV"
sleep 2
log "connectivity now: $(nmcli networking connectivity)"

log "loading a website in a real Chrome window while offline"
PROFILE="$(mktemp -d)"
google-chrome --user-data-dir="$PROFILE" --no-first-run --no-default-browser-check --new-window \
  --window-size=1000,1300 https://www.wikipedia.org >/dev/null 2>&1 &
sleep 6
spectacle -b -n -a -o "$OUT/browser-offline.png" >/dev/null 2>&1
pkill -f -- "--user-data-dir=$PROFILE" 2>/dev/null
sleep 1; rm -rf "$PROFILE"
log "browser screenshot: $( [ -s "$OUT/browser-offline.png" ] && echo saved || echo MISSING )"

for _ in $(seq 1 180); do grep -q " stopped" "$OUT/events.txt" 2>/dev/null && break; sleep 0.5; done
log "demo finished: $(tr '\n' ' ' < "$OUT/events.txt")"
pkill -f "^\./Unplugged$" 2>/dev/null; kill $APP 2>/dev/null

restore
for _ in $(seq 1 30); do [ "$(nmcli networking connectivity)" = "full" ] && break; sleep 1; done
log "connectivity after restore: $(nmcli networking connectivity)"
echo "$OUT"
