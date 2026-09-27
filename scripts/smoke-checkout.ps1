param([string]$BaseUrl = 'http://localhost:8088')
$ErrorActionPreference = 'Stop'
$sellerSession = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
$buyerSession = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
$sellerHeaders = @{ 'X-Zyven-Client' = 'web' }
$buyerHeaders = @{ 'X-Zyven-Client' = 'web' }
function Call-Checkout([string]$Path, [string]$Method, $Body, [int]$ExpectedStatus, $Headers, $Session) {
    $options = @{ Uri = "$BaseUrl$Path"; Method = $Method; Headers = $Headers; WebSession = $Session; SkipHttpErrorCheck = $true }
    if ($null -ne $Body) { $options.Body = ConvertTo-Json $Body -Depth 10; $options.ContentType = 'application/json' }
    $response = Invoke-WebRequest @options
    if ([int]$response.StatusCode -ne $ExpectedStatus) { throw "$Method $Path returned $($response.StatusCode), expected $ExpectedStatus" }
    $data = if ($response.Content -and $ExpectedStatus -lt 400) { $response.Content | ConvertFrom-Json } else { $null }
    return @{ Data = $data; Response = $response }
}
$scenarioFailed = $false
try {
    $registration = Call-Checkout '/api/auth/register' POST @{ email = "smoke-checkout-$([Guid]::NewGuid().ToString('N'))@example.test"; password = [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(24)); displayName = 'Checkout Smoke' } 200 $sellerHeaders $sellerSession
    $sellerHeaders.Authorization = "Bearer $($registration.Data.accessToken)"
    $org = (Call-Checkout '/api/organizations' POST @{ name = 'Smoke checkout' } 201 $sellerHeaders $sellerSession).Data
    $root = "/api/organizations/$($org.id)"
    $productInput = @{ name = 'Curso checkout'; slug = 'curso-checkout'; description = 'Teste local'; imageUrl = $null; status = 'DRAFT' }
    $product = (Call-Checkout "$root/products" POST $productInput 201 $sellerHeaders $sellerSession).Data
    $offerInput = @{ productId = $product.id; name = 'Oferta checkout'; slug = "checkout-$([Guid]::NewGuid().ToString('N'))"; headline = 'Aprenda na prática'; description = 'Teste local'; price = '39.90'; currency = 'BRL'; status = 'DRAFT'; billingType = 'ONE_TIME' }
    $offer = (Call-Checkout "$root/offers" POST $offerInput 201 $sellerHeaders $sellerSession).Data
    $page = @{ title = 'Curso de teste'; subtitle = 'Aprenda na prática'; description = 'Conteúdo público de teste'; color = '#002fa7'; cta = 'Continuar'; benefits = @('Aulas práticas'); testimonials = @(); faq = @(); guarantee = ''; fields = @(@{ key = 'objetivo'; label = 'Seu objetivo'; type = 'text'; required = $true }) }
    $null = Call-Checkout "$root/offers/$($offer.id)/page" PUT $page 200 $sellerHeaders $sellerSession
    $publicPath = "/api/public/offers/$($offer.slug)"
    $null = Call-Checkout $publicPath GET $null 404 $buyerHeaders $buyerSession
    $productInput.status = 'ACTIVE'
    $null = Call-Checkout "$root/products/$($product.id)" PATCH $productInput 200 $sellerHeaders $sellerSession
    $offerInput.status = 'ACTIVE'
    $null = Call-Checkout "$root/offers/$($offer.id)" PATCH $offerInput 200 $sellerHeaders $sellerSession
    $published = (Call-Checkout $publicPath GET $null 200 $buyerHeaders $buyerSession).Data
    if ($published.price -ne '39.90' -or $published.page.title -ne $page.title) { throw 'Unexpected public offer content or price' }
    if ('organizationId' -in $published.PSObject.Properties.Name) { throw 'Public response exposes internal organization data' }
    $buyerInput = @{ name = 'Comprador de teste'; email = 'checkout-buyer@example.test'; phone = $null; document = $null; fields = @{ objetivo = 'Aprender a vender' }; price = '0.01'; organizationId = [Guid]::NewGuid().ToString() }
    $untrusted = @{ 'X-Zyven-Client' = 'web'; Origin = 'https://untrusted.example' }
    $untrustedSession = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
    $null = Call-Checkout "$publicPath/checkouts" POST $buyerInput 403 $untrusted $untrustedSession
    $oversized = @{ name = 'Comprador de teste'; email = 'checkout-buyer@example.test'; fields = @{ objetivo = ('x' * 140000) } }
    $null = Call-Checkout "$publicPath/checkouts" POST $oversized 413 $buyerHeaders $buyerSession
    $created = Call-Checkout "$publicPath/checkouts" POST $buyerInput 201 $buyerHeaders $buyerSession
    $checkout = $created.Data
    if ($checkout.price -ne '39.90' -or $checkout.status -ne 'CREATED') { throw 'Checkout trusted buyer price or invalid initial status' }
    $cookies = $created.Response.Headers['Set-Cookie'] -join ';'
    if ($cookies -notmatch 'httponly' -or $cookies -notmatch 'samesite=strict' -or $cookies -notmatch "/api/public/checkouts/$($checkout.id)") { throw 'Checkout capability cookie protection is missing' }
    $checkoutPath = "/api/public/checkouts/$($checkout.id)"
    $stranger = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
    $null = Call-Checkout $checkoutPath GET $null 404 $buyerHeaders $stranger
    $saved = (Call-Checkout $checkoutPath GET $null 200 $buyerHeaders $buyerSession).Data
    if ($saved.email -ne $buyerInput.email -or $saved.fields.objetivo -ne 'Aprender a vender') { throw 'Checkout data did not persist' }
}
catch { $scenarioFailed = $true; throw }
finally {
    if ($sellerHeaders.ContainsKey('Authorization')) {
        try { $null = Call-Checkout '/api/auth/logout' POST $null 204 $sellerHeaders $sellerSession }
        catch { if (-not $scenarioFailed) { throw }; Write-Warning 'Could not revoke the smoke seller session.' }
    }
}
Write-Host 'Checkout smoke passed: page publication, server price, origin protection, capability cookie and private session access.'
