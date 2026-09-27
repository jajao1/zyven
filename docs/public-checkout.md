# Página pública e checkout

A página pública expõe conteúdo configurado e preço da oferta somente quando produto e oferta estão ativos. Dados internos de organização, integrações e entrega ficam fora da resposta pública.

O editor usa blocos simples com texto, benefícios, depoimentos, perguntas frequentes, garantia e CTA. Mídias são URLs HTTPS sem credenciais embutidas; o servidor não busca essas URLs. Campos adicionais de checkout são limitados e validados. Não há execução de HTML ou JavaScript arbitrário fornecido pelo vendedor.

Cada checkout é uma sessão persistente vinculada à organização e à oferta resolvidas no servidor. Preço e moeda vêm da oferta; valores enviados pelo comprador não definem a cobrança. O status inicial CREATED não significa pagamento confirmado. A integração de pagamentos pertence à fase 6.

O navegador recebe uma credencial aleatória em cookie HttpOnly/SameSite=Strict, com caminho restrito à sessão e Secure fora de Development. O banco armazena somente seu hash. A URL contém um identificador, sem segredo. Conhecer esse identificador não permite consultar dados pessoais sem o cookie correspondente. Respostas usam no-store; tokens não entram em logs.

## Endpoints e limites

| Método | Rota | Acesso |
| --- | --- | --- |
| GET / PUT | `/api/organizations/{org}/offers/{offer}/page` | Membros leem; OWNER/ADMIN/OPERATOR editam |
| GET | `/api/public/offers/{slug}` | Conteúdo público de oferta e produto ativos |
| POST | `/api/public/offers/{slug}/checkouts` | Cria sessão e cookie de acesso |
| GET / PATCH | `/api/public/checkouts/{id}` | Exige cookie correspondente; PATCH atualiza os dados capturados |

POST/PATCH públicos exigem `X-Zyven-Client: web` e rejeitam Origin fora da configuração permitida. Redis limita leituras a 300 e mutações a 60 por IP em 15 minutos. Requests têm limite de 128 KiB. A sessão expira em 30 minutos; a validação de acesso rejeita imediatamente após o prazo e um job Hangfire a cada cinco minutos materializa o status EXPIRED.

O formulário recebe name, email, phone/document opcionais e um dicionário fields de campos configurados. Campos adicionais aceitam texto curto/longo, até dez definições e valores limitados. A resposta inclui offerSlug para impedir que um recibo seja apresentado junto da página de outra oferta. Alterar status ou OrganizationId não é uma operação pública.

## Validação local

Executar `./scripts/smoke-checkout.ps1` após reconstruir o Compose. O teste cria vendedor, organização, produto e oferta locais, publica conteúdo, inicia checkout anônimo, verifica preço autoritativo e isolamento da sessão. Os registros de teste ficam no banco local; o vendedor encerra a sessão ao terminar. Não executar esse smoke em produção.

CustomerId e identificação de clientes entram na fase 5. Campanhas, afiliados, cupons, order bumps e pixels entram nas fases correspondentes.
