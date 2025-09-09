#!/usr/bin/env bash
set -euo pipefail

# artifacts.sh — tiny helper to collect run artifacts (PNGs/JSON)
#
# Usage examples:
#   # 1) Run a command and collect *.png/*.json it produces into artifacts/
#   scripts/artifacts.sh capture -- node script.js
#
#   # 2) Add a custom filename prefix when saving
#   scripts/artifacts.sh capture --prefix login -- node script.js
#
#   # 3) Move specific files you already have locally
#   scripts/artifacts.sh move 00-home.png out.json
#
# Env vars:
#   ARTIFACTS_DIR  Directory to store artifacts (default: artifacts)

ARTIFACTS_DIR=${ARTIFACTS_DIR:-artifacts}
mkdir -p "$ARTIFACTS_DIR"

cmd=${1:-}
shift || true

timestamp() { date +%Y%m%d-%H%M%S; }

move_into_artifacts() {
  local prefix="$1"; shift || true
  local moved=0
  for f in "$@"; do
    [ -e "$f" ] || continue
    local base
    base=$(basename -- "$f")
    local dest="$ARTIFACTS_DIR/${prefix:+$prefix-}$base"
    mkdir -p "$(dirname "$dest")"
    mv -f -- "$f" "$dest"
    echo "$dest"
    moved=$((moved+1))
  done
  return 0
}

case "$cmd" in
  capture)
    # Capture mode: run a command in a temp dir and collect *.png/*.json
    local_prefix=""
    if [[ "${1:-}" == "--prefix" ]]; then
      local_prefix="$2"; shift 2 || true
    fi
    if [[ "${1:-}" == "--" ]]; then shift; fi
    if [[ $# -lt 1 ]]; then
      echo "Usage: $0 capture [--prefix NAME] -- <command...>" >&2
      exit 2
    fi
    work=$(mktemp -d)
    trap 'rm -rf "$work"' EXIT
    ( cd "$work" && "$@" )
    # Collect common artifact types
    mapfile -t files < <(cd "$work" && ls -1 *.png *.json 2>/dev/null || true)
    if [[ ${#files[@]} -gt 0 ]]; then
      for f in "${files[@]}"; do
        move_into_artifacts "$local_prefix" "$work/$f" >/dev/null
      done
      ls -1 "$ARTIFACTS_DIR" | sed 's/^/saved: /'
    else
      echo "No *.png or *.json produced by command." >&2
    fi
    ;;
  move)
    if [[ $# -lt 1 ]]; then
      echo "Usage: $0 move <files...>" >&2
      exit 2
    fi
    move_into_artifacts "" "$@" >/dev/null
    ;;
  *)
    cat >&2 <<'HELP'
Usage:
  scripts/artifacts.sh capture [--prefix NAME] -- <command...>
  scripts/artifacts.sh move <files...>

Description:
  - capture: runs the command in a temp directory and moves any produced PNG/JSON
             files into $ARTIFACTS_DIR, optionally prefixed.
  - move:    moves the given local files into $ARTIFACTS_DIR.

Env:
  ARTIFACTS_DIR (default: artifacts)
HELP
    exit 2
    ;;
esac

