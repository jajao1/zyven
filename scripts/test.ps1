$ErrorActionPreference = 'Stop'
$repoPath = Split-Path $PSScriptRoot -Parent
Push-Location $repoPath
function Check-Exit { if ($LASTEXITCODE -ne 0) { throw "Command failed: exit $LASTEXITCODE" } }
try {
    if (!(Test-Path .env)) { & ./scripts/setup.ps1 }
    $values = @{}
    Get-Content .env | ForEach-Object { if ($_ -match '^([^#=]+)=(.*)$') { $values[$matches[1]] = $matches[2] } }
    # Separate Compose project and ports keep tests away from application data.
    $env:POSTGRES_PORT = '55433'
    $env:REDIS_PORT = '56380'
    $env:DOCKER_SUBNET = '172.31.0.0/24'
    $env:DOCKER_PROXY_IP = '172.31.0.10'
    docker compose -p zyven-tests up -d --wait postgres redis
    Check-Exit
    $env:ConnectionStrings__Database = "Host=localhost;Port=55433;Database=zyven;Username=zyven;Password=$($values.POSTGRES_PASSWORD)"
    $env:ConnectionStrings__Redis = "localhost:56380,password=$($values.REDIS_PASSWORD),abortConnect=false"
    $env:Jwt__SigningKey = $values.JWT_SIGNING_KEY
    $env:Jwt__Issuer = 'Zyven'
    $env:Jwt__Audience = 'Zyven.Web'
    $env:Cors__AllowedOrigins__0 = 'http://localhost:8088'
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    dotnet build Zyven.slnx --configuration Release
    Check-Exit
    dotnet run --no-build -c Release --project src/Api -- --migrate
    Check-Exit
    dotnet test Zyven.slnx --configuration Release --no-build
    Check-Exit
    dotnet format Zyven.slnx --verify-no-changes --no-restore
    Check-Exit
    dotnet tool restore
    Check-Exit
    dotnet ef migrations has-pending-model-changes --project src/Infrastructure --startup-project src/Api
    Check-Exit
    Push-Location web
    try {
        npm ci
        Check-Exit
        npm test
        Check-Exit
        npm run build
        Check-Exit
        npm run lint
        Check-Exit
    } finally { Pop-Location }
} finally {
    # This project is created exclusively for this test run; application volumes are untouched.
    docker compose -p zyven-tests down -v
    @('POSTGRES_PORT','REDIS_PORT','DOCKER_SUBNET','DOCKER_PROXY_IP','ConnectionStrings__Database','ConnectionStrings__Redis','Jwt__SigningKey','Jwt__Issuer','Jwt__Audience','Cors__AllowedOrigins__0','ASPNETCORE_ENVIRONMENT') | ForEach-Object { Remove-Item "Env:$_" -ErrorAction SilentlyContinue }
    Pop-Location
}
