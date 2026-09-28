# Entitlements e entrega por link

Uma oferta pode configurar uma entrega `EXTERNAL_LINK`. `OWNER`, `ADMIN` e `OPERATOR` usam a interface da oferta ou os endpoints abaixo:

| Método | Endpoint | Uso |
| --- | --- | --- |
| `GET` | `/api/organizations/{org}/offers/{offer}/fulfillments/external-link` | consultar configuração |
| `PUT` | `/api/organizations/{org}/offers/{offer}/fulfillments/external-link` | salvar `{ name, url }` |

A URL deve ser HTTPS e não pode conter credenciais embutidas.

Quando o webhook confirma o PIX, a mesma transação do PostgreSQL conclui o checkout, grava o ledger, cria um `Entitlement` ativo e cria uma única `FulfillmentExecution` por definição. Restrições únicas e bloqueio da linha do pagamento impedem duplicidade mesmo com eventos concorrentes. A execução guarda o nome e o link entregues naquele momento; alterações posteriores na oferta não reescrevem uma compra concluída.

O comprador acessa `GET /api/public/checkouts/{id}/delivery`. A autorização usa o segredo aleatório de 384 bits do checkout, armazenado somente como hash no banco e enviado em cookie `HttpOnly`, `SameSite=Strict`. A sessão de acesso dura 30 dias. Sem o cookie correspondente, o endpoint responde como recurso inexistente e não revela a compra.

O frontend mostra novamente a entrega quando o comprador abre a URL da oferta com `?checkout={id}` no mesmo navegador autenticado.
