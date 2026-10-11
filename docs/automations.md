# Funil de mensagens

Cada oferta possui no máximo um funil. OWNER, ADMIN e OPERATOR podem abrir **Editar funil de mensagens** na oferta, ordenar até 30 etapas, salvar o rascunho e publicar. A publicação cria uma versão imutável; execuções iniciadas continuam na versão que capturaram.

As etapas iniciais são mensagem Telegram, apresentação da oferta, checkout, condição, espera de 1 minuto a 30 dias, liberação do grupo/canal Telegram e encerramento. Mensagens aceitam `{{buyer_name}}`, `{{buyer_email}}`, `{{offer_name}}` e `{{checkout_url}}`. URLs opcionais exigem HTTPS sem credenciais embutidas.

O vínculo do comprador com o bot inicia o funil publicado. O cursor e a próxima execução ficam no PostgreSQL. Esperas e falhas transitórias são retomadas pelo Worker Hangfire a cada minuto. Se a oferta ainda não tiver funil publicado, o comportamento anterior permanece: o bot envia diretamente o convite individual da entrega Telegram.

Endpoints autenticados e isolados por organização:

- `GET /api/organizations/{org}/offers/{offer}/automation`
- `PUT /api/organizations/{org}/offers/{offer}/automation`
- `POST /api/organizations/{org}/offers/{offer}/automation/publish`
