param([string]$BaseUrl = 'http://localhost:8088')
$ErrorActionPreference = 'Stop'
$session = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
$headers = @{ 'X-Zyven-Client' = 'web' }
$email = "smoke-$([Guid]::NewGuid().ToString('N'))@example.test"
$password = [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(24))
function Call-Auth([string]$Path, [string]$Method, $Body, [int]$ExpectedStatus) {
    $options = @{ Uri = "$BaseUrl/api/auth/$Path"; Method = $Method; Headers = $headers; WebSession = $session; SkipHttpErrorCheck = $true }
    if ($null -ne $Body) { $options.Body = ConvertTo-Json $Body; $options.ContentType = 'application/json' }
    $response = Invoke-WebRequest @options
    if ([int]$response.StatusCode -ne $ExpectedStatus) { throw "$Path returned $($response.StatusCode), expected $ExpectedStatus" }
    return $response
}
$registration = Call-Auth register POST @{ email = $email; password = $password; displayName = 'Smoke Test' } 200
$grant = $registration.Content | ConvertFrom-Json
$headers.Authorization = "Bearer $($grant.accessToken)"
$me = Call-Auth me GET $null 200
if (($me.Content | ConvertFrom-Json).email -ne $email) { throw 'Unexpected authenticated user' }
$refresh = Call-Auth refresh POST $null 200
$headers.Authorization = "Bearer $(($refresh.Content | ConvertFrom-Json).accessToken)"
$null = Call-Auth logout POST $null 204
$null = Call-Auth me GET $null 401
$null = Call-Auth refresh POST $null 401
$headers.Remove('Authorization')
$login = Call-Auth login POST @{ email = $email; password = $password } 200
$headers.Authorization = "Bearer $(($login.Content | ConvertFrom-Json).accessToken)"
$null = Call-Auth logout POST $null 204
Write-Host 'Smoke passed: register, me, refresh, logout, revoked access, login.'
