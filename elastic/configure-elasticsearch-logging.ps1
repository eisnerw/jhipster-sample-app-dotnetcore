<#
.SYNOPSIS
  Configure Elasticsearch for Serilog ECS logs with a 3-day ILM policy and an index template.

.PARAMETER Url
  Elasticsearch base URL (e.g., http://localhost:9200)

.PARAMETER Username
  Username for Basic auth

.PARAMETER Password
  Password for Basic auth

.PARAMETER IndexPrefix
  Index name prefix (default: app-logs). Daily indices will look like: app-logs-YYYY.MM.DD

.PARAMETER RetentionDays
  Retention in days for ILM delete phase (default: 3)

.PARAMETER PolicyName
  ILM policy name (default: serilog-3d)

.PARAMETER Shards
  Number of primary shards (default: 1)

.PARAMETER Replicas
  Number of replicas (default: 0)

.PARAMETER Insecure
  Skip TLS certificate validation (self-signed dev clusters)

.EXAMPLE
  .\configure-elasticsearch-logging.ps1 -Url http://localhost:9200 -IndexPrefix app-logs -RetentionDays 3
#>
param(
  [string]$Url = 'http://localhost:9200',
  [string]$Username,
  [string]$Password,
  [string]$IndexPrefix = 'app-logs',
  [int]$RetentionDays = 3,
  [string]$PolicyName = 'serilog-3d',
  [int]$Shards = 1,
  [int]$Replicas = 0,
  [switch]$Insecure
)

if ($Insecure) {
  add-type @"
using System.Net;
using System.Security.Cryptography.X509Certificates;
public class TrustAllCertsPolicy : System.Net.ICertificatePolicy {
    public bool CheckValidationResult(
        ServicePoint srvPoint, X509Certificate certificate,
        WebRequest request, int certificateProblem) { return true; }
}
"@
  [System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustAllCertsPolicy
}

function Invoke-Es {
  param(
    [string]$Method,
    [string]$Path,
    [string]$Body
  )
  $uri = if ($Path.StartsWith('/')) { $Url.TrimEnd('/') + $Path } else { $Url.TrimEnd('/') + '/' + $Path }
  $headers = @{ 'Content-Type' = 'application/json' }
  if ($Username) { $pair = "{0}:{1}" -f $Username,$Password; $bytes = [Text.Encoding]::UTF8.GetBytes($pair); $b64 = [Convert]::ToBase64String($bytes); $headers['Authorization'] = "Basic $b64" }
  if ($Body) { return Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers -Body $Body }
  else { return Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers }
}

Write-Host "Checking cluster at $Url ..."
Invoke-Es -Method GET -Path '/' | Out-Null
Write-Host 'OK'

Write-Host "Upserting ILM policy '$PolicyName' (delete after $RetentionDays d) ..."
$ilm = @{
  policy = @{ phases = @{ delete = @{ 'min_age' = "${RetentionDays}d"; actions = @{ delete = @{} } } } }
} | ConvertTo-Json -Depth 8
Invoke-Es -Method PUT -Path "_ilm/policy/$PolicyName" -Body $ilm | Out-Null

$templateName = "$IndexPrefix-template"
$pattern = "$IndexPrefix-*"
Write-Host "Upserting index template '$templateName' for pattern '$pattern' ..."
$tmpl = @{
  index_patterns = @($pattern)
  template = @{ settings = @{ 'index.number_of_shards' = $Shards; 'index.number_of_replicas' = $Replicas; 'index.lifecycle.name' = $PolicyName } }
  priority = 500
  _meta = @{ description = 'Serilog ECS logging template (settings + ILM), mappings provided by sink'; owner = 'jhipster-sample-app-dotnetcore' }
} | ConvertTo-Json -Depth 8
Invoke-Es -Method PUT -Path "_index_template/$templateName" -Body $tmpl | Out-Null

Write-Host 'Verification:'
Invoke-Es -Method GET -Path "_ilm/policy/$PolicyName" | ConvertTo-Json -Depth 8
Invoke-Es -Method GET -Path "_index_template/$templateName" | ConvertTo-Json -Depth 8

Write-Host "Done. Point Serilog to index format '${IndexPrefix}-{0:yyyy.MM.dd}'."

