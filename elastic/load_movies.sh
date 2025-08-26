#!/usr/bin/env bash
# Delete the "movies" index (safe if it doesn't exist)
curl -X DELETE "http://localhost:9200/movies?ignore_unavailable=true&expand_wildcards=all&pretty"

curl -X PUT "http://localhost:9200/movies" -H 'Content-Type: application/json' -d'
{
  "settings": {
    "number_of_shards": 1,
    "number_of_replicas": 0
  },
  "mappings": {
    "properties": {
      "title": {
        "type": "text",
        "fields": {
          "keyword": { "type": "keyword", "ignore_above": 256 }
        }
      },
      "release_year":    { "type": "integer" },
      "runtime_minutes": { "type": "integer" },

      "genres":     { "type": "text", "fields": { "keyword": { "type": "keyword", "ignore_above": 256 } } },
      "categories": { "type": "text", "fields": { "keyword": { "type": "keyword", "ignore_above": 256 } } },
      "languages":  { "type": "text", "fields": { "keyword": { "type": "keyword", "ignore_above": 256 } } },
      "country":    { "type": "text", "fields": { "keyword": { "type": "keyword", "ignore_above": 256 } } },

      "summary":  { "type": "text" },
      "synopsis": { "type": "text" },

      "directors": { "type": "text", "fields": { "keyword": { "type": "keyword", "ignore_above": 256 } } },
      "producers": { "type": "text", "fields": { "keyword": { "type": "keyword", "ignore_above": 256 } } },
      "writers":   { "type": "text", "fields": { "keyword": { "type": "keyword", "ignore_above": 256 } } },
      "cast":      { "type": "text", "fields": { "keyword": { "type": "keyword", "ignore_above": 256 } } },

      "budget_usd":            { "type": "long" },
      "gross_usd":             { "type": "long" },
      "rotten_tomatoes_score": { "type": "integer" }
    }
  }
}'

set -euo pipefail

# Reads movies.jsonl, converts to a temporary Elasticsearch bulk file,
# uploads it, then deletes the temp file.

INPUT="movies.jsonl"
INDEX="movies"                   # change if you want a different index
ES_URL="${ES_URL:-http://localhost:9200}"  # override with env var if needed

if [[ ! -f "$INPUT" ]]; then
  echo "Error: $INPUT not found in current directory." >&2
  exit 1
fi

command -v jq >/dev/null 2>&1 || { echo "Error: jq is required but not installed." >&2; exit 1; }
command -v curl >/dev/null 2>&1 || { echo "Error: curl is required but not installed." >&2; exit 1; }

TMP_BULK="$(mktemp /tmp/movies-bulk.XXXXXX.ndjson)"
cleanup() { rm -f "$TMP_BULK"; }
trap cleanup EXIT

echo "→ Converting $INPUT to Elasticsearch bulk format (index: '$INDEX')..."
jq -c --arg idx "$INDEX" '. as $d | { index: { _index: $idx } }, $d' "$INPUT" > "$TMP_BULK"

echo "→ Uploading to $ES_URL/_bulk ..."
RESP="$(curl -sS -H 'Content-Type: application/x-ndjson' \
              -XPOST "$ES_URL/_bulk" \
              --data-binary @"$TMP_BULK")"

# Check Bulk API errors
if echo "$RESP" | jq -e '.errors == true' >/dev/null; then
  echo "⚠️  Bulk request reported errors:" >&2
  echo "$RESP" | jq '.items[] | select(.index.error) | .index.error' >&2
  exit 2
fi

DOCS=$(( $(wc -l < "$TMP_BULK") / 2 ))  # action+source per doc
echo "✅ Loaded approximately $DOCS documents into index '$INDEX'."
