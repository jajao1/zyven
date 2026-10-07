# Ledger financeiro

O ledger é a fonte do saldo financeiro da organização. A confirmação idempotente de um PIX PushinPay registra, na mesma transação do banco, um lançamento `PAYMENT_CAPTURED` com partidas dobradas:

| Conta | Débito | Crédito |
| --- | ---: | ---: |
| `PAYMENT_PROCESSOR_CLEARING` | valor bruto | — |
| `MERCHANT_AVAILABLE` | — | valor líquido |
| `PLATFORM_FEE_REVENUE` | — | taxa fixa Zyven |
| `PROVIDER_FEE_PAYABLE` | — | taxa do provedor |

Contas e lançamentos pertencem a uma organização. Chaves estrangeiras compostas impedem relações entre tenants. Um pagamento só pode originar uma transação, e o webhook repetido não altera saldo.

O PostgreSQL rejeita `UPDATE` e `DELETE` de transações e partidas e valida o equilíbrio ao concluir a transação do banco. Ajustes futuros devem usar novos lançamentos compensatórios. A migração cria o plano de contas para organizações existentes e reconstrói lançamentos de pagamentos já confirmados.

## Consultas autenticadas

Somente `OWNER`, `ADMIN` e `FINANCE` podem consultar:

| Método | Endpoint | Resultado |
| --- | --- | --- |
| `GET` | `/api/organizations/{id}/finance/wallet` | saldo disponível, totais recebidos e taxas |
| `GET` | `/api/organizations/{id}/finance/ledger?page=1&pageSize=20` | histórico paginado e partidas |

Os campos pendente, reservado e sacado começam em zero porque esses movimentos ainda não existem. Quando forem implementados, serão calculados por novas contas e lançamentos, sem campos de saldo editáveis.
