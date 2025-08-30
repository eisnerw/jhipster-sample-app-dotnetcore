#!/usr/bin/env bash
# Load birthdays bulk data, show correct count, and list bad bulk lines with reasons.

# --- Config ---
ES_HOST="${ES_HOST:-http://localhost:9200}"
INDEX_NAME="${INDEX_NAME:-birthdays}"
BULK_FILE="${BULK_FILE:-BirthdaysBulkUpdate.json}"   # NDJSON: action line + doc line

set -euo pipefail
command -v curl >/dev/null || { echo "curl is required"; exit 1; }
command -v jq   >/dev/null || { echo "jq is required";   exit 1; }

[[ -f "$BULK_FILE" ]] || { echo "❌ Bulk file not found: $BULK_FILE"; exit 1; }

echo "Deleting existing index (if exists)..."
curl -s -X DELETE "$ES_HOST/$INDEX_NAME" >/dev/null || true

echo "Creating index with mapping..."
curl -s -X PUT "$ES_HOST/$INDEX_NAME" \
     -H 'Content-Type: application/json' \
     --data-binary @- <<'EOF' >/dev/null
{
  "mappings": {
    "properties": {
      "categories": { "type": "text", "fields": { "keyword": { "type": "keyword", "ignore_above": 256 } } },
      "dob":        { "type": "date" },
      "fname":      { "type": "text", "fields": { "keyword": { "type": "keyword", "ignore_above": 256 } } },
      "id":         { "type": "keyword" },
      "isAlive":    { "type": "boolean" },
      "lname":      { "type": "text", "fields": { "keyword": { "type": "keyword", "ignore_above": 256 } } },
      "sign":       { "type": "text", "fields": { "keyword": { "type": "keyword", "ignore_above": 256 } } },
      "text":       { "type": "text", "fields": { "keyword": { "type": "keyword", "ignore_above": 256 } } },
      "wikipedia":  { "type": "text", "fields": { "keyword": { "type": "keyword", "ignore_above": 32000 } } }
    }
  }
}
EOF
echo "Index created."
echo

echo "Posting bulk file (refresh=true): $BULK_FILE"
RESP_FILE="$(mktemp /tmp/birthdays-bulk-response.XXXXXX.json)"
curl -sS -X POST "$ES_HOST/$INDEX_NAME/_bulk?refresh=true" \
     -H "Content-Type: application/x-ndjson" \
     --data-binary @"$BULK_FILE" > "$RESP_FILE"

# --- Summary ---
ERRORS=$(jq -r '.errors // false' "$RESP_FILE")
TOTAL=$(jq -r '.items | length // 0' "$RESP_FILE")
FAILED=$(jq -r '[.items[] | (.index // .create // .update // .delete) | select(.error)] | length // 0' "$RESP_FILE")
SUCCESS=$(( TOTAL - FAILED ))

echo "Summary → Success: $SUCCESS   Failed: $FAILED   Total actions: $TOTAL"

# Accurate count (already refreshed)
COUNT=$(curl -s "$ES_HOST/$INDEX_NAME/_count" | jq -r '.count // 0')
echo "Index '$INDEX_NAME' now contains $COUNT documents."
echo

# --- List bad lines with details (fixed jq iteration over .items) ---
if [[ "$ERRORS" == "true" && "$FAILED" -gt 0 ]]; then
  echo "❗ Showing failing items (action/doc line numbers, reason, offending JSON):"
  jq -cr '
    .items
    | to_entries[]
    | {i: .key, r: (.value.index // .value.create // .value.update // .value.delete)}
    | select(.r.error)
    | {
        i,
        status: .r.status,
        error: .r.error
      }
  ' "$RESP_FILE" | while IFS= read -r row; do
      i=$(jq -r '.i' <<<"$row")
      status=$(jq -r '.status' <<<"$row")
      etype=$(jq -r '.error.type' <<<"$row")
      reason=$(jq -r '.error.reason' <<<"$row")
      caused=$(jq -r '.error.caused_by.reason // empty' <<<"$row")

      action_line=$(( 2*i + 1 ))
      doc_line=$(( 2*i + 2 ))

      echo "---- Item #$i  (HTTP $status) ----"
      echo "Action line: $action_line   Doc line: $doc_line"
      echo "Error: $etype — $reason"
      [[ -n "$caused" ]] && echo "Caused by: $caused"

      echo "----- bulk:$action_line -----"
      sed -n "${action_line}p" "$BULK_FILE"
      echo "----- bulk:$doc_line -----"
      sed -n "${doc_line}p" "$BULK_FILE"
      echo
    done
else
  echo "✅ No item-level errors reported by _bulk."
fi

echo "Done."
