#!/bin/bash

set -e

# Configurable variables
ES_HOST="http://localhost:9200"
INDEX_NAME="birthdays"
BULK_FILE="BirthdaysBulkUpdate.json"

echo "Deleting existing index (if exists)..."
curl -s -X DELETE "$ES_HOST/$INDEX_NAME" || true
echo -e "\n"

echo "Creating index with mapping..."
curl -s -X PUT "$ES_HOST/$INDEX_NAME" -H 'Content-Type: application/json' -d @- <<EOF
{
  "mappings": {
    "properties": {
      "categories": {
        "type": "text",
        "fields": { "keyword": { "type": "keyword", "ignore_above": 256 } }
      },
      "dob": { "type": "date" },
      "fname": {
        "type": "text",
        "fields": { "keyword": { "type": "keyword", "ignore_above": 256 } }
      },
      "id": { "type": "keyword" },
      "isAlive": { "type": "boolean" },
      "lname": {
        "type": "text",
        "fields": { "keyword": { "type": "keyword", "ignore_above": 256 } }
      },
      "sign": {
        "type": "text",
        "fields": { "keyword": { "type": "keyword", "ignore_above": 256 } }
      },
      "text": {
        "type": "text",
        "fields": { "keyword": { "type": "keyword", "ignore_above": 256 } }
      },
      "wikipedia": {
        "type": "text",
        "fields": { "keyword": { "type": "keyword", "ignore_above": 32000 } }
      }
    }
  }
}
EOF

echo -e "\nIndex created successfully."
echo -e "\nBulk uploading data from $BULK_FILE..."

curl -X POST "http://localhost:9200/birthdays/_bulk" -H "Content-Type: application/json" --data-binary @BirthdaysBulkUpdate.json

echo -e "\nBulk upload completed."
