$ErrorActionPreference = 'Stop'
$repoPath = Split-Path $PSScriptRoot -Parent
$envPath = Join-Path $repoPath '.env'
if (Test-Path -LiteralPath $envPath) {
    Write-Host 'O arquivo .env já existe; valores preservados.'
    exit 0
}
function New-Secret {
    return [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
}
@"
POSTGRES_PASSWORD=$(New-Secret)
REDIS_PASSWORD=$(New-Secret)
JWT_SIGNING_KEY=$(New-Secret)
WEB_PORT=8088
API_PORT=5080
POSTGRES_PORT=55432
REDIS_PORT=56379
"@ | Set-Content -LiteralPath $envPath -Encoding utf8
Write-Host 'Segredos locais gerados em .env (ignorado pelo Git).'
