#!/usr/bin/env bash
set -euo pipefail

# Wrapper to run the Playwright Supreme example and store artifacts
# Usage:
#   scripts/run-playwright-supreme.sh [--prefix NAME] [--] [node-args]
# Env:
#   BASE_URL (default: http://localhost:5000)
#   USERNAME (default: admin)
#   PASSWORD (default: admin)
#   QUERY    (default: "Mark R. Freeman")

PREFIX="supreme"
if [[ "${1:-}" == "--prefix" && -n "${2:-}" ]]; then
  PREFIX="$2"; shift 2
fi

export BASE_URL=${BASE_URL:-http://localhost:5000}
export USERNAME=${USERNAME:-admin}
export PASSWORD=${PASSWORD:-admin}
export QUERY=${QUERY:-"\"Mark R. Freeman\""}

ROOT_DIR=$(cd "$(dirname "$0")/.."; pwd -P)
SCRIPT="$ROOT_DIR/scripts/examples/playwright-supreme.js"

# Use a temp working dir (via artifacts.sh capture) and install Playwright there,
# then run the example script by absolute path so module resolution works.
scripts/artifacts.sh capture --prefix "$PREFIX" -- \
  bash -lc "npm --silent init -y >/dev/null && npm --silent i playwright@1 >/dev/null && npx --yes playwright install chromium >/dev/null && NODE_PATH=\$PWD/node_modules node -e 'require(\"module\").Module._initPaths(); require(\"$SCRIPT\")' "$@""

echo "Artifacts saved under: ${ARTIFACTS_DIR:-artifacts}/"
