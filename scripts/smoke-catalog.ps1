param([string]$BaseUrl = 'http://localhost:8088')
$ErrorActionPreference = 'Stop'
$clients = [System.Collections.Generic.List[hashtable]]::new()
function Call-Catalog($Client, [string]$Path, [string]$Method = 'GET', $Body = $null, [int]$ExpectedStatus = 200) {
    $options = @{ Uri = "$BaseUrl$Path"; Method = $Method; Headers = $Client.Headers; WebSession = $Client.Session; SkipHttpErrorCheck = $true }
    if ($null -ne $Body) { $options.Body = ConvertTo-Json $Body -Depth 8; $options.ContentType = 'application/json' }
    $response = Invoke-WebRequest @options
    if ([int]$response.StatusCode -ne $ExpectedStatus) { throw "$Method $Path returned $($response.StatusCode), expected $ExpectedStatus" }
    if ($response.Content -and $ExpectedStatus -lt 400) { return $response.Content | ConvertFrom-Json }
}
function New-SmokeClient {
    $client = @{ Headers = @{ 'X-Zyven-Client' = 'web' }; Session = [Microsoft.PowerShell.Commands.WebRequestSession]::new() }
    $body = @{ email = "smoke-catalog-$([Guid]::NewGuid().ToString('N'))@example.test"; password = [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(24)); displayName = 'Catalog Smoke' }
    $grant = Call-Catalog $client '/api/auth/register' 'POST' $body
    $client.Headers.Authorization = "Bearer $($grant.accessToken)"
    $clients.Add($client)
    return $client
}
$scenarioFailed = $false
try {
    $alice = New-SmokeClient
    $bob = New-SmokeClient
    $orgA = Call-Catalog $alice '/api/organizations' 'POST' @{ name = 'Smoke catalog A' } 201
    $orgB = Call-Catalog $bob '/api/organizations' 'POST' @{ name = 'Smoke catalog B' } 201
    $rootA = "/api/organizations/$($orgA.id)"
    $rootB = "/api/organizations/$($orgB.id)"
    $productBody = @{ name = 'Curso smoke'; slug = 'curso-smoke'; description = 'Teste local do catálogo'; imageUrl = $null; status = 'DRAFT' }
    $productA = Call-Catalog $alice "$rootA/products" 'POST' $productBody 201
    $productB = Call-Catalog $bob "$rootB/products" 'POST' $productBody 201
    $offerBody = @{ productId = $productA.id; name = 'Acesso único'; slug = "smoke-$([Guid]::NewGuid().ToString('N'))"; headline = 'Curso smoke'; description = 'Teste local'; price = '19.90'; currency = 'BRL'; status = 'DRAFT'; billingType = 'ONE_TIME' }
    $offer = Call-Catalog $alice "$rootA/offers" 'POST' $offerBody 201
    if ([decimal]$offer.price -ne [decimal]19.90 -or $offer.status -ne 'DRAFT') { throw 'Unexpected persisted offer values' }
    $null = Call-Catalog $bob "$rootA/offers/$($offer.id)" 'GET' $null 404
    $null = Call-Catalog $alice "$rootB/products/$($productB.id)" 'GET' $null 404
    $offerBody.productId = $productB.id
    $null = Call-Catalog $alice "$rootA/offers/$($offer.id)" 'PATCH' $offerBody 404
    $offerBody.productId = $productA.id
    $offerBody.price = '1.001'
    $null = Call-Catalog $alice "$rootA/offers/$($offer.id)" 'PATCH' $offerBody 400
    $offerBody.price = '29.99'
    $offerBody.status = 'ARCHIVED'
    $null = Call-Catalog $alice "$rootA/offers/$($offer.id)" 'PATCH' $offerBody
    $persisted = Call-Catalog $alice "$rootA/offers/$($offer.id)"
    if ([decimal]$persisted.price -ne [decimal]29.99 -or $persisted.status -ne 'ARCHIVED') { throw 'Offer update was not persisted' }
    $listing = Call-Catalog $bob "$rootB/offers"
    if ($listing.items.Count -ne 0) { throw 'Unexpected cross-tenant offer in list' }
}
catch { $scenarioFailed = $true; throw }
finally {
    $cleanupFailed = $false
    foreach ($client in $clients) { try { $null = Call-Catalog $client '/api/auth/logout' 'POST' $null 204 } catch { $cleanupFailed = $true; Write-Warning 'Could not revoke a catalog smoke session; it remains subject to normal expiry.' } }
    if ($cleanupFailed -and -not $scenarioFailed) { throw 'Catalog session cleanup failed.' }
}
Write-Host 'Catalog smoke passed: products, offers, decimal price, tenant boundaries, foreign link rejection and archive.'
