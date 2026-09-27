param([string]$BaseUrl = 'http://localhost:8088')
$ErrorActionPreference = 'Stop'
$sellerSession = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
$sellerHeaders = @{ 'X-Zyven-Client' = 'web' }
$publicHeaders = @{ 'X-Zyven-Client' = 'web' }
function Call-Customer([string]$Path, [string]$Method, $Body, [int]$ExpectedStatus, $Headers, $Session) {
    $options = @{ Uri = "$BaseUrl$Path"; Method = $Method; Headers = $Headers; WebSession = $Session; SkipHttpErrorCheck = $true }
    if ($null -ne $Body) { $options.Body = ConvertTo-Json $Body -Depth 8; $options.ContentType = 'application/json' }
    $response = Invoke-WebRequest @options
    if ([int]$response.StatusCode -ne $ExpectedStatus) { throw "$Method $Path returned $($response.StatusCode), expected $ExpectedStatus" }
    if ($response.Content -and $ExpectedStatus -lt 400) { return $response.Content | ConvertFrom-Json }
}
function New-CustomerOffer {
    $org = Call-Customer '/api/organizations' POST @{ name = 'Customer smoke' } 201 $sellerHeaders $sellerSession
    $root = "/api/organizations/$($org.id)"
    $input = @{ name = 'Produto teste'; slug = 'produto-teste'; status = 'DRAFT' }
    $product = Call-Customer "$root/products" POST $input 201 $sellerHeaders $sellerSession
    $input.status = 'ACTIVE'
    $null = Call-Customer "$root/products/$($product.id)" PATCH $input 200 $sellerHeaders $sellerSession
    $offerInput = @{ productId = $product.id; name = 'Oferta teste'; slug = "customers-$([Guid]::NewGuid().ToString('N'))"; price = '29.90'; currency = 'BRL'; billingType = 'ONE_TIME'; status = 'DRAFT' }
    $offer = Call-Customer "$root/offers" POST $offerInput 201 $sellerHeaders $sellerSession
    $offerInput.status = 'ACTIVE'
    $null = Call-Customer "$root/offers/$($offer.id)" PATCH $offerInput 200 $sellerHeaders $sellerSession
    return @{ Root = $root; Slug = $offer.slug }
}
$scenarioFailed = $false
try {
    $registration = Call-Customer '/api/auth/register' POST @{ email = "smoke-customers-$([Guid]::NewGuid().ToString('N'))@example.test"; password = [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(24)); displayName = 'Customer Smoke' } 200 $sellerHeaders $sellerSession
    $sellerHeaders.Authorization = "Bearer $($registration.accessToken)"
    $a = New-CustomerOffer
    $firstInput = @{ name = 'Perfil original'; email = 'Customer.Smoke@example.test'; phone = '+55 (11) 99999-0000'; document = 'TEST-001' }
    $buyerOne = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
    $null = Call-Customer "/api/public/offers/$($a.Slug)/checkouts" POST $firstInput 201 $publicHeaders $buyerOne
    $buyerTwo = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
    $second = Call-Customer "/api/public/offers/$($a.Slug)/checkouts" POST @{ name = 'Dados do novo checkout'; email = 'customer.smoke@EXAMPLE.TEST'; phone = '+5511999990000'; document = $null } 201 $publicHeaders $buyerTwo
    if ($second.name -ne 'Dados do novo checkout' -or $second.document) { throw 'Anonymous checkout exposed an existing customer profile' }
    $list = Call-Customer "$($a.Root)/customers" GET $null 200 $sellerHeaders $sellerSession
    if ($list.total -ne 1) { throw 'Equivalent email/phone produced duplicate customers' }
    $customer = Call-Customer "$($a.Root)/customers/$($list.items[0].id)" GET $null 200 $sellerHeaders $sellerSession
    if ($customer.name -ne 'Perfil original' -or $customer.document -ne 'TEST-001') { throw 'Anonymous checkout changed the existing customer profile' }
    $b = New-CustomerOffer
    $buyerThree = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
    $null = Call-Customer "/api/public/offers/$($b.Slug)/checkouts" POST $firstInput 201 $publicHeaders $buyerThree
    $otherList = Call-Customer "$($b.Root)/customers" GET $null 200 $sellerHeaders $sellerSession
    if ($otherList.total -ne 1 -or $otherList.items[0].id -eq $customer.id) { throw 'Customer identity leaked across organizations' }
    $null = Call-Customer "$($b.Root)/customers/$($customer.id)" GET $null 404 $sellerHeaders $sellerSession
}
catch { $scenarioFailed = $true; throw }
finally {
    if ($sellerHeaders.ContainsKey('Authorization')) {
        try { $null = Call-Customer '/api/auth/logout' POST $null 204 $sellerHeaders $sellerSession }
        catch { if (-not $scenarioFailed) { throw }; Write-Warning 'Could not revoke the smoke seller session.' }
    }
}
Write-Host 'Customer smoke passed: normalization, deduplication, tenant scope and anonymous profile protection.'
