# Celcoin integration decision

Zyven will integrate with Celcoin BaaS & Core for PIX cash-in by dynamic QR Code with split. The platform fee remains a fixed configurable amount, initially R$ 0.50. Celcoin's own transaction fee is independent and defaults to zero until the commercial agreement supplies the real value.

## Intended flow

1. Authenticate with OAuth client credentials through `POST /v5/token`, using the mTLS certificate required for production.
2. Persist a unique Zyven request reference before any provider call.
3. Create a COB location through `POST /pix/v1/location`.
4. Create the immediate PIX charge with split through `POST /baas/v2/immediate/split`.
5. Send the fixed Zyven fee in the Celcoin split instructions and direct the remaining receivable according to the contracted originator/seller account layout.
6. Store provider references without treating the synchronous response as proof of payment.
7. Receive `pix-payment-in` events through an authenticated webhook and reconcile payment state against the Celcoin account statement or transaction query.

The request mapping is implemented with the seller as originator and the configured Zyven account as the fixed split recipient. `CELCOIN_ENABLED=false` remains the safe deployment default until homologation credentials are installed.

## Operational requirements

- Celcoin BaaS contract and successful homologation.
- Active Celcoin BaaS accounts for every split participant.
- Production client ID and client secret.
- Production mTLS certificate and password stored outside source control.
- Production source IP registered with Celcoin.
- Originator account, Pix key, merchant name, city and postal code.
- Public HTTPS webhook URL with Basic or OAuth authentication.
- A reconciliation job that uses the account statement as the financial source of truth.
- A refund reserve and policy: PIX split participants are not automatically debited when the originator refunds a payment.

## Configuration

`ZYVEN_PLATFORM_FEE` controls the fixed Zyven fee and defaults to `0.50`. `CELCOIN_TRANSACTION_FEE` represents the contracted provider charge and defaults to `0.00`. Both values use BRL with two decimal places and are validated at application startup.

Credential placeholders live in `.env.example`. Set `CELCOIN_ENABLED=true`, mount the client certificate at `CELCOIN_MTLS_CERTIFICATE_PATH`, and inject credentials through the deployment environment. Real secrets and certificates must never be committed.

## Official references

- [Access and production requirements](https://developers.celcoin.com.br/docs/obtendo-acesso-%C3%A0s-apis)
- [Authentication](https://developers.celcoin.com.br/reference/post_v5-token)
- [Create PIX location](https://developers.celcoin.com.br/reference/criar-um-qrcode-location)
- [PIX split](https://developers.celcoin.com.br/docs/split-pix)
- [Immediate PIX split endpoint](https://developers.celcoin.com.br/reference/split-de-pix-cash-in-por-qr-code-din%C3%A2mico-immediate-1)
- [Webhook management](https://developers.celcoin.com.br/docs/gerenciamento-de-webhook)
- [BaaS webhook behavior](https://developers.celcoin.com.br/docs/webhooks-baas)
- [PIX cash-in webhook](https://developers.celcoin.com.br/docs/webhook-cash-in)
