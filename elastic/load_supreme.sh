#!/usr/bin/env bash
# Delete the "supreme" index (safe if it doesn't exist)
curl -X DELETE "http://localhost:9200/supreme?ignore_unavailable=true&expand_wildcards=all&pretty"

curl -X PUT "http://localhost:9200/supreme" -H 'Content-Type: application/json' -d'
{
  "settings": {
      "dynamic" : "true",
      "properties" : {
        "Appellant" : {
          "type" : "text",
          "fields" : {
            "keyword" : {
              "type" : "keyword",
              "ignore_above" : 256
            }
          }
        },
        "Appellee" : {
          "type" : "text",
          "fields" : {
            "keyword" : {
              "type" : "keyword",
              "ignore_above" : 256
            }
          }
        },
        "Petitioner" : {
          "type" : "text",
          "fields" : {
            "keyword" : {
              "type" : "keyword",
              "ignore_above" : 256
            }
          }
        },
        "Respondent" : {
          "type" : "text",
          "fields" : {
            "keyword" : {
              "type" : "keyword",
              "ignore_above" : 256
            }
          }
        },
        "advocates" : {
          "type" : "text",
          "fields" : {
            "keyword" : {
              "type" : "keyword",
              "ignore_above" : 256
            }
          }
        },
        "argument2_url" : {
          "type" : "text",
          "fields" : {
            "keyword" : {
              "type" : "keyword",
              "ignore_above" : 256
            }
          }
        },
        "conclusion" : {
          "type" : "text",
          "fields" : {
            "keyword" : {
              "type" : "keyword",
              "ignore_above" : 256
            }
          }
        },
        "decision" : {
          "type" : "text",
          "fields" : {
            "keyword" : {
              "type" : "keyword",
              "ignore_above" : 256
            }
          }
        },
        "description" : {
          "type" : "text",
          "fields" : {
            "keyword" : {
              "type" : "keyword",
              "ignore_above" : 256
            }
          }
        },
        "docket_number" : {
          "type" : "text",
          "fields" : {
            "keyword" : {
              "type" : "keyword",
              "ignore_above" : 256
            }
          }
        },
        "facts_of_the_case" : {
          "type" : "text",
          "fields" : {
            "keyword" : {
              "type" : "keyword",
              "ignore_above" : 256
            }
          }
        },
        "heard_by" : {
          "type" : "text",
          "fields" : {
            "keyword" : {
              "type" : "keyword",
              "ignore_above" : 256
            }
          }
        },
        "justia_url" : {
          "type" : "text",
          "fields" : {
            "keyword" : {
              "type" : "keyword",
              "ignore_above" : 256
            }
          }
        },
        "lower_court" : {
          "type" : "text",
          "fields" : {
            "keyword" : {
              "type" : "keyword",
              "ignore_above" : 256
            }
          }
        },
        "majority" : {
          "type" : "text",
          "fields" : {
            "keyword" : {
              "type" : "keyword",
              "ignore_above" : 256
            }
          }
        },
        "manner_of_jurisdiction" : {
          "type" : "text",
          "fields" : {
            "keyword" : {
              "type" : "keyword",
              "ignore_above" : 256
            }
          }
        },
        "minority" : {
          "type" : "text",
          "fields" : {
            "keyword" : {
              "type" : "keyword",
              "ignore_above" : 256
            }
          }
        },
        "name" : {
          "type" : "text",
          "fields" : {
            "keyword" : {
              "type" : "keyword",
              "ignore_above" : 256
            }
          }
        },
        "question" : {
          "type" : "text",
          "fields" : {
            "keyword" : {
              "type" : "keyword",
              "ignore_above" : 256
            }
          }
        },
        "recused" : {
          "type" : "text",
          "fields" : {
            "keyword" : {
              "type" : "keyword",
              "ignore_above" : 256
            }
          }
        },
        "term" : {
          "type" : "integer",
          "coerce" : true
        }
      }
    }
  }
}'

set -euo pipefail

# Reads supreme.jsonl, converts to a temporary Elasticsearch bulk file,
# uploads it, then deletes the temp file.

INPUT="supreme.jsonl"
INDEX="supreme"                   # change if you want a different index
ES_URL="${ES_URL:-http://localhost:9200}"  # override with env var if needed

if [[ ! -f "$INPUT" ]]; then
  echo "Error: $INPUT not found in current directory." >&2
  exit 1
fi

command -v jq >/dev/null 2>&1 || { echo "Error: jq is required but not installed." >&2; exit 1; }
command -v curl >/dev/null 2>&1 || { echo "Error: curl is required but not installed." >&2; exit 1; }

TMP_BULK="$(mktemp /tmp/supreme-bulk.XXXXXX.ndjson)"
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
