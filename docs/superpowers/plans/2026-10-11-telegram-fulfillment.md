# Telegram Fulfillment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Entregar acesso idempotente a grupos e canais Telegram usando o bot próprio de cada vendedor.

**Architecture:** Uma integração tenant-scoped cifra o token e encapsula a Bot API em uma interface testável. A execução existente de fulfillment emite um vínculo curto; o webhook autenticado consome o vínculo, registra o usuário e envia convite individual.

**Tech Stack:** .NET 10, ASP.NET Core Minimal APIs, EF Core 10/PostgreSQL, React/TypeScript, Telegram Bot API.

---

### Task 1: Bot API e credenciais

**Files:** Create `src/Infrastructure/TelegramClient.cs`, `src/Infrastructure/TelegramCredentialVault.cs`; modify `src/Api/Program.cs`; test `tests/UnitTests/TelegramCredentialVaultTests.cs`.

- [ ] Escrever teste de round-trip, aleatoriedade, fingerprint e rejeição de chave inválida.
- [ ] Executar o teste e confirmar falha pela ausência do tipo.
- [ ] Implementar AES-256-GCM, contratos `ITelegramClient` e cliente HTTP com `getMe`, `getChat`, `getChatMember`, `setWebhook`, `createChatInviteLink` e `sendMessage`.
- [ ] Executar testes unitários e commit.

### Task 2: Modelo tenant-scoped

**Files:** Create `src/Domain/Telegram.cs`, `src/Infrastructure/TelegramConfiguration.cs`; modify `src/Domain/Fulfillment.cs`, `src/Infrastructure/FulfillmentConfiguration.cs`, `src/Infrastructure/ZyvenDbContext.cs`; test `tests/UnitTests/TelegramFulfillmentTests.cs`.

- [ ] Escrever testes para integração ativa, definição Telegram, vínculo de uso único e execução com snapshot.
- [ ] Implementar `TelegramIntegration`, `TelegramLinkToken`, `TelegramAccessGrant` e propriedades Telegram nas definições/execuções.
- [ ] Adicionar chaves compostas, índices únicos, limites e checks.
- [ ] Gerar migration `TelegramFulfillment`, verificar modelo e commit.

### Task 3: Configuração administrativa

**Files:** Create `src/Application/TelegramContracts.cs`, `src/Infrastructure/TelegramIntegrationService.cs`, `src/Api/TelegramEndpoints.cs`; modify `src/Api/Program.cs`; test `tests/IntegrationTests/TelegramIntegrationTests.cs`.

- [ ] Escrever testes de OWNER/ADMIN, isolamento e respostas sem token.
- [ ] Implementar conexão do bot por `getMe`, registro seguro de webhook e configuração de chat validada.
- [ ] Criar GET/PUT de integração e GET/PUT de fulfillment Telegram.
- [ ] Executar testes integrados e commit.

### Task 4: Vínculo e webhook

**Files:** Create `src/Infrastructure/TelegramFulfillmentService.cs`, `src/Api/TelegramWebhookEndpoints.cs`; modify `src/Infrastructure/FulfillmentService.cs`, `src/Infrastructure/BuyerPurchaseService.cs`; test `tests/IntegrationTests/TelegramFulfillmentTests.cs`.

- [ ] Escrever testes de código expirado, consumo único, segredo inválido, update duplicado e vínculo à compra paga.
- [ ] Implementar endpoint autenticado do comprador/checkout para gerar deep link.
- [ ] Implementar webhook que bloqueia token, valida pagamento e cria convite de uso único.
- [ ] Persistir update id e grant antes de responder; permitir retomada segura em falha transitória.
- [ ] Executar testes e commit.

### Task 5: Interface

**Files:** Modify componentes de configurações, oferta, cliente API e Área do Comprador em `web/src`; test arquivos correspondentes em `web/src/**/*.test.tsx`.

- [ ] Escrever testes de conexão mascarada, configuração do chat e ação “Vincular Telegram”.
- [ ] Implementar formulários sem reexibir token e estados de validação.
- [ ] Renderizar `TELEGRAM_ACCESS` como ação interna do Zyven.
- [ ] Executar testes, build e lint; commit.

### Task 6: Documentação e verificação

**Files:** Modify `.env.example`, `compose.yaml`, `README.md`, `docs/fulfillment.md`, `docs/implementation-status.md`.

- [ ] Documentar chave de cifra, URL pública, permissões do bot e smoke real.
- [ ] Executar migrations em PostgreSQL limpo, build, testes .NET/frontend, lint, format e verificação EF.
- [ ] Fazer revisão de segredos e isolamento tenant.
- [ ] Atualizar branch remota e PR somente após todos os checks passarem.
