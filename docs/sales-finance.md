# Vendas e financeiro

A seção **Vendas** está disponível para `OWNER`, `ADMIN` e `FINANCE`. Os demais papéis não veem a entrada no workspace e a API rejeita o acesso. Todas as consultas exigem vínculo com a organização e filtram o tenant antes de agregar ou relacionar dados.

## Endpoints

| Método | Endpoint | Uso |
| --- | --- | --- |
| `GET` | `/api/organizations/{id}/finance/summary` | saldo, recebimentos, taxas e contadores de pagamentos |
| `GET` | `/api/organizations/{id}/finance/sales?page=1&pageSize=20&status=PAID` | vendas paginadas e filtro opcional por status |
| `GET` | `/api/organizations/{id}/finance/sales/{paymentId}` | comprador, oferta, valores, pagamento e entrega |
| `GET` | `/api/organizations/{id}/finance/wallet` | carteira calculada pelo ledger |
| `GET` | `/api/organizations/{id}/finance/ledger?page=1&pageSize=20` | lançamentos e partidas imutáveis |

Os valores monetários são retornados como strings decimais. O frontend os formata em BRL sem converter a origem financeira para ponto flutuante durante o transporte.

O saldo disponível, o total recebido e as taxas são derivados das partidas do ledger. A interface não cria nem altera saldos. O detalhe não expõe documento, telefone, campos personalizados, segredo de checkout, código PIX ou credenciais do gateway.

## Limites atuais

Reembolsos, cancelamentos, saques, reservas, exportação e conciliação ainda não possuem fluxo operacional. Quando forem implementados, devem criar novos movimentos financeiros compensatórios; transações e partidas existentes continuam imutáveis.
