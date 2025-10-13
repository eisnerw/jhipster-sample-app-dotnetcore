#!/usr/bin/env pwsh
# PowerShell equivalent of load_supreme.sh

# Set error action preference to stop on errors
$ErrorActionPreference = "Stop"

# Configuration
$INPUT = "supreme.jsonl"
$INDEX = "supreme"
$ES_URL = if ($env:ES_URL) { $env:ES_URL } else { "http://localhost:9200" }

# Function to check if a command exists
function Test-Command {
    param($CommandName)
    return [bool](Get-Command $CommandName -ErrorAction SilentlyContinue)
}

# Check required commands
if (-not (Test-Command "curl")) {
    Write-Error "Error: curl is required but not installed."
    exit 1
}

if (-not (Test-Command "jq")) {
    Write-Error "Error: jq is required but not installed."
    exit 1
}

# Check if input file exists
if (-not (Test-Path $INPUT)) {
    Write-Error "Error: $INPUT not found in current directory."
    exit 1
}

Write-Host "→ Deleting existing 'supreme' index (if it exists)..."
try {
    curl -X DELETE "$ES_URL/supreme?ignore_unavailable=true&expand_wildcards=all&pretty" 2>$null
} catch {
    # Ignore errors when deleting non-existent index
}

Write-Host "→ Creating 'supreme' index with mappings..."
$indexMapping = @'
{
  "settings": {
    "number_of_shards": 1,
    "number_of_replicas": 0
  },
  "mappings": {
    "dynamic": true,
    "properties": {
      "Appellant":   { "type": "text", "fields": { "keyword": { "type": "keyword", "ignore_above": 256 } } },
      "Appellee":    { "type": "text", "fields": { "keyword": { "type": "keyword", "ignore_above": 256 } } },
      "Petitioner":  { "type": "text", "fields": { "keyword": { "type": "keyword", "ignore_above": 256 } } },
      "Respondent":  { "type": "text", "fields": { "keyword": { "type": "keyword", "ignore_above": 256 } } },
      "advocates":   { "type": "text", "fields": { "keyword": { "type": "keyword", "ignore_above": 256 } } },
      "argument2_url": { "type": "text", "fields": { "keyword": { "type": "keyword", "ignore_above": 256 } } },
      "conclusion":  { "type": "text", "fields": { "keyword": { "type": "keyword", "ignore_above": 256 } } },
      "decision":    { "type": "text", "fields": { "keyword": { "type": "keyword", "ignore_above": 256 } } },
      "description": { "type": "text", "fields": { "keyword": { "type": "keyword", "ignore_above": 256 } } },
      "docket_number": { "type": "text", "fields": { "keyword": { "type": "keyword", "ignore_above": 256 } } },
      "facts_of_the_case": { "type": "text", "fields": { "keyword": { "type": "keyword", "ignore_above": 256 } } },
      "heard_by":    { "type": "text", "fields": { "keyword": { "type": "keyword", "ignore_above": 256 } } },
      "justia_url":  { "type": "text", "fields": { "keyword": { "type": "keyword", "ignore_above": 256 } } },
      "lower_court": { "type": "text", "fields": { "keyword": { "type": "keyword", "ignore_above": 256 } } },
      "majority":    { "type": "text", "fields": { "keyword": { "type": "keyword", "ignore_above": 256 } } },
      "manner_of_jurisdiction": { "type": "text", "fields": { "keyword": { "type": "keyword", "ignore_above": 256 } } },
      "minority":    { "type": "text", "fields": { "keyword": { "type": "keyword", "ignore_above": 256 } } },
      "name":        { "type": "text", "fields": { "keyword": { "type": "keyword", "ignore_above": 256 } } },
      "question":    { "type": "text", "fields": { "keyword": { "type": "keyword", "ignore_above": 256 } } },
      "recused":     { "type": "text", "fields": { "keyword": { "type": "keyword", "ignore_above": 256 } } },
      "term":        { "type": "integer", "coerce": true }
    }
  }
}
'@

# Create temporary file for the mapping
$tempMappingFile = [System.IO.Path]::GetTempFileName()
$indexMapping | Out-File -FilePath $tempMappingFile -Encoding UTF8

try {
    curl -sS -X PUT "$ES_URL/supreme" -H 'Content-Type: application/json' --data-binary "@$tempMappingFile"
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to create index"
    }
} finally {
    Remove-Item $tempMappingFile -ErrorAction SilentlyContinue
}

# Create temporary bulk file
$TMP_BULK = [System.IO.Path]::GetTempFileName() + ".ndjson"

# Cleanup function
$cleanup = {
    Remove-Item $TMP_BULK -ErrorAction SilentlyContinue
}

# Register cleanup on exit
Register-EngineEvent PowerShell.Exiting -Action $cleanup | Out-Null

try {
    Write-Host "→ Converting $INPUT to Elasticsearch bulk format (index: '$INDEX')..."
    
    # Use jq to convert JSONL to bulk format
    & jq -c --arg idx $INDEX '. as $d | { index: { _index: $idx } }, $d' $INPUT | Out-File -FilePath $TMP_BULK -Encoding UTF8

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to convert input file to bulk format"
    }

    Write-Host "→ Uploading to $ES_URL/_bulk ..."
    
    # Upload using curl
    $response = curl -sS -H 'Content-Type: application/x-ndjson' -X POST "$ES_URL/_bulk" --data-binary "@$TMP_BULK"
    
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to upload bulk data"
    }

    # Check for bulk API errors
    $hasErrors = echo $response | jq -e '.errors == true' 2>$null
    if ($LASTEXITCODE -eq 0) {
        Write-Warning "⚠️  Bulk request reported errors:"
        echo $response | jq '.items[] | select(.index.error) | .index.error' | Write-Error
        exit 2
    }

    # Calculate number of documents (each document has 2 lines: action + source)
    $lineCount = (Get-Content $TMP_BULK | Measure-Object -Line).Lines
    $docCount = [math]::Floor($lineCount / 2)
    
    Write-Host "✅ Loaded approximately $docCount documents into index '$INDEX'." -ForegroundColor Green

} finally {
    # Cleanup
    & $cleanup
}