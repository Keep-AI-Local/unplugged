#!/usr/bin/env bash
# Uploads the website (site/) to the web server that serves keepailocal.org (Apache site from
# deploy/website/keepailocal.conf, web root /var/www/keepailocal), over SSH with rsync.
# Server address and SSH key come from deploy/website/deploy.local (private, not in git):
#   HOST=user@your-server
#   KEY=/path/to/ssh-key.pem
set -euo pipefail
HERE="$(cd "$(dirname "$0")/.." && pwd)"
[ -f "$HERE/deploy/website/deploy.local" ] && . "$HERE/deploy/website/deploy.local"
: "${HOST:?set HOST in deploy/website/deploy.local}"
: "${KEY:?set KEY in deploy/website/deploy.local}"
ROOT_DIR="/var/www/keepailocal"

KEY_TMP="$(mktemp)"; trap 'rm -f "$KEY_TMP"' EXIT
install -m 600 "$KEY" "$KEY_TMP"
SSH="ssh -i $KEY_TMP -o IdentitiesOnly=yes"

rsync -az --delete --chmod=D755,F644 --exclude ".*" -e "$SSH" "$HERE/site/" "$HOST:/tmp/keepailocal-upload/"
$SSH "$HOST" "sudo rsync -a --delete /tmp/keepailocal-upload/ $ROOT_DIR/ && sudo chown -R root:root $ROOT_DIR && rm -rf /tmp/keepailocal-upload"
echo "Deployed site/ to $ROOT_DIR"
