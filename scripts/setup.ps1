$ErrorActionPreference = 'Stop'
$repoPath = Split-Path $PSScriptRoot -Parent
$envPath = Join-Path $repoPath '.env'
function New-Secret {
    return [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
}
function New-EncryptionKey {
    return [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
}
if (Test-Path -LiteralPath $envPath) {
    $content = Get-Content -LiteralPath $envPath -Raw
    if ($content -notmatch '(?m)^PAYMENT_CREDENTIAL_ENCRYPTION_KEY=') {
        Add-Content -LiteralPath $envPath -Value "PAYMENT_CREDENTIAL_ENCRYPTION_KEY=$(New-EncryptionKey)" -Encoding utf8
        Write-Host 'Chave de criptografia de pagamentos adicionada ao .env existente.'
    } else { Write-Host 'O arquivo .env já existe; valores preservados.' }
    exit 0
}
@"
POSTGRES_PASSWORD=$(New-Secret)
REDIS_PASSWORD=$(New-Secret)
JWT_SIGNING_KEY=$(New-Secret)
PAYMENT_CREDENTIAL_ENCRYPTION_KEY=$(New-EncryptionKey)
WEB_PORT=8088
API_PORT=5080
POSTGRES_PORT=55432
REDIS_PORT=56379
"@ | Set-Content -LiteralPath $envPath -Encoding utf8
Write-Host 'Segredos locais gerados em .env (ignorado pelo Git).'
