# Zyven

SaaS de social commerce. A especificação mestre está em [`docs/zyven-master-spec.txt`](docs/zyven-master-spec.txt). O desenvolvimento segue suas fases; as fases 1 a 4 implementam base, autenticação, organizações, equipe, catálogo, página pública e checkout persistente. Clientes são a próxima etapa.

## Executar localmente

Requisitos: Docker Desktop com containers Linux e PowerShell 7. Para desenvolvimento fora do Docker: SDK .NET 10 e Node.js 24.

```powershell
./scripts/setup.ps1
docker compose up -d --build
```

Após a preparação inicial, `docker compose up -d` inicia o ambiente. Abra **http://localhost:8088**. Cadastre uma conta pela interface; não existe senha de administrador pré-definida. A API fica em http://localhost:5080; OpenAPI de desenvolvimento em `/openapi/v1.json`.

O Compose inicia PostgreSQL e Redis, aplica migrations em um processo separado, inicia API e Worker e libera o frontend quando a API estiver pronta. Dados persistem em volumes nomeados. `docker compose down` encerra os containers preservando os dados. As portas são expostas somente no loopback da máquina.

## Configuração

`scripts/setup.ps1` gera `.env` com segredos aleatórios, preservando um arquivo existente. Nunca versionar `.env`. Em outros sistemas, copie `.env.example` e gere valores aleatórios para suas três chaves. O Compose exige os valores; não contém credenciais padrão de aplicação.

| Variável | Uso |
| --- | --- |
| POSTGRES_PASSWORD | Senha local PostgreSQL |
| REDIS_PASSWORD | Senha local Redis |
| JWT_SIGNING_KEY | Chave aleatória de assinatura JWT, pelo menos 32 bytes |
| WEB_PORT / API_PORT | Portas locais 8088 / 5080 |
| POSTGRES_PORT / REDIS_PORT | Portas locais 55432 / 56379 |
| DOCKER_SUBNET | Rede local padrão 172.30.0.0/24 |
| DOCKER_PROXY_IP | Endereço do frontend/proxy na rede, padrão 172.30.0.10 |

A API aceita `ConnectionStrings__Database`, `ConnectionStrings__Redis`, `Jwt__SigningKey`, `Jwt__Issuer`, `Jwt__Audience` e `Cors__AllowedOrigins__0`. O endereço `DOCKER_PROXY_IP` configura tanto o frontend quanto `ReverseProxy__KnownProxies__0`. Ao alterar `DOCKER_SUBNET`, escolha também um `DOCKER_PROXY_IP` livre dentro da nova rede. Cabeçalhos de outros proxies não são confiados.

O Compose é um ambiente de **Development**. Em produção, configurar TLS no proxy, ambientes Production, origens HTTPS explícitas e segredos gerenciados. Cookies recebem `Secure` fora de Development. Não publicar PostgreSQL/Redis nem a porta direta da API. A configuração local não representa um deployment de produção.

## Arquitetura

`src/Domain` contém entidades; `src/Application`, contratos e validação; `src/Infrastructure`, persistência e serviços; `src/Api`, HTTP; `src/Workers`, processamento Hangfire. `web` usa React, TypeScript, Vite, Tailwind, componentes shadcn/ui, TanStack Query, React Hook Form e Zod. Recharts está disponível para a fase de dashboard.

O núcleo comercial segue **Product → Offer → Checkout → Payment → Entitlement → Fulfillment**. Organizações e isolamento estão implementados; veja [`docs/organizations.md`](docs/organizations.md) para endpoints e permissões. Nenhum identificador de organização enviado pelo cliente deve conferir acesso por si só. Pagamentos, ledger, saldo, saques e integrações não são implementados nesta base.

## Autenticação

| Método | Endpoint | Resultado |
| --- | --- | --- |
| POST | `/api/auth/register` | Cria conta e sessão; recebe email, password e displayName |
| POST | `/api/auth/login` | Autentica; recebe email e password |
| POST | `/api/auth/refresh` | Rotaciona cookie de refresh |
| POST | `/api/auth/logout` | Revoga a sessão autenticada |
| GET | `/api/auth/me` | Perfil da sessão autenticada |

POSTs exigem `X-Zyven-Client: web`; se `Origin` estiver presente, deve constar na lista explícita. Register/login/refresh retornam `{ accessToken, user: { id, email, displayName } }`. JWT dura 10 minutos; a sessão tem expiração absoluta de 30 dias. Refresh fica em cookie `HttpOnly`, `SameSite=Strict`, caminho `/api/auth`. O frontend mantém o JWT somente em memória e serializa refresh entre abas com Web Locks quando suportado.

Senhas usam PasswordHasher com PBKDF2 e 210 mil iterações. A política permite frases de 12 a 128 caracteres. Refresh tokens de 384 bits são persistidos como SHA-256; rotação e logout bloqueiam a mesma linha de sessão em transação. Reutilizar um token consumido revoga toda a família. JWTs de sessões revogadas são rejeitados imediatamente. Hashes consumidos permanecem até a expiração absoluta para detectar replay.

Redis mantém limites atômicos por IP e conta. Eventos de criação/revogação de sessão ficam em PostgreSQL, sem senhas ou tokens. Logs incluem correlação e duração, sem payload, cabeçalhos de autenticação ou query string.

## Migrations, worker e saúde

```powershell
docker compose run --rm migrate
docker compose logs api worker
```

O migrador é explícito; a API normal não altera schema ao iniciar. Para criar novas migrations com as variáveis de conexão configuradas:

```powershell
dotnet tool restore
dotnet ef migrations add Nome --project src/Infrastructure --startup-project src/Api
```

Hangfire persiste jobs no PostgreSQL. O Worker limpa sessões expiradas a cada hora; não há dashboard público. `/health/live` indica processo ativo; `/health` e `/health/ready` verificam PostgreSQL e Redis.

## Validação

```powershell
./scripts/test.ps1
./scripts/smoke.ps1
```

Esse script cria o projeto Docker exclusivo `zyven-tests` nas portas 55433/56380, aplica migrations e executa build .NET, testes, formatação, verificação de modelo EF, testes frontend, build e lint. Ao terminar, remove somente os containers e volumes desse projeto de testes. Não use esse nome de projeto para dados que deseje preservar. O workflow `.github/workflows/ci.yml` repete os checks em serviços efêmeros.

No Windows, encerre o servidor Vite antes de rodar `test.ps1`: a reinstalação reproduzível de dependências não consegue substituir arquivos nativos enquanto estiverem em uso.

`smoke.ps1` verifica o ciclo de autenticação pelo frontend/proxy local. Cria uma conta identificada por `smoke-…@example.test` e encerra suas sessões ao terminar; a conta de teste fica no banco local.

Para desenvolver o frontend com a API do Compose: `cd web`, `npm ci`, `npm run dev`; abra http://localhost:5173. O Vite encaminha `/api` para a API local.

## Evolução

As fases seguintes acrescentam clientes, pagamentos e confirmação idempotente, ledger e entrega. O primeiro fluxo completo deve terminar em `EXTERNAL_LINK` com pagamento confirmado no servidor. Telegram e Discord permanecem integrações. A fonte financeira será o ledger imutável, nunca um saldo editável. APIs e credenciais financeiras reais serão configuradas na fase correspondente; dados simulados não serão tratados como dinheiro real.

Catálogo e smoke específico: [docs/catalog.md](docs/catalog.md). Estado e dependências: [docs/implementation-status.md](docs/implementation-status.md).

Página pública e checkout: [docs/public-checkout.md](docs/public-checkout.md). A etapa de pagamento ainda não está disponível; criar checkout não confirma compra nem produz cobrança.
