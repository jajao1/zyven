# Pagamentos PIX com PushinPay

O checkout público cria uma única cobrança PIX e mantém o fluxo **Product → Offer → Checkout → Payment → Entitlement → Fulfillment**. Cada organização possui um `MerchantAccount`; somente proprietários e administradores podem conectar ou rotacionar o token PushinPay.

O token e o segredo de callback são cifrados com AES-256-GCM e nonce aleatório. O banco guarda componentes autenticados, impressão digital e hash do callback. Credenciais nunca aparecem na resposta da API ou nos logs.

`Payment.Prepare` registra o snapshot financeiro antes da chamada externa. As taxas fixas da plataforma e do processador são configuradas separadamente e `NetAmount = GrossAmount - PlatformFee - ProviderFee`. A PushinPay recebe valores e split em centavos inteiros. Rejeições definitivas falham o pagamento; resultados incertos permanecem `PROCESSING` e não são recriados cegamente.

O webhook identifica a organização pelo callback secreto e consulta a transação na PushinPay antes de aplicar efeitos. A confirmação exige correspondência de conta, transação, status pago, valor e end-to-end ID. Pagamento, checkout, ledger, entitlement e entrega são persistidos atomicamente e replays não duplicam lançamentos ou acessos.

A migration preserva pagamentos históricos com provedor `CELCOIN`, desconecta cadastros antigos e renomeia a conta corrente do ledger para `PAYMENT_PROCESSOR_CLEARING`. Consulte [pushinpay-integration.md](pushinpay-integration.md) para configuração e homologação.
