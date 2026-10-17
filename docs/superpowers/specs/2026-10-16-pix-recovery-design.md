# Recuperação automática de PIX

## Objetivo

Recuperar checkouts cujo PIX foi criado e continua pendente, usando somente o canal autorizado pela origem da jornada. Checkouts web usam e-mail. Jornadas iniciadas pelo bot próprio da organização usam Telegram. A confirmação, expiração ou cancelamento do pagamento encerra a recuperação antes de qualquer novo envio.

## Configuração por oferta

Cada oferta pode ativar a recuperação e manter até cinco lembretes. O padrão inicial é 15 minutos, 2 horas e 20 horas após a criação do PIX. Cada lembrete possui atraso, texto de e-mail e texto Telegram. O editor aceita apenas variáveis conhecidas: nome do comprador, nome da oferta, valor, vencimento, código PIX e URL segura do checkout.

OWNER, ADMIN e OPERATOR podem editar e publicar a configuração. A publicação cria uma versão imutável. Uma execução captura a versão publicada quando o PIX é criado, para que mudanças posteriores não alterem mensagens já agendadas.

## Canal e autorização

O checkout registra sua origem. Uma jornada originada no Telegram transporta uma referência aleatória, curta e de uso restrito que associa o checkout à conversa previamente autorizada, sem colocar identificadores internos sensíveis na URL. Nesse caso, os lembretes usam o bot da organização e o chat privado associado. Os demais checkouts usam o e-mail informado no próprio checkout.

O serviço não troca de canal automaticamente quando o canal escolhido falha. Sem integração Telegram ativa, conversa vinculada, endereço válido ou autorização vigente, a execução é encerrada com motivo auditável e sem envio alternativo. Opt-out impede novos lembretes sem afetar pagamento, entitlement ou entrega.

## Persistência e execução

`PixRecoveryConfiguration` pertence à organização e oferta. `PixRecoveryMessage` representa as mensagens ordenadas de uma versão. `PixRecoveryExecution` pertence ao pagamento, registra versão, canal, próximo lembrete, próxima execução e estado. `PixRecoveryAttempt` possui chave única por execução e posição, canal, status e horários, impedindo duplicação lógica.

Após a criação bem-sucedida do PIX, a aplicação cria uma única execução. O Worker Hangfire consulta execuções vencidas em lotes, bloqueia cada registro, relê pagamento e checkout e somente então reserva a tentativa. A chamada externa ocorre após essa reserva. Sucesso avança o cursor; falha transitória usa tentativas limitadas com espera crescente; falha permanente encerra a execução. Nenhum timer fica em memória.

O webhook de confirmação marca o pagamento e encerra a execução na mesma transação lógica. Como proteção contra corrida, o Worker relê e bloqueia o pagamento imediatamente antes de reservar o envio. Pagamentos `PAID`, `EXPIRED`, `CANCELLED` ou `REFUNDED`, checkouts não pendentes e PIX vencido nunca geram mensagem.

## Conteúdo e segurança

E-mails são enviados por uma interface transacional configurável; desenvolvimento usa um coletor explícito e produção exige provedor configurado. Telegram reutiliza o token cifrado do bot da organização. Logs e auditoria não incluem código PIX, token, e-mail completo, conteúdo da mensagem ou referência da conversa.

Links usam a origem pública configurada pela Zyven. Texto é simples, sem HTML fornecido pelo vendedor. O código PIX pode ser incluído somente no canal correspondente ao comprador e nunca aparece em endpoints administrativos de listagem de tentativas.

## Interface

A oferta recebe a seção **Recuperação de PIX**, com ativação, lista ordenada de lembretes, atraso e mensagens por canal. O painel mostra rascunho, versão publicada e validações. Vendas exibem apenas o estado resumido da recuperação: agendada, concluída por pagamento, encerrada, falhou ou opt-out.

## Testes

Testes cobrem versionamento, limites, variáveis, isolamento de organização e permissões. Integrações cobrem criação única após PIX, seleção de e-mail ou Telegram pela origem, bloqueio sem autorização, interrupção após confirmação e ausência de duplicidade em replays. Um cenário de corrida confirma que pagamento consolidado antes da reserva impede o envio. Frontend cobre edição, ordenação, validação e publicação.

## Escopo inicial

Esta entrega inclui PIX PushinPay, e-mail transacional por abstração e Telegram privado já vinculado. SMS, WhatsApp, campanhas promocionais, IA para geração de texto, métricas avançadas e troca automática de canal ficam fora deste incremento.
