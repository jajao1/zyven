# Recuperação de PIX

Cada oferta pode publicar de um a cinco lembretes, com atrasos crescentes de até 30 dias. O padrão sugerido é 15 minutos, 2 horas e 20 horas. Os textos aceitam `buyer_name`, `offer_name`, `amount`, `pix_expiry`, `pix_code` e `checkout_url` entre chaves duplas.

Checkouts web usam o e-mail informado pelo comprador. Links enviados pelo funil Telegram recebem uma referência aleatória de uso único; quando ela é consumida pela oferta correta, o checkout fica ligado ao bot e ao usuário Telegram sem aceitar IDs fornecidos pelo navegador.

A execução nasce somente depois que a PushinPay cria o PIX. O Worker verifica pagamento, checkout e vencimento sob bloqueio antes de cada envio. Pagamento confirmado, PIX expirado ou checkout encerrado cancela os próximos lembretes. Cada posição possui uma tentativa única, com até três chamadas e espera crescente em falhas transitórias.

Em desenvolvimento, o transportador de e-mail registra apenas uma impressão digital do destinatário. Produção falha de forma segura até que uma implementação de `ITransactionalEmail` seja registrada. Tokens Telegram permanecem cifrados e mensagens, e-mails e códigos PIX não são gravados em logs.
