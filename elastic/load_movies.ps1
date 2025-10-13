#!/usr/bin/env pwsh
# PowerShell equivalent of load_movies.sh

# Set error action preference to stop on errors
$ErrorActionPreference = "Stop"

# Configuration
$INPUT = "movies.jsonl"
$INDEX = "movies"
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

Write-Host "→ Deleting existing 'movies' index (if it exists)..."
try {
    curl -X DELETE "$ES_URL/movies?ignore_unavailable=true&expand_wildcards=all&pretty" 2>$null
} catch {
    # Ignore errors when deleting non-existent index
}

Write-Host "→ Creating 'movies' index with mappings..."
$indexMapping = @'
{
  "settings": {
    "number_of_shards": 1,
    "number_of_replicas": 0,
    "analysis": {
      "normalizer": {
        "lowercase": {
          "type": "custom",
          "filter": ["lowercase", "asciifolding"]
        }
      }
    }
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
      "country":    {
        "type": "text",
        "fields": {
          "keyword": { "type": "keyword", "ignore_above": 256 },
          "ci":      { "type": "keyword", "normalizer": "lowercase" }
        }
      },

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
}
'@

# Create temporary file for the mapping
$tempMappingFile = [System.IO.Path]::GetTempFileName()
$indexMapping | Out-File -FilePath $tempMappingFile -Encoding UTF8

try {
    curl -X PUT "$ES_URL/movies" -H 'Content-Type: application/json' --data-binary "@$tempMappingFile"
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