#!/usr/bin/env bash
set -euo pipefail

# Configure a single logical index via alias with ILM rollover + 3-day deletion.
#
# Logical alias: app-logs
# Backing indices: app-logs-000001, app-logs-000002, ...
# ILM: rollover after 1d (hot), delete after 3d.
#
# Usage:
#   ./configure-elasticsearch-alias-rollover.sh \
#     --url http://localhost:9200 \
#     --user elastic --pass changeme \
#     --retention 3d --rollover 1d \
#     --shards 1 --replicas 0 \
#     [--alias app-logs] [--pattern app-logs-*] [--insecure]

URL="http://localhost:9200"
USER=""; PASS=""; INSECURE=0
ALIAS="app-logs"; PATTERN="app-logs-*"
RETENTION="3d"; ROLLOVER="1d"
SHARDS=1; REPLICAS=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --url) URL="$2"; shift 2;;
    --user) USER="$2"; shift 2;;
    --pass|--password) PASS="$2"; shift 2;;
    --insecure) INSECURE=1; shift;;
    --alias) ALIAS="$2"; shift 2;;
    --pattern) PATTERN="$2"; shift 2;;
    --retention) RETENTION="$2"; shift 2;;
    --rollover) ROLLOVER="$2"; shift 2;;
    --shards) SHARDS="$2"; shift 2;;
    --replicas) REPLICAS="$2"; shift 2;;
    -h|--help) grep '^#' "$0" | sed 's/^# \{0,1\}//'; exit 0;;
    *) echo "Unknown arg: $1" >&2; exit 2;;
  esac
done

AUTH_ARGS=()
[[ -n "$USER" ]] && AUTH_ARGS+=("-u" "$USER:$PASS")
[[ "$INSECURE" == 1 ]] && AUTH_ARGS+=("-k")

POLICY="${ALIAS}-ilm"
TEMPLATE="${ALIAS}-template"
FIRST_INDEX="${ALIAS}-000001"

echo "Upserting ILM policy '$POLICY' (rollover $ROLLOVER, delete $RETENTION) ..."
curl -sS "${AUTH_ARGS[@]}" -H 'Content-Type: application/json' -X PUT \
  "$URL/_ilm/policy/$POLICY" -d @- <<JSON
{
  "policy": {
    "phases": {
      "hot": { "actions": { "rollover": { "max_age": "$ROLLOVER" } } },
      "delete": { "min_age": "$RETENTION", "actions": { "delete": {} } }
    }
  }
}
JSON
echo

echo "Upserting index template '$TEMPLATE' for pattern '$PATTERN' ..."
curl -sS "${AUTH_ARGS[@]}" -H 'Content-Type: application/json' -X PUT \
  "$URL/_index_template/$TEMPLATE" -d @- <<JSON
{
  "index_patterns": ["$PATTERN"],
  "template": {
    "settings": {
      "index.number_of_shards": $SHARDS,
      "index.number_of_replicas": $REPLICAS,
      "index.lifecycle.name": "$POLICY",
      "index.lifecycle.rollover_alias": "$ALIAS"
    },
    "aliases": {
      "$ALIAS": {}
    }
  },
  "priority": 500,
  "_meta": { "description": "Template for logical index alias $ALIAS", "owner": "jhipster" }
}
JSON
echo

echo "Creating initial index '$FIRST_INDEX' with write alias '$ALIAS' (idempotent)..."
curl -sS "${AUTH_ARGS[@]}" -H 'Content-Type: application/json' -X PUT \
  "$URL/$FIRST_INDEX" -d @- <<JSON || true
{
  "aliases": { "$ALIAS": { "is_write_index": true } }
}
JSON
echo

echo "Verification:"
curl -sS "${AUTH_ARGS[@]}" "$URL/_ilm/policy/$POLICY" | sed 's/.*/  &/'
curl -sS "${AUTH_ARGS[@]}" "$URL/_index_template/$TEMPLATE" | sed 's/.*/  &/'
curl -sS "${AUTH_ARGS[@]}" "$URL/$ALIAS/_alias?pretty" || true
echo "Done. Set Serilog IndexFormat to '$ALIAS' (already set). Write to alias; rollover and delete happen automatically."

