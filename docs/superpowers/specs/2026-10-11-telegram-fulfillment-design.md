# Telegram Fulfillment Design

## Objetivo

Adicionar `TELEGRAM_ACCESS` como integração de entrega, sem tornar Telegram um núcleo do produto. Cada organização conecta seu próprio bot e associa uma oferta a um grupo, supergrupo ou canal privado.

## Configuração

`TelegramIntegration` pertence à organização e armazena o identificador, nome e username retornados por `getMe`, token cifrado, fingerprint, segredo de webhook em hash e status. O token nunca é retornado ou registrado. Uma chave própria de 32 bytes em Base64 cifra tokens com AES-GCM.

OWNER e ADMIN conectam ou substituem o bot. A API valida o token com `getMe` antes de persistir e registra um webhook HTTPS exclusivo, com segredo no caminho e `X-Telegram-Bot-Api-Secret-Token`. OPERATOR pode configurar uma entrega usando uma integração ativa.

A configuração `TELEGRAM_ACCESS` guarda o chat numérico ou `@username`, nome de exibição e referência à integração. A API valida o chat com `getChat` e confirma que o bot é administrador com `getChatMember`.

## Fluxo do comprador

Depois do pagamento, a execução de fulfillment é criada uma única vez. A entrega exposta ao checkout e à Área do Comprador aponta para um endpoint Zyven que emite um código aleatório, descartável, com validade de 15 minutos. A resposta redireciona para `https://t.me/<bot_username>?start=<code>`.

Ao receber `/start <code>`, o webhook localiza apenas o hash do código, bloqueia a linha, valida validade, execução, entitlement e pagamento, registra o Telegram user id e consome o código. Em seguida chama `createChatInviteLink` com validade de 15 minutos e `member_limit=1`, e envia o convite ao usuário com `sendMessage`.

Novas tentativas para uma compra já vinculada devem pertencer ao mesmo Telegram user id. A execução e o vínculo têm chaves únicas para impedir duplicidade. Um novo convite curto pode ser emitido quando o anterior expirar.

## Revogação

O vínculo mantém o Telegram user id necessário para futura remoção via `banChatMember` seguida de `unbanChatMember`, quando expiração, reembolso ou revogação forem implementados. Esta fase fornece a operação idempotente de revogação, mas não inventa eventos de assinatura ainda inexistentes.

## Falhas e segurança

Falhas transitórias do Telegram não alteram pagamento nem entitlement. A execução registra falha operacional sem duplicar entrega e permite nova tentativa. Token, segredo, código e convite não aparecem em logs, auditoria ou respostas administrativas.

O webhook rejeita segredo inválido, update duplicado e mensagens que não sejam privadas. A integração aplica timeout HTTP e não confia em username, nome ou conteúdo enviado pelo cliente.

## Interface

Configurações recebe uma seção Telegram para conectar o bot e exibir seu estado. A oferta recebe um editor de entrega Telegram com chat id/`@username` e nome. A Área do Comprador apresenta “Vincular Telegram” para execuções deste tipo.

## Testes

Testes unitários cobrem cifra, códigos e invariantes. Testes integrados cobrem isolamento tenant, permissões, webhook autenticado, consumo único, vínculo ao comprador correto e idempotência. O cliente Telegram é substituído por fake nos testes; um smoke real requer token, chat e URL HTTPS fornecidos pelo vendedor.
