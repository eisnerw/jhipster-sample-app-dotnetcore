<#
.SYNOPSIS
  Tail ECS Serilog logs from Elasticsearch (no Kibana required).

.PARAMETER Url
  Elasticsearch base URL (default: http://localhost:9200)

.PARAMETER Username, Password
  Basic auth credentials (optional)

.PARAMETER Prefix
  Index prefix (default: app-logs). Indices are Prefix-YYYY.MM.DD

.PARAMETER Window
  Query time window (default: 5m). Examples: 15m, 1h

.PARAMETER Interval
  Poll interval seconds (default: 3)

.PARAMETER Size
  Max documents per poll (default: 20)

.PARAMETER Level
  Filter by log.level (Information, Warning, Error, etc.)

.PARAMETER UserFilter
  Filter by user.name

.PARAMETER Once
  Fetch once and exit.
#>
param(
  [string]$Url = 'http://localhost:9200',
  [string]$Username,
  [string]$Password,
  [string]$Prefix = 'app-logs',
  [string]$Window = '5m',
  [int]$Interval = 3,
  [int]$Size = 20,
  [string]$Level,
  [string]$UserFilter,
  [switch]$Once
)

$Headers = @{ 'Content-Type' = 'application/json' }
if ($Username) { $pair = "{0}:{1}" -f $Username,$Password; $Headers['Authorization'] = 'Basic ' + [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($pair)) }

$IndexPattern = "$Prefix-*"

function Build-Body {
  $filters = @(@{ range = @{ '@timestamp' = @{ gte = "now-$Window" } } })
  if ($Level) { $filters += @{ term = @{ 'log.level' = $Level } } }
  if ($UserFilter) { $filters += @{ term = @{ 'user.name' = $UserFilter } } }
  $body = @{ 
    size = $Size
    sort = @(@{ '@timestamp' = @{ order = 'desc' } })
    query = @{ bool = @{ filter = $filters } }
    _source = @('@timestamp','log.level','message','user.name','service.name','labels.route','trace.id')
  }
  return ($body | ConvertTo-Json -Depth 8)
}

function Poll-Once {
  $body = Build-Body
  $resp = Invoke-RestMethod -Method POST -Uri "$Url/$IndexPattern/_search" -Headers $Headers -Body $body

  $newest = $resp.hits.hits | Select-Object -First 1
  if (-not $newest) { return }
  $newestId = $newest._id
  $newestTs = $newest._source.'@timestamp'

  if ($script:LastId -and $script:LastId -eq $newestId) { return }

  # Print only new entries (oldest -> newest) compared to last seen
  $hits = [System.Collections.Generic.List[object]]::new()
  foreach ($h in $resp.hits.hits) { $hits.Add($h) }
  $hits = $hits | Sort-Object { $_._source.'@timestamp' }
  foreach ($hit in $hits) {
    $ts = $hit._source.'@timestamp'
    if ($ts) { $ts = ($ts -replace '\..*','') -replace 'Z$','' }
    if ($script:LastTs) {
      $isNewer = ($ts -gt $script:LastTs) -or (($ts -eq $script:LastTs) -and ($hit._id -ne $script:LastId))
      if (-not $isNewer) { continue }
    }
    $lvl = $hit._source.'log.level'
    if (-not $lvl) { $lvl = $hit._source.level }
    if (-not $lvl) { $lvl = $hit._source.log.level }
    $usr = if ($hit._source.'user.name') { $hit._source.'user.name' } else { '-' }
    $mod = if ($hit._source.'log.logger') { $hit._source.'log.logger' } elseif ($hit._source.SourceContext) { $hit._source.SourceContext } else { '-' }
    $fun = if ($hit._source.origin.function) { $hit._source.origin.function } elseif ($hit._source.CallerMemberName) { $hit._source.CallerMemberName } else { $null }
    $msg = $hit._source.message
    $who = if ($fun) { "$mod::$fun" } else { $mod }
    "${ts} [${lvl}] ${who} | ${usr} | ${msg}"
  }

  $script:LastId = $newestId
  $script:LastTs = $newestTs
}

if ($Once) { Poll-Once; exit 0 }

# Initial snapshot
Poll-Once

while ($true) {
  Poll-Once
  Start-Sleep -Seconds $Interval
}
