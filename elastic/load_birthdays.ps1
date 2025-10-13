#!/usr/bin/env pwsh
# PowerShell equivalent of load_birthdays.sh
# Load birthdays bulk data, show correct count, and list bad bulk lines with reasons.

# Set error action preference to stop on errors
$ErrorActionPreference = "Stop"

# --- Config ---
$ES_HOST = if ($env:ES_HOST) { $env:ES_HOST } else { "http://localhost:9200" }
$INDEX_NAME = if ($env:INDEX_NAME) { $env:INDEX_NAME } else { "birthdays" }
$BULK_FILE = if ($env:BULK_FILE) { $env:BULK_FILE } else { "BirthdaysBulkUpdate.json" }

# Function to check if a command exists
function Test-Command {
    param($CommandName)
    return [bool](Get-Command $CommandName -ErrorAction SilentlyContinue)
}

# Check required commands
if (-not (Test-Command "curl")) {
    Write-Error "curl is required"
    exit 1
}

if (-not (Test-Command "jq")) {
    Write-Error "jq is required"
    exit 1
}

# Check if bulk file exists
if (-not (Test-Path $BULK_FILE)) {
    Write-Error "❌ Bulk file not found: $BULK_FILE"
    exit 1
}

Write-Host "Deleting existing index (if exists)..."
try {
    curl -s -X DELETE "$ES_HOST/$INDEX_NAME" 2>$null | Out-Null
} catch {
    # Ignore errors when deleting non-existent index
}

Write-Host "Creating index with mapping..."
$indexMapping = @'
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
'@

# Create temporary file for the mapping
$tempMappingFile = [System.IO.Path]::GetTempFileName()
$indexMapping | Out-File -FilePath $tempMappingFile -Encoding UTF8

try {
    curl -s -X PUT "$ES_HOST/$INDEX_NAME" -H 'Content-Type: application/json' --data-binary "@$tempMappingFile" 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to create index"
    }
} finally {
    Remove-Item $tempMappingFile -ErrorAction SilentlyContinue
}

Write-Host "Index created."
Write-Host ""

Write-Host "Posting bulk file (refresh=true): $BULK_FILE"

# Create temporary response file
$RESP_FILE = [System.IO.Path]::GetTempFileName() + ".json"

# Cleanup function
$cleanup = {
    Remove-Item $RESP_FILE -ErrorAction SilentlyContinue
}

# Register cleanup on exit
Register-EngineEvent PowerShell.Exiting -Action $cleanup | Out-Null

try {
    # Upload bulk data
    curl -sS -X POST "$ES_HOST/$INDEX_NAME/_bulk?refresh=true" -H "Content-Type: application/x-ndjson" --data-binary "@$BULK_FILE" | Out-File -FilePath $RESP_FILE -Encoding UTF8
    
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to upload bulk data"
    }

    # --- Summary ---
    $ERRORS = & jq -r '.errors // false' $RESP_FILE
    $TOTAL = & jq -r '.items | length // 0' $RESP_FILE
    $FAILED = & jq -r '[.items[] | (.index // .create // .update // .delete) | select(.error)] | length // 0' $RESP_FILE
    $SUCCESS = [int]$TOTAL - [int]$FAILED

    Write-Host "Summary → Success: $SUCCESS   Failed: $FAILED   Total actions: $TOTAL"

    # Accurate count (already refreshed)
    $COUNT = curl -s "$ES_HOST/$INDEX_NAME/_count" | jq -r '.count // 0'
    Write-Host "Index '$INDEX_NAME' now contains $COUNT documents."
    Write-Host ""

    # --- List bad lines with details ---
    if ($ERRORS -eq "true" -and [int]$FAILED -gt 0) {
        Write-Host "❗ Showing failing items (action/doc line numbers, reason, offending JSON):"
        
        # Get failing items as JSON objects
        $failingItemsJson = & jq -c '.items | to_entries[] | {i: .key, r: (.value.index // .value.create // .value.update // .value.delete)} | select(.r.error) | {i, status: .r.status, error: .r.error}' $RESP_FILE
        
        # Process each failing item
        $failingItemsJson | ForEach-Object {
            $row = $_
            $i = echo $row | jq -r '.i'
            $status = echo $row | jq -r '.status'
            $etype = echo $row | jq -r '.error.type'
            $reason = echo $row | jq -r '.error.reason'
            $caused = echo $row | jq -r '.error.caused_by.reason // empty'

            $action_line = [int]$i * 2 + 1
            $doc_line = [int]$i * 2 + 2

            Write-Host "---- Item #$i  (HTTP $status) ----"
            Write-Host "Action line: $action_line   Doc line: $doc_line"
            Write-Host "Error: $etype — $reason"
            if ($caused -and $caused -ne "null" -and $caused -ne "" -and $caused -ne "empty") {
                Write-Host "Caused by: $caused"
            }

            Write-Host "----- bulk:$action_line -----"
            (Get-Content $BULK_FILE)[$action_line - 1]
            Write-Host "----- bulk:$doc_line -----"
            (Get-Content $BULK_FILE)[$doc_line - 1]
            Write-Host ""
        }
    } else {
        Write-Host "✅ No item-level errors reported by _bulk."
    }

    Write-Host "Done."

} finally {
    # Cleanup
    & $cleanup
}