# Fase 1 — base e autenticação

Especificação autoritativa: `zyven-master-spec.txt`. Implementação autorizada pelo usuário; executar as fases em ordem e validar antes de avançar.

## Arquitetura

Monólito modular com Domain, Application, Infrastructure e API; Worker separado usando Hangfire/PostgreSQL. React serve a experiência de autenticação. O núcleo futuro permanece Product → Offer → Checkout → Payment → Entitlement → Fulfillment. Nenhuma entidade comercial é adicionada antes da fase de organizações e isolamento.

JWT de 10 minutos; refresh tokens aleatórios armazenados somente como hash, em sessões persistentes com expiração absoluta, rotação atômica e revogação de família em reutilização. Cookies HttpOnly para refresh no navegador, access token apenas em memória. Senhas com PasswordHasher, validação, limitação de tentativas, respostas genéricas de login, CORS explícito e proteção de origem em operações com cookies.

## Execução e critérios

- [ ] Backend: projetos net10.0, endpoints register/login/refresh/logout/me, EF Core/PostgreSQL, migrations, OpenAPI, Serilog, readiness PostgreSQL/Redis, worker persistente e testes unitários e de integração reais.
- [ ] Frontend: Vite/React/TypeScript, Tailwind/shadcn, Query, Hook Form/Zod e dependência Recharts; cadastro, login, sessão e logout sem dados comerciais fictícios.
- [ ] Infra: Dockerfiles, Compose API/Worker/frontend/PostgreSQL/Redis, segredos locais ignorados, instruções de execução e CI.
- [ ] Verificar build, testes, lint, migrations, smoke e revisão de segurança; corrigir falhas antes da Fase 2.
- [ ] Commits pequenos e descritivos com documentação das evidências e limitações.

## Testes de aceitação

Cadastro válido autentica; email duplicado é rejeitado; senha inválida não autentica; /me exige JWT válido; rotação invalida token anterior; replay revoga família; logout impede uso da sessão; refresh concorrente nunca gera duas sessões válidas; expiração é respeitada. Validar contra PostgreSQL real. Frontend compila e passa lint. Compose deve iniciar serviços saudáveis e permitir cadastro/login/logout pela mesma origem.

## Próximas fases

Seguir seção 72 da especificação. Antes de integrações adicionais, concluir fluxo EXTERNAL_LINK da seção 74. Dados e credenciais do provedor PIX serão necessários quando houver integração externa real; não representar simulação como processamento financeiro real.
