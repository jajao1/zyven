# Integração PIX PushinPay

Cada organização conecta o token da própria conta PushinPay em **Configurações → Pagamentos**. O backend valida o token com uma consulta autenticada de saldo, cifra-o com AES-256-GCM e retorna somente uma impressão digital. Conectar outro token faz uma rotação; o valor anterior nunca é exibido.

Novas cobranças usam `POST /pix/cashIn`, valores inteiros em centavos e `split_rules`. A taxa Zyven é o valor fixo `ZYVEN_PLATFORM_FEE` enviado para `PUSHINPAY_PLATFORM_ACCOUNT_ID`; a taxa contratual do processador fica separada em `PUSHINPAY_TRANSACTION_FEE`. A PushinPay limita o split a 50% e a cobrança mínima a R$ 0,50.

O callback público é gerado por vendedor a partir de `PUBLIC_API_BASE_URL`. O segredo fica cifrado e seu hash é usado na identificação. Um callback `paid` não libera a compra sozinho: a API consulta `GET /transaction/{id}` com o token do vendedor e exige identificador, status, valor e end-to-end ID iguais antes de confirmar pagamento, ledger, entitlement e entrega na mesma transação. Replays concluídos são idempotentes.

Para homologação, solicite à PushinPay a ativação do sandbox e configure:

- `PUSHINPAY_ENABLED=true`
- `PUSHINPAY_BASE_URL=https://api-sandbox.pushinpay.com.br/api/`
- `PUSHINPAY_PLATFORM_ACCOUNT_ID` com a conta Zyven habilitada para split
- `PUBLIC_API_BASE_URL` com uma URL HTTPS pública que alcance a API
- `PAYMENT_CREDENTIAL_ENCRYPTION_KEY` com 32 bytes aleatórios em Base64

Execute `scripts/setup.ps1` para gerar a chave local. Nunca versionar `.env` ou tokens de vendedores. A consulta direta de transação deve ser usada somente na confirmação, pois a PushinPay limita sua frequência. O checkout exibe o aviso obrigatório sobre o papel exclusivo da processadora.
