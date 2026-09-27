param([string]$BaseUrl = 'http://localhost:8088')
$ErrorActionPreference = 'Stop'

# Development-only foundation check. No provider is configured at this stage.
function Read-FoundationCount([string]$Query) {
    $value = $Query | docker compose exec -T postgres psql -U zyven -d zyven -At -v ON_ERROR_STOP=1
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect the local payment foundation.' }
    return [long]$value
}

$before = Read-FoundationCount 'SELECT COUNT(*) FROM "Payments";'
& (Join-Path $PSScriptRoot 'smoke-customers.ps1') -BaseUrl $BaseUrl
if (-not $?) { throw 'Customer checkout smoke failed.' }
$missing = Read-FoundationCount 'SELECT COUNT(*) FROM "Organizations" o LEFT JOIN "MerchantAccounts" m ON m."OrganizationId" = o."Id" WHERE m."Id" IS NULL;'
$approved = Read-FoundationCount 'SELECT COUNT(*) FROM "MerchantAccounts" WHERE "Status" <> ''PENDING'';'
$after = Read-FoundationCount 'SELECT COUNT(*) FROM "Payments";'
if ($missing -ne 0) { throw 'An organization is missing its pending merchant account.' }
if ($approved -ne 0) { throw 'The unconfigured foundation contains an unexpectedly approved merchant.' }
if ($after -ne $before) { throw 'Capturing checkout data unexpectedly created a payment.' }
Write-Host 'Payment foundation smoke passed: pending merchant accounts, checkout preserved, no payment created.'
