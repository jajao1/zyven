# Vendas e Financeiro — Design

## Objetivo

Entregar uma área autenticada por organização que permita a proprietários, administradores e membros financeiros acompanhar vendas, valores recebidos, taxas, saldo disponível e lançamentos contábeis. Todos os números vêm de `Payment`, `LedgerTransaction` e `LedgerEntry`; nenhum saldo pode ser informado ou alterado pela interface.

## Escopo

A navegação do workspace ganha a seção **Vendas**. A página reúne uma visão geral comercial e um extrato financeiro em abas. A visão geral apresenta carteira, contadores por status e uma lista paginada de pagamentos. Cada venda pode ser aberta para mostrar comprador, oferta, checkout, provedor, valores, situação do pagamento e situação da entrega. O extrato usa o ledger imutável existente e exibe as partidas de cada lançamento.

O primeiro incremento não inclui reembolso, cancelamento, saque, gráficos históricos, exportação, conciliação manual ou edição financeira. Esses recursos dependem de movimentos e processos ainda inexistentes no domínio.

## Autorização e isolamento

Os endpoints ficam sob `/api/organizations/{organizationId}/finance` e exigem autenticação. `OWNER`, `ADMIN` e `FINANCE` podem consultar. `OPERATOR` e `SUPPORT` recebem `403`; usuários sem vínculo recebem `404`, preservando a política de não revelar organizações. Todas as consultas filtram `OrganizationId` antes de joins, paginação ou agregações.

A navegação mostra **Vendas** apenas para os três papéis autorizados. A proteção da API continua sendo a garantia autoritativa.

## API e contratos

### Resumo

`GET /api/organizations/{organizationId}/finance/summary`

Retorna:

- moeda;
- saldo disponível, total recebido e total de taxas, reutilizando a semântica de `WalletResponse`;
- total de pagamentos;
- contagem de pagos, pendentes, expirados e falhos;
- valor líquido de pagamentos pagos.

Os status `PENDING` e `PROCESSING` formam o grupo pendente. `FAILED`, `CANCELLED`, `REFUNDED` e `CHARGEBACK` formam o grupo de falha para a primeira visão agregada, sem alterar o status original exibido na lista.

### Lista de vendas

`GET /api/organizations/{organizationId}/finance/sales?page=1&pageSize=20&status=PAID`

O filtro `status` é opcional e aceita somente os estados persistidos de pagamento. A resposta paginada contém:

- ID e status do pagamento;
- nome e e-mail do comprador;
- nome da oferta;
- método e provedor;
- bruto, taxa da plataforma, taxa do provedor e líquido;
- data de criação e data de pagamento.

A ordenação é decrescente por `PaidAt`, depois `CreatedAt` e ID. A consulta projeta diretamente no banco, sem carregar entidades completas.

### Detalhe da venda

`GET /api/organizations/{organizationId}/finance/sales/{paymentId}`

Retorna os campos da lista e também checkout, referência externa, referência do provedor, end-to-end ID, expiração e estado do entitlement/fulfillment. O retorno não contém PIX copia e cola, token do gateway, segredo de checkout, documento, telefone ou campos personalizados do comprador.

### Carteira e extrato

Os endpoints existentes `/wallet` e `/ledger` permanecem compatíveis. O frontend consulta ambos diretamente. O extrato apresenta cada `LedgerTransactionResponse` e suas partidas, traduzindo códigos de conta para rótulos legíveis sem esconder o código original.

## Backend

Um `SalesService` separado concentra resumo, lista e detalhe. Ele reutiliza `TenantAuthorization` e não altera o `LedgerService`, que continua responsável pela carteira e histórico contábil. Contratos de leitura ficam em `Application/SalesContracts.cs`; endpoints ficam em `Api/SalesEndpoints.cs` ou no grupo financeiro existente.

As consultas fazem joins entre `Payments`, `Customers`, `Offers`, `Checkouts`, `Entitlements` e `FulfillmentExecutions`, sempre usando chaves acompanhadas de `OrganizationId`. Não é necessária migration: o recurso usa dados e índices existentes.

## Frontend

Arquivos próprios (`Sales.tsx`, `sales-client.ts`, testes) evitam ampliar `Organizations.tsx`. A página segue o tema Obsidian atual e contém:

1. cabeçalho “Vendas e financeiro”;
2. cartões de saldo disponível, recebido, taxas e pagamentos confirmados;
3. abas “Vendas” e “Extrato”;
4. filtro de status e paginação;
5. lista responsiva de vendas;
6. painel de detalhe selecionado;
7. extrato expansível com débitos e créditos.

Valores são formatados em `pt-BR` a partir das strings decimais retornadas pela API. Datas usam o fuso do navegador. Estados vazios distinguem “nenhuma venda” de “nenhum resultado para este filtro”. Erros preservam as mensagens seguras da API e oferecem nova tentativa.

## Consistência e erros

Resumo, lista e detalhe são leituras `AsNoTracking`. O dashboard pode refletir pequenas diferenças entre requisições simultâneas enquanto um pagamento é confirmado; cada resposta individual é consistente e nenhuma delas modifica o ledger. O frontend invalida as consultas financeiras ao trocar de organização e usa chaves que incluem usuário, organização, página e filtro.

Paginação inválida retorna `400`. Status desconhecido retorna `400`. Venda de outro tenant retorna `404`. Falhas de infraestrutura retornam o tratamento global atual, sem vazar detalhes internos.

## Validação

Testes de integração cobrem: agregados financeiros, listagem e detalhe de venda paga, filtros, paginação, papéis autorizados, bloqueio de `OPERATOR`/`SUPPORT` e isolamento entre organizações. Testes frontend cobrem carregamento, formatação monetária, filtro, paginação, detalhe, extrato e erro recuperável.

O gate final executa build .NET, testes unitários e de integração, verificação de formatação e modelo EF, testes frontend, build e lint. O ambiente Docker recebe rebuild da API e frontend, seguido de smoke autenticado e inspeção visual.
