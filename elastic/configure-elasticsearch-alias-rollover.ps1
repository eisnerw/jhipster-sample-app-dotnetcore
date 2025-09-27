param(
  [string]$Url = 'http://localhost:9200',
  [string]$Alias = 'app-logs',
  [string]$Pattern = 'app-logs-*',
  [string]$Retention = '3d',
  [string]$Rollover = '1d',
  [int]$Shards = 1,
  [int]$Replicas = 0,
  [string]$Username,
  [string]$Password
)

$Headers = @{ 'Content-Type' = 'application/json' }
if ($Username) { $pair = "{0}:{1}" -f $Username,$Password; $Headers['Authorization'] = 'Basic ' + [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($pair)) }

$Policy = "$Alias-ilm"
$Template = "$Alias-template"
$FirstIndex = "$Alias-000001"

Write-Host "Upserting ILM policy '$Policy' (rollover $Rollover, delete $Retention) ..."
$ilm = @{ policy = @{ phases = @{ hot = @{ actions = @{ rollover = @{ max_age = $Rollover } } }; delete = @{ min_age = $Retention; actions = @{ delete = @{} } } } } } | ConvertTo-Json -Depth 8
Invoke-RestMethod -Method PUT -Uri "$Url/_ilm/policy/$Policy" -Headers $Headers -Body $ilm | Out-Null

Write-Host "Upserting index template '$Template' for pattern '$Pattern' ..."
$tmpl = @{
  index_patterns = @($Pattern)
  template = @{ settings = @{ 'index.number_of_shards' = $Shards; 'index.number_of_replicas' = $Replicas; 'index.lifecycle.name' = $Policy; 'index.lifecycle.rollover_alias' = $Alias }; aliases = @{ $Alias = @{} } }
  priority = 500
  _meta = @{ description = "Template for logical index alias $Alias"; owner = 'jhipster' }
} | ConvertTo-Json -Depth 8
Invoke-RestMethod -Method PUT -Uri "$Url/_index_template/$Template" -Headers $Headers -Body $tmpl | Out-Null

Write-Host "Creating initial index '$FirstIndex' with write alias '$Alias' (idempotent)..."
try {
  $init = @{ aliases = @{ $Alias = @{ is_write_index = $true } } } | ConvertTo-Json -Depth 8
  Invoke-RestMethod -Method PUT -Uri "$Url/$FirstIndex" -Headers $Headers -Body $init | Out-Null
} catch {}

Write-Host 'Verification:'
Invoke-RestMethod -Method GET -Uri "$Url/_ilm/policy/$Policy" -Headers $Headers | ConvertTo-Json -Depth 8
Invoke-RestMethod -Method GET -Uri "$Url/_index_template/$Template" -Headers $Headers | ConvertTo-Json -Depth 8
Invoke-RestMethod -Method GET -Uri "$Url/$Alias/_alias?pretty" -Headers $Headers | ConvertTo-Json -Depth 8

