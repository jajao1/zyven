# Área do comprador

A Área do comprador reúne compras confirmadas de todas as organizações da Zyven pelo e-mail verificado. Ela está disponível em `/buyer/login` e não compartilha autenticação ou permissões com a área do vendedor.

## Acesso

1. `POST /api/buyer/auth/request-code` recebe `{ email }` e sempre responde de forma neutra.
2. Quando o e-mail possui compras, um código numérico de seis dígitos é enviado pelo provedor configurado. Em desenvolvimento ele aparece apenas no log da API.
3. `POST /api/buyer/auth/verify-code` recebe `{ email, code }`. O código dura dez minutos, aceita cinco tentativas e só pode ser usado uma vez.
4. A validação cria o cookie opaco `zyven_buyer`, `HttpOnly`, `SameSite=Lax`, com duração de 30 dias. Apenas o hash fica no banco.
5. `POST /api/buyer/auth/logout` revoga a sessão e remove o cookie.

## Compras

`GET /api/buyer/purchases` retorna apenas pagamentos `PAID` ligados a entitlements `ACTIVE` de customers com o e-mail confirmado. `GET /api/buyer/purchases/{paymentId}` valida novamente a identidade e devolve as entregas concluídas. IDs conhecidos não concedem acesso e recursos de outro comprador respondem como inexistentes.

O acesso legado pelo cookie seguro do checkout continua disponível por 30 dias. A confirmação de pagamento oferece um atalho para a biblioteca.

## Produção

Substitua `IBuyerCodeDelivery` por um provedor transacional de e-mail. O código nunca deve ser devolvido na resposta HTTP, gravado no banco em texto puro ou incluído em métricas.
